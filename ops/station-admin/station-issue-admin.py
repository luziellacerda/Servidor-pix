#!/usr/bin/env python3
"""Loopback helper: list Station licenses and issue an activation code.

Commerce first issue (POST .../issue-purchase) lasts 48 hours.
Human reissue (POST .../issue-code) lasts 30 minutes and always
rotates the verifier so a still-valid previous code stops working.

Binds 127.0.0.1 only. Reads the Station pepper and Suite store DSN from files
injected by systemd. Never logs pepper, DSN, token, or activation codes.
Does not activate a device. Private management registration creates a Station
license only after explicit administrator authorization; existing code actions
keep the same license.
A reissue always rotates the verifier so the previous code stops working.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import http.client
import json
import os
import re
import secrets
import subprocess
import socket
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from threading import Lock
from urllib.parse import urlparse

BIND = os.environ.get("STATION_ISSUE_BIND", "127.0.0.1")
PORT = int(os.environ.get("STATION_ISSUE_PORT", "5194"))
TOKEN_FILE = os.environ.get(
    "STATION_ISSUE_TOKEN_FILE",
    "/opt/turborama-station-20261001/secrets/station-admin.token",
)
PEPPER_FILE = os.environ.get(
    "STATION_ISSUE_PEPPER_FILE",
    "/etc/turborama-suite/station-activation-pepper",
)
ENV_FILE = os.environ.get(
    "STATION_ISSUE_DSN_FILE",
    "/etc/turborama-suite/station-5192.env",
)
LICENSE_RE = re.compile(r"^STA-[A-Z0-9_-]{6,64}$")
ACTOR_RE = re.compile(r"^[A-Za-z0-9._-]{1,128}$")
REQUEST_RE = re.compile(r"^[A-Za-z0-9]{16,64}$")
HEX64_RE = re.compile(r"^[0-9a-f]{64}$")
RATE_WINDOW = 900
RATE_MAX = 10
_rates: dict[tuple[str, str], list[float]] = {}
_rate_lock = Lock()
MANAGEMENT_SOCKET = os.environ.get('STATION_MANAGEMENT_SOCKET', '')
MANAGEMENT_TOKEN_FILE = os.environ.get('STATION_MANAGEMENT_TOKEN_FILE', '')


class ManagementFailure(Exception):
    def __init__(self, status: int, code: str):
        self.status, self.code = status, code


def management_request(method: str, path: str, body: dict | None = None,
                       actor: str = 'station-helper', controls: dict | None = None,
                       claim: str = 'station.licenses.read') -> tuple[int, dict]:
    if not MANAGEMENT_SOCKET or not MANAGEMENT_TOKEN_FILE:
        raise ManagementFailure(503, 'STATION_MANAGEMENT_UNAVAILABLE')
    if not ACTOR_RE.fullmatch(actor) or not path.startswith('/station/'):
        raise ManagementFailure(400, 'STATION_REQUEST_INVALID')
    class UnixHttp(http.client.HTTPConnection):
        def connect(self):
            self.sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
            self.sock.settimeout(12)
            self.sock.connect(MANAGEMENT_SOCKET)
    try:
        token = open(MANAGEMENT_TOKEN_FILE, encoding='utf-8').read().strip()
        headers = {'Content-Type': 'application/json', 'X-Suite-Admin-Token': token,
                   'X-Suite-Admin-Actor': actor, 'X-Suite-Admin-Claims': claim,
                   'X-Suite-Client-Ip-Digest': (controls or {}).get('clientIpDigest', hashlib.sha256(b'station-internal-reader').hexdigest())}
        if controls:
            headers.update({'X-Suite-Csrf-Verified': '1',
                            'X-Suite-Step-Up-At': str(controls['stepUpAt'])})
        connection = UnixHttp('localhost')
        try:
            connection.request(method, path, None if body is None else json.dumps(body), headers)
            response = connection.getresponse()
            data = response.read(1024 * 1024 + 1)
            if len(data) > 1024 * 1024:
                raise ValueError('bounded admin response')
            return response.status, json.loads(data) if data else {}
        finally:
            connection.close()
    except ManagementFailure:
        raise
    except Exception:
        raise ManagementFailure(503, 'STATION_MANAGEMENT_UNAVAILABLE') from None


def register_customer(body: dict) -> tuple[int, dict]:
    actor, request_id = body.get('actor'), body.get('requestId')
    reason, step_up, digest = body.get('reason'), body.get('stepUpAt'), body.get('clientIpDigest')
    if (not isinstance(actor, str) or not ACTOR_RE.fullmatch(actor) or
        not isinstance(request_id, str) or not re.fullmatch(r'[a-f0-9]{32}', request_id) or
        not isinstance(reason, str) or not 10 <= len(reason) <= 200 or any(ord(c) < 32 for c in reason) or
        type(step_up) is not int or abs(time.time() - step_up) > 300 or
        not isinstance(digest, str) or not HEX64_RE.fullmatch(digest) or body.get('csrfVerified') is not True or
        not isinstance(body.get('customerRef'), str) or not re.fullmatch(r'TBX-USER-[1-9][0-9]{0,11}', body['customerRef']) or
        not isinstance(body.get('displayName'), str) or not 1 <= len(body['displayName']) <= 256 or
        body.get('grantKind') not in ('paid', 'courtesy', 'test') or type(body.get('allowAdditional')) is not bool):
        raise ManagementFailure(400, 'STATION_REGISTRATION_INVALID')
    if not rate_ok(actor, 'registration', 20):
        raise ManagementFailure(429, 'STATION_RATE_LIMITED')
    payload = {key: body[key] for key in ('requestId', 'customerRef', 'displayName', 'grantKind', 'reason', 'allowAdditional')}
    return management_request('POST', '/station/registrations', payload, actor, body, 'station.licenses.manage')


def management_action(license_id: str, action: str, body: dict) -> tuple[int, dict]:
    actor, request_id = body.get('actor'), body.get('requestId')
    generation, activation = body.get('expectedGeneration'), body.get('expectedActivationGeneration')
    reason, step_up, digest = body.get('reason'), body.get('stepUpAt'), body.get('clientIpDigest')
    if (not isinstance(actor, str) or not ACTOR_RE.fullmatch(actor) or
        not isinstance(request_id, str) or not re.fullmatch(r'[A-Za-z0-9]{16,60}', request_id) or
        type(generation) is not int or generation < 0 or type(activation) is not int or activation < 0 or
        not isinstance(reason, str) or not 10 <= len(reason) <= 256 or any(ord(c) < 32 for c in reason) or
        type(step_up) is not int or abs(time.time() - step_up) > 300 or
        not isinstance(digest, str) or not HEX64_RE.fullmatch(digest) or body.get('csrfVerified') is not True):
        raise ManagementFailure(400, 'STATION_REQUEST_INVALID')
    if not rate_ok(actor, license_id, 20):
        raise ManagementFailure(429, 'STATION_RATE_LIMITED')
    if action == 'issue-code':
        return 200, issue_code(license_id, actor, request_id, expected_generation=generation,
                               expected_activation=activation,reason=reason,controls=body)
    backend_action = 'transfer' if action in ('reinstall', 'new-device') else action
    if backend_action not in ('transfer', 'block', 'unblock', 'cancel-code', 'revoke-session'):
        raise ManagementFailure(400, 'STATION_REQUEST_INVALID')
    payload = {'requestId': request_id + ('T' if action in ('reinstall', 'new-device') else ''),
               'expectedGeneration': generation, 'expectedActivationGeneration': activation, 'reason': reason,
               'targetSessionId': body.get('targetSessionId')}
    claim = 'station.sessions.revoke' if backend_action == 'revoke-session' else 'station.licenses.manage'
    status, result = management_request('POST', '/station/licenses/' + license_id + '/actions/' + backend_action,
                                        payload, actor, body, claim)
    if status != 200 or action not in ('reinstall', 'new-device'):
        return status, result
    try:
        issued = issue_code(license_id, actor, request_id + 'I', expected_generation=generation + 1,
                            expected_activation=activation + 1,reason=reason,controls=body)
        return 200, dict(issued, transferCompleted=True)
    except ManagementFailure as error:
        if error.code == 'STATION_REQUEST_ALREADY_COMPLETED':
            return 409, {'code': error.code, 'transferCompleted': True}
        return 202, {'code': 'STATION_RECOVERY_CODE_PENDING', 'transferCompleted': True}
    except ValueError as error:
        if str(error) == 'STATION_REQUEST_ALREADY_COMPLETED':
            return 409, {'code': str(error), 'transferCompleted': True}
        return 202, {'code': 'STATION_RECOVERY_CODE_PENDING', 'transferCompleted': True}
    except Exception:
        return 202, {'code': 'STATION_RECOVERY_CODE_PENDING', 'transferCompleted': True}


def log(message: str) -> None:
    sys.stderr.write(message + "\n")
    sys.stderr.flush()


def load_token() -> str:
    token = open(TOKEN_FILE, "r", encoding="utf-8").read().strip()
    if len(token) < 32:
        raise RuntimeError("admin token is too short")
    return token


def load_pepper() -> bytes:
    raw = open(PEPPER_FILE, "r", encoding="utf-8").read().strip()
    pepper = base64.b64decode(raw, validate=True)
    if len(pepper) < 32:
        raise RuntimeError("station pepper is invalid")
    return pepper


def parse_dsn(raw: str) -> dict[str, str]:
    values: dict[str, str] = {}
    for part in raw.strip().strip(";").split(";"):
        if "=" not in part:
            continue
        key, value = part.split("=", 1)
        values[key.strip()] = value.strip()
    return values


def load_dsn() -> dict[str, str]:
    raw = os.environ.get("ConnectionStrings__SuiteStore", "")
    if not raw and os.path.isfile(ENV_FILE):
        for line in open(ENV_FILE, "r", encoding="utf-8"):
            if line.startswith("ConnectionStrings__SuiteStore="):
                raw = line.split("=", 1)[1].strip()
                break
    if not raw:
        raise RuntimeError("suite store dsn missing")
    dsn = parse_dsn(raw)
    if "Database" not in dsn and "database" not in dsn:
        raise RuntimeError("suite store dsn incomplete")
    return {key.lower(): value for key, value in dsn.items()}


def psql(sql: str) -> str:
    dsn = load_dsn()
    database = dsn.get("database") or dsn.get("dbname") or "postgres"
    user = dsn.get("username") or dsn.get("user") or "turborama-suite"
    env = os.environ.copy()
    cmd = [
        "/usr/bin/psql",
        "-v",
        "ON_ERROR_STOP=1",
        "-A",
        "-t",
        "-F",
        "\t",
        "-d",
        database,
        "-U",
        user,
        "-q",
    ]
    host = dsn.get("host", "")
    # Peer auth on the unix socket; TCP would need the password.
    if host and host not in ("", "/var/run/postgresql", "/run/postgresql"):
        if host in ("127.0.0.1", "localhost") and not dsn.get("password"):
            pass
        elif dsn.get("password"):
            env["PGPASSWORD"] = dsn["password"]
            cmd.extend(["-h", host])
            if dsn.get("port"):
                cmd.extend(["-p", dsn["port"]])
    proc = subprocess.run(
        cmd,
        input=sql,
        text=True,
        capture_output=True,
        env=env,
        check=False,
        timeout=12,
    )
    if proc.returncode != 0:
        err = (proc.stderr or "").strip().splitlines()
        safe = err[-1][:180] if err else "psql failed"
        log("psql error: " + safe)
        raise RuntimeError("STATION_ADMIN_UNAVAILABLE")
    return proc.stdout


def json_bytes(payload: dict[str, Any], status: int) -> tuple[int, bytes]:
    body = json.dumps(payload, separators=(",", ":"), ensure_ascii=True).encode("utf-8")
    return status, body


def rate_ok(actor: str, license_id: str, maximum: int = RATE_MAX) -> bool:
    with _rate_lock:
        now = time.time()
        key = (actor, license_id)
        if len(_rates) >= 4096:
            for old in [k for k, hits in _rates.items() if not hits or now-hits[-1] >= RATE_WINDOW]:
                del _rates[old]
        if key not in _rates and len(_rates) >= 4096:
            return False
        hits = [stamp for stamp in _rates.get(key, []) if now - stamp < RATE_WINDOW]
        if len(hits) >= maximum:
            _rates[key] = hits
            return False
        hits.append(now)
        _rates[key] = hits
        return True


def list_licenses() -> dict[str, Any]:
    sql = """
