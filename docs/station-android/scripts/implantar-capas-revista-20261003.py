#!/usr/bin/env python3
"""Bounded Station revision 4 rollout: revista covers, same API/IDs/ROMs.

Requires authorized root access and a committed source revision. Adds one
Station index override; preserves binaries, keys, database schema and proxies.
Keeps revision 3 immutable and restores its content with revision 5 if a check fails.
The higher return revision preserves the Android catalog/cache monotonicity rule.
"""

import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import pwd
import shutil
import socket
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = Path(__file__).resolve().parent
SERVICE = "turborama-station-api.service"
SOURCE = Path("/mnt/DADOS/station-content-revista-20261003-rev4")
TARGET = Path("/mnt/DADOS/turbostation-releases/station-revista-20261003-rev4/content")
BACKUP = Path("/mnt/DADOS/station-revista-backup-20261003-rev4")
DROPIN = Path("/etc/systemd/system/turborama-station-api.service.d/zz-station-rev4-covers-20261003.conf")
ENV = Path("/etc/turborama-suite/station-covers-revista-20261003-rev4.env")
ENV_TEXT = "Station__LibraryIndexFile=" + str(TARGET / "index.json") + "\n"
DROPIN_TEXT = "[Service]\nEnvironmentFile=" + str(ENV) + "\n"
RETURN_ROOT = TARGET.parent / "rollback"
RETURN_INDEX = RETURN_ROOT / "index.json"
RETURN_ENV = Path("/etc/turborama-suite/station-rev5-return-covers-20261003.env")
RETURN_DROPIN = Path("/etc/systemd/system/turborama-station-api.service.d/zz-station-rev5-return-covers-20261003.conf")
RETURN_ENV_TEXT = "Station__LibraryIndexFile=" + str(RETURN_INDEX) + "\n"
RETURN_DROPIN_TEXT = "[Service]\nEnvironmentFile=" + str(RETURN_ENV) + "\n"
OLD_INDEX_SHA = "5b460a6f9866e30a5a5b4dad24652c512b1187b3df01244e6af9308ae6b18342"
SOURCE_INDEX_SHA = "aaaaf153eae64fec5a8f233aa49030b76a6eb91a00f9193c8685b4c2c378c407"
MANIFEST_SHA = "0609af11942b510903771c2e7d63ea50eadd412608d1adb5ffc3588cccbcd1d3"
FINAL_INDEX_SHA = "c7ea6cbcf454c55422d06ac53c797e744ca06b83efc49fa03686e6e4fab4d97a"
RESULT = Path("/home/lz-servidor/station-revista-rollout-result-20261003.json")
BEFORE = Path("/home/lz-servidor/station-revista-before-live-20261003.json")
EXPORT = Path("/home/lz-servidor/catalogo-station-revista-rev4-verificado-20261003.tsv")
SHARED = ("turborama-pix.service", "turborama-suite-api.service", "turborama-suite-admin.service",
          "turborama-suite-content-gateway.service", "nginx.service", "cloudflared.service",
          "postgresql@16-main.service", "redis-server.service", "turborama-station-management.service",
          "turborama-station-issue-admin.service", "php8.3-fpm.service", "turbobox-php-fpm.service")


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


