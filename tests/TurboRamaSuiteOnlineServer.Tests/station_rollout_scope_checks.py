"""Guarded temporary PostgreSQL check of actual rollout role limits and grants."""
from pathlib import Path
import importlib.util,os,sys,subprocess,hashlib
root=Path(__file__).resolve().parents[2]
if not os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.'):
    raise SystemExit('Temporary PostgreSQL required')
def sql(text):
    return subprocess.check_output(['psql','-X','-qAt','-v','ON_ERROR_STOP=1','-c',text],text=True).strip()
if not sql('SHOW data_directory').startswith('/tmp/pg_virtualenv.'):
    raise SystemExit('Production database refused')
for role in ('turborama-suite','turborama-suite-admin','turborama-suite-content-admin',
    'turborama-suite-content-api','turborama-suite-content-maintenance','turborama-suite-content-monitor',
    'turborama-suite-gateway','turborama-suite-publisher'):
    sql('CREATE ROLE "'+role+'" LOGIN')
for migration in sorted((root/'migrations/suite').glob('*.up.sql')):
    subprocess.run(['psql','-X','-q','-v','ON_ERROR_STOP=1','-v','migration_sha256='+hashlib.sha256(migration.read_bytes()).hexdigest(),'-f',str(migration)],check=True,stdout=subprocess.DEVNULL)
scripts=root/'docs/station-android/scripts';sys.path.insert(0,str(scripts))
spec=importlib.util.spec_from_file_location('deployment',scripts/'implantar-seguranca-station-20261007.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.ops.sql=lambda db,text:sql(text)+'\n'
assert m.scalar('postgres','SELECT 0')=='0'
assert m.pool_settings('Host=127.0.0.1;Maximum Pool Size=12;Minimum Pool Size=2')==(['Maximum Pool Size=12','Minimum Pool Size=2'],24)
assert m.database_scope('postgres')['rawSharedTablesDenied']==6
sql('GRANT SELECT ON suite.suite_licenses TO "turborama-station-api"')
try:m.database_scope('postgres')
except ValueError:pass
else:raise AssertionError('Excess shared privilege accepted')
print('PASS actual PostgreSQL scope, excess-grant rejection, newline-normalization and preserved pool sizing')
