"""Private qualification transport; DNS over verified HTTPS, normal TLS/SNI.

The ISP resolver returned EAI_AGAIN between otherwise valid test batches.
Only these test connections use this resolver; no host or service config changes.
"""
import datetime, http.client, ipaddress, json, os, socket, ssl, threading, time
import urllib.parse, urllib.request
from pathlib import Path

HOST='app.lzgames.com.br'
ALLOWED={HOST,'downloads-station.lzgames.com.br'}
W=Path(__file__).resolve().parent
lock=threading.RLock()
cache={}

def addresses(host):
    assert host in ALLOWED
    with lock:
        previous=cache.get(host)
        if previous and previous[0]>time.monotonic():return previous[1]
        url='https://1.1.1.1/dns-query?'+urllib.parse.urlencode(dict(name=host,type='A'))
        opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),
            urllib.request.HTTPSHandler(context=ssl.create_default_context()))
        with opener.open(urllib.request.Request(url,headers={'Accept':'application/dns-json'}),timeout=12) as response:
            assert response.status==200
            result=json.load(response)
        assert result.get('Status')==0
        rows=[row for row in result.get('Answer',[]) if row.get('type')==1]
        answer=tuple(dict.fromkeys(row['data'] for row in rows))
        assert answer and len(answer)<=8 and all(ipaddress.ip_address(ip).is_global for ip in answer)
        ttl=max(1,min([row.get('TTL',30) for row in rows]+[120]))
        cache[host]=(time.monotonic()+ttl,answer)
        record=dict(host=host,addresses=answer,ttl=ttl,utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
            resolver='https://1.1.1.1/dns-query',resolverTlsVerified=True,hostConfigurationChanged=False)
        p=W/('dns-observed-'+str(time.time_ns())+'.json')
        p.write_text(json.dumps(record,indent=2)+'\n');p.chmod(0o600)
        if os.geteuid()==0:os.chown(p,1000,1000)
        return answer

def open_socket(host,port=443,timeout=12):
    error=None
    for address in addresses(host):
        try:return socket.create_connection((address,port),timeout=timeout)
        except OSError as failure:error=failure
    raise error

class OriginConnection(http.client.HTTPSConnection):
    def connect(self):
        if self.host!=HOST or self._tunnel_host:return super().connect()
        raw=open_socket(HOST,self.port,min(self.timeout,12))
        try:self.sock=self._context.wrap_socket(raw,server_hostname=self.host)
        except BaseException:raw.close();raise

class OriginHandler(urllib.request.HTTPSHandler):
    def https_open(self,req):return self.do_open(OriginConnection,req,context=self._context)

class RejectRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None

def install(probe):
    if probe.address!='https://'+HOST:return
    probe.client.opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),OriginHandler(),RejectRedirect())