SELECT l.license_id,
       l.status,
       l.enrollment_state,
       CASE WHEN l.activation_consumed THEN 't' ELSE 'f' END,
       CASE WHEN l.activation_verifier IS NOT NULL
                 AND l.activation_expires_at > clock_timestamp()
            THEN 't' ELSE 'f' END,
       CASE WHEN l.activation_expires_at IS NULL THEN ''
            ELSE to_char(l.activation_expires_at AT TIME ZONE 'UTC',
                         'YYYY-MM-DD"T"HH24:MI:SS"Z"') END,
       d.provisioning_state,
       d.financial_state,
       d.source_purchase_id,
       d.source_item_key,
       coalesce(p.display_name, ''),
       coalesce(p.customer_ref, ''),
       coalesce(l.activation_generation, 0)
FROM suite.suite_licenses l
JOIN suite.suite_license_deliveries d
  ON d.license_id = l.license_id
 AND d.product_id = 'TURBORAMA_STATION_ANDROID'
LEFT JOIN suite.station_customer_projection p
  ON p.license_id = l.license_id
WHERE l.product_id = 'TURBORAMA_STATION_ANDROID'
ORDER BY l.updated_at DESC
LIMIT 200;
"""
    rows = []
    for line in psql(sql).splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        if len(parts) != 13:
            continue
        consumed = parts[3] == "t"
        code_valid = parts[4] == "t"
        enrollment = parts[2]
        status = parts[1]
        provisioning = parts[6]
        financial = parts[7]
        try:
            generation = int(parts[12] or "0")
        except ValueError:
            generation = 0
        reason = None
        eligible = False
        if consumed or enrollment in ("ENROLLED", "BOUND"):
            reason = "ALREADY_ACTIVATED"
        elif status != "ACTIVE":
            reason = "SUSPENDED" if status == "SUSPENDED" else "NOT_ACTIVE"
        elif provisioning != "PROVISIONED" or financial != "PAID":
            reason = "NOT_PAID"
        elif enrollment != "PENDING_ENROLLMENT":
            reason = "NOT_PENDING"
        else:
            eligible = True
        rows.append(
            {
                "licenseId": parts[0],
                "status": status,
                "enrollmentState": enrollment,
                "activationConsumed": consumed,
                "codeValid": code_valid,
                "activationExpiresAt": parts[5] or None,
                "provisioningState": provisioning,
                "financialState": financial,
                "sourcePurchaseId": parts[8],
                "sourceItemKey": parts[9],
                "displayName": parts[10],
                "customerRef": parts[11],
                "activationGeneration": generation,
                "firstIssuePending": generation == 0,
                "nextTtlMinutes": 30,
                "purchaseTtlMinutes": 2880,
                "eligible": eligible,
                "blockedReason": reason,
            }
        )
    return {"licenses": rows}


def issue_code(license_id: str, actor: str, request_id: str, kind: str = "human",
               expected_generation: int | None = None, expected_activation: int | None = None,
               reason: str | None = None, controls: dict | None = None) -> dict[str, Any]:
    if MANAGEMENT_SOCKET or MANAGEMENT_TOKEN_FILE:
        if not LICENSE_RE.fullmatch(license_id) or not ACTOR_RE.fullmatch(actor) or not REQUEST_RE.fullmatch(request_id):
            raise ValueError('STATION_REQUEST_INVALID')
        controls=controls or {'clientIpDigest': hashlib.sha256(b'authenticated-station-helper').hexdigest(), 'stepUpAt':int(time.time())}
        status,result=management_request('POST','/station/licenses/'+license_id+'/actions/replace-code',
            {'actor':actor,'requestId':request_id,'expectedGeneration':expected_generation,
             'expectedActivationGeneration':expected_activation,'kind':kind,'reason':reason}, actor,controls,'station.licenses.manage')
        if status!=200:
            raise ManagementFailure(status,result.get('code','STATION_ADMIN_UNAVAILABLE'))
        return result
    if not LICENSE_RE.fullmatch(license_id):
        raise ValueError("STATION_LICENSE_INVALID")
    if not ACTOR_RE.fullmatch(actor) or not REQUEST_RE.fullmatch(request_id):
        raise ValueError("STATION_REQUEST_INVALID")
    if kind not in ("human", "purchase"):
        raise ValueError("STATION_REQUEST_INVALID")
    ttl_sql = "interval '48 hours'" if kind == "purchase" else "interval '30 minutes'"
    ttl_minutes = 2880 if kind == "purchase" else 30
    detail = "PURCHASE_FIRST" if kind == "purchase" else "ADMIN_REISSUE"
    last_error = "STATION_ADMIN_UNAVAILABLE"
    for _ in range(3):
        code_bytes = secrets.token_bytes(32)
        code = base64.urlsafe_b64encode(code_bytes).decode("ascii").rstrip("=")
        pepper = load_pepper()
        try:
            verifier = hmac.new(pepper, code.encode("utf-8"), hashlib.sha256).hexdigest()
        finally:
            pepper = b"\x00" * len(pepper)
        if not HEX64_RE.fullmatch(verifier):
            raise RuntimeError("STATION_ADMIN_UNAVAILABLE")
        sql = f"""
