#!/usr/bin/env python3
"""Register a direct game TCP connection before upgrade. No auth material is read."""
import argparse
import http.server
import ipaddress
import json
import os
import pathlib
import socketserver
import tempfile
import threading
import time

from policy import TABLE, run


def tcp_peers(uid):
    result = {}
    for name, family in [("tcp", 4), ("tcp6", 6)]:
        try: rows = pathlib.Path("/proc/net/" + name).read_text().splitlines()[1:]
        except OSError: continue
        for row in rows:
            parts = row.split()
            if parts[3] != "01" or int(parts[7]) != uid:
                continue
            local, remote = parts[1].split(":"), parts[2].split(":")
            if int(local[1], 16) != 443:
                continue
            raw = bytes.fromhex(remote[0])
            if family == 4:
                raw = raw[::-1]
            else:
                raw = b"".join(raw[i:i + 4][::-1] for i in range(0, 16, 4))
            address = str(ipaddress.ip_address(raw))
            result[(family, address, int(remote[1], 16))] = parts[9]
    return result


def peer(address, port):
    value = ipaddress.ip_address(address)
    port = int(port)
    if not 1 <= port <= 65535:
        raise ValueError("invalid_port")
    return value.version, str(value), port


class Registry:
    def __init__(self, uid, limit=4096, state=None):
        self.uid, self.limit = uid, limit
        self.state = state
        self.entries = {}
        self.lock = threading.Lock()
        if state and state.is_file():
            for version, address, port, inode in json.loads(state.read_text()):
                self.entries[peer(address, port)] = str(inode)

    def save(self):
        if self.state:
            with tempfile.NamedTemporaryFile(mode="w", dir=self.state.parent, delete=False) as f:
                json.dump([list(k) + [v] for k, v in self.entries.items()], f)
            os.replace(f.name, self.state)

    def add(self, value):
        peers = tcp_peers(self.uid)
        if value not in peers:
            return False
        with self.lock:
            if len(self.entries) >= self.limit and value not in self.entries:
                return False
            version, address, port = value
            text = (f"add element inet {TABLE} games{version} "
                    f"{{ {address} . {port} timeout 60s }}\n")
            run(["nft", "-f", "-"], data=text)
            self.entries[value] = peers[value]
            self.save()
        return True

    def refresh(self):
        with self.lock:
            alive = tcp_peers(self.uid)
            retained = {k: v for k, v in self.entries.items() if alive.get(k) == v}
            # Both sets are replaced in a single nft transaction; no unclassified gap.
            lines = [f"flush set inet {TABLE} games4", f"flush set inet {TABLE} games6"]
            for version in [4, 6]:
                values = [f"{a} . {p} timeout 60s" for v, a, p in retained if v == version]
                if values:
                    lines.append(f"add element inet {TABLE} games{version} {{ " + ", ".join(values) + " }")
            run(["nft", "-f", "-"], data="\n".join(lines) + "\n")
            self.entries = retained
            self.save()


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.0"

    def log_message(self, *args):
        pass

    def do_GET(self):
        # This grants a queue class only. Station still validates every ticket/proof.
        # Always fail open for QoS errors so an auxiliary daemon cannot block gameplay.
        if self.path == "/mark":
            marked = False
            try:
                value = peer(self.headers.get("X-Station-Qos-IP", ""),
                             self.headers.get("X-Station-Qos-Port", ""))
                marked = self.server.registry.add(value)
            except (ValueError, RuntimeError, OSError):
                pass
            self.send_response(204)
            self.send_header("X-Station-Qos-Applied", "1" if marked else "0")
            self.end_headers()
        else:
            self.send_response(404); self.end_headers()


class Server(socketserver.ThreadingMixIn, socketserver.UnixStreamServer):
    daemon_threads = True
    request_queue_size = 64

    def __init__(self, path, registry):
        self.registry = registry
        self.workers = threading.BoundedSemaphore(16)
        super().__init__(path, Handler)

    def process_request(self, request, address):
        if not self.workers.acquire(blocking=False):
            self.shutdown_request(request); return
        try: super().process_request(request, address)
        except Exception:
            self.workers.release(); raise

    def process_request_thread(self, request, address):
        try: super().process_request_thread(request, address)
        finally: self.workers.release()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", default="/etc/station-network-qos/config.json")
    args = parser.parse_args()
    config = json.loads(pathlib.Path(args.config).read_text())
    registry = Registry(config.get("nginxUid", 33),
                        state=pathlib.Path("/run/station-network-qos/active.json"))
    registry.refresh()
    path = pathlib.Path("/run/station-network-qos/priority.sock")
    path.unlink(missing_ok=True)
    server = Server(str(path), registry)
    os.chown(path, 0, config.get("nginxGid", 33)); path.chmod(0o660)

    def maintain():
        while True:
            time.sleep(10)
            try: registry.refresh()
            except (RuntimeError, OSError): pass
    threading.Thread(target=maintain, daemon=True).start()
    server.serve_forever(poll_interval=.5)


if __name__ == "__main__":
    main()
