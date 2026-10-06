"""Verify rollback scopes and successor guards on disposable files, without root."""
import hashlib,importlib.util,json,tempfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('registration_deployment',ROOT/'ops/station-admin/deploy-registration.py')
deploy=importlib.util.module_from_spec(spec);spec.loader.exec_module(deploy)
with tempfile.TemporaryDirectory(prefix='station-registration-rollback-') as tmp:
    directory=Path(tmp);site=directory/'site';site.mkdir();backup=directory/'backup';backup.mkdir();(backup/'site').mkdir()
    dropins={unit:directory/(unit+'.conf') for unit in deploy.DROPINS}
    saved={'site':str(site),'files':{},'dropinSha':{}}
    for name in deploy.NAMES:
        before=None if name=='station-registration.php' else ('old '+name+'\n').encode()
        after=('new '+name+'\n').encode();(site/name).write_bytes(after)
        if before:(backup/'site'/name).write_bytes(before)
        saved['files'][name]={'before':hashlib.sha256(before).hexdigest() if before else None,
            'after':hashlib.sha256(after).hexdigest(),'uid':(site/name).stat().st_uid,'gid':(site/name).stat().st_gid,'mode':0o600}
    for unit,path in dropins.items():path.write_text('own override');saved['dropinSha'][unit]=deploy.sha(path)
    (site/'private-ledger.sqlite').write_bytes(b'preserve active entitlements and receipts')
    (backup/'state.json').write_text(json.dumps(saved));calls=[]
    deploy.SITE=site;deploy.DROPINS=dropins;deploy.ops.run=lambda args,**kw:calls.append(args) or ''
    deploy.ops.health=lambda:None;deploy.guard=lambda state:None
    deploy.private=lambda path,value:path.write_text(json.dumps(value))
    (site/'station-admin.js').write_text('later release')
    try:deploy.rollback(backup)
    except ValueError:pass
    else:raise AssertionError('rollback accepted a successor')
    assert not calls and all(path.exists() for path in dropins.values())
    assert (site/'station-admin.php').read_text().startswith('new ')
    (site/'station-admin.js').write_text('new station-admin.js\n')
    deploy.rollback(backup)
    assert not (site/'station-registration.php').exists()
    assert all(deploy.current(site/name)==entry['before'] for name,entry in saved['files'].items())
    assert (site/'private-ledger.sqlite').read_bytes()==b'preserve active entitlements and receipts'
    assert calls==[['systemctl','daemon-reload'],['systemctl','restart',deploy.SERVICE],['systemctl','restart',deploy.HELPER]]
print('STATION REGISTRATION ROLLBACK: OK (successor refused before any mutation, exact UI restore, two private services only, database/receipts preserved)')
