"""Extract only the six real core/runtime libraries from the final verified APK.

This is a private administrative kit: do NOT commit or upload its binaries to
Git. Dependencies are hashed against the same APK but are not copied. This tool
does not authenticate, upload, install, execute libraries or modify a server.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile
import zipfile
from build_incoming_manifest import incoming


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda:f.read(1024*1024), b""):
            h.update(block)
    return h.hexdigest()


def basename(name):
    if not isinstance(name, str) or not re.fullmatch(r"lib[a-zA-Z0-9_+.-]+\.so", name) or Path(name).name != name:
        raise ValueError("Expected a regular Android library basename")
    return name


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--apk", type=Path, required=True)
    p.add_argument("--package-receipt", type=Path, required=True)
    p.add_argument("--runtime-receipt", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    a = p.parse_args()
    package = json.loads(a.package_receipt.read_bytes())
    runtime = json.loads(a.runtime_receipt.read_bytes())
    if package.get("identityReceiptSha256") is not None and digest(a.runtime_receipt)!=package["identityReceiptSha256"]:
        raise ValueError("Runtime receipt differs from the final package identity seal")
    if a.apk.is_symlink() or not a.apk.is_file() or a.apk.stat().st_size != package["apkBytes"] or digest(a.apk) != package["apkSha256"]:
        raise ValueError("The complete APK differs from the signed final package receipt")
    if package.get("signatureSchemesVerified") != ["v2", "v3"] or package.get("zipAlignmentVerifiedBytes") != 16384:
        raise ValueError("Final signature/alignment verification receipt required")
    engines = runtime["engines"]
    if sorted(e["engineId"] for e in engines) != sorted(package["engineIds"]):
        raise ValueError("Runtime and package identities differ")
    incoming(runtime,package)
    selected, dependencies = {}, {}
    for e in engines:
        for key, sha in (("library","coreSha256"),("runtimeLibrary","runtimeSha256")):
            name = basename(e[key])
            value = e[sha]
            if name in selected and selected[name] != value:
                raise ValueError("Conflicting shared library identity")
            selected[name] = value
        for name, sha in e.get("dependencySha256", {}).items():
            name = basename(name)
            if name in dependencies and dependencies[name] != sha:
                raise ValueError("Conflicting dependency identity")
            dependencies[name] = sha
    if len(selected) != 6:
        raise ValueError("Expected exactly six distinct final core/runtime libraries")
    if any(name in selected and selected[name]!=sha for name,sha in dependencies.items()):
        raise ValueError("Selected/dependency library identity conflicts")
    a.output.mkdir(parents=True, exist_ok=True)
    if a.output.is_symlink():
        raise ValueError("Private kit output must be a real directory")
    allowed = set(selected) | {"artifact-kit-receipt.json", "incoming-engine-manifest.json", "profile-canonical-receipt.json"}
    if any(p.is_symlink() or not p.is_file() or p.name not in allowed for p in a.output.iterdir()):
        raise ValueError("Private current kit contains unexpected files; no cleanup attempted")
    checked, staged = {}, {}
    try:
        with zipfile.ZipFile(a.apk) as z:
            names = z.namelist()
            if len(names) != len(set(names)):
                raise ValueError("Duplicate APK entries")
            for name, expected in {**dependencies, **selected}.items():
                entry = "lib/arm64-v8a/"+name
                info = z.getinfo(entry)
                if info.file_size < 1 or info.file_size > 256*1024*1024:
                    raise ValueError("Library exceeds the administrative byte bound")
                h = hashlib.sha256()
                fd, temp = tempfile.mkstemp(prefix=".kit-verification-", dir=a.output) if name in selected else (None, None)
                if temp is not None:staged[name]=temp
                with z.open(info) as source:
                    target = os.fdopen(fd,"wb") if fd is not None else None
                    try:
                        for block in iter(lambda:source.read(1024*1024), b""):
                            h.update(block)
                            if target is not None:
                                target.write(block)
                    finally:
                        if target is not None:
                            target.close()
                if h.hexdigest() != expected:raise ValueError("Final APK library differs: "+name)
                checked[name] = dict(sha256=expected, bytes=info.file_size, copiedToPrivateKit=name in selected)
        # A receipt or library mismatch leaves an existing current kit untouched.
        # All real bytes, including noncopied dependencies, pass before replacement.
        for name,temp in staged.items():os.replace(temp,a.output/name)
    finally:
        for temp in staged.values():
            if os.path.exists(temp):os.unlink(temp)
    receipt = dict(schemaVersion=1, revision=package["revision"], apkSha256=package["apkSha256"], apkBytes=package["apkBytes"],
        engineIds=package["engineIds"], selectedCoreRuntimeLibraryCount=len(selected),
        libraryChecks=checked, privateBinaryKit=True, uploaded=False, deployed=False,
        signatureVerifiedByFinalReceipt=True, libraryBytesVerifiedAgainstCompleteFinalApk=True)
    (a.output/"artifact-kit-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(dict(revision=package["revision"],apkSha256=package["apkSha256"],copiedLibraries=len(selected),dependenciesChecked=len(dependencies),uploaded=False)))


if __name__ == "__main__":
    main()
