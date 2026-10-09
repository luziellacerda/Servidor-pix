"""Enroll exact trusted engine/content/controller bindings without recompilation.

Prepared modes carry no fabricated engine hashes. Existing approvals and refusals
are preserved. Native engines must declare the controller configuration they use.
"""
import hashlib
import json
from pathlib import Path
import re
import unicodedata
from urllib.parse import urlsplit

ALIASES = {'snesbr':'snes', 'super nintendo':'snes', 'super nintendo - br':'snes',
           'megadrivebr':'megadrive', 'megadrive - br':'megadrive',
           'n64br':'n64', 'nintendo 64':'n64', 'nintendo 64 - br':'n64',
           'neo geo':'neogeo', 'neo geo cd':'neogeocd', 'playstation 1':'psx'}
CEILINGS = dict(snes=5, megadrive=2, n64=4, dreamcast=4, gamecube=4,
                wii=4, wiiu=4, switch=2, neogeo=2, neogeocd=2,
                psx=2, fbneo=2, cps1=2, cps2=2, cps3=2)
NATIVE_CONTROLLERS = dict(dreamcast='dreamcast-four-ports-v1',
    gamecube='gamecube-four-ports-v1', wii='wii-four-remotes-v1',
    wiiu='wiiu-four-controllers-v1', switch='switch-two-controllers-v1')
ARCADES = {'neogeo','fbneo','cps1','cps2','cps3'}
BINDING = ('itemId','contentSha256','engineId','coreSha256','runtimeSha256')
PROFILE_KEY = (*BINDING, 'profileId','profileSha256')
MODE_FIELDS = ('profileId','maximumPlayers','allowedPlayerCounts','approved',
               'controllerProfile','mode','modeTitle','instructions','sources')

def platform(value):
    return ALIASES.get(value.lower(), value.lower())

def hash_value(value):
    return isinstance(value,str) and re.fullmatch('[0-9a-f]{64}',value) is not None

def text(value, limit):
    return isinstance(value,str) and bool(value.strip()) and len(value.encode('utf-16-le'))//2<=limit and all(unicodedata.category(c)!='Cc' for c in value)

def encoded(value):
    return (json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False)+'\n').encode()

def profile_configuration(engine, controller, maximum):
    for definition in engine.get('controllerProfiles',[]):
        if definition['controllerProfile'] == controller:
            if maximum > definition['maximumPlayers']:return None
            config=definition['configuration']
            # Fixed field order is the public SHA-256 recipe. Native device
            # descriptors are data; the server never executes emulator code.
            return dict(schemaVersion=1,controllerProfile=controller,
                        devices=config['devices'],coreOptions=config['coreOptions'])
    system=platform(engine['platform'])
    if system in NATIVE_CONTROLLERS:return None
    if controller == 'direct-four-ports-v1' and system == 'n64':devices=[1,1,1,1]
    elif controller == 'psx-dualshock-2p-v1' and system == 'psx' and maximum <= 2:devices=[517,517]
    elif controller == 'standard-2p-v1' and maximum <= 2:devices=[1,1]
    else:return None
    return dict(schemaVersion=1,controllerProfile=controller,devices=devices,coreOptions=engine['options'])

