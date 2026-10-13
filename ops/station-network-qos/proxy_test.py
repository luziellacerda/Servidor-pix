#!/usr/bin/env python3
"""Exercise actual nginx TLS, WebSocket forwarding, refusal and QoS failure paths."""
import base64
import hashlib
import http.server
import json
import pathlib
import socket
import socketserver
import ssl
import struct
import subprocess
import tempfile
import threading
import time

HOST = "downloads-station.lzgames.com.br"
PATH = "/v1/station/online/multiplayer/relay"
AUTH = "StationRelay " + "T" * 43
PROOF = "fixture-request-proof"
PAYLOAD = b"station-stream-v3-proxy-fixture\x00\x01\x02"


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0)); return s.getsockname()[1]


def read_exact(sock, size):
    result = b""
    while len(result) < size:
        data = sock.recv(size - len(result))
        if not data: raise EOFError("unexpected_eof")
        result += data
    return result


def main():
    here = pathlib.Path(__file__).parent
    observations = {"backend": [], "qos": []}

    class Backend(http.server.BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *args): pass

        def do_GET(self):
            observations["backend"].append({"path": self.path, "authorization": self.headers.get("Authorization"),
                                             "proof": self.headers.get("X-Station-Request-Proof"),
                                             "cookie": self.headers.get("Cookie"),
                                             "qos_ip": self.headers.get("X-Station-Qos-IP")})
            if self.path != PATH:
                data = b"artifact-fixture"; self.send_response(200)
                self.send_header("Content-Length", str(len(data))); self.end_headers(); self.wfile.write(data); return
            if self.headers.get("Authorization") != AUTH or self.headers.get("X-Station-Request-Proof") != PROOF:
                data = b"unauthorized"; self.send_response(401)
                self.send_header("Content-Length", str(len(data))); self.end_headers(); self.wfile.write(data); return
            accept = base64.b64encode(hashlib.sha1((self.headers["Sec-WebSocket-Key"] +
                                                   "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()).decode()
            self.send_response(101); self.send_header("Upgrade", "websocket"); self.send_header("Connection", "Upgrade")
            self.send_header("Sec-WebSocket-Accept", accept)
            self.send_header("Sec-WebSocket-Protocol", "station-stream.v3"); self.end_headers(); self.wfile.flush()
            header = self.rfile.read(2); size = header[1] & 127
            mask = self.rfile.read(4); encoded = self.rfile.read(size)
            plain = bytes(x ^ mask[i % 4] for i, x in enumerate(encoded))
            assert plain == PAYLOAD
            self.wfile.write(bytes([130, len(plain)]) + plain); self.wfile.flush()
            self.close_connection = True

    class Qos(http.server.BaseHTTPRequestHandler):
        def log_message(self, *args): pass
        def do_GET(self):
            observations["qos"].append(dict(self.headers))
            self.send_response(204); self.send_header("X-Station-Qos-Applied", "1"); self.end_headers()

    class Unix(socketserver.ThreadingMixIn, socketserver.UnixStreamServer):
        daemon_threads = True

    with tempfile.TemporaryDirectory(prefix="station-qos-proxy-") as tmp:
        temp = pathlib.Path(tmp)
        backend = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Backend)
        qos = Unix(str(temp / "priority.sock"), Qos)
        for server in [backend, qos]: threading.Thread(target=server.serve_forever, daemon=True).start()
        cert, key = temp / "cert.pem", temp / "key.pem"
        p = subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1",
                            "-subj", "/CN=" + HOST, "-addext", "subjectAltName=DNS:" + HOST,
                            "-keyout", str(key), "-out", str(cert)], capture_output=True, timeout=10)
        assert p.returncode == 0
        proxy = (here / "game.proxy.conf").read_text().replace("127.0.0.1:5192", "127.0.0.1:" + str(backend.server_port))
        (temp / "game.proxy.conf").write_text(proxy)
        locations = (here / "game.locations.conf").read_text().replace("/etc/nginx/snippets/station-direct-game-proxy.conf", str(temp / "game.proxy.conf"))
        locations = locations.replace("/run/station-network-qos/priority.sock", str(temp / "priority.sock"))
        port = free_port()
        baseline = pathlib.Path("/etc/nginx/conf.d/station-direct-download.conf").read_text()
        baseline = baseline.replace("    include /etc/nginx/snippets/station-direct-game.locations.conf;", "")
        baseline = baseline.replace("listen 443 ssl;", f"listen 127.0.0.1:{port} ssl;")
        baseline = baseline.replace("/etc/station-direct-download/tls/fullchain.pem", str(cert)).replace("/etc/station-direct-download/tls/private-key.pem", str(key))
        baseline = baseline.replace("127.0.0.1:5192", "127.0.0.1:" + str(backend.server_port))
        baseline = baseline.replace("/var/log/nginx/station-direct-download.error.log", str(temp / "proxy.error.log"))
        baseline = baseline.replace("    location / { return 404; }", locations + "\n    location / { return 404; }")
        conf = temp / "nginx.conf"
        conf.write_text(f"pid {temp}/nginx.pid; error_log {temp}/nginx.error.log warn; events {{ worker_connections 128; }}\n"
                        + f"http {{ access_log off; client_body_temp_path {temp}/body; proxy_temp_path {temp}/proxy;\n" + baseline + "\n}\n")
        check = subprocess.run(["nginx", "-t", "-p", str(temp), "-c", str(conf)], capture_output=True, timeout=10)
        if check.returncode: raise RuntimeError(check.stderr.decode()[:1000])
        nginx = subprocess.Popen(["nginx", "-p", str(temp), "-c", str(conf), "-g", "daemon off;"],
                                 stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        context = ssl.create_default_context(cafile=str(cert))
        time.sleep(.2)

        def request(path=PATH, method="GET", upgrade=True, auth=AUTH, proof=PROOF, protocol="station-stream.v3", echo=False):
            with context.wrap_socket(socket.create_connection(("127.0.0.1", port), timeout=3), server_hostname=HOST) as conn:
                conn.settimeout(3)
                lines = [f"{method} {path} HTTP/1.1", "Host: " + HOST, "Connection: " + ("Upgrade" if upgrade else "close"),
                         "Cookie: must-not-arrive", "X-Station-Qos-IP: forged", "X-Station-Qos-Port: 1"]
                if upgrade:
                    lines += ["Upgrade: websocket", "Sec-WebSocket-Version: 13", "Sec-WebSocket-Key: " + base64.b64encode(b"0123456789abcdef").decode(),
                              "Sec-WebSocket-Protocol: " + protocol]
                if auth is not None: lines.append("Authorization: " + auth)
                if proof is not None: lines.append("X-Station-Request-Proof: " + proof)
                conn.sendall(("\r\n".join(lines) + "\r\n\r\n").encode())
                headers = b""
                while b"\r\n\r\n" not in headers: headers += read_exact(conn, 1)
                status = int(headers.split(b" ")[1])
                if echo:
                    assert status == 101
                    mask = b"abcd"; encoded = bytes(x ^ mask[i % 4] for i, x in enumerate(PAYLOAD))
                    conn.sendall(bytes([130, 128 | len(PAYLOAD)]) + mask + encoded)
                    assert read_exact(conn, 2) == bytes([130, len(PAYLOAD)])
                    assert read_exact(conn, len(PAYLOAD)) == PAYLOAD
                return status, headers
        try:
            status, headers = request(echo=True)
            assert status == 101 and b"X-Station-Qos: 1" in headers
            refused = [request(path="/")[0], request(path="/v1/station/catalog")[0],
                       request(path=PATH + "?x=1")[0], request(method="POST")[0],
                       request(path="/__station_qos_mark")[0]]
            assert refused == [404] * 5
            assert request(upgrade=False)[0] == 400
            assert request(protocol="station-stream.v2")[0] == 400
            assert request(auth=None)[0] == 401
            status, headers = request(auth="StationRelay " + "X" * 43)
            assert status == 401 and b"Connection: close" in headers
            assert request(proof=None)[0] == 401
            artifact = "/v1/station/artifacts/" + "0" * 43
            assert request(path=artifact, upgrade=False, auth="Bearer fixture")[0] == 200
            for row in observations["qos"]:
                assert not any("authorization" in k.lower() or "proof" in k.lower() or "cookie" in k.lower() for k in row)
                assert row["X-Station-Qos-IP"] == "127.0.0.1"
                assert row["X-Station-Qos-Port"] != "1"
            for row in observations["backend"]:
                assert row["cookie"] is None
                if row["path"] == PATH: assert row["qos_ip"] is None
            qos.shutdown(); qos.server_close(); (temp / "priority.sock").unlink(missing_ok=True)
            status, headers = request(echo=True)
            assert status == 101 and b"X-Station-Qos: 0" in headers, "qos_failure_blocked_authorized_upgrade"
            try:
                with context.wrap_socket(socket.create_connection(("127.0.0.1", port), timeout=3), server_hostname="wrong.invalid"):
                    raise AssertionError("hostname_mismatch_accepted")
            except ssl.SSLCertVerificationError: pass
            result = {"passed": True, "actualNginxTlsTest": True, "binaryRoundTrip": True,
                      "backendAuthPreserved": True, "qosCannotReadAuthMaterial": True,
                      "forgedPriorityPeerHeadersOverwritten": True, "refusalClosesConnection": True,
                      "qosFailurePreservesBackendAuthAndGameplay": True, "artifactPreserved": True,
                      "hostnameMismatchRefused": True, "unrelatedPathsRefused": 5,
                      "notAProductionGameTest": True}
            print(json.dumps(result))
        finally:
            nginx.terminate()
            try: nginx.wait(timeout=5)
            except subprocess.TimeoutExpired: nginx.kill(); nginx.wait()
            backend.shutdown(); backend.server_close()
            if (temp / "priority.sock").exists(): qos.shutdown(); qos.server_close()


if __name__ == "__main__":
    main()
