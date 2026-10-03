#!/usr/bin/env python3
"""Verify a captured Station catalog envelope and export only comparison fields.

The input envelope is private: it contains license, device and session identities.
Capture it after an authenticated GET /v1/station/catalog, never commit the input.
Requires Python cryptography. Outputs a private TSV without bearer or identities.
"""

import argparse
import base64
import csv
import hashlib
import json
import os
from pathlib import Path
import re

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa


PRODUCT = "TURBORAMA_STATION_ANDROID"
DOMAIN = "TurboRamaStationAndroid/catalog/v1"
EXPECTED_KEY_ID = "06b41b778041d81b5b86a115a031418e0c4b0b2bd24ec8b340e62eaa82fb5268"
MAX_ENVELOPE = 12 * 1024 * 1024
ID = re.compile(r"[A-Za-z0-9_-]{8,64}\Z")
BASE64URL = re.compile(r"[A-Za-z0-9_-]+\Z")


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate JSON property")
        result[key] = value
    return result


def decode(value):
    if not isinstance(value, str) or not BASE64URL.fullmatch(value):
        raise ValueError("invalid Base64URL")
    data = base64.urlsafe_b64decode(value + "=" * (-len(value) % 4))
    if base64.urlsafe_b64encode(data).decode().rstrip("=") != value:
        raise ValueError("noncanonical Base64URL")
    return data


def positive_int(value):
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def text(value, maximum):
    return (isinstance(value, str) and 0 < len(value) <= maximum and
            not any(ord(char) < 32 or ord(char) == 127 or
                    0xD800 <= ord(char) <= 0xDFFF for char in value))


def verified_catalog(envelope_path, public_key_path):
    if envelope_path.stat().st_size > MAX_ENVELOPE:
        raise ValueError("catalog envelope exceeds 12 MiB")
    envelope = json.loads(envelope_path.read_text(encoding="utf-8"),
                          object_pairs_hook=unique_pairs)
    if not isinstance(envelope, dict):
        raise ValueError("catalog envelope is not an object")
    spki = public_key_path.read_bytes()
    try:
        public_key = serialization.load_der_public_key(spki)
    except ValueError:
        public_key = serialization.load_pem_public_key(spki)
        spki = public_key.public_bytes(serialization.Encoding.DER,
                                       serialization.PublicFormat.SubjectPublicKeyInfo)
    if not isinstance(public_key, rsa.RSAPublicKey) or public_key.key_size != 2048:
        raise ValueError("unexpected Station public key")
    key_id = hashlib.sha256(spki).hexdigest()
    if key_id != EXPECTED_KEY_ID or envelope.get("keyId") != key_id:
        raise ValueError("Station keyId mismatch")
    payload = decode(envelope.get("payload"))
    signature = decode(envelope.get("signature"))
    public_key.verify(signature, payload,
                      padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32),
                      hashes.SHA256())
    body = json.loads(payload.decode("utf-8"), object_pairs_hook=unique_pairs)
    if not isinstance(body, dict) or type(body.get("schemaVersion")) is not int or \
            body.get("schemaVersion") != 1 or \
            body.get("domain") != DOMAIN or body.get("productId") != PRODUCT or \
            body.get("applicationId") != PRODUCT:
        raise ValueError("Station catalog identity mismatch")
    if not all(text(body.get(key), 128) for key in
               ("licenseId", "deviceId", "sessionId")):
        raise ValueError("Station catalog session identity missing")
    if not positive_int(body.get("revision")):
        raise ValueError("Station catalog revision missing")
    rows = body.get("items")
    if not isinstance(rows, list) or len(rows) > 4096:
        raise ValueError("Station catalog item count exceeds contract")
    seen = set()
    clean = []
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("invalid Station catalog item")
        item_id, cover_id = row.get("itemId"), row.get("coverId")
        if not isinstance(item_id, str) or not ID.fullmatch(item_id) or \
                not isinstance(cover_id, str) or not ID.fullmatch(cover_id) or \
                item_id in seen or not text(row.get("name"), 120) or \
                not text(row.get("platform"), 120) or \
                not positive_int(row.get("revision")):
            raise ValueError("invalid or duplicate Station catalog item")
        seen.add(item_id)
        clean.append({"platform": row["platform"], "itemId": item_id,
                      "name": row["name"], "itemRevision": row["revision"],
                      "coverId": cover_id})
    return body["revision"], sorted(clean, key=lambda row:
                                     (row["platform"], row["name"].casefold(), row["itemId"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--envelope", type=Path, required=True)
    parser.add_argument("--public-key", type=Path, required=True,
                        help="Station assertion public SPKI, DER or PEM")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.resolve() == args.envelope.resolve():
        parser.error("output must differ from the private envelope")
    revision, rows = verified_catalog(args.envelope, args.public_key)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    fd = os.open(args.output, flags, 0o600)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as destination:
            writer = csv.DictWriter(destination,
                fieldnames=["platform", "itemId", "name", "itemRevision", "coverId"],
                delimiter="\t", lineterminator="\n")
            writer.writeheader()
            writer.writerows(rows)
    except BaseException:
        args.output.unlink(missing_ok=True)
        raise
    counts = {}
    for row in rows:
        counts[row["platform"]] = counts.get(row["platform"], 0) + 1
    print(json.dumps({"revision": revision, "total": len(rows),
                      "platformCounts": dict(sorted(counts.items()))},
                     ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
