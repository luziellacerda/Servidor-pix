#!/usr/bin/env python3
"""Compare reference checksums offline, once, over the current bound launch file.

The private map remains on the server. Output contains IDs/hashes/decisions only.
No download or app runtime checks are introduced. The full content SHA is always
the authorization identity. Header removal is for SNES CRC research only.
"""
import argparse
import hashlib
import json
import zipfile
import zlib
from pathlib import Path


def payload_hashes(stream, size, snes):
    full_sha = hashlib.sha256()
    full_md5 = hashlib.md5(usedforsecurity=False)
    normalized_md5 = hashlib.md5(usedforsecurity=False)
    crc, normalized_crc, total = 0, 0, 0
    skip = 512 if snes and size % 1024 == 512 else 0
    while block := stream.read(1024 * 1024):
        full_sha.update(block)
        full_md5.update(block)
        crc = zlib.crc32(block, crc)
        normalized = block[max(0, skip - total):]
        normalized_md5.update(normalized)
        normalized_crc = zlib.crc32(normalized, normalized_crc)
        total += len(block)
        if total > size:
            raise ValueError("Unexpected content size")
    if total != size:
        raise ValueError("Incomplete content")
    return {"contentSha256": full_sha.hexdigest(), "md5FullLaunchFile": full_md5.hexdigest(),
            "crc32FullLaunchFile": f"{crc & 0xffffffff:08x}", "sizeBytes": total,
            "researchOnlyHeaderBytesSkipped": skip,
            "researchOnlyHeaderlessMd5": normalized_md5.hexdigest(),
            "researchOnlyHeaderlessCrc32": f"{normalized_crc & 0xffffffff:08x}"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--artifact-map", type=Path, required=True)
    parser.add_argument("--bundle", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--output", type=Path, required=True)
    ns = parser.parse_args()
    rows = json.loads((ns.bundle / "inventory.json").read_text(encoding="utf-8"))["items"]
    private_map = json.loads(ns.artifact_map.read_text(encoding="utf-8"))
    output = []
    for row in rows:
        if not row["catalogVisible"] or not row["referenceAdapters"] or not row["contentSha256"]:
            continue
        result = {"itemId": row["itemId"], "contentSha256": row["contentSha256"]}
        try:
            path = Path(private_map[row["itemId"]])
            with path.open("rb") as raw:
                artifact_sha = hashlib.file_digest(raw, "sha256").hexdigest()
            if artifact_sha != row["artifactSha256"]:
                raise ValueError("Bound artifact changed")
            size = row["expandedSizeBytes"]
            if row["artifactFormat"] == "zip":
                with zipfile.ZipFile(path) as archive:
                    info = archive.getinfo(row["launchPath"])
                    if info.file_size != size:
                        raise ValueError("Bound launch size changed")
                    with archive.open(info) as launch:
                        hashes = payload_hashes(launch, size, row["enginePlatform"] == "snes")
            else:
                with path.open("rb") as launch:
                    hashes = payload_hashes(launch, size, row["enginePlatform"] == "snes")
            if hashes["contentSha256"] != row["contentSha256"]:
                raise ValueError("Full launch content changed")
            matches = []
            for ref in row["referenceAdapters"]:
                if "referenceMd5" in ref:
                    match = ref["referenceMd5"] == hashes["md5FullLaunchFile"]
                else:
                    match = ref["referenceCrc32"] == hashes["researchOnlyHeaderlessCrc32"]
                if match:
                    matches.append(ref)
            result.update(status="exact-current-content-checked", hashes=hashes, matchingReferences=matches)
        except PermissionError:
            result["status"] = "file-not-readable-by-current-os-account"
        except (OSError, KeyError, ValueError, zipfile.BadZipFile):
            # File paths, exception text and configuration never enter the public output.
            result["status"] = "binding-check-failed-or-file-unavailable"
        output.append(result)
    ns.output.parent.mkdir(parents=True, exist_ok=True)
    ns.output.write_text(json.dumps({"schemaVersion": 1, "purpose": "Offline research checks; never a server registry or admission decision.",
                                    "entries": output, "authorizationNormalization": "none-full-launch-file",
                                    "productionModified": False}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"items": len(output), "exactCurrentContentChecked": sum(x["status"] == "exact-current-content-checked" for x in output),
                      "referenceMatches": sum(bool(x.get("matchingReferences")) for x in output)}))


if __name__ == "__main__":
    main()
