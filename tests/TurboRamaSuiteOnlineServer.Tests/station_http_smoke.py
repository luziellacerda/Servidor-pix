#!/usr/bin/env python3
"""Exercise the candidate Station HTTP API with an ephemeral PostgreSQL cluster.

Run from the repository root: pg_virtualenv python3 tests/TurboRamaSuiteOnlineServer.Tests/station_http_smoke.py
Set STATION_HTTP_EXTRA_INDEX to a private, readable candidate index to exercise its
real items and covers in the same isolated API. Requires .NET, psql and cryptography.
Set STATION_HTTP_API_DLL to exercise a previously published release DLL.
Set STATION_HTTP_REAL_TTL=1 to verify 60/180-second expiry using the real clock.
Never targets production.
"""

import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import zipfile
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa


ROOT = Path(__file__).resolve().parents[2]
PRODUCT = "TURBORAMA_STATION_ANDROID"
PREFIX = "TurboRamaStationAndroid/"
PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNgYGBgAAAABQABpfZFQAAAAABJRU5ErkJggg==")


def b64(data):
    return base64.urlsafe_b64encode(data).decode().rstrip("=")


def sql(statement):
    subprocess.run(["psql", "-v", "ON_ERROR_STOP=1", "-q", "-c", statement],
                   cwd=ROOT, check=True, stdout=subprocess.DEVNULL)


def sql_value(statement):
    return subprocess.check_output(["psql", "-v", "ON_ERROR_STOP=1", "-Atqc",
                                    statement], cwd=ROOT, text=True).strip()


def request(base, method, route, body=None, bearer=None):
    headers = {"Accept": "application/json"}
    if bearer:
        headers["Authorization"] = "Bearer " + bearer
    if body is not None:
        headers["Content-Type"] = "application/json"
        body = json.dumps(body, separators=(",", ":")).encode()
    req = Request(base + route, data=body, headers=headers, method=method)
    try:
        with urlopen(req, timeout=10) as response:
            return response.status, dict(response.headers), response.read()
    except HTTPError as error:
        return error.code, dict(error.headers), error.read()


def assert_error(response, status, code):
    actual, _, body = response
    assert actual == status and json.loads(body)["code"] == code, (actual, body)


