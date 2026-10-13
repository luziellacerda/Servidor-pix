#!/usr/bin/env python3
"""Authenticated install with isolated kernel qualification and automatic rollback."""
import argparse
import base64
import datetime
import hashlib
import http.client
import json
import os
import pathlib
import shutil
import ssl
import subprocess
import sys
import urllib.request

sys.dont_write_bytecode = True

UNITS = ["station-network-qos.service", "station-network-priority.service",
         "station-network-qos-check.service", "station-network-qos-check.timer"]
HOST = "downloads-station.lzgames.com.br"
RELAY = "/v1/station/online/multiplayer/relay"
PIN = "bcd134d59fcdbb35e0343c86d31a666cc95cf158d70b27aceceefd19158d072f"
VHOST = pathlib.Path("/etc/nginx/conf.d/station-direct-download.conf")
LOCATIONS = pathlib.Path("/etc/nginx/snippets/station-direct-game.locations.conf")
PROXY = pathlib.Path("/etc/nginx/snippets/station-direct-game-proxy.conf")


def run(args, *, timeout=30):
    result = subprocess.run(args, capture_output=True, text=True, timeout=timeout)
    if result.returncode:
        raise RuntimeError("command_failed:" + pathlib.Path(args[0]).name + ":" + result.stderr[:250])
    return result.stdout


def pid(unit):
    return run(["systemctl", "show", unit, "-p", "MainPID", "--value"]).strip()


def put(path, data, mode=0o644):
    temp = path.with_name(path.name + ".station-qos-new")
    with temp.open("wb") as f:
        os.chmod(temp, mode); f.write(data); f.flush(); os.fsync(f.fileno())
    os.replace(temp, path)


def write_result(stage, name, result):
    path = stage / name
    put(path, (json.dumps(result, indent=2) + "\n").encode(), 0o600)
    os.chown(path, int(os.environ["PKEXEC_UID"]), -1)


