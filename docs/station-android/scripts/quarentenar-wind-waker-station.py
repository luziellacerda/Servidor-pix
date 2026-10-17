#!/usr/bin/env python3
"""Quarantine one proven-unreadable GameCube image in the live Station library.

Default mode is read-only. A root operator must explicitly supply --apply and the
fresh plan file printed by a preceding --plan-output run. No service is restarted.
The source is moved, never deleted; immutable published artifacts are retained.
"""

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile


ITEM_ID = "station_12b835c9539b4800a6bb29ca00a25f94"
BAD_SHA256 = "3106a01516f59854a55fd6343da8b0f489ca8cfc523f300bb1913d7f1c602a2f"
BAD_SIZE = 546_776_387
GAME_HEADER = b"GZLE01"
SERVICE = "turborama-station-api.service"
CONFIG = Path("/mnt/DADOS/station-library-auto-private-20261004/config.json")
QUARANTINE_NAME = ".station-quarantine-wind-waker-20261014"
MAX_INDEX = 128 * 1024 * 1024
MAX_STATE = 64 * 1024 * 1024


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def file_sha(path):
    result = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def encode(document):
    return (json.dumps(document, ensure_ascii=False, separators=(",", ":")) + "\n").encode()


def bounded(path, maximum):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, "rb") as source:
        before = os.fstat(source.fileno())
        require(stat.S_ISREG(before.st_mode) and before.st_size <= maximum and
                before.st_uid == 0 and not before.st_mode & 0o022,
                "Protected regular file required.")
        raw = source.read(maximum + 1)
        after = os.fstat(source.fileno())
        require((before.st_ino, before.st_size, before.st_mtime_ns) ==
                (after.st_ino, after.st_size, after.st_mtime_ns) and
                len(raw) == before.st_size, "File changed during read.")
    return raw, before


