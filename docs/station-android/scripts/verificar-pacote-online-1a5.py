"""Check exact catalog, preserved profiles, controller hashes and compiled app inputs.

This is an offline delivery check. It does not restart services, read secrets or
claim that a ROM has been played on Android.
"""
from pathlib import Path
from collections import Counter
import argparse, hashlib, json
from urllib.parse import urlsplit
import unicodedata

OLD_RUNTIME = '351cee4540e916468a504788911e4c5fcb4fe141e3b544b1b1255d32e1d04a26'

def need(ok, message):
    if not ok:
        raise RuntimeError(message)

def read(path):
    return json.loads(Path(path).read_text())

def sha(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('bundle', 'base-profiles', 'catalog'):
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--app-root', type=Path)
    args = parser.parse_args()
    profiles = read(args.bundle/'profiles.json')
    old = read(args.base_profiles)
    catalog = read(args.catalog)
    items = catalog['items'] if isinstance(catalog, dict) else catalog
    catalog_ids = {x.get('itemId', x.get('id')) for x in items}
    support = read(args.bundle/'game-support-completo.json')
    inventory = {x['itemId']: x for x in support['items']}
    need(len(items) == len(catalog_ids) == len(inventory) == 3848 and set(inventory) == catalog_ids, 'Catalog/inventory identity drift')
    need(support['revision'] == 24, 'Unexpected catalog revision')
    need(len(old) == 1816 and profiles[:len(old)] == old, 'Published original profiles changed')
    need(len(profiles) == 3672, 'Expected 3672 exact profile/runtime bindings')
    engines = read(args.bundle/'engines-app.json')
    need(engines['maximumPlayers'] == 5, 'Five-player client engine manifest required')
    by_platform = {x['platform']: x for x in engines['engines']}
    new_runtime = by_platform['snes']['runtimeSha256']
    need(len({x['runtimeSha256'] for x in engines['engines']}) == 1, 'Runtime manifest is inconsistent')
    identities = set()
    per_item = Counter()
    five = []
    for p in profiles:
        need(p['itemId'] in inventory and p['approved'] is True, 'Profile does not identify an approved catalog item')
        item = inventory[p['itemId']]
        need(p['contentSha256'] == item['contentSha256'], 'Content identity mismatch: '+p['itemId'])
        platform = 'snes' if item['platform'] in ('snes', 'snesbr') else 'megadrive' if item['platform'] in ('megadrive', 'megadrivebr') else item['platform']
        need(platform == p['platform'], 'Platform mismatch')
        engine = by_platform[platform]
        need(p['coreSha256'] == engine['coreSha256'], 'Core changed without a corresponding manifest')
        need(p['runtimeSha256'] in (OLD_RUNTIME, new_runtime), 'Unknown runtime')
        if p['runtimeSha256'] == new_runtime:
            need(p['engineId'] == engine['engineId'], 'New runtime engine ID mismatch')
        maximum, allowed = p['maximumPlayers'], p['allowedPlayerCounts']
        need(type(maximum) is int and 1 <= maximum <= 5 and allowed == sorted(set(allowed)), 'Invalid player classification')
        need(all(type(n) is int and 2 <= n <= maximum for n in allowed), 'Invalid permitted room capacity')
        need(not allowed if maximum == 1 else maximum in allowed, 'Maximum does not match allowed counts')
        controller = p['controllerProfile']
        options = engine['options']
        if controller == 'standard-2p-v1':
            need(maximum <= 2, 'Standard pads cannot grant extra players')
            devices = [1, 1]
        elif controller == 'snes-multitap-port2-v1':
            need(platform == 'snes', 'SNES Multitap on the wrong platform')
            devices = [1, 257, 1, 1, 1]
        elif controller == 'megadrive-sega-teamplayer-v1':
            need(platform == 'megadrive' and maximum <= 4, 'Unsupported Sega count')
            devices, options = [1]*8, 'clownmdemu_input_protocol = "sega"\n'
        elif controller == 'megadrive-ea-4way-v1':
            need(platform == 'megadrive' and maximum <= 4, 'Unsupported EA count')
            devices, options = [1]*4, 'clownmdemu_input_protocol = "ea"\n'
        else:
            raise RuntimeError('Unknown controller: '+controller)
        canonical = json.dumps(dict(schemaVersion=1, controllerProfile=controller, devices=devices, coreOptions=options), ensure_ascii=False, separators=(',', ':'))
        need(hashlib.sha256(canonical.encode()).hexdigest() == p['profileSha256'], 'Controller configuration hash mismatch')
        identity = tuple(p[k] for k in ('itemId', 'contentSha256', 'engineId', 'coreSha256', 'runtimeSha256', 'profileId', 'profileSha256'))
        need(identity not in identities, 'Ambiguous complete profile identity')
        identities.add(identity)
        per_item[p['itemId']] += 1
        if maximum == 5:
            need(platform == 'snes' and controller == 'snes-multitap-port2-v1' and p['runtimeSha256'] == new_runtime, 'Unsupported five-player controller/runtime')
            need(p.get('modeTitle') and p.get('instructions') and p.get('sources'), 'Five-player mode help is missing')
            five.append(p)
        title=p.get('modeTitle')
        if title is not None:
            need(isinstance(title,str) and 0<len(title.encode('utf-16-le'))//2<=80 and not any(unicodedata.category(c)=='Cc' for c in title), 'Invalid mode title')
        for key,bound in (('instructions',500),('sources',512)):
            lines=p.get(key)
            if lines is not None:
                need(isinstance(lines,list) and len(lines)<=8 and all(isinstance(s,str) and s.strip() and len(s.encode('utf-16-le'))//2<=bound and not any(unicodedata.category(c)=='Cc' for c in s) for s in lines), 'Invalid bounded mode help: '+key)
        for value in p.get('sources') or []:
            url = urlsplit(value)
            need(url.scheme == 'https' and url.hostname and not url.username and not url.password, 'Invalid help source')
    need(max(per_item.values()) <= 32, 'Per-game response limit exceeded')
    need(len(five) == 2 and {p['itemId'] for p in five} == {'station_46fe7356ab5cc63a1438f720b9c7ce2d'}, 'Unexpected five-player games in this delivery')
    for iid,expected in (
        ('station_6e4c23a9931b4c2cf3449662605f43d7',{'normal':2,'battle-single':4}),
        ('station_df50d575815ab105084a79d68e0c8fb3',{'normal':1,'battle-single':4,'battle-team':4}),
        ('station_80b7594ba22922d87fb43d720ba8be5a',{'normal':1,'battle-single':4,'battle-team':4}),
        ('station_46fe7356ab5cc63a1438f720b9c7ce2d',{'normal':2,'battle-single':5,'battle-team':5})):
        exact={p['mode']:p for p in profiles if p['itemId']==iid and p['runtimeSha256']==new_runtime}
        for mode,maximum in expected.items():
            need(mode in exact and exact[mode]['maximumPlayers']==maximum and exact[mode].get('instructions') and exact[mode].get('sources'), 'Bomberman campaign/battle classification drift')
    for item_id, item in inventory.items():
        available = [p for p in profiles if p['itemId'] == item_id and p['runtimeSha256'] == new_runtime]
        need(item['maximumOnlinePlayers'] == max((p['maximumPlayers'] for p in available), default=0), 'Inventory player count mismatch')
        need({m['profileId'] for m in item['modes']} == {p['profileId'] for p in available}, 'Inventory modes are incomplete')
    if args.app_root:
        app = args.app_root
        need(sha(app/'assets/station-online/engines.json') == sha(args.bundle/'engines-app.json'), 'Client and server engine manifest differ')
        native = read(app/'evidence/native-build.json')
        java = read(app/'evidence/java-build.json')
        need(native['runtimeSHA256'] == new_runtime and sha(app/'native/runtime/libstation_retroarch.so') == new_runtime, 'Compiled runtime drift')
        need(sha(app/'native/retroarch-corresponding-source.tar.gz') == native['correspondingSourceArchiveSHA256'], 'Corresponding GPL source drift')
        need(sha(app/'native/CORRESPONDING-SOURCE-MANIFEST.json') == native['sourceManifestSHA256'], 'Native source manifest drift')
        need(len(java['sourceHashes']) == 209, 'Incomplete Java source set')
        for name, digest in java['sourceHashes'].items():
            need(sha(app/'java'/name) == digest, 'Java source drift: '+name)
        for module in ('client', 'rooms'):
            need(sha(app/'compiled'/(module+'.dex')) == java[module+'DexSHA256'], 'Compiled DEX drift')
        delivery=read(app/'DELIVERY-MANIFEST.json')['files']
        files={p.relative_to(app).as_posix() for p in app.rglob('*') if p.is_file() and p.name!='DELIVERY-MANIFEST.json'}
        need(files==set(delivery),'App delivery file set drift')
        for name,digest in delivery.items():
            need(sha(app/name)==digest,'App delivery hash drift: '+name)
    review=read(args.bundle/'collection-review-completo.json')
    need({x['itemId'] for x in review['items']}==catalog_ids and len(review['items'])==len(items),'Complete collection review drift')
    print(json.dumps(dict(passed=True, catalogItems=len(items), preservedProfiles=len(old), profiles=len(profiles), fivePlayerProfiles=len(five), fivePlayerGames=1, maximumProfilesPerGame=max(per_item.values()), appInputsVerified=bool(args.app_root), physicalAndroidGameplayVerified=False)))

if __name__ == '__main__':
    main()
