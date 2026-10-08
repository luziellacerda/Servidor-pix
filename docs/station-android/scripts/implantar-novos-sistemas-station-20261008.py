#!/usr/bin/env python3
"""Publish five reviewed systems, covers and automatic discovery on the current R81 API."""
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import base64
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import pwd
import re
import secrets
import shutil
import stat
import subprocess
import sys
import time
import traceback
import urllib.error
import urllib.request
import uuid
import zipfile

SCRIPTS = Path(__file__).resolve().parent
PRIVATE = Path('/mnt/DADOS/station-new-systems-private-20261008')
VOLUME = Path('/media/lz-servidor/a2700961-7d8b-435f-9408-9132877ff0fc')
STORE = VOLUME / '.station-content-new-systems-20261008'
HOME = Path('/mnt/DADOS/turbostation-library-auto-20261004')
CONFIG = Path('/mnt/DADOS/station-library-auto-private-20261004/config.json')
API = 'turborama-station-api.service'
SCAN = 'turborama-station-library-scan.service'
TIMER = 'turborama-station-library-scan.timer'
DLL = Path('/opt/turborama-station-online-r81-20261008-db50a98/TurboRamaSuiteOnlineServer.dll')
DLL_SHA = 'cdf14b8067de415413c503de787c6d621c6e8f0466eb2cdd7c0eced8b5a619af'
OLD_SCANNER = Path('/opt/turborama-station-library-r81-20261008-b472d8a/atualizar-biblioteca-station.py')
OLD_SCANNER_SHA = 'fdce74d82478b5a6688bcb67baad55193580aad139603c1d02b379610c868b05'
TOOLS = ('atualizar-biblioteca-station.py', 'station_packages.py', 'station_revista.py',
         'station_disc.py', 'preparar-indice-artefatos.py')
COUNTS = {'gamecube': 21, 'psx': 84, 'wii': 7, 'wiiu': 1, 'switch': 1}
API_DROPIN = Path('/etc/systemd/system') / (API + '.d') / ('z' * 30 + '-station-new-systems-20261008.conf')
SCAN_DROPIN = Path('/etc/systemd/system') / (SCAN + '.d') / ('z' * 30 + '-station-new-systems-20261008.conf')
PREPARED_CACHE = HOME / '.new-systems-stage-194833'


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def run(*args):
    return subprocess.check_output(args, text=True, stderr=subprocess.PIPE).strip()


