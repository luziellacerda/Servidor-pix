"""Pure configuration transforms; no service, filesystem or database operations."""
import re

ORIGIN_INCLUDE='include /etc/nginx/snippets/turborama-station-origin-guard.conf;'
PROOF_HEADER='proxy_set_header X-Station-Request-Proof $http_x_station_request_proof;'
ORIGIN_MAP='map $server_addr $station_tunnel_origin {\n    default 0;\n    127.0.0.1 1;\n    ::1 1;\n}\n'
ORIGIN_GUARD='if ($station_tunnel_origin = 0) { return 404; }\n'

def station_locations(text,authentication=False):
    if ORIGIN_INCLUDE in text or PROOF_HEADER in text:raise ValueError('Station proxy already modified')
    matches=list(re.finditer(r'(?m)^\s*location\s+([^\n]+)\{\s*$',text));edits=[];generic=None
    for match in matches:
        target=match.group(1).strip()
        if '/v1/station/' not in target:raise ValueError('Unrelated location in Station snippet')
        start=match.end();depth=1;end=start
        while end<len(text) and depth:
            if text[end]=='{':depth+=1
            elif text[end]=='}':depth-=1
            end+=1
        if depth:raise ValueError('Unbalanced Station location')
        body=text[start:end-1]
        if 'proxy_pass http://127.0.0.1:5192;' not in body or 'client_max_body_size 8k;' not in body:
            raise ValueError('Unexpected Station upstream/body limit')
        updated='\n    '+ORIGIN_INCLUDE+'\n    '+PROOF_HEADER+'\n'+body.lstrip('\n')
        edits.append((start,end-1,updated))
        if target=='^~ /v1/station/':generic=updated
    if not matches:raise ValueError('No Station locations')
    for start,end,body in reversed(edits):text=text[:start]+body+text[end:]
    if authentication:
        if generic is None or generic.count('client_max_body_size 8k;')!=1:raise ValueError('Generic Station location is missing')
        body=generic.replace('client_max_body_size 8k;','client_max_body_size 64k;')
        for route in ['activations/complete','sessions']:
            text+='\nlocation = /v1/station/'+route+' {'+body+'}\n'
    return text

def samba_guest(text,path):
    sections=list(re.finditer(r'(?m)^\s*\[([^]\n]+)\]\s*$',text));found=[]
    for i,match in enumerate(sections):
        end=sections[i+1].start() if i+1<len(sections) else len(text)
        body=text[match.end():end]
        configured=re.search(r'(?mi)^\s*path\s*=\s*(.+?)\s*$',body)
        if configured and configured.group(1).strip('\"\'')==path:found.append((match.group(1),match.end(),end,body))
    if len(found)!=1:raise ValueError('Exact obsolete share is required')
    name,start,end,body=found[0]
    guest=re.compile(r'(?mi)^(\s*(?:guest ok|public)\s*=\s*)(yes|no|true|false)\s*$')
    if not guest.search(body):raise ValueError('Guest share setting not found')
    body=guest.sub(lambda m:m.group(1)+'no',body)
    return text[:start]+body+text[end:],name

def sandbox_properties(target,identity,roots):
    for path in [target,*roots]:
        if not re.fullmatch(r'/(?:opt|mnt/DADOS)/[a-zA-Z0-9_./-]+',str(path)):
            raise ValueError('Unexpected sandbox mount')
    return {
        'User':identity,'Group':identity,'SupplementaryGroups':'',
        'NoNewPrivileges':'yes','ProtectSystem':'strict','ProtectHome':'yes','PrivateTmp':'yes',
        'PrivateDevices':'yes','ProtectKernelTunables':'yes','ProtectKernelModules':'yes',
        'ProtectKernelLogs':'yes','ProtectControlGroups':'yes','ProtectClock':'yes','ProtectHostname':'yes',
        'ProtectProc':'invisible','RestrictSUIDSGID':'yes','RestrictNamespaces':'yes',
        'RestrictRealtime':'yes','LockPersonality':'yes','RemoveIPC':'yes','UMask':'0077',
        'LimitCORE':'0','CoredumpFilter':'0x0',
        'CapabilityBoundingSet':'','AmbientCapabilities':'',
        'RestrictAddressFamilies':'AF_UNIX AF_INET AF_INET6 AF_NETLINK',
        'TemporaryFileSystem':'/mnt:ro /media:ro /opt:ro',
        'BindReadOnlyPaths':' '.join(map(str,[target,*roots])),
        'InaccessiblePaths':' '.join('-'+p for p in ['/etc/turborama-suite','/etc/cloudflared',
            '/etc/nginx','/etc/ssh','/etc/samba','/var/lib/postgresql','/var/lib/mysql','/var/lib/redis','/var/www'])}

def systemd_override(target,identity,roots,environment):
    properties=sandbox_properties(target,identity,roots)
    lines=['[Service]','WorkingDirectory='+str(target),'ExecStart=',
        'ExecStart=/usr/bin/dotnet '+str(target)+'/TurboRamaSuiteOnlineServer.dll',
        'Environment=','EnvironmentFile=','EnvironmentFile='+str(environment)]
    # Reset list properties before applying our allowlist, including historical overrides.
    for key,value in properties.items():
        if key in ['TemporaryFileSystem','BindReadOnlyPaths','InaccessiblePaths','RestrictAddressFamilies']:
            lines.append(key+'=')
        lines.append(key+'='+value)
    return '\n'.join(lines)+'\n'
