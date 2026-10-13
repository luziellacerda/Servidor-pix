#!/usr/bin/env python3
"""Real Station ticket/proof and direct WSS test using disposable licensed clients."""
import argparse
import base64
import hashlib
import importlib.util
import json
import os
import pathlib
import secrets
import socket
import ssl
import sys
import time
import uuid

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import padding

from deploy import HOST, PIN, RELAY, pid


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec); sys.modules[name] = module
    spec.loader.exec_module(module); return module


def need(ok, code):
    if not ok: raise RuntimeError(code)


def proof(probe, token):
    stamp = int(time.time()); nonce = base64.urlsafe_b64encode(secrets.token_bytes(16)).decode().rstrip("=")
    canonical = ("TurboRamaStationAndroid/request/v1\nGET\n" + RELAY + "\n" + hashlib.sha256(b"").hexdigest() +
                 "\n" + hashlib.sha256(token.encode()).hexdigest() + "\n" + str(stamp) + "\n" + nonce + "\n").encode()
    signature = probe.client.device.sign(canonical, padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
    return "v1." + str(stamp) + "." + nonce + "." + base64.urlsafe_b64encode(signature).decode().rstrip("=")


def exact(sock, size):
    result = b""
    while len(result) < size:
        chunk = sock.recv(size - len(result))
        if not chunk: raise EOFError("direct_socket_closed")
        result += chunk
    return result


def open_relay(probe, token, *, expected=101):
    context = ssl.create_default_context()
    sock = context.wrap_socket(socket.create_connection((HOST, 443), timeout=8), server_hostname=HOST)
    sock.settimeout(8)
    from cryptography import x509
    from cryptography.hazmat.primitives import serialization
    public = x509.load_der_x509_certificate(sock.getpeercert(binary_form=True)).public_key()
    der = public.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    need(hashlib.sha256(der).hexdigest() == PIN, "direct_pin_mismatch")
    key = base64.b64encode(secrets.token_bytes(16)).decode()
    headers = ["GET " + RELAY + " HTTP/1.1", "Host: " + HOST, "Upgrade: websocket", "Connection: Upgrade",
               "Sec-WebSocket-Version: 13", "Sec-WebSocket-Key: " + key, "Sec-WebSocket-Protocol: station-stream.v3",
               "Authorization: StationRelay " + token, "X-Station-Request-Proof: " + proof(probe, token)]
    sock.sendall(("\r\n".join(headers) + "\r\n\r\n").encode())
    raw = b""
    while b"\r\n\r\n" not in raw:
        raw += exact(sock, 1); need(len(raw) < 16384, "direct_header_bound_exceeded")
    status = int(raw.split(b" ")[1])
    need(status == expected, "direct_status_unexpected:" + str(status))
    fields = {}
    for line in raw.decode().split("\r\n")[1:]:
        if ":" in line:
            name, value = line.split(":", 1); fields[name.lower()] = value.strip()
    if expected == 101:
        accept = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()).decode()
        need(fields.get("sec-websocket-accept") == accept, "websocket_accept_mismatch")
        need(fields.get("sec-websocket-protocol") == "station-stream.v3", "websocket_protocol_mismatch")
        need(fields.get("x-station-qos") == "1", "direct_connection_not_prioritized")
    return sock


