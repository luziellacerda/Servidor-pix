#!/usr/bin/env python3
"""Test real Linux queues in two disposable network namespaces, never the host link."""
import argparse
import json
import os
import pathlib
import subprocess
import tempfile
import time

import policy

RECEIVER = r'''
import socket,threading,time,json,struct,statistics
def serve(port):
 listener=socket.socket();listener.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1)
 listener.bind(('0.0.0.0',port));listener.listen()
 while True:
  connection,_=listener.accept()
  threading.Thread(target=handle,args=(connection,port),daemon=True).start()
def handle(connection,port):
 count=0;start=None;last=None;latency=[];pending=b''
 with connection:
  while True:
   data=connection.recv(65536)
   if not data:break
   now=time.monotonic();start=start or now;last=now;count+=len(data)
   if port==15002:
    pending+=data
    while len(pending)>=8:
     sent=struct.unpack('!d',pending[:8])[0];pending=pending[8:];latency.append((now-sent)*1000)
  result={'bytes':count,'seconds':max((last or time.monotonic())-(start or time.monotonic()),.001)}
  if latency:
   latency.sort();result['latencyP95Millis']=latency[int(.95*(len(latency)-1))];result['frames']=len(latency)
  connection.sendall(json.dumps(result).encode()+b'\n')
for port in [15001,15002,15003,15004,15005,15006,15008]:threading.Thread(target=serve,args=(port,),daemon=True).start()
print('READY',flush=True)
threading.Event().wait()
'''

