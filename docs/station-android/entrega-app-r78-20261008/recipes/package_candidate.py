"""Package the synopsis-only R78 carousel over frozen R77; preserve every other entry.

Build artifacts stay in E:. Publication to G: is a separate, non-overwriting copy.
No ADB, server deployment, checkout, data clearing or signature changes.
"""
from pathlib import Path
import argparse, copy, datetime, hashlib, importlib.util, json, os, re, shutil, struct, subprocess, zipfile

ROOT = Path(__file__).resolve().parent.parent
R77 = ROOT.parent / 'station-multiplayer-r77-20261008'
spec = importlib.util.spec_from_file_location('frozen_package_helpers', R77 / 'recipes/package_candidate.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
require, sha, zsha, load, signature = helpers.require, helpers.sha, helpers.zsha, helpers.load, helpers.signature
BASE = Path(r'G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R77-20261008.apk')
BASE_SHA = '14450f3aa52ca2c795b50afba7e5a75c5bd4f71aa0007d2c43c6542051bdb737'
BASE_CAROUSEL = '98e951b2aad45d63ebe563de95d7a9299339928e54ce082915a147a992ab0302'
ENTRY = 'lib/arm64-v8a/libturbo_carousel.so'
WORK_ROOT = Path(r'E:\ESTUDO APK\work\station-synopses-r78-20261008')
FINAL = Path(r'G:\BAKUP SISTEMA APP 03-10-2026\apks-candidatos-visuais\TurboStations-Premium-R78-20261008.apk')
CERT = helpers.CERT


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, required=True)
    parser.add_argument('--workspace', type=Path, default=WORK_ROOT / 'package-01')
    parser.add_argument('--publish-final', action='store_true')
    args = parser.parse_args()
    build, work = args.build.resolve(), args.workspace.resolve()
    require(build.is_relative_to(WORK_ROOT.resolve()), 'Build must be in the R78 E: workspace')
    require(work != WORK_ROOT.resolve() and work.is_relative_to(WORK_ROOT.resolve()) and not work.exists(), 'Fresh child of R78 E: workspace required')
    require(sha(BASE) == BASE_SHA, 'Exact frozen R77 APK required')
    manifest_file = ROOT / 'OVERLAY-MANIFEST.json'
    build_file = build / 'evidence/build.json'
    receipt, manifest = load(build_file), load(manifest_file)
    carousel = build / 'libturbo_carousel.so'
    require(receipt['compiled'] and receipt['synopsisOnly'] and receipt['baseNativeSHA256'] == BASE_CAROUSEL, 'Reviewed synopsis-only build required')
    require(receipt['baselineReproductionSHA256'] == BASE_CAROUSEL, 'R77 must reproduce byte for byte')
    require(sha(carousel) == receipt['nativeSHA256'] != BASE_CAROUSEL, 'Compiled carousel mismatch')
    require(receipt['overlayManifestSHA256'] == sha(manifest_file), 'Overlay changed after build')
    require(receipt['recipeSHA256'] == sha(ROOT / 'recipes/build_carousel.py'), 'Build recipe changed')
    require(all(sha(ROOT / 'native' / name) == digest for name, digest in manifest['sources'].items()), 'Source overlay changed')
    require(all(sha(ROOT / name) == digest for name, digest in manifest['dataSources'].items()), 'Editorial data changed')
    require(all(sha(build / 'native' / name) == digest for name, digest in receipt['nativeSources'].items()), 'Staged sources changed')
    require(all(sha(path) == digest for path, digest in receipt['quotedDependencies'].items()), 'Quoted dependency changed')
    require(all(sha(path) == digest for path, digest in receipt['objects'].items()), 'Object dependency changed')
    require(all(sha(item['path']) == item['sha256'] for item in receipt['testSources'].values()), 'Test source changed')
    helpers.elf(carousel)
    require(shutil.disk_usage(work.parent).free > 2 * BASE.stat().st_size + 256 * 1024**2, 'Insufficient build disk space')
    if args.publish_final:
        require(not FINAL.exists() and shutil.disk_usage(FINAL.parent).free > BASE.stat().st_size + 128 * 1024**2, 'G: needs room for a new final APK; do not replace an existing artifact')
    (work / 'evidence').mkdir(parents=True)
    (work / 'temp').mkdir()
    env = dict(os.environ, TEMP=str(work / 'temp'), TMP=str(work / 'temp'))
    for name in ('STATION_KEYSTORE', 'STATION_KEY_ALIAS', 'STATION_KS_PASS', 'STATION_KEY_PASS'):
        require(bool(env.get(name)), 'Existing signing environment required: ' + name)
    require(Path(env['STATION_KEYSTORE']).resolve() == helpers.AUTHORIZED_KEY.resolve(), 'Use only the explicitly authorized matching key')
    tools = Path(r'E:\ESTUDO APK\TurboRetroEmu-build\android-build-tools\35.0.0\android-15')
    jdk = Path(r'C:\Program Files\Eclipse Adoptium\jdk-17.0.20.101-hotspot\bin')
    public = subprocess.run([str(jdk / 'keytool.exe'), '-exportcert', '-keystore', env['STATION_KEYSTORE'], '-alias', env['STATION_KEY_ALIAS'], '-storepass:env', 'STATION_KS_PASS'], capture_output=True, env=env)
    require(public.returncode == 0 and hashlib.sha256(public.stdout).hexdigest() == CERT, 'Signing certificate must match exactly')
    recipe_sha, build_sha, manifest_sha = sha(__file__), sha(build_file), sha(manifest_file)
    helpers_sha = sha(R77 / 'recipes/package_candidate.py')

    def run(command, label):
        process = subprocess.run(list(map(str, command)), capture_output=True, env=env)
        (work / 'evidence' / (label + '-private.log')).write_bytes(process.stdout + process.stderr)
        require(process.returncode == 0, 'Packaging tool failed: ' + label)
        return (process.stdout + process.stderr).decode('utf8', 'replace')

    def signer(path, label):
        text = run([jdk / 'java.exe', '-jar', tools / 'lib/apksigner.jar', 'verify', '--print-certs', path], label)
        found = re.findall(r'^Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]{64})\s*$', text, re.MULTILINE)
        require([value.lower() for value in found] == [CERT], 'Single original signer required')

    signer(BASE, 'base-cert')
    unsigned, signed = work / 'unsigned-r78.apk', work / 'TurboStations-Premium-R78-20261008.apk'
    with zipfile.ZipFile(BASE) as old, zipfile.ZipFile(unsigned, 'w', allowZip64=True) as new:
        names = [n for n in old.namelist() if not signature(n)]
        require(len(names) == len(set(names)) == 13226, 'R77 inventory differs')
        require(zsha(old, ENTRY) == BASE_CAROUSEL, 'Base carousel differs')
        for info in old.infolist():
            if signature(info.filename): continue
            item = copy.copy(info)
            item.extra = b''
            if item.filename == ENTRY: item.file_size = carousel.stat().st_size
            if item.compress_type == zipfile.ZIP_STORED:
                alignment = 16384 if item.filename.endswith('.so') else 4
                offset = new.fp.tell() + 30 + len(item.filename.encode('utf8'))
                if offset % alignment:
                    pad = (-(offset + 4)) % alignment
                    item.extra = struct.pack('<HH', 0xffff, pad) + bytes(pad)
            with new.open(item, 'w') as target, (carousel.open('rb') if item.filename == ENTRY else old.open(info)) as source:
                shutil.copyfileobj(source, target, 1024 * 1024)
    run([tools / 'zipalign.exe', '-c', '-P', '16', '4', unsigned], 'unsigned-alignment')
    run([jdk / 'java.exe', '-Djava.io.tmpdir=' + str(work / 'temp'), '-jar', tools / 'lib/apksigner.jar', 'sign', '--alignment-preserved', 'true', '--v4-signing-enabled', 'false', '--ks', env['STATION_KEYSTORE'], '--ks-key-alias', env['STATION_KEY_ALIAS'], '--ks-pass', 'env:STATION_KS_PASS', '--key-pass', 'env:STATION_KEY_PASS', '--out', signed, unsigned], 'sign')
    signer(signed, 'signed-cert')
    run([tools / 'zipalign.exe', '-c', '-P', '16', '4', signed], 'signed-alignment')
    preserved, videos, changes = 0, {}, []
    with zipfile.ZipFile(BASE) as old, zipfile.ZipFile(signed) as new:
        final_names = [n for n in new.namelist() if not signature(n)]
        require(len(final_names) == len(set(final_names)) and set(final_names) == set(names), 'Entry set changed')
        for name in sorted(names):
            before, after = zsha(old, name), zsha(new, name)
            require(after == (receipt['nativeSHA256'] if name == ENTRY else before), 'Unexpected byte change: ' + name)
            require(new.getinfo(name).compress_type == old.getinfo(name).compress_type, 'Compression changed: ' + name)
            if after == before: preserved += 1
            else: changes.append(name)
            if name.endswith('.mp4'): videos[name] = after
    require(preserved == 13225 and changes == [ENTRY] and len(videos) == 59, 'Only carousel may change')
    require(sha(carousel) == receipt['nativeSHA256'] and sha(__file__) == recipe_sha and sha(build_file) == build_sha and sha(manifest_file) == manifest_sha and sha(R77 / 'recipes/package_candidate.py') == helpers_sha, 'Input/recipe changed during package')
    require(all(sha(ROOT / 'native' / name) == digest for name, digest in manifest['sources'].items()) and all(sha(ROOT / name) == digest for name, digest in manifest['dataSources'].items()), 'Source/data changed during package')
    digest = sha(signed)
    final_path = None
    if args.publish_final:
        with FINAL.open('xb') as target, signed.open('rb') as source: shutil.copyfileobj(source, target, 1024 * 1024)
        require(sha(FINAL) == digest, 'Final copy mismatch')
        final_path = str(FINAL)
    record = dict(version='R78', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), sha256=digest, bytes=signed.stat().st_size, temporaryApk=str(signed), finalApk=final_path, baseSHA256=BASE_SHA, certificateSHA256=CERT, carouselSHA256=receipt['nativeSHA256'], changes=changes, preservedEntries=preserved, totalVideos=59, videoHashes=videos, allPackageEntriesVerified=True, alignment16KiB=True, buildReceiptSHA256=build_sha, overlayManifestSHA256=manifest_sha, packageRecipeSHA256=recipe_sha, helperRecipeSHA256=helpers_sha, onlineRuntimeDexEnginesUnchangedFromR77=True, newEngineRegistrationForSynopsisChangeRequired=False, inheritedR77ProductionQualificationStillRequired=True, installed=False, physicalDisplayVerified=False)
    for destination in (work / 'evidence/package.json', ROOT / 'evidence/package.json'):
        destination.write_text(json.dumps(record, indent=2) + '\n', 'utf8')
    require(unsigned.resolve().parent == work and unsigned.name == 'unsigned-r78.apk', 'Cleanup path mismatch')
    unsigned.unlink()
    print(json.dumps({key:value for key,value in record.items() if key != 'videoHashes'}, indent=2))


if __name__ == '__main__': main()