def probe(path, *, websocket=False, fake_ticket=False):
    conn = http.client.HTTPSConnection(HOST, timeout=10, context=ssl.create_default_context())
    conn.connect()
    pem = ssl.DER_cert_to_PEM_cert(conn.sock.getpeercert(binary_form=True))
    pub = subprocess.run(["openssl", "x509", "-pubkey", "-noout"], input=pem.encode(), capture_output=True, check=True).stdout
    der = subprocess.run(["openssl", "pkey", "-pubin", "-outform", "DER"], input=pub, capture_output=True, check=True).stdout
    if hashlib.sha256(der).hexdigest() != PIN:
        raise RuntimeError("production_pin_changed")
    headers = {"User-Agent": "Station-QoS-Validation/1.0", "Connection": "close"}
    if websocket:
        headers.update({"Upgrade": "websocket", "Connection": "Upgrade", "Sec-WebSocket-Version": "13",
                        "Sec-WebSocket-Key": base64.b64encode(os.urandom(16)).decode(), "Sec-WebSocket-Protocol": "station-stream.v3"})
    if fake_ticket:
        headers["Authorization"] = "StationRelay " + "0" * 43
        headers["X-Station-Request-Proof"] = "invalid-fixture-proof"
    conn.request("GET", path, headers=headers)
    response = conn.getresponse(); data = response.read(8192)
    result = {"status": response.status, "connectionClose": response.getheader("Connection", "").lower() == "close",
              "directGamePriority": response.getheader("X-Station-Qos")}
    try: result["code"] = json.loads(data).get("code")
    except (json.JSONDecodeError, AttributeError, UnicodeDecodeError): pass
    conn.close()
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", required=True)
    args = parser.parse_args()
    if os.geteuid() != 0 or not os.environ.get("PKEXEC_UID"):
        raise SystemExit("native_administrative_authentication_required")
    stage = pathlib.Path(args.stage).resolve()
    manifest = json.loads((stage / "manifest.json").read_text())
    package = stage / "payload"
    for relative, digest in manifest["sha256"].items():
        if not pathlib.Path(relative).name == relative:
            raise RuntimeError("invalid_package_path")
        if hashlib.sha256((package / relative).read_bytes()).hexdigest() != digest:
            raise RuntimeError("sealed_package_mismatch")
    config = json.loads((stage / "config.json").read_text())
    if hashlib.sha256((stage / "config.json").read_bytes()).hexdigest() != manifest["configSha256"]:
        raise RuntimeError("sealed_config_mismatch")
    if (stage / "applied.json").exists():
        raise SystemExit("already_applied_use_status")
    if not json.loads((stage / "proxy-test.json").read_text()).get("passed"):
        raise RuntimeError("proxy_gate_missing")
    sys.path.insert(0, str(package))
    import policy
    policy.validate(config)
    original = VHOST.read_bytes()
    if hashlib.sha256(original).hexdigest() != manifest["vhostBeforeSha256"]:
        raise RuntimeError("vhost_changed_since_preparation")
    if RELAY.encode() in original:
        raise RuntimeError("relay_already_configured")
    for target in [pathlib.Path("/opt/station-network-qos"), pathlib.Path("/etc/station-network-qos"), LOCATIONS, PROXY,
                   *[pathlib.Path("/etc/systemd/system") / x for x in UNITS]]:
        if target.exists(): raise RuntimeError("refusing_existing_installation:" + target.name)
    root_before = policy.require_safe_root(config["interface"], config.get("baselineHandles", ["0:"]))
    if policy.owned(root_before): raise RuntimeError("unexpected_existing_owned_qdisc")
    tables = json.loads(policy.run(["nft", "-j", "list", "tables"]))
    if any(x.get("table", {}).get("name") == policy.TABLE for x in tables.get("nftables", [])):
        raise RuntimeError("refusing_existing_nft_table")
    with urllib.request.urlopen("http://127.0.0.1:5192/ready/station/online/multiplayer-diagnostics", timeout=5) as response:
        rooms = json.load(response)
    if rooms.get("activeConnections", 0): raise RuntimeError("wait_for_current_game_before_queue_replacement")
    speed = int(pathlib.Path("/sys/class/net", config["interface"], "speed").read_text())
    if config["linkMbps"] > speed: raise RuntimeError("physical_link_slower_than_config")
    baseline = {"stationPid": pid("turborama-station-api.service"), "nginxPid": pid("nginx.service"),
                "physicalLinkMbps": speed, "queue": root_before}
    write_result(stage, "baseline.json", baseline)
    # Test actual nft grammar, queue borrowing, fairness and socket matching before host writes.
    result = subprocess.run(["python3", str(package / "kernel_test.py"), "--output", str(stage / "kernel-test.json")],
                            capture_output=True, text=True, timeout=70)
    (stage / "kernel-test-output.txt").write_text(result.stdout + result.stderr)
    if result.returncode:
        write_result(stage, "failed.json", {"phase": "isolated-kernel-qualification", "hostChanged": False})
        raise RuntimeError("isolated_kernel_gate_failed_read_private_output")
    os.chown(stage / "kernel-test.json", int(os.environ["PKEXEC_UID"]), -1)
    kernel = json.loads((stage / "kernel-test.json").read_text())
    if not kernel.get("passed"): raise RuntimeError("kernel_gate_missing")
    backup = stage / "vhost-before.conf"
    put(backup, original, 0o600)
    changed = False
    try:
        for directory in [pathlib.Path("/opt/station-network-qos"), pathlib.Path("/etc/station-network-qos")]:
            directory.mkdir(mode=0o755)
        for name in ["policy.py", "priority.py"]:
            put(pathlib.Path("/opt/station-network-qos") / name, (package / name).read_bytes(), 0o644)
        put(pathlib.Path("/etc/station-network-qos/config.json"), (stage / "config.json").read_bytes(), 0o644)
        for unit in UNITS:
            put(pathlib.Path("/etc/systemd/system") / unit, (package / unit).read_bytes())
        put(LOCATIONS, (package / "game.locations.conf").read_bytes())
        put(PROXY, (package / "game.proxy.conf").read_bytes())
        marker = b"    location / { return 404; }"
        if original.count(marker) != 1: raise RuntimeError("unexpected_vhost_layout")
        updated = original.replace(marker, b"    include /etc/nginx/snippets/station-direct-game.locations.conf;\n\n" + marker)
        put(VHOST, updated); changed = True
        run(["nginx", "-t"])
        run(["systemctl", "daemon-reload"])
        for unit in ["station-network-qos.service", "station-network-priority.service", "station-network-qos-check.timer"]:
            run(["systemctl", "enable", "--now", unit])
            if run(["systemctl", "is-active", unit]).strip() != "active":
                raise RuntimeError("unit_not_active:" + unit)
        run(["systemctl", "reload", "nginx"])
        blocked = {}
        for path in ["/", "/v1/station/catalog", "/v1/station/online/command", "/v1/station/online/relay",
                     "/ready/station/online/multiplayer-diagnostics", "/__station_qos_mark", RELAY + "?x=1"]:
            reply = probe(path)
            if reply["status"] != 404: raise RuntimeError("direct_unrelated_path_exposed")
            blocked[path] = reply["status"]
        missing = probe(RELAY, websocket=True)
        invalid = probe(RELAY, websocket=True, fake_ticket=True)
        write_result(stage, "direct-auth-check.json", {"missingTicket": missing, "invalidTicket": invalid})
        if missing["status"] != 401 or invalid["status"] != 401 or not invalid["connectionClose"] or invalid["directGamePriority"] != "1":
            raise RuntimeError("direct_relay_auth_or_connection_reuse_gate_failed")
        artifact = probe("/v1/station/artifacts/" + "0" * 43)
        if artifact["status"] != 404 or artifact.get("code") != "STATION_GRANT_NOT_FOUND":
            raise RuntimeError("artifact_route_regressed")
        state = policy.status(config)
        if baseline["stationPid"] != pid("turborama-station-api.service") or baseline["nginxPid"] != pid("nginx.service"):
            raise RuntimeError("unexpected_application_restart")
        receipt = {"applied": True, "atUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                   "qos": state, "physicalLinkMbps": speed, "stationPidPreserved": True, "nginxMasterPreserved": True,
                   "directRelayUri": "wss://" + HOST + RELAY, "spkiSha256": PIN,
                   "publicCaAndHostnameAndPinVerified": True, "blockedPaths": blocked,
                   "missingTicket": missing, "invalidTicket": invalid, "artifactRoutePreserved": artifact,
                   "kernelQualificationPassed": True, "proxyQualificationPassed": True,
                   "androidRouteUpdated": False, "physicalGameplayValidated": False,
                   "existingTunnelSharesInteractiveClassWithOtherTunnelTraffic": True,
                   "sourceFilesSha256": manifest["sha256"]}
        write_result(stage, "applied.json", receipt)
        print(json.dumps({k: v for k, v in receipt.items() if k != "sourceFilesSha256"}))
    except Exception as error:
        if changed: put(VHOST, original)
        for unit in ["station-network-priority.service", "station-network-qos-check.timer", "station-network-qos.service"]:
            subprocess.run(["systemctl", "disable", "--now", unit], capture_output=True, timeout=20)
        try: policy.stop(config)
        except Exception: pass
        for target in [LOCATIONS, PROXY, *[pathlib.Path("/etc/systemd/system") / x for x in UNITS]]:
            target.unlink(missing_ok=True)
        for directory in ["/opt/station-network-qos", "/etc/station-network-qos"]:
            if pathlib.Path(directory).is_dir(): shutil.rmtree(directory)
        subprocess.run(["systemctl", "daemon-reload"], capture_output=True, timeout=10)
        if changed:
            if subprocess.run(["nginx", "-t"], capture_output=True, timeout=10).returncode == 0:
                subprocess.run(["systemctl", "reload", "nginx"], capture_output=True, timeout=15)
        write_result(stage, "failed.json", {"phase": "apply", "rollbackAttempted": True,
                                            "errorType": type(error).__name__, "error": str(error)[:300]})
        raise


if __name__ == "__main__":
    main()