def ping(sock):
    payload = b"StationQoS"; mask = secrets.token_bytes(4)
    sock.sendall(bytes([0x89, 0x80 | len(payload)]) + mask + bytes(x ^ mask[i % 4] for i, x in enumerate(payload)))
    for _ in range(20):
        header = exact(sock, 2); size = header[1] & 127
        if size == 126: size = int.from_bytes(exact(sock, 2), "big")
        if size == 127: size = int.from_bytes(exact(sock, 8), "big")
        need(size <= 1048576, "websocket_frame_bound_exceeded")
        body = exact(sock, size)
        if header[0] & 15 == 10:
            need(body == payload, "pong_payload_mismatch"); return
    raise RuntimeError("pong_not_received")


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--stage", required=True); args = parser.parse_args()
    need(os.geteuid() == 0 and bool(os.environ.get("PKEXEC_UID")), "native_administrative_authentication_required")
    stage = pathlib.Path(args.stage)
    need(json.loads((stage / "applied.json").read_text())["applied"], "qos_not_applied")
    sys.path.insert(0, "/mnt/DADOS/servidor-pix-station-nes-20261012/docs/station-android/online-plataformas-20261009/library-tools")
    checkup = load("station_direct_qualification_checkup", "/mnt/DADOS/station-checkup-20261010/checkup.py")
    b = checkup.b; values, _ = b.ops.runtime(); database = b.r71.database(values)
    before = b.security.real_licenses(database)
    index = json.loads(pathlib.Path(values["Station__LibraryIndexFile"]).read_text())
    profiles = json.loads(pathlib.Path("/mnt/DADOS/station-r141-20261013/current-exports/final-profiles.json").read_text())
    sample = next(p for p in profiles if p["platform"] == "snes" and p["approved"] and 2 in p["allowedPlayerCounts"])
    binding = {k: sample[k] for k in ["itemId", "contentSha256", "engineId", "coreSha256", "runtimeSha256", "profileId", "profileSha256"]}
    clients = [checkup.SignedProbe(index, values, b.PUBLIC, lambda query: b.ops.sql(database, query)) for _ in range(2)]
    opener = load("station_direct_qualification_http", "/mnt/DADOS/station-r139-20261013/proof_http.py")
    for client in clients: opener.install(client)
    sockets = []; room = None; entered = []
    original_pid = pid("turborama-station-api.service")
    result = {}

    def command(client, action, **fields):
        payload = dict(action=action, requestId=str(uuid.uuid4()), clientMaximumPlayers=5, **binding)
        payload.update(fields)
        return client.signed("POST", "/v1/station/online/multiplayer/command", "online", payload)["snapshot"]

    try:
        for i, client in enumerate(clients):
            client.start()
            client.signed("POST", "/v1/station/online/command", "online",
                          dict(action="enter", requestId=str(uuid.uuid4()), nickname="Station transport test " + str(i + 1)))
            entered.append(client)
        host, guest = clients
        room = command(host, "create", capacity=2)["room"]
        room = command(guest, "join", roomId=room["roomId"], generation=room["generation"])["room"]
        for client in clients: command(client, "ready", roomId=room["roomId"], generation=room["generation"], value=True)
        room = command(host, "start", roomId=room["roomId"], generation=room["generation"])["room"]
        link = room["links"][0]["linkId"]
        grants = []
        for client in clients:
            grant = command(client, "ticket", roomId=room["roomId"], generation=room["generation"], linkId=link)["ticket"]
            grants.append(grant["ticket"])
            connection = open_relay(client, grant["ticket"]); sockets.append(connection)
            ping(connection)
        refused = open_relay(host, grants[0], expected=401); refused.close()
        snapshot = command(host, "snapshot", roomId=room["roomId"], generation=room["generation"])
        need(snapshot["room"]["players"] == 2, "two_members_missing")
        result = {"passed": True, "directWssAuthenticatedConnections": 2, "subprotocol": "station-stream.v3",
                  "bothConnectionsPrioritized": True, "ticketProofVerifiedByLiveApi": True, "pingPongBothDirections": True,
                  "consumedTicketReplayRefused": True, "tlsPublicCaHostnameAndSpkiVerified": True,
                  "physicalAndroidGameplayValidated": False}
    finally:
        for connection in sockets:
            try: connection.sendall(b"\x88\x80" + secrets.token_bytes(4))
            except OSError: pass
            connection.close()
        if room and entered:
            try: command(clients[0], "leave", roomId=room["roomId"], generation=room["generation"])
            except Exception: pass
        cleanup = []
        for client in clients:
            if client in entered:
                try: client.request("POST", "/v1/station/online/command", dict(action="offline", requestId=str(uuid.uuid4())))
                except Exception: pass
            cleanup.append(client.client.cleanup())
        need(all(cleanup), "own_synthetic_license_cleanup_failed")
        need(before == b.security.real_licenses(database), "real_licenses_changed")
        need(original_pid == pid("turborama-station-api.service"), "station_pid_changed")
        result.update(syntheticLicensesRemoved=True, realLicensesPreserved=True, stationPidPreserved=True)
    path = stage / "signed-wss-test.json"
    path.write_text(json.dumps(result, indent=2) + "\n"); path.chmod(0o600); os.chown(path, int(os.environ["PKEXEC_UID"]), -1)
    print(json.dumps(result))


if __name__ == "__main__":
    try: main()
    except Exception as error:
        # Credentials and private room/session identifiers must never reach tool output.
        controlled = ["direct_", "websocket_", "two_members_", "qos_not_", "native_administrative_", "own_synthetic_", "real_licenses_", "station_pid_", "pong_"]
        reason = str(error) if isinstance(error, RuntimeError) and any(str(error).startswith(x) for x in controlled) else "private_qualification_error"
        if isinstance(error, KeyError) and error.args[0] in ["Station__LibraryIndexFile", "room", "snapshot", "links", "linkId", "ticket"]:
            reason = "missing_field:" + error.args[0]
        if isinstance(error, ModuleNotFoundError): reason = "missing_module:" + error.name
        print(json.dumps({"passed": False, "errorType": type(error).__name__,
                          "reason": reason}))
        raise SystemExit(1)