SENDER = r'''
import socket,threading,time,json,struct,sys,pathlib
sys.path.insert(0,sys.argv[1]);from priority import Registry,peer
case=sys.argv[2];registry=Registry(0);result={};lock=threading.Lock()
def transfer(name,address,port,game=False,sparse=False):
 sock=socket.socket();sock.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1);sock.setsockopt(socket.IPPROTO_TCP,socket.TCP_NODELAY,1)
 sock.setsockopt(socket.SOL_SOCKET,socket.SO_SNDBUF,65536);sock.bind(('10.203.1.1',443));sock.connect((address,port));sock.settimeout(5)
 if game:
  assert registry.add(peer(address,port)), 'game_peer_not_registered'
  assert not registry.add(peer('192.0.2.3',port)), 'foreign_peer_registered'
 begin=time.monotonic();block=b'x'*16384
 while time.monotonic()-begin<4:
  sock.sendall(struct.pack('!d',time.monotonic()) if sparse else block)
  if sparse:time.sleep(.01)
 sock.shutdown(socket.SHUT_WR);payload=b''
 while b'\n' not in payload:payload+=sock.recv(4096)
 sock.close();row=json.loads(payload);row['Mbps']=round(row['bytes']*8/row['seconds']/1e6,3)
 with lock:result[name]=row
threads=[]
if case=='idle':targets=[('bulk','192.0.2.2',15001,False,False)]
elif case=='sparse':targets=[('bulk','192.0.2.2',15004,False,False),('game','192.0.2.2',15002,True,True)]
elif case=='busy':targets=[('bulk','192.0.2.2',15006,False,False),('game','192.0.2.2',15005,True,False)]
elif case=='lan':targets=[('bulk','10.203.1.2',15008,False,False)]
for target in targets:
 t=threading.Thread(target=transfer,args=target);t.start();threads.append(t)
for t in threads:t.join()
assert len(result)==len(targets),'sender_failed'
print(json.dumps(result),flush=True)
'''


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    if os.geteuid() != 0:
        raise SystemExit("isolated_kernel_test_requires_administrative_authentication")
    token = str(os.getpid())
    left, right = "stq-left-" + token, "stq-right-" + token
    left_dev, right_dev = "stq" + token + "l", "stq" + token + "r"
    receiver = None
    left_created = right_created = link_created = False
    result = {}
    config = {"interface": left_dev, "linkMbps": 40, "wanMbps": 10,
              "lan4": ["10.203.1.0/24"], "lan6": [], "nginxUid": 0}
    try:
        policy.run(["ip", "netns", "add", left]); left_created = True
        policy.run(["ip", "netns", "add", right]); right_created = True
        policy.run(["ip", "link", "add", left_dev, "type", "veth", "peer", "name", right_dev]); link_created = True
        policy.run(["ip", "link", "set", left_dev, "netns", left])
        policy.run(["ip", "link", "set", right_dev, "netns", right])
        for ns, dev, address in [(left, left_dev, "10.203.1.1/24"), (right, right_dev, "10.203.1.2/24")]:
            policy.run(["ip", "netns", "exec", ns, "ip", "link", "set", "lo", "up"])
            policy.run(["ip", "netns", "exec", ns, "ip", "addr", "add", address, "dev", dev])
            policy.run(["ip", "netns", "exec", ns, "ip", "link", "set", dev, "up"])
        policy.run(["ip", "netns", "exec", right, "ip", "addr", "add", "192.0.2.2/32", "dev", "lo"])
        policy.run(["ip", "netns", "exec", left, "ip", "route", "add", "192.0.2.2/32", "via", "10.203.1.2"])
        with tempfile.TemporaryDirectory(prefix="station-qos-kernel-") as tmp:
            temp = pathlib.Path(tmp)
            batch = temp / "qdisc.tc"; batch.write_text(policy.tc_batch(config))
            nft = temp / "qdisc.nft"; nft.write_text(policy.nft_text(config))
            policy.run(["ip", "netns", "exec", left, "nft", "--check", "-f", str(nft)])
            policy.run(["ip", "netns", "exec", left, "tc", "-batch", str(batch)])
            policy.run(["ip", "netns", "exec", left, "nft", "-f", str(nft)])
            inspector = ("import sys,json;sys.path.insert(0," + repr(str(pathlib.Path(__file__).parent)) + ");"
                         "import policy;print(json.dumps(policy.status(" + repr(config) + ")))" )
            state = json.loads(policy.run(["ip", "netns", "exec", left, "python3", "-c", inspector]))
            assert state["active"] and len(state["classes"]) == 9, "class_status_not_compatible"
            result["classStatusCompatible"] = True
            rx, tx = temp / "receiver.py", temp / "sender.py"
            rx.write_text(RECEIVER); tx.write_text(SENDER)
            receiver = subprocess.Popen(["ip", "netns", "exec", right, "python3", "-u", str(rx)],
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            if receiver.stdout.readline().strip() != "READY":
                raise RuntimeError("receiver_not_ready")
            for case in ["idle", "sparse", "busy", "lan"]:
                output = policy.run(["ip", "netns", "exec", left, "python3", str(tx),
                                     str(pathlib.Path(__file__).parent), case])
                result[case] = json.loads(output)
            # Actual forwarding/capacity gates, not string comparisons with generated code.
            assert 8 <= result["idle"]["bulk"]["Mbps"] <= 11.5, "idle_capacity_not_borrowed"
            assert result["sparse"]["game"]["frames"] >= 250, "game_frames_missing"
            assert result["sparse"]["game"]["latencyP95Millis"] < 40, "game_queue_latency_high"
            assert result["sparse"]["bulk"]["Mbps"] >= 7, "sparse_game_wastes_bandwidth"
            assert result["busy"]["bulk"]["Mbps"] >= 4.8, "downloads_starved"
            assert result["busy"]["game"]["Mbps"] >= 3, "game_cannot_borrow_idle_capacity"
            assert result["lan"]["bulk"]["Mbps"] >= 29, "lan_incorrectly_limited_to_wan"
            assert result["lan"]["bulk"]["Mbps"] <= 46, "link_ceiling_not_applied"
        result["passed"] = True
        result["isolatedNamespaces"] = True
        result["physicalPhonesOrInternetGameplay"] = False
        pathlib.Path(args.output).write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result))
    finally:
        if receiver:
            receiver.terminate()
            try: receiver.wait(timeout=3)
            except subprocess.TimeoutExpired: receiver.kill(); receiver.wait()
        for ns, created in [(left, left_created), (right, right_created)]:
            if created: subprocess.run(["ip", "netns", "delete", ns], capture_output=True, timeout=5)
        # Handles an exception before the interfaces are moved to their namespaces.
        if link_created: subprocess.run(["ip", "link", "delete", left_dev], capture_output=True, timeout=5)


if __name__ == "__main__":
    main()
