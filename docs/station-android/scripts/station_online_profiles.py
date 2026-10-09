"""Declarative online enrollment after a stable offline library publication."""
import hashlib, json
from pathlib import Path

def platform(value):
    return {'snesbr':'snes','megadrivebr':'megadrive','n64br':'n64'}.get(value.lower(),value.lower())

def prepare(items,existing,manifest):
    if not isinstance(existing,list) or len(existing)>50000 or manifest.get('schemaVersion')!=1:raise ValueError('invalid automatic profile input')
    output=list(existing)
    bindings={tuple(p[k] for k in ('itemId','contentSha256','engineId','coreSha256','runtimeSha256')) for p in existing}
    by_platform={}
    for engine in manifest['engines']:
        if engine.get('launchReady') and engine.get('recoveryProtocol')=='station-stream.v3':by_platform.setdefault(engine['platform'],[]).append(engine)
    for item in items:
        system=platform(item['platform']);engines=by_platform.get(system,[])
        if len(engines)!=1 or not item.get('catalogVisible',True) or not item.get('contentSha256'):continue
        engine=engines[0]
        if Path(item['artifact']['launchPath']).suffix[1:].lower() not in engine['extensions']:continue
        binding=(item['itemId'],item['contentSha256'],engine['engineId'],engine['coreSha256'],engine['runtimeSha256'])
        if binding in bindings:continue
        # A new game is recognized without recompiling the server or app. More
        # than two players still needs a reviewed explicit mode/controller row.
        maximum=1 if item.get('metadata',{}).get('players')=='1' else 2
        controller='direct-four-ports-v1' if system=='n64' else 'standard-2p-v1'
        devices=[1,1,1,1] if system=='n64' else [1,1]
        profile=dict(schemaVersion=1,controllerProfile=controller,devices=devices,coreOptions=engine['options'])
        record={key:engine[key] for key in ('engineId','coreSha256','runtimeSha256')}
        record.update(itemId=item['itemId'],contentSha256=item['contentSha256'],platform=system,profileId='local-auto-2p-v1',profileSha256=hashlib.sha256(json.dumps(profile,ensure_ascii=False,separators=(',',':')).encode()).hexdigest(),
            maximumPlayers=maximum,allowedPlayerCounts=[2] if maximum==2 else [],approved=maximum==1 or system in ('snes','megadrive','n64','neogeocd','psx'),
            controllerProfile=controller,mode='solo' if maximum==1 else 'local-multiplayer',modeTitle='Modo individual' if maximum==1 else 'Multiplayer local: até dois controles',
            instructions=['Selecione o modo multijogador local do jogo. Uma campanha individual não ganha mais personagens pela sala.',
                'A importação reconhece o arquivo; compatibilidade do jogo e desempenho nos aparelhos precisam ser conferidos.',
                'Acima de duas pessoas exige o cadastro do modo correto. Ver detalhes não transmite gameplay.'],sources=[])
        # Unknown arcade sets need an exact supported driver. The reviewed
        # initial collection already carries these approvals in existing rows.
        if system in ('neogeo','fbneo','cps1','cps2','cps3') and maximum>1:
            record['instructions']=['Novo conjunto arcade reconhecido. Falta associar o driver e o modo a este conteúdo antes da sala online.']
        output.append(record);bindings.add(binding)
    if len(output)>50000:raise ValueError('automatic profile limit exceeded')
    counts={}
    for p in output:
        counts[p['itemId']]=counts.get(p['itemId'],0)+1
        if counts[p['itemId']]>32:raise ValueError('automatic profile per-game limit exceeded')
    if len((json.dumps(output,ensure_ascii=False,indent=2)+'\n').encode())>16*1024*1024:
        raise ValueError('automatic profile registry exceeds the server byte limit')
    return output