BEGIN;
DO $$BEGIN PERFORM pg_advisory_xact_lock(hashtextextended('station-issue:{license_id}', 0)); END$$;
UPDATE suite.suite_licenses l SET
  activation_verifier = '{verifier}',
  activation_expires_at = clock_timestamp() + {ttl_sql},
  activation_consumed = false,
  activation_generation = coalesce(l.activation_generation, 0) + 1,
  updated_at = clock_timestamp()
WHERE l.license_id = '{license_id}'
  AND l.product_id = 'TURBORAMA_STATION_ANDROID'
  AND l.status = 'ACTIVE'
  AND l.enrollment_state = 'PENDING_ENROLLMENT'
  AND l.activation_consumed = false
  AND EXISTS (
        SELECT 1 FROM suite.suite_license_deliveries d
        WHERE d.license_id = l.license_id
          AND d.product_id = 'TURBORAMA_STATION_ANDROID'
          AND d.provisioning_state = 'PROVISIONED'
          AND d.financial_state = 'PAID'
      );
INSERT INTO suite.suite_audit_events(
  event_type, license_id, correlation_id, outcome, detail_code,
  admin_actor, request_id)
SELECT 'STATION_CODE_ISSUED', '{license_id}', '{request_id}',
       'SUCCESS', '{detail}', '{actor}', '{request_id}'
