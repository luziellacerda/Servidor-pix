"""Exercise signed session concurrency only inside a disposable PostgreSQL cluster."""
import hashlib
import importlib.util
import os
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('station_smoke',Path(__file__).with_name('station_http_smoke.py'))
fixture=importlib.util.module_from_spec(spec);spec.loader.exec_module(fixture)
assert os.environ.get('PG_CLUSTER_CONF_ROOT','').startswith('/tmp/pg_virtualenv.')
assert fixture.sql_value("SHOW data_directory").startswith('/tmp/pg_virtualenv.')
roles=('turborama-suite','turborama-suite-admin','turborama-suite-content-admin',
    'turborama-suite-content-api','turborama-suite-content-maintenance','turborama-suite-content-monitor',
    'turborama-suite-gateway','turborama-suite-publisher')
for role in roles:fixture.sql('CREATE ROLE "'+role+'" LOGIN')
fixture.sql('ALTER ROLE "turborama-suite" PASSWORD \'fixture-only-password\'')
for migration in sorted((ROOT/'migrations/suite').glob('*.up.sql')):
    subprocess.run(['psql','-v','ON_ERROR_STOP=1','-v','migration_sha256='+hashlib.sha256(migration.read_bytes()).hexdigest(),
        '-q','-f',str(migration)],check=True,stdout=subprocess.DEVNULL)
env=dict(os.environ)
base='Host=127.0.0.1;Port='+env['PGPORT']+';Database='+env.get('PGDATABASE','postgres')+';'
env['STATION_TEST_PG']=base+'Username='+env['PGUSER']+';Password='+env['PGPASSWORD']+';'
env['STATION_TEST_PG_API']=base+'Username=turborama-suite;Password=fixture-only-password;'
env['STATION_SESSION_LOAD_TEST']='1'
subprocess.run(['dotnet','run','--project','tests/TurboRamaSuiteOnlineServer.Tests','-c','Release'],cwd=ROOT,env=env,check=True)
