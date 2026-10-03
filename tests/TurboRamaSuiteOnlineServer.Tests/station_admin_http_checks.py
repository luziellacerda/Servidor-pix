"""Optional Station commerce/admin checks inside station_http_smoke's guarded cluster."""
import base64
import hashlib
import http.client
import json
import os
from pathlib import Path
import socket
import subprocess
import time
import uuid

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa

from station_http_smoke import (PREFIX, PRODUCT, assert_error, b64, proof,
                                request, signed_payload, sql, sql_value)


def run(base, folder, api_env, public_key, dll):
    cluster_root = os.environ.get("PG_CLUSTER_CONF_ROOT", "")
    if (not cluster_root.startswith("/tmp/pg_virtualenv.") or
            not Path(cluster_root).is_dir() or
            not sql_value("SELECT current_setting('data_directory')").startswith("/tmp/pg_virtualenv.") or
            not base.startswith("http://127.0.0.1:")):
        raise RuntimeError("Station admin checks require the guarded temporary cluster and loopback API")
    assert Path(dll).is_file(), "Candidate admin DLL unavailable"
    sql("ALTER ROLE \"turborama-suite-admin\" PASSWORD 'admin-fixture-only-password'")
    admin_token = base64.b64encode(os.urandom(32)).decode()
    commerce_token = base64.b64encode(os.urandom(32)).decode()
    def secret(name, content):
        path = folder / name
        path.write_text(content)
        path.chmod(0o600)
        return str(path)
    uds_path = folder / "admin.sock"
    env = api_env.copy()
    env.update(SUITE_ADMIN_SOCKET=str(uds_path),
        SUITE_ADMIN_TOKEN_FILE=secret("admin-token", admin_token),
        SUITE_COMMERCE_TOKEN_FILE=secret("commerce-token", commerce_token),
        SUITE_ADMIN_PEPPER_FILE=api_env["Suite__ActivationPepperFile"],
        STATION_ADMIN_PEPPER_FILE=api_env["Station__ActivationPepperFile"],
        SUITE_COMMERCE_ENABLED="1", STATION_COMMERCE_ENABLED="1",
        SUITE_CONTENT_ADMIN_ENABLED="0",
        SUITE_ADMIN_CONNECTION="Host=127.0.0.1;Port=" + os.environ["PGPORT"] +
            ";Database=postgres;Username=turborama-suite-admin;Password=admin-fixture-only-password")
    class UnixHttp(http.client.HTTPConnection):
        def connect(self):
            self.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
            self.sock.settimeout(10)
            self.sock.connect(str(uds_path))
    def internal(method, route, data=None, claim="station.licenses.read", controls=True,
                 authenticated=True):
        commerce = route.startswith("/commerce/")
        headers = {"Content-Type": "application/json"}
        if authenticated:
            headers["X-Suite-Commerce-Token" if commerce else "X-Suite-Admin-Token"] = commerce_token if commerce else admin_token
        headers.update({"X-Suite-Admin-Actor": "synthetic-admin",
            "X-Suite-Admin-Claims": claim, "X-Suite-Client-Ip-Digest": "a" * 64,
            "X-Suite-Step-Up-At": str(int(time.time()))})
        if controls:
            headers["X-Suite-Csrf-Verified"] = "1"
        connection = UnixHttp("localhost")
        try:
            connection.request(method, route, None if data is None else json.dumps(data), headers)
            response = connection.getresponse()
            return response.status, dict(response.getheaders()), response.read()
        finally:
            connection.close()
    def data(response):
        assert response[0] == 200, "Unexpected Station admin HTTP status " + str(response[0])
        return json.loads(response[2])
    log = (folder / "admin.log").open("wb")
    process = subprocess.Popen(["dotnet", dll], env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        for _ in range(80):
            if process.poll() is not None:
                raise RuntimeError("Candidate admin exited; inspect isolated test log")
            if uds_path.exists():
                try:
                    if internal("GET", "/health")[0] == 200:
                        break
                except (OSError, http.client.HTTPException):
                    pass
            time.sleep(0.1)
        else:
            raise RuntimeError("Candidate admin startup timed out")
        assert internal("GET", "/health", authenticated=False)[0] == 404
        purchase = "synthetic-" + uuid.uuid4().hex
        event = dict(sourceSystem="TURBOBOX_V1", sourceEventId=uuid.uuid4().hex,
            sourcePurchaseId=purchase, sourceItemKey="station", sourceVersion=1,
            sourceProductSku="STATION_ANDROID_LIFETIME_1_DEVICE", eventType="PURCHASE_PAID",
            amountCents=9990, currency="BRL", customerRef="synthetic-buyer", displayName="Synthetic Buyer")
        def commerce_event():
            fields = ("sourceSystem", "sourceEventId", "sourcePurchaseId", "sourceItemKey",
                "sourceVersion", "sourceProductSku", "eventType", "amountCents", "currency",
                "customerRef", "displayName")
            event["payloadDigest"] = hashlib.sha256("\n".join(str(event[key]) for key in fields).encode()).hexdigest()
            return data(internal("POST", "/commerce/station/events", event))
        paid = commerce_event()
        license_id = paid["licenseId"]
        assert paid["financialState"] == "PAID" and license_id.startswith("STA-")
        assert commerce_event() == paid, "Paid event replay must be idempotent"
        issue = data(internal("POST", "/commerce/station/deliveries/" + purchase + "/station/issue",
            {"actor": "synthetic-admin", "requestId": uuid.uuid4().hex}))
        def device():
            key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
            spki = key.public_key().public_bytes(serialization.Encoding.DER,
                serialization.PublicFormat.SubjectPublicKeyInfo)
            identity = dict(schemaVersion=1, productId=PRODUCT, applicationId=PRODUCT,
                deviceId=b64(hashlib.sha256(spki).digest()), clientVersion="test-admin",
                deviceManufacturer="Synthetic", deviceModel="Admin Fixture", androidSdk=35)
            return key, spki, identity
        def activate(who, code):
            key, spki, identity = who
            challenge = signed_payload(request(base, "POST", "/v1/station/activations/challenge",
                dict(identity, domain=PREFIX + "request-activation-challenge/v1", activationCode=code,
                     devicePublicKey=b64(spki))), public_key, "activation-challenge")
            activated = signed_payload(request(base, "POST", "/v1/station/activations/complete",
                proof(key, dict(identity, domain=PREFIX + "activate/v1", activationCode=code,
                    devicePublicKey=b64(spki), challengeId=challenge["challengeId"], nonce=challenge["nonce"]))),
                public_key, "activated")
            assert activated["licenseId"] == license_id
        def session(who):
            key, _, identity = who
            challenge = signed_payload(request(base, "POST", "/v1/station/challenges",
                dict(identity, domain=PREFIX + "request-session-challenge/v1", licenseId=license_id)),
                public_key, "session-challenge")
            return signed_payload(request(base, "POST", "/v1/station/sessions",
                proof(key, dict(identity, domain=PREFIX + "open-session/v1", licenseId=license_id,
                    challengeId=challenge["challengeId"], nonce=challenge["nonce"]))), public_key, "session")
        first = device()
        activate(first, issue["activationCode"])
        current = session(first)
        status_route = "/station/licenses/" + license_id
        assert data(internal("GET", status_route))["activeDeviceCount"] == 1
        assert_error(internal("GET", status_route, claim="suite.content.read"), 403, "STATION_PERMISSION_DENIED")
        grant = signed_payload(request(base, "POST", "/v1/station/downloads/authorize",
            dict(first[2], domain=PREFIX + "request-download/v1", itemId="item-synthetic-01"), current["accessToken"]),
            public_key, "download-grant")
        def action(name, body=None, controls=True):
            if body is None:
                body = dict(requestId=uuid.uuid4().hex,
                    expectedGeneration=data(internal("GET", status_route))["revocationGeneration"],
                    reason="Synthetic isolated Station test", targetSessionId=None)
            response = internal("POST", status_route + "/actions/" + name, body,
                claim="station.sessions.revoke" if name == "revoke-session" else "station.licenses.manage",
                controls=controls)
            return response, body
        assert_error(action("block", controls=False)[0], 403, "STATION_PERMISSION_DENIED")
        blocked, command = action("block")
        assert data(blocked)["code"] == "BLOCK"
        assert data(action("block", command)[0]) == data(blocked), "Admin command replay must be idempotent"
        assert_error(request(base, "GET", "/v1/station/me", bearer=current["accessToken"]), 401, "STATION_SESSION_INVALID")
        assert_error(request(base, "GET", "/v1/station/artifacts/" + grant["grantId"], bearer=current["accessToken"]),
                     404, "STATION_GRANT_NOT_FOUND")
        stale = dict(command, requestId=uuid.uuid4().hex)
        assert_error(action("unblock", stale)[0], 409, "STATION_STATE_CHANGED")
        assert data(action("unblock")[0])["code"] == "UNBLOCK"
        current = session(first)
        assert request(base, "GET", "/v1/station/me", bearer=current["accessToken"])[0] == 200
        revoke = dict(requestId=uuid.uuid4().hex,
            expectedGeneration=data(internal("GET", status_route))["revocationGeneration"],
            reason="Synthetic isolated Station revoke", targetSessionId=current["sessionId"])
        assert data(action("revoke-session", revoke)[0])["code"] == "REVOKE-SESSION"
        assert_error(request(base, "GET", "/v1/station/me", bearer=current["accessToken"]), 401, "STATION_SESSION_INVALID")
        current = session(first)
        assert data(action("transfer")[0])["code"] == "TRANSFER"
        assert data(internal("GET", status_route))["activeDeviceCount"] == 0
        assert_error(request(base, "GET", "/v1/station/me", bearer=current["accessToken"]), 401, "STATION_SESSION_INVALID")
        assert_error(request(base, "POST", "/v1/station/challenges",
            dict(first[2], domain=PREFIX + "request-session-challenge/v1", licenseId=license_id)), 403, "STATION_DEVICE_DENIED")
        issued = data(internal("POST", status_route + "/actions/issue-code",
            {"actor": "synthetic-admin", "requestId": uuid.uuid4().hex}, claim="station.licenses.manage"))
        replacement = device()
        activate(replacement, issued["activationCode"])
        current = session(replacement)
        assert request(base, "GET", "/v1/station/me", bearer=current["accessToken"])[0] == 200
        event.update(sourceEventId=uuid.uuid4().hex, sourceVersion=2, eventType="PURCHASE_SUSPENDED")
        assert commerce_event()["financialState"] == "SUSPENDED"
        assert_error(request(base, "GET", "/v1/station/me", bearer=current["accessToken"]), 401, "STATION_SESSION_INVALID")
        assert_error(action("unblock")[0], 409, "STATION_FINANCIAL_BLOCK")
        print("STATION ADMIN HTTP: OK (commerce/issue, permission/CSRF, idempotency, block/unblock, revoke, transfer/new device, financial suspension)")
        if os.environ.get("STATION_HTTP_PANEL_CHECKS") == "1":
            from station_panel_checks import run as panel_checks
            panel_checks(base,folder,api_env,public_key,dll,env,internal)

    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        log.close()
