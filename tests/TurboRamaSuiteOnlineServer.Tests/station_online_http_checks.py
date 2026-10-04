"""Online checks against the smoke test's isolated API and PostgreSQL only."""

from concurrent.futures import ThreadPoolExecutor
import hashlib
import importlib.util
import json
import time
import uuid


def run(base, root, index, pepper, server_public, execute_sql, primary_identity,
        primary_session, request, signed_payload, exclusive=True):
    helper_path = root / "docs/station-android/scripts/verificar-http-release-station.py"
    spec = importlib.util.spec_from_file_location("station_online_fixture", helper_path)
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    guest = helper.StationReleaseVerification(execute_sql, pepper, server_public, index)
    checks = 0

    def check(ok, label):
        nonlocal checks
        checks += 1
        if not ok:
            raise AssertionError("Station online: " + label)

    def online(identity, session, command, route="command"):
        response = request(base, "POST", "/v1/station/online/" + route,
                           command, session["accessToken"])
        check(response[0] == 200, "authenticated online response")
        check(response[1].get("Cache-Control") == "no-store" and
              response[1].get("X-Content-Type-Options") == "nosniff", "private response")
        check(len(response[2]) <= 524288, "Android envelope bound")
        payload = signed_payload(response, server_public, "online")
        check(payload["licenseId"] == session["licenseId"] and
              payload["deviceId"] == identity["deviceId"] and
              payload["sessionId"] == session["sessionId"] and
              payload["requestId"] == command["requestId"], "signed request and session binding")
        return payload["snapshot"]

    def command(action, **values):
        return dict(action=action, requestId=str(uuid.uuid4()), **values)

    def denied(identity, session, body, status, code, route="command"):
        response = request(base, "POST", "/v1/station/online/" + route,
                           body, session["accessToken"])
        check(response[0] == status and json.loads(response[2])["code"] == code,
              "expected online rejection")

    a = lambda body: online(primary_identity, primary_session, body)
    try:
        guest.create()
        challenge = guest.signed(guest.request(base, "POST", "/v1/station/activations/challenge",
            dict(guest.identity, domain=helper.PREFIX + "request-activation-challenge/v1",
                 activationCode=guest.code, devicePublicKey=guest.spki)), "activation-challenge")
        guest.signed(guest.request(base, "POST", "/v1/station/activations/complete",
            guest.proof(dict(guest.identity, domain=helper.PREFIX + "activate/v1",
                activationCode=guest.code, devicePublicKey=guest.spki,
                challengeId=challenge["challengeId"], nonce=challenge["nonce"]))), "activated")
        challenge = guest.signed(guest.request(base, "POST", "/v1/station/challenges",
            dict(guest.identity, domain=helper.PREFIX + "request-session-challenge/v1",
                 licenseId=guest.license_id)), "session-challenge")
        guest_session = guest.signed(guest.request(base, "POST", "/v1/station/sessions",
            guest.proof(dict(guest.identity, domain=helper.PREFIX + "open-session/v1",
                licenseId=guest.license_id, challengeId=challenge["challengeId"],
                nonce=challenge["nonce"]))), "session")
        b = lambda body: online(guest.identity, guest_session, body)
        first = a(command("enter", nickname="Synthetic host"))
        second = b(command("enter", nickname="Synthetic guest"))
        check(first["selfId"] != second["selfId"] and
              (second["totalPeers"] == 2 if exclusive else second["totalPeers"] >= 2),
              "two independently activated licensed identities")
        check(primary_session["licenseId"] not in json.dumps(second["peers"]) and
              primary_identity["deviceId"] not in json.dumps(second["peers"]), "public peer privacy")
        engine = first["engines"][0]
        row = next(row for row in index["items"] if row["platform"] == engine["platform"]
                   and row["artifact"]["format"] == "raw" and row.get("catalogVisible") is not False)
        hashes = dict(contentSha256=row["artifact"]["sha256"],
                      optionsSha256=hashlib.sha256(b"synthetic-options").hexdigest(),
                      coreSha256=engine["coreSha256"], runtimeSha256=engine["runtimeSha256"])
        create = command("create", itemId=row["itemId"], engineId=engine["engineId"], **hashes)
        room = a(create)["room"]
        rid = room["roomId"]
        check(a(create)["room"]["roomId"] == rid, "create retry preserves room")
        outsider = b(command("heartbeat"))
        check(outsider["room"] is None and room["connectionPassword"] not in json.dumps(outsider),
              "nonmember receives no connection secret")
        denied(guest.identity, guest_session, command("chat", roomId=rid, text="outsider"),
               404, "STATION_ONLINE_ROOM_NOT_FOUND")
        a(command("invite", roomId=rid, peerId=second["selfId"]))
        page = b(command("heartbeat", page=1))
        check(not page["rooms"] and page["invites"][0]["itemId"] == row["itemId"],
              "invitation usable outside visible room page")
        wrong = dict(hashes, contentSha256="0" * 64)
        denied(guest.identity, guest_session, command("join", roomId=rid, **wrong),
               409, "STATION_ONLINE_BUILD_MISMATCH")
        check(b(command("join", roomId=rid, **hashes))["room"]["members"] ==
              [first["selfId"], second["selfId"]], "same ROM core runtime options join")
        message = command("chat", roomId=rid, text="Synthetic message")
        a(message)
        a(message)
        check(len(b(command("heartbeat"))["room"]["messages"]) == 1, "chat retry deduplicated")
        a(command("ready", roomId=rid, value=True))
        b(command("ready", roomId=rid, value=True))
        check(a(command("start", roomId=rid, address="192.0.2.10", port=55435))["room"]["state"] ==
              "starting", "guest held until actual host listening signal")
        check(a(command("host-listening", roomId=rid))["room"]["state"] == "connecting",
              "host signal releases guest without claiming P2P established")
        current = b(command("heartbeat"))
        event = dict(requestId=str(uuid.uuid4()), instance=current["instance"],
                     revision=current["revision"], page=0)
        with ThreadPoolExecutor(max_workers=1) as pool:
            pending = pool.submit(request, base, "POST", "/v1/station/online/events",
                                  event, guest_session["accessToken"])
            time.sleep(0.15)
            denied(guest.identity, guest_session, dict(event, requestId=str(uuid.uuid4())),
                   429, "STATION_ONLINE_POLL_EXISTS", route="events")
            execute_sql("UPDATE suite.suite_licenses SET revocation_generation=revocation_generation+1 "
                        "WHERE license_id='" + guest.license_id + "'")
            a(command("leave"))
            response = pending.result(timeout=5)
            check(response[0] == 401 and json.loads(response[2])["code"] == "STATION_SESSION_INVALID",
                  "database revocation rechecked after long poll")
            check(room["connectionPassword"] not in response[2].decode(), "no secret after revocation")
        remaining = a(command("heartbeat"))
        check(remaining["room"] is None and
              (remaining["totalPeers"] == 1 if exclusive else
               all(peer["peerId"] != second["selfId"] for peer in remaining["peers"])),
              "revoked guest and departed room removed")
        a(command("offline"))
    finally:
        check(guest.cleanup(), "synthetic guest cleanup")
    result = {"passed": True, "checks": checks, "twoSyntheticLicenses": True,
              "scope": "isolated PostgreSQL" if exclusive else "authorized synthetic production probe",
              "p2pGameVerified": False}
    print(json.dumps(result))
    return result
