#!/usr/bin/env python3
"""Root-run, one-time export of the protected 5192 Station index for reconciliation.

Reads only Station__LibraryIndexFile from the service environment file. Never
prints source paths, environment values, item paths or the index contents.
"""

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import pwd
import shlex


ENVIRONMENT_FILE = Path("/etc/turborama-suite/station-5192.env")
SETTING = "Station__LibraryIndexFile"


def configured_index():
    values = []
    for line in ENVIRONMENT_FILE.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in stripped:
            continue
        name, value = stripped.split("=", 1)
        if name.strip() != SETTING:
            continue
        parsed = shlex.split(value, comments=False, posix=True)
        if len(parsed) != 1 or not Path(parsed[0]).is_absolute():
            raise ValueError("invalid Station index configuration")
        values.append(Path(parsed[0]))
    if len(values) != 1:
        raise ValueError("Station index configuration is missing or ambiguous")
    return values[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if os.geteuid() != 0:
        parser.error("run with authorized root access")
    target = args.output
    owner = pwd.getpwnam("lz-servidor")
    if not target.is_absolute() or target.parent.is_symlink() or \
            target.parent.resolve() != Path(owner.pw_dir).resolve() or target.exists():
        parser.error("choose a new filename directly under the lz-servidor home")
    source = configured_index()
    if not source.is_file() or source.resolve() == target.resolve():
        raise ValueError("configured Station index is unavailable")
    data = source.read_bytes()
    index = json.loads(data)
    if not isinstance(index, dict) or not isinstance(index.get("items"), list) or \
            type(index.get("revision")) is not int or index["revision"] < 1 or \
            len(index["items"]) > 4096:
        raise ValueError("configured Station index has invalid envelope")
    counts = Counter()
    for item in index["items"]:
        if not isinstance(item, dict) or not isinstance(item.get("platform"), str):
            raise ValueError("configured Station index has invalid item")
        counts[item["platform"]] += 1
    fd = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    try:
        os.fchown(fd, owner.pw_uid, owner.pw_gid)
        with os.fdopen(fd, "wb") as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
    except BaseException:
        target.unlink(missing_ok=True)
        raise
    print(json.dumps({"revision": index["revision"], "items": len(index["items"]),
        "platformCounts": dict(sorted(counts.items())),
        "sha256": hashlib.sha256(data).hexdigest()}, sort_keys=True))


if __name__ == "__main__":
    main()