def sha(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def save(name, data):
    path = PRIVATE / name
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    temporary.chmod(0o600)
    os.chown(temporary, 1000, 1000)
    os.replace(temporary, path)


def progress(phase, **details):
    save('progress.json', dict(phase=phase, utc=datetime.now(timezone.utc).isoformat(), **details))
    print(json.dumps(dict(phase=phase, **details)), flush=True)


def ready():
    return json.load(urllib.request.urlopen('http://127.0.0.1:5192/ready/station/online', timeout=10))


def idle(value):
    return (value['activeRooms'] == value['activeConnections'] == 0 and
            all(value.get('recovery', {}).get(k, 0) == 0 for k in ('retainedRooms', 'activeConnections', 'pendingBytes')) and
            all(value.get('multiplayer', {}).get(k, 0) == 0 for k in ('activeRooms', 'activeConnections', 'retainedBytes')))


def empty_closed_room(value):
    """Only a single empty v3 lobby, after the operator confirmed leaving both phones."""
    return (value['activeRooms'] == value['activeConnections'] == 0 and
            all(value.get('recovery', {}).get(k, 0) == 0 for k in ('retainedRooms', 'activeConnections', 'pendingBytes')) and
            value.get('multiplayer', {}).get('activeRooms') == 1 and
            all(value.get('multiplayer', {}).get(k, 0) == 0 for k in ('activeConnections', 'retainedBytes')))


def protected(path):
    info = path.lstat()
    if stat.S_ISLNK(info.st_mode) or info.st_uid != 0 or info.st_mode & 0o022:
        raise ValueError('Prepared cache is not protected by the administrative account')


def reuse_prepared(stage, before, plan, library, cache):
    """Reuse only reviewed, unpublished items from our root-owned failed preparation."""
    if cache.parent != HOME or not re.fullmatch(r'\.new-systems-stage-\d{6}', cache.name):
        raise ValueError('Prepared cache leaves the protected staging area')
    if not cache.exists():
        return 0
    for path in (cache, cache / 'index.json', cache / 'state.json'):
        protected(path)
    cached = json.loads((cache / 'index.json').read_bytes())
    cached_state = json.loads((cache / 'state.json').read_bytes())
    cached_rows = {r['itemId']: r for r in cached['items']}
    original_revision = json.loads((PRIVATE / 'index-before.json').read_bytes())['revision']
    if not original_revision < cached['revision'] <= before['revision'] + 1 or any(cached_rows.get(r['itemId']) != r for r in before['items']):
        raise ValueError('Prepared cache does not preserve the current production baseline')
    reviewed = {str((VOLUME / r['platform'] / r['rom']).resolve()): r for r in plan['games']}
    state = json.loads((stage / 'state.json').read_bytes())
    rows = list(before['items'])
    baseline_ids = {r['itemId'] for r in rows}
    for key, prepared_source in cached_state['sources'].items():
        if key not in reviewed:
            continue  # The unpublished loose Jackie BIN is deliberately excluded.
        game = reviewed[key]
        expected_id = 'station_' + hashlib.sha256((game['platform'] + ':' + game['rom']).encode()).hexdigest()[:32]
        if prepared_source['ids'] != [expected_id] or expected_id in baseline_ids:
            raise ValueError('Prepared game identity differs from its reviewed source')
        row = cached_rows[expected_id]
        file = Path(row['filePath'])
        cover = Path(row['coverPath'])
        if row['platform'] != game['platform'] or file != STORE / 'games' / row['artifact']['sha256'] / row['artifact']['fileName']:
            raise ValueError('Prepared artifact identity differs')
        cover_home = cover.parent.parent
        if cover.parent.name != 'covers' or cover_home.parent != HOME or not re.fullmatch(r'\.new-systems-stage-\d{6}', cover_home.name) or cover.name != sha(cover) + '.jpg':
            raise ValueError('Prepared cover identity differs')
        for path in (STORE, file.parent.parent, file.parent, file, cover_home, cover.parent, cover):
            protected(path)
        if file.stat().st_size != row['artifact']['sizeBytes']:
            raise ValueError('Prepared immutable artifact size differs')
        rows.append(row)
        state['sources'][key] = prepared_source
        if key in cached_state.get('observations', {}):
            state['observations'][key] = cached_state['observations'][key]
    reused = len(rows) - len(before['items'])
    library.atomic_json(stage / 'index.json', dict(before, revision=before['revision'] + (1 if reused else 0), items=rows))
    library.atomic_json(stage / 'state.json', state)
    return reused


def prove_public(index, values, rows, ops):
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import padding
    helper = load(SCRIPTS / 'verificar-http-release-station.py', 'new_systems_fixture')
    public = serialization.load_pem_private_key(Path(values['Station__AssertionPrivateKeyPemFile']).read_bytes(), None).public_key()
    pepper = base64.b64decode(Path(values['Station__ActivationPepperFile']).read_bytes(), validate=True)
    connection = Path(values['Station__DatabaseConnectionFile']).read_text()
    db = re.search(r'(?:^|;)Database=([a-zA-Z0-9_-]+)(?:;|$)', connection)
    if not db or not connection.startswith('Host=127.0.0.1;'):
        raise ValueError('Unexpected Station database target')
    base = 'https://app.lzgames.com.br'

    class Client(helper.StationReleaseVerification):
        def request(self, base, method, route, payload=None, bearer=None, prefix=False):
            correlation = uuid.uuid4().hex
            headers = {'Accept': 'application/json', 'X-Correlation-ID': correlation,
                       'User-Agent': 'Dalvik/2.1.0 Station new systems qualification'}
            body = None if payload is None else json.dumps(payload, separators=(',', ':')).encode()
            if body is not None:
                headers['Content-Type'] = 'application/json'
            if bearer:
                headers['Authorization'] = 'Bearer ' + bearer
                stamp = str(int(time.time()))
                nonce = helper.b64(secrets.token_bytes(16))
                canonical = ('TurboRamaStationAndroid/request/v1\n' + method + '\n' + route + '\n' +
                             hashlib.sha256(body or b'').hexdigest() + '\n' + hashlib.sha256(bearer.encode()).hexdigest() +
                             '\n' + stamp + '\n' + nonce + '\n').encode()
                signature = self.device.sign(canonical, padding.PSS(mgf=padding.MGF1(hashes.SHA256()), salt_length=32), hashes.SHA256())
                headers['X-Station-Request-Proof'] = 'v1.' + stamp + '.' + nonce + '.' + helper.b64(signature)
            try:
                response = self.opener.open(urllib.request.Request(base + route, body, headers, method=method), timeout=30)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                if response.url != base + route or response.headers.get('X-Correlation-ID') != correlation:
                    raise ValueError('Public HTTP correlation differs')
                content = response.read(65536 if prefix else 16 * 1024 * 1024 + 1)
                if not prefix and len(content) > 16 * 1024 * 1024:
                    raise ValueError('Public response exceeds limit')
                return response.status, response.headers, content

    client = Client(lambda statement: ops.sql(db[1], statement), pepper, public, index)
    try:
        client.create()
        challenge = client.signed(client.request(base, 'POST', '/v1/station/activations/challenge',
            dict(client.identity, domain=helper.PREFIX + 'request-activation-challenge/v1', activationCode=client.code, devicePublicKey=client.spki)), 'activation-challenge')
        client.signed(client.request(base, 'POST', '/v1/station/activations/complete', client.proof(
            dict(client.identity, domain=helper.PREFIX + 'activate/v1', activationCode=client.code, devicePublicKey=client.spki,
                 challengeId=challenge['challengeId'], nonce=challenge['nonce'], requestProof='rsa-pss-v1'))), 'activated')
        challenge = client.signed(client.request(base, 'POST', '/v1/station/challenges', dict(client.identity,
            domain=helper.PREFIX + 'request-session-challenge/v1', licenseId=client.license_id)), 'session-challenge')
        session = client.signed(client.request(base, 'POST', '/v1/station/sessions', client.proof(dict(client.identity,
            domain=helper.PREFIX + 'open-session/v1', licenseId=client.license_id, challengeId=challenge['challengeId'],
            nonce=challenge['nonce'], requestProof='rsa-pss-v1'))), 'session')
        bearer = session['accessToken']
        expected = {r['itemId']: r for r in index['items'] if r.get('catalogVisible', True)}
        for metadata in (False, True):
            deadline = time.monotonic() + 60
            while True:
                catalog = client.signed(client.request(base, 'GET', '/v1/station/catalog' + ('?metadata=1' if metadata else ''), bearer=bearer), 'catalog')
                if catalog['revision'] == index['revision']:
                    break
                if time.monotonic() > deadline:
                    raise TimeoutError('Live catalog did not reload')
                time.sleep(3)
            received = {r['itemId']: r for r in catalog['items']}
            if catalog['revision'] != index['revision'] or received.keys() != expected.keys():
                raise ValueError('Public catalog revision or IDs differ')
            for item_id, row in expected.items():
                if any(received[item_id][key] != row[key] for key in ('itemId', 'name', 'platform', 'revision', 'coverId')):
                    raise ValueError('Public game identity differs')
                if metadata and (received[item_id]['metadata'] != row['metadata'] or received[item_id]['folderPath'] != row.get('folderPath', [])):
                    raise ValueError('Public game metadata differs')
                if received[item_id].get('contentSha256') != row.get('contentSha256'):
                    raise ValueError('Existing online content identity differs')
                if any(k in received[item_id] for k in ('filePath', 'coverPath')):
                    raise ValueError('Private path in public catalog')

        def cover(row):
            response = client.request(base, 'GET', '/v1/station/covers/' + row['coverId'], bearer=bearer)
            if response[0] != 200 or response[2] != Path(row['coverPath']).read_bytes() or not response[1].get('Content-Type', '').startswith('image/'):
                raise ValueError('Public cover bytes differ')
            return len(response[2])

        with ThreadPoolExecutor(4) as workers:
            lengths = list(workers.map(cover, rows))
        grants = []
        for platform in COUNTS:
            row = min((r for r in rows if r['platform'] == platform), key=lambda r: r['artifact']['sizeBytes'])
            grant = client.signed(client.request(base, 'POST', '/v1/station/downloads/authorize',
                dict(client.identity, domain=helper.PREFIX + 'request-download/v1', itemId=row['itemId']), bearer), 'download-grant')
            if grant['artifact'] != row['artifact'] or grant['itemId'] != row['itemId'] or grant['itemRevision'] != row['revision']:
                raise ValueError('Public download descriptor differs')
            route = '/v1/station/artifacts/' + grant['grantId']
            response = client.request(base, 'GET', route, bearer=bearer, prefix=True)
            with Path(row['filePath']).open('rb') as original:
                prefix = original.read(65536)
            if response[0] != 200 or int(response[1]['Content-Length']) != row['artifact']['sizeBytes'] or response[2] != prefix:
                raise ValueError('Public download stream differs')
            repeated = client.request(base, 'GET', route, bearer=bearer)
            if repeated[0] != 404:
                raise ValueError('Public download grant reused')
            grants.append({'platform': platform, 'format': row['artifact']['format'],
                           'launchPath': row['artifact']['launchPath'], 'fileCount': row['artifact']['fileCount'],
                           'bytes': row['artifact']['sizeBytes'], 'streamPrefixMatched': True, 'oneUseVerified': True})
        return dict(passed=True, signedCatalogMatched=True, visibleItems=len(expected),
            allMetadataMatched=True, coversVerified=len(rows), coverWorkers=4, coverBytes=sum(lengths),
            downloadStreams=grants, completeLargeDownloadMeasured=False, phoneGameplayTested=False)
    finally:
        if not client.cleanup():
            raise ValueError('Owned content fixture cleanup failed')


def main():
    if os.geteuid() != 0:
        raise SystemExit('Linux administrative authentication is required')
    os.umask(0o077)
    sys.dont_write_bytecode = True
    confirmed_closed = sys.argv[1:] == ['--closed-empty-room-confirmed']
    if sys.argv[1:] and not confirmed_closed:
        raise ValueError('Unknown deployment argument')
    # This one-use maintenance option is used only following the explicit human
    # reply "Saí da sala nos celulares". It never admits a connected or buffered game.
    closed_room_cleared = False
    organization = json.loads((PRIVATE / 'organization-receipt.json').read_bytes())
    baseline_file = PRIVATE / 'retry-baseline.json'
    baseline = json.loads(baseline_file.read_bytes()) if baseline_file.exists() else organization
    source_config = json.loads((PRIVATE / 'library-config.json').read_bytes())
    if json.loads(CONFIG.read_bytes()) != source_config or sha(HOME / 'index.json') != baseline['indexSha256']:
        raise ValueError('Reviewed configuration or catalog was superseded')
    if sha(DLL) != DLL_SHA or str(DLL) not in run('systemctl', 'show', API, '-p', 'ExecStart', '--value'):
        raise ValueError('Current R81 API was superseded')
    if sha(OLD_SCANNER) != OLD_SCANNER_SHA or str(OLD_SCANNER) not in run('systemctl', 'show', SCAN, '-p', 'ExecStart', '--value'):
        raise ValueError('Current R81 scanner was superseded')
    if VOLUME.stat().st_dev != source_config['volumeDevice'] or any((VOLUME / 'snes' / p).exists() for p in COUNTS):
        raise ValueError('Organized system folders differ')
    plan = json.loads((PRIVATE / 'prepared/cover-plan.json').read_bytes())
    if plan['platforms'] != COUNTS or plan['totalGames'] != 114 or plan['missing']:
        raise ValueError('Complete cover plan is required')
    for p in COUNTS:
        if (VOLUME / p / 'gamelist.xml').read_bytes() != (PRIVATE / 'prepared' / p / 'gamelist.xml').read_bytes():
            raise ValueError('Prepared metadata changed')
    seal = json.loads((PRIVATE / 'scanner-seal.json').read_bytes())
    if {name: sha(SCRIPTS / name) for name in TOOLS} != seal['tools']:
        raise ValueError('Tested scanner tools changed')
    identity = pwd.getpwnam('turborama-station-api')
    before_pid = run('systemctl', 'show', API, '-p', 'MainPID', '--value')
    shared_units = ('turborama-pix', 'turborama-suite-api', 'turborama-suite-admin', 'turborama-suite-content-gateway',
                    'nginx', 'cloudflared', 'postgresql@16-main', 'redis-server', 'turbobox-php-fpm')
    shared = {name: run('systemctl', 'show', name, '-p', 'MainPID', '--value') for name in shared_units}
    backup = Path('/mnt/DADOS') / ('station-new-systems-backup-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir(mode=0o700)
    installed = False
    mounted = False
    was_timer = subprocess.run(['systemctl', 'is-active', '--quiet', TIMER]).returncode == 0
    library = None
    stage = HOME / ('.new-systems-stage-' + backup.name.rsplit('-', 1)[1])
    try:
        run('systemctl', 'stop', TIMER)
        deadline = time.monotonic() + 120
        while run('systemctl', 'show', SCAN, '-p', 'ActiveState', '--value') in ('active', 'activating', 'deactivating'):
            if time.monotonic() > deadline:
                raise TimeoutError('Existing library scan is still running')
            time.sleep(1)
        with (HOME / 'scan.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            if sha(HOME / 'index.json') != baseline['indexSha256']:
                raise ValueError('Catalog changed before preparation')
            for name in ('index.json', 'state.json', 'report.json'):
                shutil.copy2(HOME / name, backup / name)
            shutil.copy2(CONFIG, backup / 'config.json')
            before = json.loads((backup / 'index.json').read_bytes())
            if any(r['platform'] in COUNTS for r in before['items']) or len(before['items']) + 114 > 4096:
                raise ValueError('New system registration or capacity differs')
            release_hash = hashlib.sha256(json.dumps(seal['tools'], sort_keys=True).encode()).hexdigest()
            release = Path('/opt') / ('turborama-station-library-new-systems-20261008-' + release_hash[:10])
            release.mkdir(mode=0o755, exist_ok=True)
            for name in TOOLS:
                target = release / name
                if target.exists():
                    if sha(target) != seal['tools'][name]:
                        raise ValueError('Immutable scanner release differs')
                else:
                    shutil.copyfile(SCRIPTS / name, target)
                    target.chmod(0o644)
            STORE.mkdir(mode=0o750, exist_ok=True)
            os.chown(STORE, 0, identity.pw_gid)
            run('setfacl', '-m', 'u:turborama-station-api:r-x,d:u:turborama-station-api:r-x', str(STORE))
            stage.mkdir(mode=0o750)
            run('setfacl', '-m', 'u:turborama-station-api:r-x,d:u:turborama-station-api:r-x', str(stage))
            for name in ('index.json', 'state.json'):
                shutil.copy2(backup / name, stage / name)
            sys.path.insert(0, str(release))
            library = load(release / TOOLS[0], 'reviewed_new_systems_scanner')
            cache = Path(baseline.get('priorFailedCandidate', str(PREPARED_CACHE)))
            reused = reuse_prepared(stage, before, plan, library, cache)
            updated = json.loads(json.dumps(source_config))
            extensions = {'gamecube': ['.iso', '.gcm', '.rvz', '.gcz', '.wia', '.ciso'],
                          'wii': ['.iso', '.rvz', '.wbfs', '.gcz', '.wia', '.ciso'],
                          'psx': ['.cue', '.pbp', '.chd', '.iso', '.img', '.bin'],
                          'wiiu': ['.rpx', '.wud', '.wux', '.wua'], 'switch': ['.nsp', '.xci']}
            for platform in COUNTS:
                updated['platforms'][platform] = {'extensions': extensions[platform],
                    'artifactMode': 'cue-disc' if platform == 'psx' else 'wiiu-folder' if platform == 'wiiu' else 'single-rom',
                    'artifactDirectory': str(STORE / 'games'), 'copyRawOnce': True,
                    'catalogSeed': 'station-catalog-seed.json', 'regionalPlatform': platform}
            candidate = dict(updated, outputDirectory=str(stage))
            candidate['platforms'] = {p: updated['platforms'][p] for p in COUNTS}
            progress('Compilando jogos e capas', expectedGames=114, reusedPreparedGames=reused, apiChanged=False)
            report = library.publish(candidate, bootstrap=True, on_progress=lambda added, changed:
                progress('Compilando jogos e capas', importedGames=added, expectedGames=114) if added % 10 == 0 else None)
            if report['pending'] or report['placeholderCovers'] or report['added'] != 114 - reused or report['updated']:
                save('candidate-report-private.json', report)
                raise ValueError('The complete game and cover import did not pass')
            # Prove the full timer configuration before any production write or
            # restart. Existing, unchanged pending entries belong to the baseline.
            previous_report = json.loads((backup / 'report.json').read_bytes())
            known_pending = {json.dumps(p, sort_keys=True) for p in previous_report.get('pending', [])}
            audit_config = json.loads(json.dumps(updated))
            audit_config['outputDirectory'] = str(stage)
            audit = library.publish(audit_config)
            if audit['changed'] or audit['updated'] or audit['added'] or any(
                    json.dumps(p, sort_keys=True) not in known_pending for p in audit['pending']):
                save('full-stage-report-private.json', audit)
                raise ValueError('Full staged scan introduced catalog changes or new pending sources')
            report = audit
            index = json.loads((stage / 'index.json').read_bytes())
            current = {r['itemId']: r for r in index['items']}
            if any(current.get(r['itemId']) != r for r in before['items']):
                raise ValueError('An existing catalog row changed')
            rows = [r for r in index['items'] if r['platform'] in COUNTS]
            if dict(Counter(r['platform'] for r in rows)) != COUNTS:
                raise ValueError('Game counts differ from reviewed plan')
            from PIL import Image
            for row in rows:
                with Image.open(row['coverPath']) as image:
                    if image.size != (480, 720) or image.format != 'JPEG':
                        raise ValueError('Compiled cover format differs')
                if row['artifact']['format'] == 'zip':
                    with zipfile.ZipFile(row['filePath']) as archive:
                        if row['artifact']['launchPath'] not in archive.namelist() or len(archive.namelist()) != row['artifact']['fileCount']:
                            raise ValueError('Disc package structure differs')
            # Move only the small covers into the existing mounted content area.
            for row in rows:
                source = Path(row['coverPath'])
                target = HOME / 'covers' / source.name
                if not target.exists():
                    shutil.copyfile(source, target)
                    target.chmod(0o640)
                    os.chown(target, 0, source_config['serviceGid'])
                    run('setfacl', '-m', 'u:turborama-station-api:r--', str(target))
                if sha(target) != sha(source):
                    raise ValueError('Immutable compiled cover differs')
                row['coverPath'] = str(target)
            save('candidate-index-private.json', index)
            progress('Pacote completo preparado', games=114, existingIdsPreserved=len(before['items']))
            deadline = time.monotonic() + 180
            empty_since = None
            while True:
                value = ready()
                if idle(value):
                    break
                if confirmed_closed and empty_closed_room(value):
                    empty_since = empty_since or time.monotonic()
                    if time.monotonic() - empty_since >= 30:
                        closed_room_cleared = True
                        break
                else:
                    empty_since = None
                if time.monotonic() > deadline:
                    raise TimeoutError('A real game is active; restart was withheld')
                progress('Conferindo ausência de conexões e dados de partida', apiChanged=False,
                         humanConfirmedExit=confirmed_closed, emptyV3Rooms=value['multiplayer']['activeRooms'])
                time.sleep(5)
            if sha(HOME / 'index.json') != baseline['indexSha256'] or run('systemctl', 'show', API, '-p', 'MainPID', '--value') != before_pid:
                raise ValueError('Production changed before publication')
            if API_DROPIN.exists() or SCAN_DROPIN.exists():
                raise ValueError('Deployment overlay already exists')
            API_DROPIN.write_text('[Service]\nBindReadOnlyPaths=' + str(STORE) + '\n')
            API_DROPIN.chmod(0o644)
            SCAN_DROPIN.write_text('[Service]\nExecStart=\nExecStart=/usr/bin/python3 ' + str(release / TOOLS[0]) +
                ' --config ' + str(CONFIG) + '\nReadWritePaths=' + str(STORE) + '\n')
            SCAN_DROPIN.chmod(0o644)
            mounted = True
            run('systemctl', 'daemon-reload')
            value = ready()
            if not idle(value) and not (closed_room_cleared and empty_closed_room(value)):
                raise ValueError('A game appeared before the bounded restart')
            run('systemctl', 'restart', API)
            deadline = time.monotonic() + 45
            while True:
                try:
                    value = ready()
                    if value['multiplayer']['approvedProfiles'] != 1816:
                        raise ValueError('Existing online profiles differ')
                    break
                except (OSError, urllib.error.URLError):
                    if time.monotonic() > deadline:
                        raise TimeoutError('Station did not become ready')
                    time.sleep(1)
            api_pid = run('systemctl', 'show', API, '-p', 'MainPID', '--value')
            namespace_check = "import json,sys;from pathlib import Path;rows=json.loads(Path(sys.argv[1]).read_text())['items'];[(open(r[k],'rb').read(8)) for r in rows for k in ('filePath','coverPath')];print(len(rows))"
            # An index copy in the currently mounted home is used for the real
            # service identity/namespace check before the public catalog changes.
            access_index = HOME / '.new-systems-access-check.json'
            library.atomic_json(access_index, index, source_config['serviceGid'])
            run('setfacl', '-m', 'u:turborama-station-api:r--', str(access_index))
            try:
                count = run('nsenter', '--target', api_pid, '--mount', '--', 'runuser', '-u', 'turborama-station-api', '--',
                            '/usr/bin/python3', '-c', namespace_check, str(access_index))
                if count != str(len(index['items'])):
                    raise ValueError('API sandbox cannot read all game/cover files')
            finally:
                access_index.unlink(missing_ok=True)
            installed = True  # Every subsequent write belongs to the rollback transaction.
            library.atomic_json(CONFIG, updated)
            library.atomic_json(HOME / 'state.json', json.loads((stage / 'state.json').read_bytes()))
            library.atomic_json(HOME / 'index.json', index, source_config['serviceGid'])
            library.atomic_json(HOME / 'report.json', report)
        progress('Catálogo publicado; conferindo leitura autenticada', revision=index['revision'], games=114)
        # An unchanged second scan proves persistence and catches accidental IDs,
        # metadata or cover churn before leaving the automatic timer enabled.
        repeat = library.publish(updated)
        if repeat['changed'] or repeat['updated'] or repeat['added'] or any(
                json.dumps(p, sort_keys=True) not in known_pending for p in repeat['pending']):
            save('repeat-report-private.json', repeat)
            raise ValueError('Full automatic scan introduced catalog changes or new pending sources')
        ops = load(SCRIPTS / 'implantar-station-20261003.py', 'content_fixture_database_ops')
        values, _ = ops.runtime()
        proof = prove_public(index, values, rows, ops)
        proof['ownedSyntheticFixtureRemoved'] = True
        save('http-proof.json', proof)
        if any(run('systemctl', 'show', name, '-p', 'MainPID', '--value') != pid for name, pid in shared.items()):
            raise ValueError('A shared service changed during publication')
        result = dict(applied=True, revision=index['revision'], newGames=114, platformCounts=COUNTS,
            totalIds=len(index['items']), totalVisible=sum(r.get('catalogVisible', True) for r in index['items']),
            preservedPreviousIds=len(before['items']), allPreviousRowsPreserved=True, sourceRomFilesPreserved=True,
            sourceWiiuCompletedFromLocalOriginal=True, scannerRelease=release.name, scannerSha256=sha(release / TOOLS[0]),
            immutableGamesOnGameVolume=True, apiDllSha256=DLL_SHA, apiPidBefore=int(before_pid),
            apiPidAfter=int(api_pid), apiRestartedOnce=True, otherServicePidsPreserved=True,
            closedEmptyV3RoomClearedWithHumanConfirmation=closed_room_cleared, reusedPreparedGames=reused,
            preexistingSourcePending=repeat['pending'], noNewSourcePending=True, missingXmlReferences=repeat['missingXmlRoms'],
            sandboxReadOnlyStore=True, apiIdentityReadsAllFiles=True, automaticSecondScanUnchanged=True,
            profilesPreserved=1816, indexSha256=sha(HOME / 'index.json'), http=proof,
            backupDirectory=str(backup), finishedUtc=datetime.now(timezone.utc).isoformat())
        save('deployment-result.json', result)
        progress('Integração concluída', revision=index['revision'], newGames=114, totalVisible=result['totalVisible'])
    except BaseException as error:
        (backup / 'error-private.txt').write_text(traceback.format_exc())
        diagnostic = PRIVATE / 'deployment-error-private.txt'
        diagnostic.write_text(traceback.format_exc())
        diagnostic.chmod(0o600)
        os.chown(diagnostic, 1000, 1000)
        if installed and library is not None:
            restored = json.loads((backup / 'index.json').read_bytes())
            restored['revision'] = max(restored['revision'], json.loads((HOME / 'index.json').read_bytes())['revision']) + 1
            library.atomic_json(HOME / 'index.json', restored, source_config['serviceGid'])
            shutil.copy2(backup / 'state.json', HOME / 'state.json')
            shutil.copy2(backup / 'report.json', HOME / 'report.json')
            shutil.copy2(backup / 'config.json', CONFIG)
        if mounted:
            API_DROPIN.unlink(missing_ok=True)
            SCAN_DROPIN.unlink(missing_ok=True)
            run('systemctl', 'daemon-reload')
            # The only restart was preceded by an empty-room check. Avoid
            # destroying a room that may have appeared after publication.
            try:
                restart_safe = idle(ready())
            except (OSError, urllib.error.URLError):
                restart_safe = True
            if restart_safe:
                run('systemctl', 'restart', API)
        save('deployment-result.json', dict(applied=False, backupDirectory=str(backup), rollbackCatalog=installed,
                                            errorType=type(error).__name__, privateDiagnosticAvailable=True))
        raise
    finally:
        if was_timer:
            run('systemctl', 'start', TIMER)


if __name__ == '__main__':
    main()
