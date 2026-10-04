#!/usr/bin/env python3
"""Station release verification using one disposable synthetic license.

Imported by the bounded rollout or the isolated HTTP smoke test. Credentials,
private paths and signed response envelopes never enter the returned report.
"""

from collections import Counter
import base64
import csv
import hashlib
import hmac
import json
import os
from pathlib import Path
import uuid
from urllib.error import HTTPError
from urllib.request import HTTPRedirectHandler, Request, build_opener

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa


PRODUCT = "TURBORAMA_STATION_ANDROID"
PREFIX = "TurboRamaStationAndroid/"


def b64(data):
    return base64.urlsafe_b64encode(data).decode().rstrip("=")


class RejectRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class StationReleaseVerification:
    def __init__(self, execute_sql, pepper, server_public, index):
        self.execute_sql = execute_sql
        self.pepper = pepper
        self.server_public = server_public
        self.index = index
        self.license_id = "STA-" + os.urandom(16).hex().upper()
        self.marker = uuid.uuid4().hex
        self.code = b64(os.urandom(32))
        self.device = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        spki = self.device.public_key().public_bytes(serialization.Encoding.DER,
                                                    serialization.PublicFormat.SubjectPublicKeyInfo)
        self.spki = b64(spki)
        self.identity = dict(schemaVersion=1, productId=PRODUCT, applicationId=PRODUCT,
            deviceId=b64(hashlib.sha256(spki).digest()), clientVersion="station-release-check-20261003",
            deviceManufacturer="Synthetic", deviceModel="Release verification", androidSdk=35)
        self.created = False
        self.activated = False
        self.opener = build_opener(RejectRedirect)

    def create(self):
        verifier = hmac.digest(self.pepper, self.code.encode(), "sha256").hex()
        self.execute_sql("BEGIN; SET LOCAL lock_timeout='5s'; "
            "INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,"
            "activation_expires_at,activation_consumed,license_term,expires_at,identity_policy,"
            "maximum_active_devices,provisioning_origin,enrollment_state,claim_mode) VALUES('" +
            self.license_id + "','" + PRODUCT + "','ACTIVE','" + verifier +
            "',clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,'SOFTWARE_ONLY',"
            "1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM'); "
            "INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,"
            "source_item_key,source_product_sku,product_id,license_id,provisioning_state,"
            "financial_state,last_source_version) VALUES('STATION_ROLLOUT_TEST','" + self.marker +
            "','synthetic','STATION_ANDROID_LIFETIME_1_DEVICE','" + PRODUCT + "','" +
            self.license_id + "','PROVISIONED','PAID',1); "
            "INSERT INTO suite.station_customer_projection(license_id,source_system,source_purchase_id,"
            "source_item_key,customer_ref,display_name) VALUES('" + self.license_id +
            "','STATION_ROLLOUT_TEST','" + self.marker +
            "','synthetic','synthetic','Synthetic Station verification'); COMMIT;")
        self.created = True

    def cleanup(self):
        if not self.created:
            return True
        statement = "BEGIN; SET LOCAL lock_timeout='5s'; DO $$ BEGIN IF NOT EXISTS ("
        statement += "SELECT 1 FROM suite.suite_license_deliveries WHERE license_id='" + self.license_id
        statement += "' AND source_system='STATION_ROLLOUT_TEST' AND source_purchase_id='" + self.marker
        statement += "') THEN RAISE EXCEPTION 'synthetic ownership marker changed'; END IF; END $$; "
        for table in ("station_download_grants", "station_sessions", "station_challenges",
                      "station_customer_projection", "station_devices", "suite_license_deliveries",
                      "suite_licenses"):
            statement += "DELETE FROM suite." + table + " WHERE license_id='" + self.license_id + "'; "
        statement += "COMMIT; SELECT count(*) FROM suite.suite_licenses WHERE license_id='"
        statement += self.license_id + "';"
        removed = self.execute_sql(statement).strip() == "0"
        if removed:
            self.created = False
        return removed

    def request(self, base, method, route, payload=None, bearer=None):
        correlation = uuid.uuid4().hex
        headers = {"Accept":"application/json", "X-Correlation-ID":correlation,
            "User-Agent":"Dalvik/2.1.0 (Linux; U; Android 13; Station Release Verification)"}
        if bearer:
            headers["Authorization"] = "Bearer " + bearer
        body = None
        if payload is not None:
            body = json.dumps(payload, separators=(",",":")).encode()
            headers["Content-Type"] = "application/json"
        try:
            response = self.opener.open(Request(base + route, body, headers, method=method), timeout=30)
        except HTTPError as error:
            response = error
        with response:
            if response.headers.get("X-Correlation-ID") != correlation or response.url != base + route:
                raise ValueError("HTTP correlation or redirect policy failed")
            data = response.read(64 * 1024 * 1024 + 1)
            if len(data) > 64 * 1024 * 1024:
                raise ValueError("verification response exceeded limit")
            return response.code, response.headers, data

    def signed(self, response, domain):
        if response[0] != 200:
            raise ValueError("signed endpoint status " + str(response[0]))
        envelope = json.loads(response[2])
        payload = base64.urlsafe_b64decode(envelope["payload"] + "===")
        signature = base64.urlsafe_b64decode(envelope["signature"] + "===")
        self.server_public.verify(signature, payload,
            padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
        spki = self.server_public.public_bytes(serialization.Encoding.DER,
                                               serialization.PublicFormat.SubjectPublicKeyInfo)
        if envelope["keyId"] != hashlib.sha256(spki).hexdigest():
            raise ValueError("assertion key identity differs")
        result = json.loads(payload)
        if result["domain"] != PREFIX + domain + "/v1" or \
                result["productId"] != PRODUCT or result["applicationId"] != PRODUCT or \
                result["schemaVersion"] != 1 or \
                (domain != "activation-challenge" and result["licenseId"] != self.license_id) or \
                result["deviceId"] != self.identity["deviceId"]:
            raise ValueError("signed identity differs")
        return result

    def proof(self, payload):
        data = json.dumps(payload, separators=(",",":")).encode()
        signature = self.device.sign(data,
            padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
        return {"payload":b64(data), "signature":b64(signature)}

    def check(self, base, catalog_output=None):
        if not self.created:
            raise ValueError("create the synthetic license first")
        if not self.activated:
            challenge = self.signed(self.request(base,"POST","/v1/station/activations/challenge",
                dict(self.identity, domain=PREFIX+"request-activation-challenge/v1",
                     activationCode=self.code, devicePublicKey=self.spki)), "activation-challenge")
            activated = self.signed(self.request(base,"POST","/v1/station/activations/complete", self.proof(dict(self.identity,
                domain=PREFIX+"activate/v1", activationCode=self.code, devicePublicKey=self.spki,
                challengeId=challenge["challengeId"], nonce=challenge["nonce"]))), "activated")
            if any(activated[k] != challenge[k] for k in ("challengeId","nonce")):
                raise ValueError("activation challenge binding differs")
            self.activated = True
        challenge = self.signed(self.request(base,"POST","/v1/station/challenges",dict(self.identity,
            domain=PREFIX+"request-session-challenge/v1", licenseId=self.license_id)),"session-challenge")
        session = self.signed(self.request(base,"POST","/v1/station/sessions",self.proof(dict(self.identity,
            domain=PREFIX+"open-session/v1", licenseId=self.license_id,
            challengeId=challenge["challengeId"], nonce=challenge["nonce"]))),"session")
        bearer = session["accessToken"]
        if any(session[k] != challenge[k] for k in ("challengeId","nonce")):
            raise ValueError("session challenge binding differs")
        profile = self.signed(self.request(base,"GET","/v1/station/me",bearer=bearer),"profile")
        if profile["displayName"] != "Synthetic Station verification" or \
                profile["sessionId"] != session["sessionId"]:
            raise ValueError("profile projection differs")
        catalog = self.signed(self.request(base,"GET","/v1/station/catalog",bearer=bearer),"catalog")
        expected = {r["itemId"]:{k:r.get(k,self.index["revision"]) for k in
            ("itemId","name","platform","revision","coverId")} for r in self.index["items"]
            if r.get("catalogVisible") is not False}
        published = {r["itemId"]:{k:r[k] for k in ("itemId","name","platform","revision","coverId")} for r in catalog["items"]}
        if published != expected or len(published) != len(catalog["items"]) or \
                catalog["revision"] != self.index["revision"] or \
                catalog["sessionId"] != session["sessionId"] or "filePath" in json.dumps(catalog):
            raise ValueError("signed catalog differs from the reviewed index")
        rows = [r for r in self.index["items"] if r.get("catalogVisible") is not False]
        platforms = sorted({r["platform"] for r in rows})
        selected = [min((r for r in rows if r["platform"]==p),
                        key=lambda r:r["artifact"]["sizeBytes"]) for p in platforms]
        hidden = [r for r in self.index["items"] if r.get("catalogVisible") is False]
        if hidden:
            selected.append(min(hidden,key=lambda r:r["artifact"]["sizeBytes"]))
        for row in selected:
            cover = self.request(base,"GET","/v1/station/covers/"+row["coverId"],bearer=bearer)
            if cover[0] != 200 or cover[2] != Path(row["coverPath"]).read_bytes() or \
                    not cover[1].get("Content-Type","").startswith("image/"):
                raise ValueError("cover status, MIME or bytes differ")
            grant = self.signed(self.request(base,"POST","/v1/station/downloads/authorize",
                dict(self.identity,domain=PREFIX+"request-download/v1",itemId=row["itemId"]),bearer),
                "download-grant")
            if grant["sessionId"] != session["sessionId"] or grant["itemId"] != row["itemId"] or \
                    grant["itemRevision"] != row.get("revision",self.index["revision"]) or \
                    grant["artifact"] != row["artifact"]:
                raise ValueError("signed grant differs")
            route = "/v1/station/artifacts/"+grant["grantId"]
            artifact = self.request(base,"GET",route,bearer=bearer)
            if artifact[0] != 200 or artifact[1].get("Content-Type") != "application/octet-stream" or \
                    int(artifact[1]["Content-Length"]) != row["artifact"]["sizeBytes"] or \
                    len(artifact[2]) != row["artifact"]["sizeBytes"] or \
                    hashlib.sha256(artifact[2]).hexdigest() != row["artifact"]["sha256"]:
                raise ValueError("artifact status, length or SHA256 differs")
            repeated = self.request(base,"GET",route,bearer=bearer)
            if repeated[0] != 404 or json.loads(repeated[2])["code"] != "STATION_GRANT_NOT_FOUND":
                raise ValueError("grant reuse was accepted")
        if catalog_output is not None:
            fd = os.open(catalog_output,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
            with os.fdopen(fd,"w",encoding="utf-8",newline="") as output:
                writer = csv.DictWriter(output,fieldnames=("platform","itemId","name",
                    "itemRevision","coverId"),delimiter="\t",lineterminator="\n")
                writer.writeheader()
                for row in sorted(published.values(),key=lambda r:(r["platform"],r["name"].casefold(),r["itemId"])):
                    writer.writerow({"platform":row["platform"],"itemId":row["itemId"],
                        "name":row["name"],"itemRevision":row["revision"],"coverId":row["coverId"]})
                output.flush();os.fsync(output.fileno())
        return {"catalogItems":len(published),"catalogRevision":catalog["revision"],
            "platformCounts":dict(sorted(Counter(r["platform"] for r in rows).items())),
            "verifiedCoverAndDownloadPairs":len(selected),"compatibilityIdVerified":bool(hidden),
            "signaturesAndIdentityVerified":True,"correlationVerified":True,"oneUseVerified":True}