WHERE EXISTS (
  SELECT 1 FROM suite.suite_licenses
  WHERE license_id = '{license_id}'
    AND activation_verifier = '{verifier}'
);
SELECT coalesce(l.license_id, ''),
       coalesce(l.status, 'MISSING'),
       coalesce(l.enrollment_state, ''),
       CASE WHEN l.activation_consumed THEN 't' ELSE 'f' END,
       coalesce(d.provisioning_state, ''),
       coalesce(d.financial_state, ''),
       CASE WHEN l.activation_verifier = '{verifier}'
            THEN to_char(l.activation_expires_at AT TIME ZONE 'UTC',
                         'YYYY-MM-DD"T"HH24:MI:SS"Z"')
            ELSE '' END,
       coalesce(l.activation_generation, 0)
FROM suite.suite_licenses l
LEFT JOIN suite.suite_license_deliveries d
  ON d.license_id = l.license_id
 AND d.product_id = 'TURBORAMA_STATION_ANDROID'
WHERE l.license_id = '{license_id}'
  AND l.product_id = 'TURBORAMA_STATION_ANDROID';
COMMIT;
"""
        try:
            raw = psql(sql)
        except RuntimeError:
            last_error = "STATION_ADMIN_UNAVAILABLE"
            continue
        lines = [line.strip() for line in raw.splitlines() if line.strip()]
        if not lines:
            raise ValueError("STATION_NOT_FOUND")
        parts = lines[-1].split("\t")
        if len(parts) != 8:
            last_error = "STATION_ADMIN_UNAVAILABLE"
            continue
        found_id, status, enrollment, consumed, provisioning, financial, expires, gen_raw = parts
        if found_id == "" or found_id == "MISSING":
            raise ValueError("STATION_NOT_FOUND")
        if expires:
            try:
                generation = int(gen_raw or "0")
            except ValueError:
                generation = 0
            log("station code issued actor=" + actor + " kind=" + kind)
            return {
                "licenseId": license_id,
                "activationCode": code,
                "expiresAt": expires,
                "ttlMinutes": ttl_minutes,
                "firstIssue": kind == "purchase",
                "kind": kind,
                "activationGeneration": generation,
            }
        if consumed == "t" or enrollment in ("ENROLLED", "BOUND"):
            raise ValueError("STATION_ALREADY_ACTIVATED")
        raise ValueError("STATION_DELIVERY_NOT_ELIGIBLE")
    raise RuntimeError(last_error)


class Handler(BaseHTTPRequestHandler):
    server_version = "station-issue-admin/3"
    sys_version = ""

    def log_message(self, fmt: str, *args: Any) -> None:
        log("%s %s" % (self.address_string(), fmt % args))

    def _send(self, status: int, payload: dict[str, Any]) -> None:
        status, body = json_bytes(payload, status)
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _auth(self) -> bool:
        header = self.headers.get("X-Station-Admin-Token", "")
        if not header:
            auth = self.headers.get("Authorization", "")
            if auth.startswith("Bearer "):
                header = auth[7:]
        try:
            expected = load_token()
        except Exception:
            self._send(503, {"code": "STATION_ADMIN_UNAVAILABLE"})
            return False
        got = header.encode("utf-8")
        want = expected.encode("utf-8")
        ok = len(got) == len(want) and hmac.compare_digest(got, want)
        if not ok:
            self._send(401, {"code": "STATION_ADMIN_UNAUTHORIZED"})
        return ok

    def do_GET(self) -> None:
        path = urlparse(self.path).path
        if path == "/health":
            self._send(200, {"status": "ok"})
            return
        if not self._auth():
            return
        if path == '/management/licenses' or re.fullmatch(r'/management/licenses/STA-[A-Z0-9_-]{6,64}/support', path):
            try:
                query = urlparse(self.path).query
                status, result = management_request('GET', '/station' + path[len('/management'):] + ('?' + query if query else ''))
                self._send(status, result)
            except ManagementFailure as e:
                self._send(e.status, {'code': e.code})
            return
        if path == "/licenses":
            try:
                self._send(200, list_licenses())
            except Exception:
                self._send(503, {"code": "STATION_ADMIN_UNAVAILABLE"})
            return
        self._send(404, {"code": "STATION_NOT_FOUND"})

    def do_POST(self) -> None:
        if not self._auth():
            return
        path = urlparse(self.path).path
        if path == '/management/registrations':
            try:
                length = int(self.headers.get('Content-Length', '0'))
                if not 2 <= length <= 8192:
                    raise ManagementFailure(400, 'STATION_REGISTRATION_INVALID')
                body = json.loads(self.rfile.read(length))
                if not isinstance(body, dict):
                    raise ManagementFailure(400, 'STATION_REGISTRATION_INVALID')
                status, result = register_customer(body)
                self._send(status, result)
            except ManagementFailure as e:
                self._send(e.status, {'code': e.code})
            except (ValueError, TypeError):
                self._send(400, {'code': 'STATION_REGISTRATION_INVALID'})
            except Exception:
                self._send(503, {'code': 'STATION_ADMIN_UNAVAILABLE'})
            return
        managed = re.fullmatch(r'/management/licenses/(STA-[A-Z0-9_-]{6,64})/(issue-code|reinstall|new-device|transfer|block|unblock|cancel-code|revoke-session)', path)
        if managed:
            try:
                length = int(self.headers.get('Content-Length', '0'))
                if not 2 <= length <= 8192:
                    raise ManagementFailure(400, 'STATION_REQUEST_INVALID')
                data = json.loads(self.rfile.read(length))
                if not isinstance(data, dict):
                    raise ManagementFailure(400, 'STATION_REQUEST_INVALID')
                status, result = management_action(managed[1], managed[2], data)
                self._send(status, result)
            except ManagementFailure as e:
                self._send(e.status, {'code': e.code})
            except ValueError as e:
                code = str(e)
                self._send(409 if code.startswith('STATION_') else 400, {'code': code if code.startswith('STATION_') else 'STATION_REQUEST_INVALID'})
            except Exception:
                self._send(503, {'code': 'STATION_ADMIN_UNAVAILABLE'})
            return
        match = re.fullmatch(
            r"/licenses/(STA-[A-Z0-9_-]{6,64})/(issue-code|issue-purchase)",
            path,
        )
        if not match:
            self._send(404, {"code": "STATION_NOT_FOUND"})
            return
        license_id = match.group(1)
        kind = "purchase" if match.group(2) == "issue-purchase" else "human"
        length = int(self.headers.get("Content-Length") or "0")
        if length < 2 or length > 8192:
            self._send(400, {"code": "STATION_REQUEST_INVALID"})
            return
        raw = self.rfile.read(length)
        try:
            body = json.loads(raw.decode("utf-8"))
        except Exception:
            self._send(400, {"code": "STATION_REQUEST_INVALID"})
            return
        if not isinstance(body, dict):
            self._send(400, {"code": "STATION_REQUEST_INVALID"})
            return
        actor = str(body.get("actor") or "")
        request_id = str(body.get("requestId") or "")
        if not rate_ok(actor or "unknown", license_id):
            self._send(429, {"code": "STATION_RATE_LIMITED"})
            return
        try:
            self._send(200, issue_code(license_id, actor, request_id, kind))
        except ManagementFailure as error:
            self._send(error.status, {'code': error.code})
        except ValueError as error:
            code = str(error)
            status = 404 if code == "STATION_NOT_FOUND" else 409
            if code == "STATION_LICENSE_INVALID" or code == "STATION_REQUEST_INVALID":
                status = 400
            self._send(status, {"code": code})
        except Exception:
            self._send(503, {"code": "STATION_ADMIN_UNAVAILABLE"})


def main() -> None:
    if BIND != "127.0.0.1":
        raise SystemExit("refusing to bind outside loopback")
    load_token()
    load_pepper()
    load_dsn()
    server = ThreadingHTTPServer((BIND, PORT), Handler)
    log("listening %s:%s" % (BIND, PORT))
    server.serve_forever()


if __name__ == "__main__":
    main()