def validate_manifest(manifest):
    if not isinstance(manifest,dict) or manifest.get('schemaVersion') != 1 or not isinstance(manifest.get('engines'),list) or len(manifest['engines'])>64 or len(encoded(manifest))>512*1024:
        raise ValueError('invalid trusted engine manifest')
    seen=set()
    for engine in manifest['engines']:
        if not isinstance(engine,dict) or not text(engine.get('engineId'),120) or engine['engineId'] in seen or platform(engine.get('platform','')) not in CEILINGS:
            raise ValueError('invalid or duplicate trusted engine identity')
        seen.add(engine['engineId'])
        if not all(hash_value(engine.get(k)) for k in ('coreSha256','runtimeSha256')) or type(engine.get('launchReady')) is not bool:
            raise ValueError('exact binary hashes required')
        extensions=engine.get('extensions')
        if not isinstance(extensions,list) or not 1<=len(extensions)<=32 or any(not isinstance(e,str) or re.fullmatch('[a-z0-9]{1,12}',e) is None for e in extensions) or len(set(extensions))!=len(extensions):
            raise ValueError('invalid engine extensions')
        if not isinstance(engine.get('options'),str) or len(engine['options'])>8192:
            raise ValueError('bounded fixed engine options required')
        maximum=engine.get('maximumPlayers',CEILINGS[platform(engine['platform'])])
        if type(maximum) is not int or not 1<=maximum<=CEILINGS[platform(engine['platform'])]:
            raise ValueError('engine exceeds platform ceiling')
        definitions=engine.get('controllerProfiles',[])
        if not isinstance(definitions,list) or len(definitions)>16:raise ValueError('bounded controller definitions required')
        controllers=set()
        for definition in definitions:
            if not isinstance(definition,dict) or set(definition)!={'controllerProfile','maximumPlayers','configuration'} or not text(definition['controllerProfile'],64) or definition['controllerProfile'] in controllers:
                raise ValueError('invalid controller identity')
            controllers.add(definition['controllerProfile'])
            capacity=definition['maximumPlayers'];config=definition['configuration']
            if type(capacity) is not int or not 1<=capacity<=maximum or not isinstance(config,dict) or set(config)!={'schemaVersion','controllerProfile','devices','coreOptions'} or type(config['schemaVersion']) is not int or config['schemaVersion']!=1 or config['controllerProfile']!=definition['controllerProfile']:
                raise ValueError('invalid native controller configuration')
            if not isinstance(config['devices'],list) or not capacity<=len(config['devices'])<=5 or not isinstance(config['coreOptions'],str) or len(encoded(config))>8192:
                raise ValueError('bounded exact native ports required')
        if platform(engine['platform']) in NATIVE_CONTROLLERS and engine['launchReady'] and engine.get('recoveryProtocol')=='station-stream.v3' and not definitions:
            raise ValueError('native engine needs explicit controller definitions')
    return manifest

def validate_modes(document):
    if document is None:return []
    if not isinstance(document,dict) or document.get('schemaVersion')!=1 or not isinstance(document.get('modes'),list) or len(document['modes'])>16000 or len(encoded(document))>8*1024*1024:
        raise ValueError('invalid prepared mode registry')
    seen=set()
    for mode in document['modes']:
        if not isinstance(mode,dict) or not text(mode.get('itemId'),80) or not hash_value(mode.get('contentSha256')) or platform(mode.get('platform','')) not in CEILINGS:
            raise ValueError('exact prepared content identity required')
        for key,limit in (('profileId',64),('controllerProfile',64),('mode',40),('modeTitle',80)):
            if not text(mode.get(key),limit):raise ValueError('invalid prepared mode text')
        key=(mode['itemId'],mode['contentSha256'],mode['profileId'])
        if key in seen:raise ValueError('duplicate prepared mode')
        seen.add(key);maximum=mode.get('maximumPlayers');counts=mode.get('allowedPlayerCounts')
        if type(maximum) is not int or not 1<=maximum<=CEILINGS[platform(mode['platform'])] or not isinstance(counts,list) or any(type(n) is not int or not 2<=n<=maximum for n in counts) or counts!=sorted(set(counts)) or (maximum==1 and counts) or (maximum>1 and (not counts or counts[-1]!=maximum)) or type(mode.get('approved')) is not bool:
            raise ValueError('invalid prepared player counts')
        if maximum==5 and mode['controllerProfile']!='snes-multitap-port2-v1':raise ValueError('five players require SNES multitap')
        for key,limit,entries in (('instructions',400,8),('sources',500,8)):
            values=mode.get(key)
            if not isinstance(values,list) or len(values)>entries or any(not text(value,limit) or key=='sources' and not value.startswith('https://') for value in values):
                raise ValueError('invalid prepared mode instructions or sources')
            if key=='sources' and any(not urlsplit(value).hostname or urlsplit(value).username is not None for value in values):raise ValueError('invalid HTTPS mode source')
    return document['modes']

