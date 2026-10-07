"""Protect unrelated routes/shares and the JIT while preparing the isolated host."""
import importlib.util
from pathlib import Path
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('config',root/'docs/station-android/scripts/station_hardening_config.py')
config=importlib.util.module_from_spec(spec);spec.loader.exec_module(config)
sample='''include /etc/nginx/snippets/turborama-station-online.locations.conf;
location ^~ /v1/station/artifacts/ {
    client_max_body_size 8k;
    proxy_pass http://127.0.0.1:5192;
    proxy_buffering off;
    proxy_read_timeout 900s;
}
location ^~ /v1/station/ {
    client_max_body_size 8k;
    proxy_pass http://127.0.0.1:5192;
    proxy_set_header Authorization $http_authorization;
}
'''
updated=config.station_locations(sample,True)
assert updated.count(config.ORIGIN_INCLUDE)==4 and updated.count(config.PROOF_HEADER)==4
assert updated.count('client_max_body_size 64k;')==2 and updated.count('client_max_body_size 8k;')==2
assert 'proxy_buffering off;' in updated and 'proxy_read_timeout 900s;' in updated
assert updated.count('location = /v1/station/activations/complete')==1 and updated.count('location = /v1/station/sessions')==1
try:config.station_locations(updated);raise AssertionError('Repeated mutation accepted')
except ValueError:pass
samba='[global]\nmap to guest = Bad User\n[old]\npath = /home/lz-servidor/Imagens/ROMS PS1\nread only = no\nguest ok = yes\n[another]\npath=/mnt/other\nguest ok=yes\n'
changed,name=config.samba_guest(samba,'/home/lz-servidor/Imagens/ROMS PS1')
assert name=='old' and '[another]\npath=/mnt/other\nguest ok=yes\n' in changed and 'read only = no' in changed
assert '[global]\nmap to guest = Bad User' in changed and 'guest ok = no' in changed
unit=config.systemd_override('/opt/turborama-station-security-test','turborama-station-api',
    ['/mnt/DADOS/turbostation-library-auto-20261004'],'/etc/turborama-station-api-security-20261007/station.env')
for expected in ['ProtectHome=yes','ProtectSystem=strict','PrivateTmp=yes','SupplementaryGroups=\n',
                 'Environment=\nEnvironmentFile=\n','CapabilityBoundingSet=\n','TemporaryFileSystem=/mnt:ro /media:ro /opt:ro']:
    assert expected in unit
assert 'MemoryDenyWriteExecute' not in unit and 'SystemCallFilter' not in unit and 'ProcSubset=pid' not in unit
print('PASS scoped origin/authentication limits, proof forwarding, unchanged transfer policy, targeted Samba transform and JIT-compatible sandbox generation')
