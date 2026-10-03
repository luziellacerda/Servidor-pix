#!/usr/bin/env python3
"""Check real Station HTTPS cover bytes using a disposable synthetic license.

Reads privileged runtime credentials locally and emits only sanitized evidence.
No customer license is used or changed; every synthetic row is removed.
"""

import argparse
import base64
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import sys

from cryptography.hazmat.primitives import serialization


SCRIPTS = Path(__file__).resolve().parent
OLD_INDEX_SHA = "5b460a6f9866e30a5a5b4dad24652c512b1187b3df01244e6af9308ae6b18342"
AUDIT_SHA = "5a526e06cdbf07b2d36b4149c0f473c836054267291a4a7c10ab9125a45afa04"
AUDIT = Path("/mnt/DADOS/station-revista-correction-20261003-rev4/cover-audit.json")
DLL = Path("/opt/turborama-station-20261003-fd13c0d/TurboRamaSuiteOnlineServer.dll")
EXTRA_NAMES = {"Mario Paint", "Rockman & Forte", "Tom and Jerry", "Top Gear 3000 (PT-BR)"}


def module(name):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), SCRIPTS / name)
    value = importlib.util.module_from_spec(spec)
    before = sys.dont_write_bytecode
    try:
        sys.dont_write_bytecode = True
        spec.loader.exec_module(value)
    finally:
        sys.dont_write_bytecode = before
    return value


def verify(index, values, phase, catalog_output=None, base="https://app.lzgames.com.br"):
    operations = module("implantar-station-20261003.py")
    if operations.digest(DLL) != operations.DLL_SHA or operations.digest(AUDIT) != AUDIT_SHA:
        raise ValueError("reviewed DLL or cover audit changed")
    if index["revision"] != (3 if phase == "before" else 4):
        raise ValueError("unexpected catalog revision for cover verification")
    audit = {r["itemId"]: r for r in json.loads(AUDIT.read_text())["items"]}
    indexed = {r["itemId"]: r for r in index["items"]}
    if set(audit) != set(indexed):
        raise ValueError("cover audit and live index identities differ")
    public = serialization.load_pem_private_key(
        Path(values["Station__AssertionPrivateKeyPemFile"]).read_bytes(), password=None).public_key()
    spki = public.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    if hashlib.sha256(spki).hexdigest() != operations.KEY_ID:
        raise ValueError("Station assertion key changed")
    pepper = base64.b64decode(Path(values["Station__ActivationPepperFile"]).read_bytes().strip(), validate=True)
    db = operations.database_name(values)
    helper = module("verificar-http-release-station.py")
    check = helper.StationReleaseVerification(lambda query: operations.sql(db, query), pepper, public, index)
    request = check.request
    state, samples = {}, []
    by_cover = {r["coverId"]: r for r in index["items"]}

    def observed_request(base, method, route, payload=None, bearer=None):
        response = request(base, method, route, payload, bearer)
        if route == "/v1/station/catalog":
            state["bearer"] = bearer
        if method == "GET" and route.startswith("/v1/station/covers/"):
            cover_id = route.rsplit("/", 1)[-1]
            row = by_cover[cover_id]
            actual = hashlib.sha256(response[2]).hexdigest()
            expected = audit[row["itemId"]]["revistaSelectedSha256"]
            samples.append({"platform": row["platform"], "name": row["name"],
                            "itemId": row["itemId"], "coverId": cover_id,
                            "status": response[0], "mime": response[1].get("Content-Type", ""),
                            "sizeBytes": len(response[2]), "httpSha256": actual,
                            "revistaSha256": expected, "matchesRevista": actual == expected,
                            "matchesIndexedFile": actual == operations.digest(Path(row["coverPath"]))})
            if response[0] != 200 or not samples[-1]["matchesIndexedFile"] or \
                    int(response[1].get("Content-Length", -1)) != len(response[2]) or \
                    response[1].get("Cache-Control") != "no-store" or \
                    response[1].get("X-Content-Type-Options") != "nosniff":
                raise ValueError("HTTPS cover differs from indexed file or length")
            if phase != "before" and actual != expected:
                raise ValueError("HTTPS cover differs from selected revista image")
        return response

    check.request = observed_request
    try:
        check.create()
        report = check.check(base, catalog_output)
        extra = [r for r in index["items"] if r.get("catalogVisible") is not False and
                 r["name"] in EXTRA_NAMES]
        if len(extra) != len(EXTRA_NAMES):
            raise ValueError("the four folder variant cases are missing")
        for row in extra:
            check.request(base, "GET", "/v1/station/covers/" + row["coverId"],
                          bearer=state["bearer"])
        report.update(phase=phase, coverSamples=samples, verifiedHttpsCovers=len(samples),
                      coversMatchingRevista=sum(r["matchesRevista"] for r in samples),
                      customerLicenseUsed=False)
        if not base.startswith("https://"):
            report["verifiedHttpCovers"] = report.pop("verifiedHttpsCovers")
    finally:
        if not check.cleanup():
            raise ValueError("synthetic Station verification rows remain")
    report["syntheticRowsRemoved"] = True
    return report


def save_report(output, report):
    fd = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as file:
        json.dump(report, file, ensure_ascii=False, indent=2)
        file.write("\n"); file.flush(); os.fsync(file.fileno())
    owner = pwd.getpwnam("lz-servidor")
    os.chown(output, owner.pw_uid, owner.pw_gid)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", action="store_true", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if os.geteuid() != 0:
        parser.error("authorized root access is required")
    operations = module("implantar-station-20261003.py")
    values, _ = operations.runtime()
    path = Path(values["Station__LibraryIndexFile"])
    if operations.digest(path) != OLD_INDEX_SHA:
        raise ValueError("effective revision 3 changed before cover investigation")
    report = verify(json.loads(path.read_text()), values, "before")
    report["effectiveIndexSha256"] = operations.digest(path)
    save_report(args.output, report)
    print(json.dumps({k:v for k,v in report.items() if k != "coverSamples"}, sort_keys=True))


if __name__ == "__main__":
    main()
