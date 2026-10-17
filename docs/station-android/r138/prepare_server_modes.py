"""Preserve existing approved edition bindings; unknown SNES remains unapproved."""
import argparse, copy, hashlib, importlib.util, json, sys
from pathlib import Path

BASE_RUNTIME='81b3daa38fb9c051e1c83646b87df7120f84f31ed8b48fe6b0b362b617b847dc'
HANDOFF='https://github.com/luziellacerda/Servidor-pix/blob/270027cdcf00846823489eea7f539a27892800cd/docs/station-android/r138/README.md'
def read(path): return json.loads(Path(path).read_bytes())
def sha(value): return hashlib.sha256(json.dumps(value,ensure_ascii=False,separators=(',',':')).encode()).hexdigest()
def scoped_id(value,engine):
    suffix='-owner-'+hashlib.sha256(engine.encode()).hexdigest()[:12]
    while len((value+suffix).encode('utf-16-le'))//2>64:value=value[:-1]
    return value+suffix
def build(module,catalog,profiles,current,modes,incoming):
    module.validate_manifest(current);module.validate_manifest(incoming);module.validate_modes(modes)
    rows={r['itemId']:r for r in catalog['items'] if r.get('catalogVisible',True)}
    old_engines={e['engineId']:e for e in current['engines']}
    output=copy.deepcopy(modes);keys={(m['itemId'],m['contentSha256'],m['profileId']):m for m in output['modes']}
    added=[];skipped=[]
    for engine in incoming['engines']:
        if engine['platform'] in ('psx','n64','gamecube','wii') or not engine['launchReady']:
            raise ValueError('R138 enrollment is limited to eight ready RetroArch engines')
        for profile in profiles:
            source=old_engines.get(profile['engineId']);row=rows.get(profile['itemId'])
            if source is None or source['runtimeSha256']!=BASE_RUNTIME or row is None:continue
            if source['platform']!=engine['platform'] or source['coreSha256']!=engine['coreSha256']:continue
            if profile['coreSha256']!=engine['coreSha256'] or profile['contentSha256']!=row['contentSha256']:continue
            if module.platform(row['platform'])!=engine['platform']:continue
            if Path(row['artifact']['launchPath']).suffix[1:].lower() not in engine['extensions']:continue
            controller=profile['controllerProfile'];maximum=profile['maximumPlayers']
            original=module.profile_configuration(source,controller,maximum)
            target=module.profile_configuration(engine,controller,maximum)
            # Historical SNES ledgers predate native controller declarations.
            # Accept only the exact documented four/five-port canonical hashes;
            # the new binding always uses R138's declared five-port configuration.
            if engine['platform']=='snes' and controller=='snes-multitap-port2-v1' and (original is None or sha(original)!=profile['profileSha256']):
                original=None
                for devices in ([1,257,1,1],[1,257,1,1,1]):
                    value=dict(schemaVersion=1,controllerProfile=controller,devices=devices,coreOptions=source['options'])
                    if sha(value)==profile['profileSha256']:original=value;break
            if original is None or target is None or sha(original)!=profile['profileSha256']:
                skipped.append(dict(itemId=row['itemId'],engineId=engine['engineId'],reason='exact-controller-binding-not-preserved'));continue
            evidence=profile.get('sources',[])
            if engine['platform']=='snes':
                name=row['name'].casefold()
                kart='super mario kart' in name
                gear='top gear 3000' in name
                # Copy only the already approved, exact current content/core/port
                # binding. Player metadata never creates or enlarges an approval.
                if profile['approved'] is not True:continue
                if kart and (controller!='standard-2p-v1' or profile['allowedPlayerCounts']!=[2]):
                    raise ValueError('Super Mario Kart requires exactly two independent ports')
                if gear and (profile['mode'] not in ('vs-race','championship-2p') or maximum>4):continue
                if maximum>2 and controller!='snes-multitap-port2-v1':raise ValueError('Explicit SNES multitap required')
            elif source['options']!=engine['options'] or original!=target:
                raise ValueError('Non-SNES R138 configuration must remain identical')
            mode={k:copy.deepcopy(profile[k]) for k in ('itemId','contentSha256','platform','maximumPlayers','allowedPlayerCounts','approved','controllerProfile','mode')}
            mode.update(profileId=scoped_id(profile['profileId'],engine['engineId']),engineIds=[engine['engineId']],
                        modeTitle=profile.get('modeTitle','Modo individual' if maximum==1 else 'Dois jogadores'),
                        instructions=copy.deepcopy(profile.get('instructions',[])),sources=copy.deepcopy(evidence))
            if engine['platform']=='snes' and not evidence:mode['sources']=[HANDOFF]
            module.validate_modes(dict(schemaVersion=1,modes=[mode]))
            key=(mode['itemId'],mode['contentSha256'],mode['profileId'])
            if key in keys:
                if keys[key]!=mode:raise ValueError('Scoped mode identity collision')
                continue
            keys[key]=mode;output['modes'].append(mode)
            added.append(dict(itemId=row['itemId'],name=row['name'],engineId=engine['engineId'],platform=engine['platform'],
                              mode=mode['mode'],controllerProfile=controller,profileId=mode['profileId'],profileSha256=sha(target),
                              allowedPlayerCounts=mode['allowedPlayerCounts'],approved=mode['approved']))
    module.validate_modes(output)
    if output['modes'][:len(modes['modes'])]!=modes['modes']:raise ValueError('Historical modes changed')
    preview=module.prepare(catalog['items'],profiles,incoming,output)
    if preview[:len(profiles)]!=profiles:raise ValueError('Historical profiles changed')
    known={p['engineId'] for p in incoming['engines']}
    for p in preview[len(profiles):]:
        if p['engineId'] not in known:raise ValueError('Enrollment escaped incoming engines')
    return output,dict(schemaVersion=1,addedModes=added,skipped=skipped,profilesAdded=len(preview)-len(profiles),
                      previousProfilesPreserved=len(profiles),previousModesPreserved=len(modes['modes']),
                      unknownSnesNotAutomaticallyApproved=True,ps3Ps4Offline=True,gameplayValidated=False)
def main():
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('catalog','profiles','current','modes','incoming','profiles-module','output','audit'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();spec=importlib.util.spec_from_file_location('trusted_profiles',a.profiles_module);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
    out,audit=build(m,read(a.catalog),read(a.profiles),read(a.current),read(a.modes),read(a.incoming))
    for path,value in ((a.output,out),(a.audit,audit)):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:v for k,v in audit.items() if k not in ('addedModes','skipped')}))
if __name__=='__main__':main()