def signed_payload(response, server_public, domain):
    status, _, body = response
    assert status == 200, (status, body)
    envelope = json.loads(body)
    payload = base64.urlsafe_b64decode(envelope["payload"] + "===")
    signature = base64.urlsafe_b64decode(envelope["signature"] + "===")
    server_public.verify(signature, payload, padding.PSS(mgf=padding.MGF1(hashes.SHA256()),
                                              salt_length=hashes.SHA256().digest_size),
                         hashes.SHA256())
    assert envelope["keyId"] == hashlib.sha256(server_public.public_bytes(
        serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
    data = json.loads(payload)
    assert data["domain"] == PREFIX + domain + "/v1"
    assert data["productId"] == data["applicationId"] == PRODUCT
    return data


def proof(key, payload):
    data = json.dumps(payload, separators=(",", ":")).encode()
    signature = key.sign(data, padding.PSS(mgf=padding.MGF1(hashes.SHA256()),
                                           salt_length=hashes.SHA256().digest_size), hashes.SHA256())
    return {"payload": b64(data), "signature": b64(signature)}


def wait_until(deadline):
    while (remaining := deadline - time.monotonic()) > 0:
        time.sleep(min(1, remaining))


def main():
    cluster_root = os.environ.get("PG_CLUSTER_CONF_ROOT", "")
    if (not cluster_root.startswith("/tmp/pg_virtualenv.") or
            not Path(cluster_root).is_dir() or
            not sql_value("SELECT current_setting('data_directory')").startswith(
                "/tmp/pg_virtualenv.")):
        raise RuntimeError("Run inside pg_virtualenv, never against production")
    with tempfile.TemporaryDirectory(prefix="station-http-", dir="/mnt/DADOS") as temporary:
        folder = Path(temporary)
        roles = ("turborama-suite", "turborama-suite-admin",
                 "turborama-suite-content-admin", "turborama-suite-content-api",
                 "turborama-suite-content-maintenance", "turborama-suite-content-monitor",
                 "turborama-suite-gateway", "turborama-suite-publisher")
        for role in roles:
            sql(f'CREATE ROLE "{role}" LOGIN')
        sql('ALTER ROLE "turborama-suite" PASSWORD \'fixture-only-password\'')
        for migration in sorted((ROOT / "migrations/suite").glob("*.up.sql")):
            digest = hashlib.sha256(migration.read_bytes()).hexdigest()
            subprocess.run(["psql", "-v", "ON_ERROR_STOP=1", "-v",
                            "migration_sha256=" + digest, "-q", "-f", str(migration)],
                           check=True, stdout=subprocess.DEVNULL)

        device = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        server = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        suite = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        server_public = server.public_key()
        spki = device.public_key().public_bytes(serialization.Encoding.DER,
                                                serialization.PublicFormat.SubjectPublicKeyInfo)
        device_id = b64(hashlib.sha256(spki).digest())
        code = b64(os.urandom(32))
        station_pepper = os.urandom(32)
        suite_pepper = os.urandom(32)
        verifier = hmac.digest(station_pepper, code.encode(), "sha256").hex()
        license_id = "STA-" + os.urandom(16).hex().upper()
        sql("INSERT INTO suite.suite_licenses(license_id,product_id,status,"
            "activation_verifier,activation_expires_at,activation_consumed,license_term,"
            "expires_at,identity_policy,maximum_active_devices,provisioning_origin,"
            "enrollment_state,claim_mode) VALUES('" + license_id + "','" + PRODUCT +
            "','ACTIVE','" + verifier + "',clock_timestamp()+interval '15 minutes',"
            "false,'LIFETIME',NULL,'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM')")
        sql("INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,"
            "source_item_key,source_product_sku,product_id,license_id,provisioning_state,"
            "financial_state,last_source_version) VALUES('STATION_HTTP_TEST','synthetic-1',"
            "'synthetic','STATION_ANDROID_LIFETIME_1_DEVICE','" + PRODUCT + "','" +
            license_id + "','PROVISIONED','PAID',1)")
        game = b"\x01\x02\x03\x04" * (1024 * 1024)
        (folder / "item.bin").write_bytes(game)
        (folder / "cover.png").write_bytes(PNG)
        with zipfile.ZipFile(folder / "item.zip", "w") as archive:
            archive.writestr("disc/game.cue", b"CUE")
            archive.writestr("disc/game.bin", b"BIN!")
        packed = (folder / "item.zip").read_bytes()
        descriptor = {"fileName": "item.bin", "sizeBytes": len(game),
                      "sha256": hashlib.sha256(game).hexdigest(), "format": "raw",
                      "launchPath": "item.bin", "expandedSizeBytes": len(game), "fileCount": 1}
        zip_descriptor = {"fileName": "item.zip", "sizeBytes": len(packed),
                          "sha256": hashlib.sha256(packed).hexdigest(), "format": "zip",
                          "launchPath": "disc/game.cue", "expandedSizeBytes": 7, "fileCount": 2}
        index = {"revision": 3, "items": [{"itemId": "item-synthetic-01", "name": "Synthetic",
                 "platform": "snes", "revision": 3, "coverId": "cover-synthetic-01",
                 "filePath": str(folder / "item.bin"), "coverPath": str(folder / "cover.png"),
                 "artifact": descriptor},
                {"itemId": "item-synthetic-02", "name": "Synthetic ZIP", "platform": "ps2",
                 "revision": 3, "coverId": "cover-synthetic-02",
                 "filePath": str(folder / "item.zip"), "coverPath": str(folder / "cover.png"),
                 "artifact": zip_descriptor}]}
        extra_path = os.environ.get("STATION_HTTP_EXTRA_INDEX")
        extra = json.loads(Path(extra_path).read_text(encoding="utf-8")) if extra_path else None
        if extra is not None:
            if not isinstance(extra.get("items"), list):
                raise ValueError("invalid extra Station index")
            index["items"].extend(extra["items"])
        (folder / "index.json").write_text(json.dumps(index))

        def secret(name, value):
            path = folder / name
            path.write_bytes(value)
            path.chmod(0o600)
            return str(path)

        def private_pem(key):
            return key.private_bytes(serialization.Encoding.PEM,
                                     serialization.PrivateFormat.PKCS8,
                                     serialization.NoEncryption())

        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            port = listener.getsockname()[1]
        base = f"http://127.0.0.1:{port}"
        env = os.environ.copy()
        env.update({"ASPNETCORE_ENVIRONMENT": "Development", "Suite__Enabled": "true",
                    "Station__Enabled": "true", "Suite__ActivationPepperFile":
                    secret("suite-pepper", base64.b64encode(suite_pepper)),
                    "Station__ActivationPepperFile":
                    secret("station-pepper", base64.b64encode(station_pepper)),
                    "Suite__OnlineAssertionPrivateKeyPemFile": secret("suite.pem", private_pem(suite)),
                    "Station__AssertionPrivateKeyPemFile": secret("station.pem", private_pem(server)),
                    "Station__DownloadKeyFile": secret("download-key", os.urandom(32)),
                    "Station__LibraryIndexFile": str(folder / "index.json"),
                    "ConnectionStrings__SuiteStore": "Host=127.0.0.1;Port=" +
                    os.environ["PGPORT"] + ";Database=postgres;Username=turborama-suite;"
                    "Password=fixture-only-password"})
        log = (folder / "api.log").open("wb")
        candidate_dll = os.environ.get("STATION_HTTP_API_DLL")
        if candidate_dll and not Path(candidate_dll).is_file():
            raise ValueError("STATION_HTTP_API_DLL is unavailable")
        api_command = (["dotnet", candidate_dll, "--urls", base] if candidate_dll else
                       ["dotnet", "run", "--project",
                        "src/TurboRamaSuiteOnlineServer/TurboRamaSuiteOnlineServer.csproj",
                        "--no-launch-profile", "--", "--urls", base])
        api = subprocess.Popen(api_command,
                               cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            for _ in range(120):
                if api.poll() is not None:
                    raise RuntimeError("Candidate API exited during startup; inspect local test log")
                try:
                    if request(base, "GET", "/ready/station")[0] == 200:
                        break
                except (URLError, TimeoutError):
                    pass
                time.sleep(0.25)
            else:
                raise RuntimeError("Candidate API readiness timed out")

            sql("DELETE FROM suite.schema_migrations WHERE version='029_station_download_grants'")
            assert_error(request(base, "GET", "/ready/station"), 503,
                         "STATION_NOT_READY")
            sql("INSERT INTO suite.schema_migrations(version) VALUES('029_station_download_grants')")
            assert request(base, "GET", "/ready/station")[0] == 200

            identity = {"schemaVersion": 1, "productId": PRODUCT, "applicationId": PRODUCT,
                        "deviceId": device_id, "clientVersion": "test", "deviceManufacturer":
                        "Synthetic", "deviceModel": "Fixture", "androidSdk": 35}
            activation = signed_payload(request(base, "POST", "/v1/station/activations/challenge",
                dict(identity, domain=PREFIX + "request-activation-challenge/v1",
                     activationCode=code, devicePublicKey=b64(spki))), server_public,
                "activation-challenge")
            activated = signed_payload(request(base, "POST", "/v1/station/activations/complete",
                proof(device, dict(identity, domain=PREFIX + "activate/v1", activationCode=code,
                    devicePublicKey=b64(spki), challengeId=activation["challengeId"],
                    nonce=activation["nonce"]))), server_public, "activated")
            assert activated["licenseId"] == license_id
            def open_session():
                challenge = signed_payload(request(base, "POST", "/v1/station/challenges",
                    dict(identity, domain=PREFIX + "request-session-challenge/v1",
                         licenseId=license_id)), server_public, "session-challenge")
                return signed_payload(request(base, "POST", "/v1/station/sessions",
                    proof(device, dict(identity, domain=PREFIX + "open-session/v1",
                        licenseId=license_id, challengeId=challenge["challengeId"],
                        nonce=challenge["nonce"]))), server_public, "session")

            session = open_session()
            token = session["accessToken"]
            sql("INSERT INTO suite.station_customer_projection(license_id,source_system,"
                "source_purchase_id,source_item_key,customer_ref,display_name) VALUES('" +
                license_id + "','STATION_HTTP_TEST','synthetic-1','synthetic','synthetic',"
                "'Synthetic Buyer')")
            profile = signed_payload(request(base, "GET", "/v1/station/me", bearer=token),
                                     server_public, "profile")
            assert profile["displayName"] == "Synthetic Buyer"
            catalog = signed_payload(request(base, "GET", "/v1/station/catalog", bearer=token),
                                     server_public, "catalog")
            assert catalog["revision"] == 3 and len(catalog["items"]) == len(index["items"])
            assert "filePath" not in json.dumps(catalog)
            cover = request(base, "GET", "/v1/station/covers/cover-synthetic-01", bearer=token)
            assert cover[0] == 200 and cover[1]["Content-Type"] == "image/png" and cover[2] == PNG
            assert_error(request(base, "GET", "/v1/station/covers/cover-missing-01",
                                 bearer=token), 404, "STATION_COVER_NOT_FOUND")
            if extra is not None:
                for platform in sorted({row["platform"] for row in extra["items"]}):
                    row = min((row for row in extra["items"] if row["platform"] == platform),
                              key=lambda row: row["artifact"]["sizeBytes"])
                    published = next(item for item in catalog["items"]
                                     if item["itemId"] == row["itemId"])
                    assert published["name"] == row["name"] and \
                        published["coverId"] == row["coverId"]
                    image = request(base, "GET", "/v1/station/covers/" + row["coverId"],
                                    bearer=token)
                    assert image[0] == 200 and image[2] == Path(row["coverPath"]).read_bytes()
                    real_grant = signed_payload(request(base, "POST",
                        "/v1/station/downloads/authorize",
                        dict(identity, domain=PREFIX + "request-download/v1",
                             itemId=row["itemId"]), token), server_public, "download-grant")
                    assert real_grant["itemRevision"] == row["revision"] and \
                        real_grant["artifact"] == row["artifact"]
                    transfer = request(base, "GET", "/v1/station/artifacts/" +
                                       real_grant["grantId"], bearer=token)
                    assert transfer[0] == 200 and \
                        int(transfer[1]["Content-Length"]) == row["artifact"]["sizeBytes"] and \
                        hashlib.sha256(transfer[2]).hexdigest() == row["artifact"]["sha256"]

            def authorize():
                grant = signed_payload(request(base, "POST", "/v1/station/downloads/authorize",
                    dict(identity, domain=PREFIX + "request-download/v1",
                         itemId="item-synthetic-01"), token), server_public, "download-grant")
                assert grant["itemRevision"] == 3 and grant["artifact"] == descriptor
                assert grant["sessionId"] == session["sessionId"]
                assert "filePath" not in json.dumps(grant)
                return grant["grantId"]

            grant_id = authorize()
            route = "/v1/station/artifacts/" + grant_id
            artifact = request(base, "GET", route, bearer=token)
            assert artifact[0] == 200 and artifact[1]["Content-Type"] == "application/octet-stream"
            assert int(artifact[1]["Content-Length"]) == len(game)
            assert hashlib.sha256(artifact[2]).hexdigest() == descriptor["sha256"]
            assert_error(request(base, "GET", route, bearer=token), 404, "STATION_GRANT_NOT_FOUND")
            zip_grant = signed_payload(request(base, "POST", "/v1/station/downloads/authorize",
                dict(identity, domain=PREFIX + "request-download/v1",
                     itemId="item-synthetic-02"), token), server_public, "download-grant")
            assert zip_grant["artifact"] == zip_descriptor
            zip_response = request(base, "GET", "/v1/station/artifacts/" +
                                   zip_grant["grantId"], bearer=token)
            assert zip_response[0] == 200 and zip_response[2] == packed
            grant_id = authorize()
            with urlopen(Request(base + "/v1/station/artifacts/" + grant_id,
                                 headers={"Authorization": "Bearer " + token}), timeout=10) as partial:
                assert partial.status == 200 and partial.read(1) == game[:1]
            assert_error(request(base, "GET", "/v1/station/artifacts/" + grant_id,
                                 bearer=token), 404, "STATION_GRANT_NOT_FOUND")
            retry_id = authorize()
            assert retry_id != grant_id
            retry = request(base, "GET", "/v1/station/artifacts/" + retry_id, bearer=token)
            assert retry[0] == 200 and len(retry[2]) == len(game) and \
                hashlib.sha256(retry[2]).hexdigest() == descriptor["sha256"]

            grant_id = authorize()
            next_session = open_session()
            assert next_session["sessionId"] != session["sessionId"]
            assert_error(request(base, "GET", "/v1/station/artifacts/" + grant_id,
                                 bearer=next_session["accessToken"]), 404, "STATION_GRANT_NOT_FOUND")
            assert sql_value("SELECT consumed_at IS NULL FROM suite.station_download_grants "
                             "WHERE grant_id='" + grant_id + "'") == "t"
            token = next_session["accessToken"]
            session = next_session

            other = rsa.generate_private_key(public_exponent=65537, key_size=2048)
            other_spki = other.public_key().public_bytes(serialization.Encoding.DER,
                serialization.PublicFormat.SubjectPublicKeyInfo)
            assert_error(request(base, "POST", "/v1/station/challenges",
                dict(identity, domain=PREFIX + "request-session-challenge/v1",
                     deviceId=b64(hashlib.sha256(other_spki).digest()), licenseId=license_id)),
                403, "STATION_DEVICE_DENIED")

            grant_id = authorize()
            sql("UPDATE suite.station_download_grants SET expires_at=clock_timestamp()-interval '1 second' "
                "WHERE grant_id='" + grant_id + "'")
            assert_error(request(base, "GET", "/v1/station/artifacts/" + grant_id,
                                 bearer=token), 404, "STATION_GRANT_NOT_FOUND")
            grant_id = authorize()
            sql("UPDATE suite.suite_licenses SET revocation_generation=revocation_generation+1 "
                "WHERE license_id='" + license_id + "'")
            assert_error(request(base, "GET", "/v1/station/artifacts/" + grant_id,
                                 bearer=token), 404, "STATION_GRANT_NOT_FOUND")
            if os.environ.get("STATION_HTTP_REAL_TTL") == "1":
                session = open_session()
                token = session["accessToken"]
                session_started = time.monotonic()
                assert session["expiresInSeconds"] == 180
                ttl_grant = authorize()
                pending = signed_payload(request(base, "POST", "/v1/station/challenges",
                    dict(identity, domain=PREFIX + "request-session-challenge/v1",
                         licenseId=license_id)), server_public, "session-challenge")
                assert pending["expiresInSeconds"] == 60
                ttl_code = b64(os.urandom(32))
                ttl_license = "STA-" + os.urandom(16).hex().upper()
                ttl_verifier = hmac.digest(station_pepper, ttl_code.encode(), "sha256").hex()
                sql("INSERT INTO suite.suite_licenses(license_id,product_id,status,"
                    "activation_verifier,activation_expires_at,activation_consumed,license_term,"
                    "expires_at,identity_policy,maximum_active_devices,provisioning_origin,"
                    "enrollment_state,claim_mode) VALUES('" + ttl_license + "','" + PRODUCT +
                    "','ACTIVE','" + ttl_verifier + "',clock_timestamp()+interval '15 minutes',"
                    "false,'LIFETIME',NULL,'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM')")
                sql("INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,"
                    "source_item_key,source_product_sku,product_id,license_id,provisioning_state,"
                    "financial_state,last_source_version) VALUES('STATION_HTTP_TEST','synthetic-ttl',"
                    "'synthetic','STATION_ANDROID_LIFETIME_1_DEVICE','" + PRODUCT + "','" +
                    ttl_license + "','PROVISIONED','PAID',1)")
                pending_activation = signed_payload(request(base, "POST",
                    "/v1/station/activations/challenge",
                    dict(identity, domain=PREFIX + "request-activation-challenge/v1",
                         activationCode=ttl_code, devicePublicKey=b64(spki))),
                    server_public, "activation-challenge")
                assert pending_activation["expiresInSeconds"] == 60
                print("STATION TTL: waiting for activation/session challenges and grant to expire", flush=True)
                wait_until(time.monotonic() + 62)
                assert_error(request(base, "POST", "/v1/station/activations/complete",
                    proof(device, dict(identity, domain=PREFIX + "activate/v1",
                        activationCode=ttl_code, devicePublicKey=b64(spki),
                        challengeId=pending_activation["challengeId"],
                        nonce=pending_activation["nonce"]))), 409, "STATION_CHALLENGE_INVALID")
                assert_error(request(base, "POST", "/v1/station/sessions",
                    proof(device, dict(identity, domain=PREFIX + "open-session/v1",
                        licenseId=license_id, challengeId=pending["challengeId"],
                        nonce=pending["nonce"]))), 409, "STATION_CHALLENGE_INVALID")
                assert_error(request(base, "GET", "/v1/station/artifacts/" + ttl_grant,
                                     bearer=token), 404, "STATION_GRANT_NOT_FOUND")
                assert request(base, "GET", "/v1/station/me", bearer=token)[0] == 200
                retry_id = authorize()
                retry = request(base, "GET", "/v1/station/artifacts/" + retry_id, bearer=token)
                assert retry[0] == 200 and hashlib.sha256(retry[2]).hexdigest() == descriptor["sha256"]
                print("STATION TTL: 60-second expiry passed; waiting for 180-second session", flush=True)
                wait_until(session_started + 182)
                assert_error(request(base, "GET", "/v1/station/me", bearer=token),
                             401, "STATION_SESSION_INVALID")
                assert_error(request(base, "POST", "/v1/station/downloads/authorize",
                    dict(identity, domain=PREFIX + "request-download/v1",
                         itemId="item-synthetic-01"), token), 401, "STATION_SESSION_INVALID")
                session = open_session()
                token = session["accessToken"]
                assert request(base, "GET", "/v1/station/me", bearer=token)[0] == 200
                retry_id = authorize()
                retry = request(base, "GET", "/v1/station/artifacts/" + retry_id, bearer=token)
                assert retry[0] == 200 and hashlib.sha256(retry[2]).hexdigest() == descriptor["sha256"]
                print("STATION TTL: OK (real 60/180 seconds, fresh grant/session recovery)", flush=True)
            print("STATION HTTP SMOKE: OK (activation, session, signed profile/catalog/grant, "
                  "cover 200/404, raw and ZIP bytes/hash, one use, interrupted transfer and new grant, other session/device, "
                  "expiry, revocation, extra platforms=" +
                  str(len({row["platform"] for row in extra["items"]}) if extra else 0) + ")")
        finally:
            api.terminate()
            try:
                api.wait(timeout=5)
            except subprocess.TimeoutExpired:
                api.kill()
                api.wait()
            log.close()


if __name__ == "__main__":
    main()
