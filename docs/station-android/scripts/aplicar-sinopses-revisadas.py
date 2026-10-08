#!/usr/bin/env python3
"""Apply a reviewed synopsis plan to the configured Station index and importer overrides.

Writes only descriptions and the catalog revision, under the importer's own lock.
No ROM reads, service restart, environment export, network or customer operation.
"""
import argparse
import copy
from datetime import datetime, timezone
import fcntl
import hashlib
import json
import os
from pathlib import Path
import pwd
import stat
import subprocess
import tempfile


SERVICE = "turborama-station-api.service"
CONFIG = Path("/mnt/DADOS/station-library-auto-private-20261004/config.json")
SCANNER = Path("/opt/turborama-station-library-neogeocd-20261005-9cff9b3/atualizar-biblioteca-station.py")


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def encode(document):
    return (json.dumps(document, ensure_ascii=False, separators=(",", ":")) + "\n").encode()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def prepare(index_raw, override_raw, state, plan):
    """Pure comparison and projection; file writes are confined to main()."""
    before = json.loads(index_raw)
    old_overrides = json.loads(override_raw) if override_raw is not None else {}
    require(sha(index_raw) == plan["expectedIndexSha256"], "Index changed since review.")
    require(before["revision"] == plan["expectedIndexRevision"], "Catalog revision changed.")
    require((sha(override_raw) if override_raw is not None else None) ==
            plan["expectedOverridesSha256"], "Importer overrides changed since review.")
    require(isinstance(old_overrides, dict), "Override object required.")
    after = copy.deepcopy(before)
    rows = {row["itemId"]: row for row in after["items"]}
    require(len(rows) == len(after["items"]) <= 4096, "Unique bounded catalog required.")
    overrides = copy.deepcopy(old_overrides)
    proposals = {}
    for change in plan["proposals"]:
        expected = change["precondition"]
        item_id = expected["itemId"]
        require(item_id not in proposals and item_id in rows, "Unique existing proposed ID required.")
        require(set(expected) == {"itemId", "platform", "itemRevision", "artifactSha256",
                                  "previousDescription", "previousDescriptionSha256"},
                "Exact synopsis preconditions required.")
        row = rows[item_id]
        metadata = row.get("metadata", {})
        require(isinstance(metadata, dict), "Item metadata object required.")
        old_text = metadata.get("description", "")
        actual = dict(itemId=item_id, platform=row["platform"],
                      itemRevision=row.get("revision", before["revision"]),
                      artifactSha256=row.get("artifact", {}).get("sha256"),
                      previousDescription=old_text, previousDescriptionSha256=sha(old_text.encode()))
        require(actual == expected, "Synopsis precondition changed.")
        text = change["proposedDescription"]
        require(isinstance(text, str) and text.strip() == text and text,
                "Explicit nonempty reviewed text required.")
        require(0 < len(text.encode("utf-16-le")) // 2 <= 2000 and
                not any(ord(character) < 32 and character not in "\n\t" for character in text),
                "Description exceeds the existing contract.")
        require(sha(text.encode()) == change["proposedDescriptionSha256"] and text != old_text,
                "Proposed description digest/no-op invalid.")
        next_metadata = dict(metadata, description=text)
        # The importer reserves additional room below the API's 8192-byte limit.
        require(len(json.dumps(next_metadata, ensure_ascii=True).encode()) <= 7600,
                "Description would be shortened by the importer.")
        row["metadata"] = next_metadata
        proposals[item_id] = text
    require(proposals, "A nonempty reviewed plan is required.")
    covered = set()
    keys = set()
    for group in plan["sourceGroups"]:
        source = group["sourcePath"]
        bound = state.get("sources", {}).get(source, {}).get("ids")
        require(bound == group["itemIds"], "Importer source binding changed.")
        key = group["overrideKey"]
        require(key not in keys and isinstance(key, str) and ":" in key,
                "Unique importer override key required.")
        keys.add(key)
        affected = set(bound) & set(proposals)
        require(affected and not (affected & covered), "Each proposed ID needs one source binding.")
        covered.update(affected)
        texts = {proposals[item_id] for item_id in affected}
        require(len(texts) == 1, "Shared source has conflicting proposed descriptions.")
        text = next(iter(texts))
        for item_id in bound:
            require(item_id in rows, "Source refers to an unknown ID.")
            if item_id not in proposals:
                require(rows[item_id].get("metadata", {}).get("description", "") == text,
                        "Override would change an unproposed item.")
        old = overrides.get(key, {})
        require(isinstance(old, dict), "Importer override object required.")
        overrides[key] = dict(old, description=text)
    require(covered == set(proposals), "A proposed ID has no persistent source binding.")
    after["revision"] = before["revision"] + 1
    # Prove every field outside the selected descriptions/global revision is preserved.
    restored = copy.deepcopy(after)
    restored["revision"] = before["revision"]
    originals = {row["itemId"]: row for row in before["items"]}
    for row in restored["items"]:
        if row["itemId"] in proposals:
            row["metadata"] = copy.deepcopy(originals[row["itemId"]].get("metadata", {}))
            if "metadata" not in originals[row["itemId"]]:
                del row["metadata"]
    require(restored == before, "Unexpected change outside reviewed descriptions.")
    return after, overrides, sorted(proposals)