def atomic(path, raw, old_info=None):
    fd, name = tempfile.mkstemp(prefix=".station-wind-waker-", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as target:
            os.fchmod(target.fileno(), stat.S_IMODE(old_info.st_mode) if old_info else 0o600)
            os.fchown(target.fileno(), old_info.st_uid if old_info else 0,
                      old_info.st_gid if old_info else 0)
            target.write(raw)
            target.flush()
            os.fsync(target.fileno())
        os.replace(name, path)
        sync_dir(path.parent)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def sync_dir(path):
    fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def process_id():
    value = int(subprocess.check_output(
        ["systemctl", "show", SERVICE, "-p", "MainPID", "--value"],
        text=True).strip())
    require(value > 1, "Station API must be running.")
    return value


def effective_index(pid):
    command = Path(f"/proc/{pid}/cmdline").read_bytes().split(b"\0")
    require(any(arg.startswith(b"/opt/turborama-station-") and
                arg.endswith(b"/TurboRamaSuiteOnlineServer.dll") for arg in command),
            "Unexpected Station API process.")
    values = [part.split(b"=", 1)[1].decode() for part in
              Path(f"/proc/{pid}/environ").read_bytes().split(b"\0")
              if part.startswith(b"Station__LibraryIndexFile=")]
    require(len(values) == 1 and Path(values[0]).is_absolute(),
            "Exactly one configured Station index required.")
    return Path(values[0])


def find_source(state):
    matches = [(Path(path), binding) for path, binding in
               state.get("sources", {}).items() if ITEM_ID in binding.get("ids", [])]
    require(len(matches) == 1 and matches[0][1]["ids"] == [ITEM_ID],
            "Item must have one exclusive scanner source.")
    return matches[0][0]


def source_guard(config, source):
    volume = Path(config["volumeRoot"])
    require(volume.is_absolute() and not volume.is_symlink() and volume.is_dir() and
            volume == volume.resolve(),
            "Protected source volume required.")
    require(source.is_absolute() and not source.is_symlink() and source.is_file(),
            "Original source file required.")
    require(source == source.resolve() and source.is_relative_to(volume.resolve()),
            "Source must be a regular file under the configured volume.")
    platform_folders = []
    for platform, spec in config["platforms"].items():
        folder = volume / spec.get("folder", platform)
        require(folder.resolve().is_relative_to(volume.resolve()),
                "Configured platform folder leaves source volume.")
        platform_folders.append(folder.resolve())
    require(sum(source.is_relative_to(folder) for folder in platform_folders) == 1,
            "Source must belong to exactly one platform folder.")
    gamecube = volume / config["platforms"]["gamecube"].get("folder", "gamecube")
    require(source.is_relative_to(gamecube.resolve()),
            "Source must belong to the configured GameCube folder.")
    quarantine = volume / QUARANTINE_NAME
    require(not any(quarantine.is_relative_to(folder) for folder in platform_folders),
            "Quarantine would be scanned as game media.")
    target = quarantine / (ITEM_ID + "-" + BAD_SHA256 + ".iso")
    require(not target.exists() and not target.is_symlink(),
            "Quarantine destination already exists.")
    info = source.stat()
    require(stat.S_ISREG(info.st_mode) and info.st_dev == volume.stat().st_dev and
            info.st_size == BAD_SIZE and file_sha(source) == BAD_SHA256,
            "Source bytes differ from the diagnosed bad image.")
    with source.open("rb") as stream:
        require(stream.read(len(GAME_HEADER)) == GAME_HEADER,
                "Source disc header differs from the diagnosed bad image.")
    return quarantine, target, info


def restore_path_guard(config, home, source, target, target_must_exist=True):
    """Revalidate manifest paths before any recovery/rollback rename."""
    volume = Path(config["volumeRoot"])
    require(volume.is_absolute() and volume.is_dir() and not volume.is_symlink() and
            volume == volume.resolve(), "Configured source volume changed.")
    expected_target = volume / QUARANTINE_NAME / (ITEM_ID + "-" + BAD_SHA256 + ".iso")
    require(target == expected_target and target.parent == target.parent.resolve() and
            not target.parent.is_symlink() and not target.is_symlink(),
            "Quarantine path changed.")
    gamecube = (volume / config["platforms"]["gamecube"].get("folder", "gamecube")).resolve()
    require(gamecube.is_relative_to(volume), "GameCube source folder leaves configured volume.")
    require(source.is_absolute() and source == source.resolve(strict=False) and
            source.parent.is_dir() and source.parent == source.parent.resolve() and
            source.is_relative_to(gamecube) and not source.is_symlink(),
            "Original GameCube source path changed.")
    folders = [(volume / spec.get("folder", platform)).resolve()
               for platform, spec in config["platforms"].items()]
    require(all(folder.is_relative_to(volume) for folder in folders) and
            sum(source.is_relative_to(folder) for folder in folders) == 1 and
            source.parent.stat().st_dev == volume.stat().st_dev,
            "Original source now overlaps another platform.")
    state = json.loads(bounded(home / "state.json", MAX_STATE)[0])
    require(find_source(state) == source, "Scanner source binding changed.")
    if target_must_exist:
        require(target.parent.is_dir() and target.is_file(),
                "Quarantined source is missing.")
    if target.parent.exists():
        info = target.parent.stat()
        require(stat.S_ISDIR(info.st_mode) and info.st_uid == 0 and
                not info.st_mode & 0o077 and
                info.st_dev == source.parent.stat().st_dev == volume.stat().st_dev,
                "Quarantine directory or filesystem changed.")
    else:
        require(not target_must_exist and source.exists(),
                "Quarantine directory is missing while source is absent.")


def prepare_index(raw):
    before = json.loads(raw)
    require(type(before.get("revision")) is int and before["revision"] > 0 and
            isinstance(before.get("items"), list) and len(before["items"]) <= 20_000,
            "Unexpected Station catalog envelope.")
    rows = [row for row in before["items"] if row.get("itemId") == ITEM_ID]
    require(len(rows) == 1, "Exactly one affected item required.")
    row = rows[0]
    descriptor = row.get("artifact")
    require(row.get("platform") == "gamecube" and row.get("catalogVisible", True) is True and
            isinstance(descriptor, dict) and descriptor.get("sha256") == BAD_SHA256 and
            descriptor.get("sizeBytes") == BAD_SIZE and descriptor.get("format") == "raw" and
            isinstance(row.get("filePath"), str) and row["filePath"].startswith("/"),
            "Catalog item differs from the diagnosed bad image.")
    after = copy.deepcopy(before)
    affected = next(row for row in after["items"] if row["itemId"] == ITEM_ID)
    affected["catalogVisible"] = False
    affected.pop("artifact")
    after["revision"] = before["revision"] + 1
    proof = copy.deepcopy(after)
    proof["revision"] = before["revision"]
    restored = next(row for row in proof["items"] if row["itemId"] == ITEM_ID)
    restored["artifact"] = copy.deepcopy(descriptor)
    if "catalogVisible" in row:
        restored["catalogVisible"] = row["catalogVisible"]
    else:
        restored.pop("catalogVisible")
    require(proof == before, "Unexpected changes outside the one item and catalog revision.")
    return before, after, row


def plan(config, index, state, source, pid):
    raw, _ = bounded(index, MAX_INDEX)
    before, after, row = prepare_index(raw)
    quarantine, target, source_info = source_guard(config, source)
    published = Path(row["filePath"])
    require(published.is_absolute() and published != source and not published.is_symlink() and
            published.is_file() and published.stat().st_size == BAD_SIZE and
            file_sha(published) == BAD_SHA256,
            "Published immutable artifact differs from the diagnosed image.")
    index_parent = Path(config["outputDirectory"])
    require(index == index_parent / "index.json" and not index_parent.is_symlink(),
            "Importer and live API must use the same index.")
    return dict(schema="station.wind-waker-quarantine.plan.v1", itemId=ITEM_ID,
                expectedPid=pid, indexPath=str(index), expectedIndexSha256=sha(raw),
                expectedRevision=before["revision"], afterIndexSha256=sha(encode(after)),
                sourcePath=str(source), expectedSourceSha256=BAD_SHA256,
                expectedSourceBytes=source_info.st_size, expectedSourceDevice=source_info.st_dev,
                expectedSourceInode=source_info.st_ino,
                expectedSourceMtimeNs=source_info.st_mtime_ns,
                quarantinePath=str(target), artifactFilePath=row["filePath"],
                itemCount=len(before["items"]), sourceBindingIds=state["sources"][str(source)]["ids"])


def compare_plan(actual, expected):
    require(actual == expected, "Production baseline changed since planning.")


def create_private_file(path, data):
    require(not path.exists() and not path.is_symlink(),
            "Private output already exists.")
    fd, temporary = tempfile.mkstemp(prefix=".station-private-", dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as out:
            os.fchmod(out.fileno(), 0o600)
            out.write(data)
            out.flush()
            os.fsync(out.fileno())
        # link() refuses an existing destination. The public name can only
        # appear after the complete body has been fsynced.
        os.link(temporary, path, follow_symlinks=False)
        os.unlink(temporary)
        sync_dir(path.parent)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def private_output_guard(path):
    import pwd
    owner = pwd.getpwnam("lz-servidor")
    parent = path.parent.lstat()
    require(path.is_absolute() and stat.S_ISDIR(parent.st_mode) and
            parent.st_uid == owner.pw_uid and not parent.st_mode & 0o077 and
            not path.exists() and not path.is_symlink(),
            "Fresh private output path in lz-servidor directory required.")
    return owner


def load_context():
    require(os.geteuid() == 0, "Native Linux authorization required.")
    config = json.loads(bounded(CONFIG, 1024 * 1024)[0])
    pid = process_id()
    index = effective_index(pid)
    home = Path(config["outputDirectory"])
    require(home.is_absolute() and home.is_dir() and not home.is_symlink() and
            index == home / "index.json", "Configured importer and API index must match.")
    return config, pid, index, home


def under_lock(home):
    # Import only on Linux, allowing pure projection to be tested elsewhere.
    import fcntl
    lock = (home / "scan.lock").open("a")
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BaseException:
        lock.close()
        raise
    return lock


def current_plan(config, index, home, pid):
    state = json.loads(bounded(home / "state.json", MAX_STATE)[0])
    source = find_source(state)
    return plan(config, index, state, source, pid)


def apply(config, pid, index, home, submitted, receipt):
    with under_lock(home):
        compare_plan(current_plan(config, index, home, pid), submitted)
        raw, index_info = bounded(index, MAX_INDEX)
        _, after, _ = prepare_index(raw)
        next_raw = encode(after)
        source = Path(submitted["sourcePath"])
        quarantine = Path(submitted["quarantinePath"]).parent
        target = Path(submitted["quarantinePath"])
        require(not quarantine.exists() and not quarantine.is_symlink(),
                "Quarantine directory already exists; fresh review required.")
        backup = Path(tempfile.mkdtemp(prefix="station-wind-waker-backup-20261014-", dir=home.parent))
        os.chmod(backup, 0o700)
        create_private_file(backup / "index-before.json", raw)
        manifest = dict(schema="station.wind-waker-quarantine.backup.v1", plan=submitted,
                        beforeIndexSha256=sha(raw), afterIndexSha256=sha(next_raw),
                        beforeRevision=after["revision"] - 1,
                        sourcePath=str(source), quarantinePath=str(target))
        create_private_file(backup / "manifest.json", encode(manifest))
        require(process_id() == pid and effective_index(pid) == index and
                bounded(index, MAX_INDEX)[0] == raw, "Live Station state changed before publication.")
        source_info = source.stat()
        require((source_info.st_dev, source_info.st_ino, source_info.st_size,
                 source_info.st_mtime_ns) ==
                (submitted["expectedSourceDevice"], submitted["expectedSourceInode"],
                 BAD_SIZE, submitted["expectedSourceMtimeNs"]),
                "Source changed before quarantine.")
        quarantine.mkdir(mode=0o700)
        moved = False
        atomic_started = False
        post_issue = None
        try:
            require(source.stat().st_dev == quarantine.stat().st_dev,
                    "Quarantine must be on the same filesystem as the source.")
            os.replace(source, target)
            moved = True
            sync_dir(source.parent)
            sync_dir(quarantine)
            require(target.stat().st_size == BAD_SIZE and file_sha(target) == BAD_SHA256,
                    "Moved source bytes changed before index publication.")
            atomic_started = True
            atomic(index, next_raw, index_info)
        except Exception as error:
            if not atomic_started:
                if moved:
                    os.replace(target, source)
                    sync_dir(source.parent)
                    sync_dir(quarantine)
                if not any(quarantine.iterdir()):
                    quarantine.rmdir()
                raise
            # An exception after os.replace(index) may mean publication succeeded.
            # Restore the source only when the old index is proven still active.
            try:
                observed = bounded(index, MAX_INDEX)[0]
            except Exception:
                observed = None
            if observed == raw:
                os.replace(target, source)
                sync_dir(source.parent)
                sync_dir(quarantine)
                if not any(quarantine.iterdir()):
                    quarantine.rmdir()
                raise
            post_issue = "index-write-" + type(error).__name__
        try:
            observed = bounded(index, MAX_INDEX)[0]
            published = observed == next_raw
        except Exception as error:
            published = False
            post_issue = post_issue or "index-read-" + type(error).__name__
        if not published:
            post_issue = post_issue or "index-not-at-projected-revision"
        if published:
            try:
                create_private_file(backup / "applied.json", encode(dict(
                    utc=datetime.now(timezone.utc).isoformat(), pid=pid,
                    beforeIndexSha256=sha(raw), afterIndexSha256=sha(next_raw),
                    beforeRevision=after["revision"] - 1,
                    afterRevision=after["revision"], sourceSha256=BAD_SHA256,
                    sourceBytes=BAD_SIZE, sourceMoved=True,
                    serviceRestarted=False, otherItemsChanged=False)))
            except Exception as error:
                post_issue = post_issue or "applied-receipt-" + type(error).__name__
    report = dict(operation="apply", itemId=ITEM_ID, revision=after["revision"],
                  indexSha256=sha(next_raw), backupDirectory=str(backup),
                  sourceQuarantined=True, serviceRestarted=False,
                  publicationState="published" if published else "unknown-after-index-write",
                  recoveryNeeded=post_issue is not None)
    if post_issue is not None:
        report["recoveryReason"] = post_issue
    try:
        write_receipt(receipt, report)
        report["receiptWritten"] = True
    except Exception as error:
        report["receiptWritten"] = False
        report["recoveryNeeded"] = True
        report.setdefault("recoveryReason", "private-receipt-" + type(error).__name__)
    return report


def rollback(config, pid, index, home, backup, receipt):
    require(backup.parent == home.parent and backup.name.startswith("station-wind-waker-backup-20261014-") and
            not backup.is_symlink() and backup.is_dir(), "Guarded backup directory required.")
    with under_lock(home):
        manifest = json.loads(bounded(backup / "manifest.json", 1024 * 1024)[0])
        original, _ = bounded(backup / "index-before.json", MAX_INDEX)
        current, info = bounded(index, MAX_INDEX)
        require(manifest.get("schema") == "station.wind-waker-quarantine.backup.v1" and
                manifest["beforeIndexSha256"] == sha(original) and
                manifest["afterIndexSha256"] == sha(current) and
                manifest["plan"]["itemId"] == ITEM_ID and
                manifest["plan"]["expectedPid"] == pid and
                manifest["plan"]["indexPath"] == str(index) and
                manifest["plan"]["sourcePath"] == manifest["sourcePath"] and
                manifest["plan"]["quarantinePath"] == manifest["quarantinePath"],
                "Index changed after quarantine; rollback requires fresh review.")
        source = Path(manifest["sourcePath"])
        target = Path(manifest["quarantinePath"])
        restore_path_guard(config, home, source, target)
        expected_target = (Path(config["volumeRoot"]) / QUARANTINE_NAME /
                           (ITEM_ID + "-" + BAD_SHA256 + ".iso"))
        require(not source.exists() and not source.is_symlink() and target.is_file() and
                not target.is_symlink() and target == expected_target and
                target.stat().st_size == BAD_SIZE and
                file_sha(target) == BAD_SHA256, "Quarantined source changed.")
        before = json.loads(original)
        after = json.loads(current)
        require(after["revision"] == before["revision"] + 1 and
                process_id() == pid and effective_index(pid) == index,
                "Production state changed since quarantine.")
        before["revision"] = after["revision"] + 1
        restore_raw = encode(before)
        os.replace(target, source)
        sync_dir(source.parent)
        sync_dir(target.parent)
        post_issue = None
        try:
            atomic(index, restore_raw, info)
        except Exception as error:
            try:
                observed = bounded(index, MAX_INDEX)[0]
            except Exception:
                observed = None
            if observed == current:
                os.replace(source, target)
                sync_dir(source.parent)
                sync_dir(target.parent)
                raise
            post_issue = "rollback-index-write-" + type(error).__name__
        try:
            observed = bounded(index, MAX_INDEX)[0]
            restored = (observed == restore_raw and source.stat().st_size == BAD_SIZE and
                        file_sha(source) == BAD_SHA256)
        except Exception as error:
            restored = False
            post_issue = post_issue or "rollback-verify-" + type(error).__name__
        if not restored:
            post_issue = post_issue or "rollback-state-not-verified"
        if restored:
            try:
                if not any(target.parent.iterdir()):
                    target.parent.rmdir()
                    sync_dir(target.parent.parent)
                create_private_file(backup / "rolled-back.json", encode(dict(
                    utc=datetime.now(timezone.utc).isoformat(), revision=before["revision"],
                    restoredIndexSha256=sha(restore_raw), sourceRestored=True,
                    serviceRestarted=False)))
            except Exception as error:
                post_issue = post_issue or "rollback-receipt-" + type(error).__name__
    report = dict(operation="rollback", itemId=ITEM_ID, revision=before["revision"],
                  indexSha256=sha(restore_raw), backupDirectory=str(backup),
                  sourceRestored=restored, serviceRestarted=False,
                  publicationState="published" if restored else "unknown-after-index-write",
                  recoveryNeeded=post_issue is not None)
    if post_issue is not None:
        report["recoveryReason"] = post_issue
    try:
        write_receipt(receipt, report)
        report["receiptWritten"] = True
    except Exception as error:
        report["receiptWritten"] = False
        report["recoveryNeeded"] = True
        report.setdefault("recoveryReason", "private-receipt-" + type(error).__name__)
    return report


def recover(config, pid, index, home, backup, receipt):
    """Resolve a SIGKILL/power-loss window using the pre-written manifest.

    A source move with the old index is rolled back. The quarantine index is
    kept paired with the quarantined source. A completed rollback is recognized.
    """
    require(backup.parent == home.parent and backup.name.startswith("station-wind-waker-backup-20261014-") and
            not backup.is_symlink() and backup.is_dir(), "Guarded backup directory required.")
    with under_lock(home):
        manifest = json.loads(bounded(backup / "manifest.json", 1024 * 1024)[0])
        original, _ = bounded(backup / "index-before.json", MAX_INDEX)
        current, _ = bounded(index, MAX_INDEX)
        source = Path(manifest["sourcePath"])
        target = Path(manifest["quarantinePath"])
        expected_target = (Path(config["volumeRoot"]) / QUARANTINE_NAME /
                           (ITEM_ID + "-" + BAD_SHA256 + ".iso"))
        require(manifest.get("schema") == "station.wind-waker-quarantine.backup.v1" and
                manifest["beforeIndexSha256"] == sha(original) and
                manifest["plan"]["itemId"] == ITEM_ID and
                manifest["plan"]["indexPath"] == str(index) and
                manifest["plan"]["expectedPid"] == pid and
                manifest["plan"]["sourcePath"] == str(source) and
                manifest["plan"]["quarantinePath"] == str(target) and
                target == expected_target and process_id() == pid and
                effective_index(pid) == index,
                "Crash recovery state does not match the sealed plan.")
        _, projected, _ = prepare_index(original)
        require(manifest["afterIndexSha256"] == sha(encode(projected)),
                "Projected quarantine index changed.")
        restored_document = json.loads(original)
        restored_document["revision"] = projected["revision"] + 1
        restored_raw = encode(restored_document)
        source_present, target_present = source.exists(), target.exists()
        restore_path_guard(config, home, source, target, target_must_exist=target_present)
        require(source_present != target_present and not source.is_symlink() and
                not target.is_symlink(), "Source/quarantine presence is ambiguous.")
        post_issue = None
        if sha(current) == manifest["beforeIndexSha256"]:
            if target_present:
                require(target.is_file() and target.stat().st_size == BAD_SIZE and
                        file_sha(target) == BAD_SHA256,
                        "Quarantined source changed after interrupted move.")
                os.replace(target, source)
                sync_dir(source.parent)
                sync_dir(target.parent)
                outcome = "original-source-restored"
            else:
                require(source.is_file() and source.stat().st_size == BAD_SIZE and
                        file_sha(source) == BAD_SHA256,
                        "Original source changed after interrupted operation.")
                outcome = "original-state-already-intact"
            revision = json.loads(current)["revision"]
            if target.parent.exists() and not any(target.parent.iterdir()):
                target.parent.rmdir()
                sync_dir(target.parent.parent)
        elif sha(current) == manifest["afterIndexSha256"]:
            if source_present:
                require(source.is_file() and source.stat().st_size == BAD_SIZE and
                        file_sha(source) == BAD_SHA256,
                        "Source changed during interrupted rollback.")
                os.replace(source, target)
                sync_dir(source.parent)
                sync_dir(target.parent)
            else:
                require(target.is_file() and target.stat().st_size == BAD_SIZE and
                        file_sha(target) == BAD_SHA256,
                        "Applied quarantine source changed.")
            revision = json.loads(current)["revision"]
            outcome = "quarantine-already-applied"
            applied_file = backup / "applied.json"
            if applied_file.exists():
                existing = json.loads(bounded(applied_file, 1024 * 1024)[0])
                require(existing.get("beforeIndexSha256") == sha(original) and
                        existing.get("afterIndexSha256") == sha(current) and
                        existing.get("sourceSha256") == BAD_SHA256 and
                        existing.get("sourceBytes") == BAD_SIZE and
                        existing.get("sourceMoved") is True,
                        "Existing applied receipt is incomplete or inconsistent.")
            else:
                try:
                    create_private_file(applied_file, encode(dict(
                        utc=datetime.now(timezone.utc).isoformat(), pid=pid,
                        beforeIndexSha256=sha(original), afterIndexSha256=sha(current),
                        afterRevision=revision, sourceSha256=BAD_SHA256,
                        sourceBytes=BAD_SIZE, sourceMoved=True,
                        serviceRestarted=False, recoveredAfterInterruption=True)))
                except Exception as error:
                    post_issue = "recovery-applied-receipt-" + type(error).__name__
        elif current == restored_raw:
            if target_present:
                require(target.is_file() and target.stat().st_size == BAD_SIZE and
                        file_sha(target) == BAD_SHA256,
                        "Quarantined source changed during interrupted rollback.")
                os.replace(target, source)
                sync_dir(source.parent)
                sync_dir(target.parent)
            else:
                require(source.is_file() and source.stat().st_size == BAD_SIZE and
                        file_sha(source) == BAD_SHA256,
                        "Restored source changed after rollback.")
            revision = restored_document["revision"]
            outcome = "rollback-already-applied"
            if target.parent.exists() and not any(target.parent.iterdir()):
                target.parent.rmdir()
                sync_dir(target.parent.parent)
            rolled_back = backup / "rolled-back.json"
            if rolled_back.exists():
                existing = json.loads(bounded(rolled_back, 1024 * 1024)[0])
                require(existing.get("restoredIndexSha256") == sha(restored_raw) and
                        existing.get("sourceRestored") is True and
                        existing.get("revision") == revision,
                        "Existing rollback receipt is incomplete or inconsistent.")
            else:
                try:
                    create_private_file(rolled_back, encode(dict(
                        utc=datetime.now(timezone.utc).isoformat(), revision=revision,
                        restoredIndexSha256=sha(restored_raw), sourceRestored=True,
                        serviceRestarted=False, recoveredAfterInterruption=True)))
                except Exception as error:
                    post_issue = "recovery-rollback-receipt-" + type(error).__name__
        else:
            raise ValueError("Index changed after interrupted operation; fresh review required.")
    report = dict(operation="recover", itemId=ITEM_ID, revision=revision,
                  outcome=outcome, backupDirectory=str(backup), serviceRestarted=False,
                  recoveryNeeded=post_issue is not None)
    if post_issue is not None:
        report["recoveryReason"] = post_issue
    try:
        write_receipt(receipt, report)
        report["receiptWritten"] = True
    except Exception as error:
        report["receiptWritten"] = False
        report["recoveryNeeded"] = True
        report.setdefault("recoveryReason", "private-receipt-" + type(error).__name__)
    return report


def write_receipt(path, document):
    owner = private_output_guard(path)
    create_private_file(path, encode(document))
    os.chown(path, owner.pw_uid, owner.pw_gid)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--apply", type=Path, metavar="PLAN_FILE")
    modes.add_argument("--rollback", type=Path, metavar="BACKUP_DIRECTORY")
    modes.add_argument("--recover", type=Path, metavar="BACKUP_DIRECTORY")
    parser.add_argument("--plan-output", type=Path,
                        help="Write a new private 0600 plan; default operation is read-only.")
    parser.add_argument("--receipt", type=Path,
                        help="Write a new private receipt for --apply or --rollback.")
    args = parser.parse_args()
    require(not (args.apply or args.rollback or args.recover) or
            (args.receipt is not None and args.plan_output is None),
            "Apply or rollback needs a private receipt and no plan output.")
    require(not (args.plan_output and args.receipt),
            "Planning does not use an application receipt.")
    require(args.apply is not None or args.rollback is not None or args.recover is not None or args.receipt is None,
            "Read-only planning does not use a receipt.")
    config, pid, index, home = load_context()
    if args.apply:
        private_output_guard(args.receipt)
        submitted = json.loads(bounded(args.apply, 1024 * 1024)[0])
        with_receipt = apply(config, pid, index, home, submitted, args.receipt)
        print(json.dumps(with_receipt, sort_keys=True))
        if with_receipt["recoveryNeeded"]:
            raise SystemExit(2)
    elif args.rollback:
        private_output_guard(args.receipt)
        with_receipt = rollback(config, pid, index, home, args.rollback, args.receipt)
        print(json.dumps(with_receipt, sort_keys=True))
        if with_receipt["recoveryNeeded"]:
            raise SystemExit(2)
    elif args.recover:
        private_output_guard(args.receipt)
        with_receipt = recover(config, pid, index, home, args.recover, args.receipt)
        print(json.dumps(with_receipt, sort_keys=True))
        if with_receipt["recoveryNeeded"]:
            raise SystemExit(2)
    else:
        with under_lock(home):
            proposal = current_plan(config, index, home, pid)
        if args.plan_output:
            private_output_guard(args.plan_output)
            create_private_file(args.plan_output, encode(proposal))
        print(json.dumps(dict(operation="plan", itemId=ITEM_ID,
                              revision=proposal["expectedRevision"],
                              indexSha256=proposal["expectedIndexSha256"],
                              sourceSha256=proposal["expectedSourceSha256"],
                              sourceBytes=proposal["expectedSourceBytes"],
                              itemCount=proposal["itemCount"],
                              productionChanged=False), sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except BlockingIOError:
        raise SystemExit("Station scan is running; retry after it completes.")
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError,
            subprocess.CalledProcessError) as error:
        raise SystemExit("Wind Waker quarantine rejected (" + type(error).__name__ + ").")