def prepare(items,existing,manifest,modes=None):
    validate_manifest(manifest);prepared=validate_modes(modes)
    if not isinstance(existing,list) or len(existing)>50000:raise ValueError('invalid automatic profile input')
    output=list(existing)
    bindings={tuple(p[k] for k in BINDING) for p in existing}
    keys={tuple(p[k] for k in PROFILE_KEY) for p in existing}
    by_item={}
    for mode in prepared:by_item.setdefault(mode['itemId'],[]).append(mode)
    for item in items:
        system=platform(item['platform'])
        if system not in CEILINGS or not item.get('catalogVisible',True) or not hash_value(item.get('contentSha256')):continue
        planned=by_item.get(item['itemId'])
        if planned is not None:
            planned=[m for m in planned if m['contentSha256']==item['contentSha256'] and platform(m['platform'])==system]
            if not planned:continue # changed ROM needs a new explicit mode binding
        for engine in manifest['engines']:
            if platform(engine['platform'])!=system or not engine['launchReady'] or engine.get('recoveryProtocol')!='station-stream.v3' or Path(item['artifact']['launchPath']).suffix[1:].lower() not in engine['extensions']:continue
            binding=(item['itemId'],item['contentSha256'],engine['engineId'],engine['coreSha256'],engine['runtimeSha256'])
            if planned is None and binding in bindings:continue
            maximum=1 if item.get('metadata',{}).get('players')=='1' else 2
            controller=NATIVE_CONTROLLERS.get(system,'direct-four-ports-v1' if system=='n64' else 'standard-2p-v1')
            defaults=[dict(profileId='local-auto-2p-v1',maximumPlayers=maximum,allowedPlayerCounts=[2] if maximum==2 else [],approved=maximum==1 or system not in ARCADES,
                controllerProfile=controller,mode='solo' if maximum==1 else 'local-multiplayer',modeTitle='Modo individual' if maximum==1 else 'Multiplayer local: até dois controles',
                instructions=['Selecione o modo multijogador local do jogo. Uma campanha individual não ganha mais personagens pela sala.',
                    'A importação reconhece o arquivo; compatibilidade do jogo e desempenho nos aparelhos precisam ser conferidos.',
                    'Acima de duas pessoas exige o cadastro do modo correto. Ver detalhes não transmite gameplay.'],sources=[])]
            if system in ARCADES and maximum>1:defaults[0]['instructions']=['Novo conjunto arcade reconhecido. Falta associar o driver e o modo a este conteúdo antes da sala online.']
            for mode in planned if planned is not None else defaults:
                if mode['maximumPlayers']>engine.get('maximumPlayers',CEILINGS[system]):continue
                config=profile_configuration(engine,mode['controllerProfile'],mode['maximumPlayers'])
                if config is None:continue # engine must implement the exact port type
                record={key:engine[key] for key in ('engineId','coreSha256','runtimeSha256')}
                record.update(itemId=item['itemId'],contentSha256=item['contentSha256'],platform=system,
                    profileSha256=hashlib.sha256(encoded(config)[:-1]).hexdigest())
                record.update({key:mode[key] for key in MODE_FIELDS})
                key=tuple(record[k] for k in PROFILE_KEY)
                if key in keys:continue
                output.append(record);keys.add(key);bindings.add(binding)
    if len(output)>50000:raise ValueError('automatic profile limit exceeded')
    counts={}
    for p in output:
        counts[p['itemId']]=counts.get(p['itemId'],0)+1
        if counts[p['itemId']]>32:raise ValueError('automatic profile per-game limit exceeded')
    if len((json.dumps(output,ensure_ascii=False,indent=2)+'\n').encode())>16*1024*1024:
        raise ValueError('automatic profile registry exceeds the server byte limit')
    return output