def bounded(path, maximum=128 * 1024 * 1024):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as source:
        info = os.fstat(source.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_size <= maximum and info.st_uid == 0
                and not info.st_mode & 0o022, "Protected bounded regular file required.")
        raw = source.read(maximum + 1)
        after = os.fstat(source.fileno())
        require((info.st_ino, info.st_size, info.st_mtime_ns) ==
                (after.st_ino, after.st_size, after.st_mtime_ns) and len(raw) == info.st_size,
                "File changed during read.")
    return raw, info


def atomic(path, raw, info=None):
    fd, temporary = tempfile.mkstemp(prefix=".station-synopsis-", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as target:
            os.fchmod(target.fileno(), stat.S_IMODE(info.st_mode) if info else 0o600)
            os.fchown(target.fileno(), info.st_uid if info else 0, info.st_gid if info else 0)
            target.write(raw)
            target.flush()
            os.fsync(target.fileno())
        os.replace(temporary, path)
        directory = os.open(path.parent, os.O_DIRECTORY)
        try:
            os.fsync(directory)
        finally:
            os.close(directory)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def pid():
    value = int(subprocess.check_output(
        ["systemctl", "show", SERVICE, "-p", "MainPID", "--value"], text=True).strip())
    require(value > 1, "Station must be running.")
    return value


def configured_index(process):
    command = Path(f"/proc/{process}/cmdline").read_bytes().split(b"\0")
    require(any(value.startswith(b"/opt/turborama-station-") and
                value.endswith(b"/TurboRamaSuiteOnlineServer.dll") for value in command),
            "Unexpected Station process.")
    values = [part.split(b"=", 1)[1].decode() for part in
              Path(f"/proc/{process}/environ").read_bytes().split(b"\0")
              if part.startswith(b"Station__LibraryIndexFile=")]
    require(len(values) == 1 and Path(values[0]).is_absolute(), "One configured index required.")
    return Path(values[0])


def write_receipt(path, document):
    owner = pwd.getpwnam("lz-servidor")
    info = path.parent.lstat()
    require(stat.S_ISDIR(info.st_mode) and info.st_uid == owner.pw_uid and
            not info.st_mode & 0o077, "Private operator receipt directory required.")
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "wb") as target:
        os.fchown(target.fileno(), owner.pw_uid, owner.pw_gid)
        target.write(encode(document))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=Path)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--rollback", type=Path, help="Private backup directory from a prior application")
    args = parser.parse_args()
    require(os.geteuid() == 0, "Native Linux authorization required.")
    require(not args.receipt.exists() and not args.receipt.is_symlink(), "Fresh receipt required.")
    require(bool(args.plan) != bool(args.rollback), "Choose application or guarded rollback.")
    config_raw, _ = bounded(CONFIG, 1024 * 1024)
    config = json.loads(config_raw)
    home = Path(config["outputDirectory"])
    require(home.is_absolute() and not home.is_symlink(), "Protected importer directory required.")
    process = pid()
    index = configured_index(process)
    require(index == home / "index.json", "Importer and API must use the same index.")
    override = Path(config["metadataOverrides"])
    require(override.is_absolute() and not override.is_symlink(), "Protected override path required.")
    with (home / "scan.lock").open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        raw, index_info = bounded(index)
        old_override, override_info = bounded(override, 16 * 1024 * 1024) if override.exists() else (None, None)
        if args.rollback:
            require(not args.rollback.is_symlink(), "Protected rollback directory required.")
            manifest = json.loads(bounded(args.rollback / "manifest.json", 1024 * 1024)[0])
            require(sha(raw) == manifest["afterIndexSha256"] and
                    (sha(old_override) if old_override is not None else None) == manifest["afterOverridesSha256"],
                    "Index or overrides changed after application; fresh review required.")
            before_raw, _ = bounded(args.rollback / "index-before.json")
            before_overrides, _ = bounded(args.rollback / "overrides-before.json", 16 * 1024 * 1024)
            require(sha(before_raw) == manifest["beforeIndexSha256"] and
                    sha(before_overrides) == manifest["beforeOverridesBackupSha256"], "Backup digest changed.")
            restored = json.loads(before_raw)
            restored["revision"] = json.loads(raw)["revision"] + 1
            next_index, next_overrides = encode(restored), before_overrides
            affected = manifest["appliedIds"]
            backup = args.rollback
        else:
            plan = json.loads(args.plan.read_bytes())
            require(process == plan["expectedPid"], "Station process changed since review.")
            require(sha(bounded(SCANNER, 1024 * 1024)[0]) == plan["expectedScannerSha256"],
                    "Reviewed importer source changed.")
            state = json.loads(bounded(home / "state.json", 64 * 1024 * 1024)[0])
            document, overrides, affected = prepare(raw, old_override, state, plan)
            # Confirm private mappings are actual configured platform paths, not arbitrary keys.
            volume = Path(config["volumeRoot"])
            for group in plan["sourceGroups"]:
                source = Path(group["sourcePath"])
                choices = [(len(folder.parts), platform, folder) for platform, spec in config["platforms"].items()
                           if source.is_relative_to(folder := volume / spec.get("folder", platform))]
                require(choices, "Source no longer belongs to a configured platform.")
                _, platform, folder = max(choices)
                require(group["overrideKey"] == platform + ":" + source.relative_to(folder).as_posix(),
                        "Override key differs from the importer source.")
            next_index, next_overrides = encode(document), encode(overrides)
            backup = Path(tempfile.mkdtemp(prefix="station-synopsis-backup-20261008-", dir="/mnt/DADOS"))
            os.chmod(backup, 0o700)
            atomic(backup / "index-before.json", raw)
            atomic(backup / "overrides-before.json", old_override if old_override is not None else b"{}\n")
            manifest = dict(beforeIndexSha256=sha(raw), afterIndexSha256=sha(next_index),
                            beforeOverridesSha256=sha(old_override) if old_override is not None else None,
                            beforeOverridesBackupSha256=sha(old_override if old_override is not None else b"{}\n"),
                            afterOverridesSha256=sha(next_overrides), appliedIds=affected)
            atomic(backup / "manifest.json", encode(manifest))
        require(pid() == process and bounded(index)[0] == raw and
                (bounded(override, 16 * 1024 * 1024)[0] if override.exists() else None) == old_override,
                "State changed before publication.")
        atomic(override, next_overrides, override_info)
        try:
            atomic(index, next_index, index_info)
        except Exception:
            if old_override is None:
                override.unlink()
            else:
                atomic(override, old_override, override_info)
            raise
        require(bounded(index)[0] == next_index and bounded(override, 16 * 1024 * 1024)[0] == next_overrides,
                "Published bytes differ.")
    document = json.loads(next_index)
    report = dict(utc=datetime.now(timezone.utc).isoformat(), pid=process,
                  operation="rollback" if args.rollback else "apply", revision=document["revision"],
                  appliedDescriptions=len(affected), itemCount=len(document["items"]),
                  visibleCount=sum(row.get("catalogVisible", True) for row in document["items"]),
                  beforeIndexSha256=sha(raw), afterIndexSha256=sha(next_index),
                  beforeOverridesSha256=sha(old_override) if old_override is not None else None,
                  afterOverridesSha256=sha(next_overrides),
                  backupDirectory=str(backup), appliedIds=affected, otherItemFieldsChanged=False,
                  gameBytesRead=False, serviceRestarted=False, authenticatedApiChecked=False)
    write_receipt(args.receipt, report)
    print(json.dumps({key: report[key] for key in (
        "operation", "pid", "revision", "appliedDescriptions", "itemCount", "serviceRestarted"
    )}))


if __name__ == "__main__":
    try:
        main()
    except BlockingIOError:
        raise SystemExit("Station scan is running; retry the same guarded plan.")
    except ValueError as error:
        # All ValueError messages above are fixed review conditions, never paths/settings.
        raise SystemExit(str(error))
    except (OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        raise SystemExit("Station synopsis operation rejected (" + type(error).__name__ + ").")