def write_json(path, value):
    operations.private_text(path, json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n")


def identities(index):
    return {r["itemId"]: {k:v for k,v in r.items() if k not in {"revision", "filePath", "coverPath"}}
            for r in index["items"]}


def return_index(previous):
    restored = json.loads(json.dumps(previous))
    revision = max([4, previous["revision"]] +
                   [r.get("revision", previous["revision"]) for r in previous["items"]]) + 1
    restored["revision"] = revision
    for row in restored["items"]:
        row["revision"] = revision
    return restored


def rollback():
    state = json.loads((BACKUP / "state.json").read_text())
    if operations.digest(Path(state["previousIndex"])) != OLD_INDEX_SHA or \
            operations.digest(BACKUP / "index-rev3.json") != OLD_INDEX_SHA or \
            operations.digest(http.DLL) != operations.DLL_SHA:
        raise ValueError("rollback index or API binary changed")
    current, ids = operations.runtime()
    if current["Station__LibraryIndexFile"] not in {
            state["previousIndex"], str(TARGET / "index.json"), str(RETURN_INDEX)}:
        raise ValueError("effective Station index changed after this rollout")
    # Check every existing override before writing anything. The return is a new
    # index/override; revision 4 and every prior file remain available unchanged.
    for path, text in ((DROPIN, DROPIN_TEXT), (ENV, ENV_TEXT)):
        if path.exists():
            if path.is_symlink() or path.read_text() != text:
                raise ValueError("rollback override was changed by another operation")
    restored = return_index(json.loads((BACKUP / "index-rev3.json").read_text()))
    expected = json.dumps(restored, ensure_ascii=False, separators=(",", ":")) + "\n"
    for path, text in ((RETURN_INDEX, expected), (RETURN_ENV, RETURN_ENV_TEXT),
                       (RETURN_DROPIN, RETURN_DROPIN_TEXT)):
        if path.exists() and (path.is_symlink() or path.read_text() != text):
            raise ValueError("return artifact or override was changed by another operation")
    RETURN_ROOT.mkdir(mode=0o750, exist_ok=True)
    os.chown(RETURN_ROOT, 0, ids["Gid"][1])
    for path, text in ((RETURN_INDEX, expected), (RETURN_ENV, RETURN_ENV_TEXT),
                       (RETURN_DROPIN, RETURN_DROPIN_TEXT)):
        if not path.exists():
            operations.private_text(path, text)
    os.chown(RETURN_INDEX, 0, ids["Gid"][1])
    RETURN_INDEX.chmod(0o640)
    RETURN_DROPIN.chmod(0o644)
    operations.run(["systemctl", "daemon-reload"])
    operations.run(["systemctl", "restart", SERVICE])
    operations.ready("http://127.0.0.1:5192")
    values, _ = operations.runtime()
    if values["Station__LibraryIndexFile"] != str(RETURN_INDEX) or \
            RETURN_INDEX.read_text() != expected:
        raise ValueError("previous content did not return with the higher revision")


def apply(source_revision):
    stage = "preflight"
    activated = False
    prepared = False
    report = {"service": SERVICE, "preparationSourceRevision": source_revision,
              "apiSourceRevision": operations.SOURCE_REVISION, "dllSha256": operations.DLL_SHA,
              "applied": False, "previousRevision": 3, "revision": 4}
    try:
        head = operations.run(["git", "-c", "safe.directory=" + str(ROOT), "rev-parse", "HEAD"], cwd=ROOT).strip()
        dirty = operations.run(["git", "-c", "safe.directory=" + str(ROOT), "status", "--porcelain"], cwd=ROOT).strip()
        if head != source_revision or dirty:
            raise ValueError("rollout source must be the specified clean commit")
        values, ids = operations.runtime()
        old_index_path = Path(values["Station__LibraryIndexFile"])
        if ids["Uid"][1] != 995 or operations.digest(old_index_path) != OLD_INDEX_SHA or \
                operations.digest(http.DLL) != operations.DLL_SHA or \
                operations.digest(SOURCE / "index.json") != SOURCE_INDEX_SHA or \
                operations.digest(SOURCE / "files.sha256") != MANIFEST_SHA or \
                operations.digest(http.AUDIT) != http.AUDIT_SHA:
            raise ValueError("runtime or prepared content differs from reviewed artifacts")
        for path in (TARGET.parent, BACKUP, DROPIN, ENV, RETURN_ENV, RETURN_DROPIN, RESULT, EXPORT):
            if path.exists() or path.is_symlink():
                raise ValueError("rollout target already exists")
        before = json.loads(BEFORE.read_text())
        if before.get("effectiveIndexSha256") != OLD_INDEX_SHA or \
                before.get("verifiedHttpsCovers") != 9 or before.get("coversMatchingRevista") != 0 or \
                before.get("syntheticRowsRemoved") is not True:
            raise ValueError("HTTPS investigation evidence is missing")
        baseline = {unit: operations.state(unit) for unit in SHARED}
        if any("ActiveState=active" not in state for state in baseline.values()):
            raise ValueError("a shared service is unhealthy")
        old = json.loads(old_index_path.read_text())
        candidate = json.loads((SOURCE / "index.json").read_text())
        audit = {r["itemId"]:r for r in json.loads(http.AUDIT.read_text())["items"]}
        visible = [r for r in candidate["items"] if r.get("catalogVisible") is not False]
        if candidate["revision"] != 4 or len(candidate["items"]) != 2071 or len(visible) != 1816 or \
                identities(old) != identities(candidate) or set(audit) != set(identities(old)) or \
                any(r["revision"] != 4 for r in candidate["items"]):
            raise ValueError("candidate changed identities/ROM descriptors or counts")
        for row in old["items"]:
            if operations.digest(Path(row["coverPath"])) != audit[row["itemId"]]["previousCoverSha256"]:
                raise ValueError("a production cover changed after investigation")
        for row in candidate["items"]:
            if operations.digest(Path(row["coverPath"])) != audit[row["itemId"]]["revistaSelectedSha256"]:
                raise ValueError("candidate cover differs from selected revista image")
        report.update(identitiesAndArtifactsPreserved=True, indexedItems=2071, catalogItems=1816,
                      compatibilityItems=255, changedVisibleCovers=1783, changedCompatibilityCovers=252,
                      allIndexedCoversMatchRevista=True,
                      platformCounts=dict(sorted(Counter(r["platform"] for r in visible).items())))
        stage = "backup_and_restore"
        BACKUP.mkdir(mode=0o700)
        operations.private_text(BACKUP / "index-rev3.json", old_index_path.read_text())
        write_json(BACKUP / "state.json", {"previousIndex": str(old_index_path),
                   "previousIndexSha256": OLD_INDEX_SHA, "baseline": baseline,
                   "preparationSourceRevision": source_revision})
        # The prior content release is retained untouched. Prove the saved index
        # restores byte-for-byte; no database migration or restore is involved.
        with tempfile.TemporaryDirectory(prefix="restore-", dir=BACKUP) as restored:
            copy = Path(restored) / "index.json"
            shutil.copyfile(BACKUP / "index-rev3.json", copy)
            if operations.digest(copy) != OLD_INDEX_SHA:
                raise ValueError("saved index restore failed")
        report["indexBackupRestoreVerified"] = True
        stage = "materialize_and_service_permissions"
        TARGET.parent.mkdir(mode=0o750)
        os.chown(TARGET.parent, 0, ids["Gid"][1])
        shutil.copytree(SOURCE, TARGET)
        for row in candidate["items"]:
            for field in ("filePath", "coverPath"):
                row[field] = str(TARGET / Path(row[field]).relative_to(SOURCE))
        (TARGET / "index.json").write_text(json.dumps(candidate, ensure_ascii=False, separators=(",", ":")) + "\n")
        for path in [TARGET, *TARGET.rglob("*")]:
            if path.is_symlink():
                raise ValueError("release contains a symlink")
            os.chown(path, 0, ids["Gid"][1])
            path.chmod(0o750 if path.is_dir() else 0o640)
        if operations.digest(TARGET / "index.json") != FINAL_INDEX_SHA:
            raise ValueError("final index hash differs")
        files = []
        for line in (TARGET / "files.sha256").read_text().splitlines():
            sha, relative = line.split("  ", 1)
            path = TARGET / relative
            if not path.resolve().is_relative_to(TARGET):
                raise ValueError("file manifest escaped content release")
            files.append({"path": str(path), "sha256": sha})
        if len(files) != 4142:
            raise ValueError("file manifest must cover every ROM and cover")
        worker = '''import hashlib,json,sys
for row in json.load(sys.stdin):
 h=hashlib.sha256()
 with open(row['path'],'rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 if h.hexdigest()!=row['sha256']:raise ValueError('service file digest mismatch')
print('verified')
'''
        if operations.run(["/usr/bin/python3", "-c", worker], input=json.dumps(files),
                          user=ids["Uid"][1], group=ids["Gid"][1], extra_groups=ids["Groups"],
                          cwd="/", timeout=180).strip() != "verified":
            raise ValueError("service identity did not verify all files")
        report["serviceReadableVerifiedFiles"] = len(files)
        stage = "candidate_http_verification"
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0)); port = probe.getsockname()[1]
        shadow_base = "http://127.0.0.1:" + str(port)
        shadow_values = dict(values, Station__LibraryIndexFile=str(TARGET / "index.json"))
        for name in ("INVOCATION_ID", "NOTIFY_SOCKET", "LISTEN_FDS", "LISTEN_PID",
                     "LISTEN_FDNAMES", "JOURNAL_STREAM"):
            shadow_values.pop(name, None)
        with (BACKUP / "candidate.log").open("xb") as log:
            os.fchmod(log.fileno(), 0o600)
            process = subprocess.Popen(["/usr/bin/dotnet", str(http.DLL), "--urls", shadow_base],
                env=shadow_values, cwd=http.DLL.parent, stdout=log, stderr=log, user=ids["Uid"][1],
                group=ids["Gid"][1], extra_groups=ids["Groups"])
            try:
                operations.ready(shadow_base, process)
                report["candidateVerification"] = http.verify(candidate, shadow_values, "candidate", base=shadow_base)
            finally:
                process.terminate()
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill(); process.wait()
        prepared = True
        print(json.dumps({"stage": "candidate_verified", "revision": 4, "covers": 2071,
                          "serviceReadableFiles": 4142}), flush=True)
        stage = "activate_station_index"
        if operations.digest(old_index_path) != OLD_INDEX_SHA or any(
                operations.state(unit) != state for unit, state in baseline.items()):
            raise ValueError("production changed before Station activation")
        # Mark this before either write so a partial override is also rolled back.
        activated = True
        operations.private_text(ENV, ENV_TEXT)
        operations.private_text(DROPIN, DROPIN_TEXT)
        DROPIN.chmod(0o644)
        operations.run(["systemctl", "daemon-reload"])
        operations.run(["systemctl", "restart", SERVICE])
        operations.ready("http://127.0.0.1:5192")
        running, _ = operations.runtime()
        if running["Station__LibraryIndexFile"] != str(TARGET / "index.json"):
            raise ValueError("effective Station index override did not apply")
        stage = "public_https_verification"
        report["publicHttpsVerification"] = http.verify(candidate, running, "after", catalog_output=BACKUP / "catalog.tsv")
        if any(operations.state(unit) != state for unit, state in baseline.items()):
            raise ValueError("a shared service changed")
        if operations.digest(http.DLL) != operations.DLL_SHA or \
                operations.digest(old_index_path) != OLD_INDEX_SHA or \
                operations.digest(TARGET / "index.json") != FINAL_INDEX_SHA:
            raise ValueError("an immutable runtime artifact changed")
        operations.private_text(EXPORT, (BACKUP / "catalog.tsv").read_text())
        owner = pwd.getpwnam("lz-servidor")
        os.chown(EXPORT, owner.pw_uid, owner.pw_gid)
        report.update(applied=True, indexSha256=FINAL_INDEX_SHA,
                      publicCatalogTsvSha256=operations.digest(EXPORT),
                      sharedServicesPreserved=True, previousContentReleasePreserved=True,
                      apiBinaryPreserved=True, schemaAndKeysPreserved=True,
                      pid=int(operations.run(["systemctl", "show", SERVICE, "-p", "MainPID", "--value"]).strip()),
                      completedAtUtc=datetime.now(timezone.utc).isoformat())
    except Exception as error:
        report.update(failedStage=stage, errorType=type(error).__name__, candidatePrepared=prepared)
        if activated:
            rollback()
            report["rolledBack"] = True
            report["returnRevision"] = 5
        if BACKUP.exists():
            write_json(BACKUP / "result.json", report)
        http.save_report(RESULT, report)
        print(json.dumps({k:v for k,v in report.items() if k not in {"candidateVerification", "publicHttpsVerification"}}, sort_keys=True))
        raise SystemExit(1)
    write_json(BACKUP / "result.json", report)
    http.save_report(RESULT, report)
    print(json.dumps({k:v for k,v in report.items() if k not in {"candidateVerification", "publicHttpsVerification"}}, sort_keys=True))


def main():
    global operations, http
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--apply", action="store_true")
    modes.add_argument("--rollback", action="store_true")
    parser.add_argument("--source-revision")
    args = parser.parse_args()
    if os.geteuid() != 0:
        parser.error("authorized root access is required")
    operations = module("implantar-station-20261003.py")
    http = module("verificar-capas-revista-http.py")
    if args.apply:
        if not args.source_revision:
            parser.error("apply requires the reviewed source commit")
        apply(args.source_revision)
    else:
        rollback()
        print(json.dumps({"rolledBack": True, "revision": 5, "previousContentReleasePreserved": True}))


if __name__ == "__main__":
    main()
