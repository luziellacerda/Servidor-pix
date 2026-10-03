#!/usr/bin/env python3
"""Read-only Station production checks, reporting no credentials or private paths."""

from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


SERVICE = "turborama-station-api.service"
CANDIDATE = Path("/mnt/DADOS/station-content-candidate-20261003-45fe4df/index.json")
WANTED = {"Station__LibraryIndexFile", "Station__DownloadKeyFile",
          "ConnectionStrings__SuiteStore", "Station__Enabled", "Suite__Enabled"}


def run(command, **kwargs):
    result = subprocess.run(command, capture_output=True, text=True, timeout=30, **kwargs)
    if result.returncode:
        raise RuntimeError("read-only subprocess failed")
    return result.stdout


def main():
    if os.geteuid() != 0:
        raise PermissionError("authorized root access is required")
    pid = int(run(["systemctl", "show", SERVICE, "--property=MainPID", "--value"]).strip())
    if pid <= 0:
        raise ValueError("Station service is not running")
    values = {}
    for entry in Path(f"/proc/{pid}/environ").read_bytes().split(b"\0"):
        key, _, value = entry.partition(b"=")
        name = key.decode("utf-8", errors="strict")
        if name in WANTED:
            values[name] = value.decode("utf-8", errors="strict")
    index_path = Path(values["Station__LibraryIndexFile"])
    data = index_path.read_bytes()
    index = json.loads(data)
    status = {}
    for line in Path(f"/proc/{pid}/status").read_text().splitlines():
        key, _, value = line.partition(":")
        if key in {"Uid", "Gid", "Groups"}:
            status[key] = [int(number) for number in value.split()]
    checks = []
    for row in index["items"]:
        for key in ("filePath", "coverPath"):
            checks.append({"kind": "production_" + key, "path": row[key]})
    for row in json.loads(CANDIDATE.read_text())["items"]:
        for key in ("filePath", "coverPath"):
            checks.append({"kind": "candidate_" + key, "path": row[key]})
    key_path = values.get("Station__DownloadKeyFile")
    if key_path:
        checks.append({"kind": "download_key", "path": key_path})
    worker = '''import collections,json,sys
checks=json.load(sys.stdin);counts=collections.Counter()
for row in checks:
 try:
  with open(row['path'],'rb') as f: data=f.read(64)
  counts[row['kind']+('_readable' if data else '_empty')]+=1
 except PermissionError:counts[row['kind']+'_permission_denied']+=1
 except FileNotFoundError:counts[row['kind']+'_missing']+=1
 except OSError:counts[row['kind']+'_io_error']+=1
print(json.dumps(dict(counts),sort_keys=True))
'''
    access = json.loads(run(["/usr/bin/python3", "-c", worker], input=json.dumps(checks),
        user=status["Uid"][1], group=status["Gid"][1], extra_groups=status["Groups"],
        cwd="/"))
    connection = values.get("ConnectionStrings__SuiteStore", "")
    database_match = re.search(r"(?:^|;)\s*(?:Database|Initial Catalog)\s*=\s*([^;]+)",
                               connection, flags=re.I)
    host_match = re.search(r"(?:^|;)\s*Host\s*=\s*([^;]+)", connection, flags=re.I)
    port_match = re.search(r"(?:^|;)\s*Port\s*=\s*([^;]+)", connection, flags=re.I)
    database = database_match[1].strip().strip('\"\'') if database_match else ""
    host = host_match[1].strip().strip('\"\'') if host_match else ""
    if not re.fullmatch(r"[A-Za-z0-9_-]+", database) or \
            host not in {"127.0.0.1", "localhost", "/var/run/postgresql"} or \
            (port_match and port_match[1].strip() != "5432"):
        raise ValueError("production database could not be safely identified")
    sql = """BEGIN READ ONLY;
SELECT json_build_object(
 'migration028', EXISTS(SELECT 1 FROM suite.schema_migrations WHERE version='028_station_android'),
 'migration029', EXISTS(SELECT 1 FROM suite.schema_migrations WHERE version='029_station_download_grants'),
 'grantTable', to_regclass('suite.station_download_grants') IS NOT NULL,
 'apiGrantPrivileges', CASE WHEN to_regclass('suite.station_download_grants') IS NULL THEN false
   ELSE has_table_privilege('turborama-suite','suite.station_download_grants','SELECT,INSERT,UPDATE') END);
ROLLBACK;"""
    ledger = json.loads(run(["runuser", "-u", "postgres", "--", "psql", "-X", "-qAt",
                            "-v", "ON_ERROR_STOP=1", "--dbname", database, "-c", sql]).strip())
    root_available = Counter()
    for row in checks:
        try:
            with open(row["path"], "rb") as source:
                present = bool(source.read(1))
            root_available[row["kind"] + ("_readable" if present else "_empty")] += 1
        except OSError:
            root_available[row["kind"] + "_unavailable"] += 1
    key_size = Path(key_path).stat().st_size if key_path and Path(key_path).is_file() else None
    report = {"service": SERVICE, "pid": pid, "uid": status["Uid"][1],
        "indexSha256": hashlib.sha256(data).hexdigest(), "revision": index["revision"],
        "items": len(index["items"]),
        "platformCounts": dict(sorted(Counter(row["platform"] for row in index["items"]).items())),
        "stationEnabled": values.get("Station__Enabled", "").lower() == "true",
        "suiteDependencyEnabled": values.get("Suite__Enabled", "").lower() == "true",
        "serviceAccess": access, "rootAccess": dict(root_available),
        "downloadKeyConfigured": bool(key_path), "downloadKeyBytes": key_size,
        "ledger": ledger, "mutations": False}
    print(json.dumps(report, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(json.dumps({"diagnosticFailed": type(error).__name__, "mutations": False}))
        raise SystemExit(2)
