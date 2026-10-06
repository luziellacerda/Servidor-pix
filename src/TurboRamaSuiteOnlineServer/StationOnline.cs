using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TurboRamaSuiteOnlineServer.Online;

// Ephemeral, single-instance rooms. Authoritative authentication is injected by StationEndpoints.
// This component never handles activation secrets, ROM bytes, payment data or public relays.
public sealed record OnlineIdentity(string LicenseId, string DeviceId);
public sealed record OnlineEngine(string Id, string Platform, string CoreSha256, string RuntimeSha256);
public sealed record OnlineCommand(string Action, string RequestId, string? Nickname = null,
    string? RoomId = null, string? PeerId = null, string? ItemId = null,
    string? EngineId = null, string? ContentSha256 = null, string? OptionsSha256 = null,
    string? CoreSha256 = null, string? RuntimeSha256 = null,
    string? Address = null, int Port = 0, string? Text = null, bool Value = false, int Page = 0, string? Transport = null);
public sealed class OnlineFailure(int status, string code) : Exception(code)
{ public int Status { get; } = status; public string Code { get; } = code; }

public sealed class StationOnline
{
    private sealed class Peer(string id, OnlineIdentity identity, string nickname, long seen)
    {
        public string Id = id, Nickname = nickname;
        public OnlineIdentity Identity = identity;
        public long Seen = seen, LastChat = long.MinValue / 2;
        public string? Room;
        public readonly HashSet<string> Blocks = [];
        // Retain request fingerprints, never 64 full snapshots per player or old room secrets.
        public readonly Dictionary<string, string> Receipts = [];
        public readonly Queue<string> ReceiptOrder = [];
        public readonly Queue<long> Actions = [];
        public readonly Queue<DirectMessage> DirectMessages = [];
    }
    private sealed class Room(string id, string host, string item, OnlineEngine engine,
        string content, string options)
    {
        public string Id = id, Host = host, Item = item, Content = content, Options = options;
        public OnlineEngine Engine = engine;
        public string State = "waiting";
        public readonly string Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        public string? Address;
        public int Port;
        public string Transport = "direct";
        public long Generation = 1;
        public long StartingAt;
        public readonly List<string> Members = [host];
        public readonly HashSet<string> Ready = [];
        public readonly Queue<object> Messages = [];
    }
    private sealed record DirectMessage(string messageId,string fromPeerId,string toPeerId,string nickname,string text,DateTimeOffset utc);
    private sealed record JoinRequest(string Id,string From,string Room,long Expires);
    private readonly Dictionary<string,JoinRequest> joinRequests = [];
    private readonly bool socialEnabled;
    private sealed record Invite(string Id, string From, string To, string Room, long Expires);
    public sealed record RelayLease(string RoomId,string PeerId,long Generation,bool Host);
    private sealed record RelayGrant(string Token,RelayLease Lease,long Expires);
    private readonly Dictionary<string,RelayGrant> relayGrants = [];
    private readonly HashSet<string> usedRelayPeers = [];
    private readonly bool relayEnabled;
    private readonly object gate = new();
    private readonly Dictionary<string, Peer> peers = [];
    private readonly Dictionary<OnlineIdentity, string> identities = [];
    private readonly Dictionary<string, Room> rooms = [];
    private readonly Dictionary<string, Invite> invites = [];
    private readonly HashSet<OnlineIdentity> polls = [];
    private readonly Dictionary<string, OnlineEngine> engines;
    private readonly Func<string, string?> platformForItem;
    private readonly Func<long> clock;
    private readonly string instance = Id();
    private long revision;
    private TaskCompletionSource pulse = NewPulse();
    public StationOnline(IEnumerable<OnlineEngine> engines, Func<string, string?> platformForItem,
        Func<long>? clock = null, bool relayEnabled = false, bool socialEnabled = false)
    {
        this.relayEnabled=relayEnabled;this.socialEnabled=socialEnabled;
        this.engines = engines.ToDictionary(x => x.Id);
        foreach (var e in this.engines.Values) { Hash(e.CoreSha256); Hash(e.RuntimeSha256); }
        this.platformForItem = platformForItem;
        this.clock = clock ?? (() => Environment.TickCount64);
    }
    private static TaskCompletionSource NewPulse() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string Id() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    private static void Require(bool ok, int status, string code) { if (!ok) throw new OnlineFailure(status, code); }
    private static string Hash(string? value)
    { Require(value is not null && Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z"), 400, "STATION_ONLINE_HASH_INVALID"); return value!; }
    private static string Text(string? value, int max)
    {
        var s = value?.Trim() ?? "";
        Require(s.Length is > 0 && s.Length <= max && !s.Any(char.IsControl), 400, "STATION_ONLINE_TEXT_INVALID");
        return s;
    }
    private void Changed() { revision++; var old = pulse; pulse = NewPulse(); old.TrySetResult(); }
    private void Sweep()
    {
        long now = clock();
        foreach(var request in joinRequests.Values.Where(x=>x.Expires<=now).ToArray()){joinRequests.Remove(request.Id);Changed();}
        foreach(var key in relayGrants.Where(x=>x.Value.Expires<=now).Select(x=>x.Key).ToArray())relayGrants.Remove(key);
        foreach (var p in peers.Values.Where(p => now - p.Seen >= 60000).ToArray()) Remove(p);
        foreach (var r in rooms.Values.Where(r=>r.State=="starting" && now-r.StartingAt>=(r.Transport=="relay-wss-v1"?90000:30000)).ToArray())
            if(peers.TryGetValue(r.Host,out var host))Leave(host);
        foreach (var i in invites.Values.Where(i => i.Expires <= now).ToArray()) { invites.Remove(i.Id); Changed(); }
    }
    private Peer Get(OnlineIdentity identity)
    { Require(identities.TryGetValue(identity, out var id) && peers.ContainsKey(id), 409, "STATION_ONLINE_ENTER_REQUIRED"); return peers[id!]; }
    private Room Member(Peer p, string? id)
    {
        Require(id is not null && rooms.TryGetValue(id, out var r) && r.Members.Contains(p.Id), 404, "STATION_ONLINE_ROOM_NOT_FOUND");
        return rooms[id!];
    }
    private void Leave(Peer p)
    {
        if (p.Room is null) return;
        if (rooms.TryGetValue(p.Room, out var room))
        {
            if (room.Host == p.Id || room.State != "waiting")
            {
                rooms.Remove(room.Id);
                foreach(var request in joinRequests.Values.Where(x=>x.Room==room.Id).ToArray())joinRequests.Remove(request.Id);
                foreach(var member in room.Members){relayGrants.Remove(member);usedRelayPeers.Remove(member);}
                foreach (var member in room.Members) if (peers.TryGetValue(member, out var other)) other.Room = null;
                foreach (var i in invites.Values.Where(i => i.Room == room.Id).ToArray()) invites.Remove(i.Id);
            }
            else { room.Members.Remove(p.Id); room.Ready.Clear(); room.Generation++; }
        }
        p.Room = null; Changed();
    }
    private void Remove(Peer p)
    {
        Leave(p); peers.Remove(p.Id); identities.Remove(p.Identity);
        foreach(var request in joinRequests.Values.Where(x=>x.From==p.Id).ToArray())joinRequests.Remove(request.Id);
        foreach (var i in invites.Values.Where(i => i.From == p.Id || i.To == p.Id).ToArray()) invites.Remove(i.Id);
        Changed();
    }
    public void Revoke(OnlineIdentity identity) { lock (gate) if (identities.TryGetValue(identity, out var id)) Remove(peers[id]); }
    private static bool Blocked(Peer a, Peer b) => a.Blocks.Contains(b.Id) || b.Blocks.Contains(a.Id);
    private void Rate(Peer p)
    {
        long now = clock(); while (p.Actions.TryPeek(out var t) && now - t >= 10000) p.Actions.Dequeue();
        Require(p.Actions.Count < 30, 429, "STATION_ONLINE_RATE_LIMITED"); p.Actions.Enqueue(now);
    }
    private object View(Peer p, int page)
    {
        Require(page >= 0 && page <= 40, 400, "STATION_ONLINE_PAGE_INVALID");
        var visible = peers.Values.Where(x => !Blocked(p,x)).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        Room? selected = p.Room is not null ? rooms.GetValueOrDefault(p.Room) : null;
        return new {
            schemaVersion = 1, instance, revision, selfId = p.Id, heartbeatSeconds = 20, expiresAfterSeconds = 60,
            transports=relayEnabled?new[]{"direct","relay-wss-v1"}:new[]{"direct"},
            socialCapabilities=socialEnabled?new[]{"direct-chat-v1","join-request-v1"}:Array.Empty<string>(),
            directMessages=socialEnabled?p.DirectMessages.ToArray():Array.Empty<DirectMessage>(),
            joinRequests=socialEnabled?joinRequests.Values.Where(x=>rooms.TryGetValue(x.Room,out var target)&&target.Host==p.Id)
                .Select(x=>new{requestId=x.Id,fromPeerId=x.From,roomId=x.Room,itemId=rooms[x.Room].Item}).ToArray():[],
            sentJoinRequests=socialEnabled?joinRequests.Values.Where(x=>x.From==p.Id).Select(x=>new{requestId=x.Id,roomId=x.Room}).ToArray():[],
            page, totalPeers = visible.Length, totalRooms=rooms.Count,
            nextPage = (page+1)*100 < Math.Max(visible.Length,rooms.Count) ? page+1 : (int?)null,
            peers = visible.Skip(page*100).Take(100).Select(x => new { peerId=x.Id, nickname=x.Nickname, status=x.Room is null ? "online" : "in-room" }).ToArray(),
            rooms = rooms.Values.Where(r => peers.TryGetValue(r.Host,out var host) && !Blocked(p,host)).OrderBy(r=>r.Id,StringComparer.Ordinal)
                .Skip(page*100).Take(100).Select(r => new { roomId=r.Id,itemId=r.Item,engineId=r.Engine.Id,hostId=r.Host,state=r.State,players=r.Members.Count,maximumPlayers=2 }).ToArray(),
            invites = invites.Values.Where(i => i.To==p.Id).Select(i => new { inviteId=i.Id,fromPeerId=i.From,roomId=i.Room,itemId=rooms[i.Room].Item }).ToArray(),
            room = selected is null ? null : new { roomId=selected.Id,itemId=selected.Item,engineId=selected.Engine.Id,
                contentSha256=selected.Content,optionsSha256=selected.Options,coreSha256=selected.Engine.CoreSha256,
                runtimeSha256=selected.Engine.RuntimeSha256,hostId=selected.Host,state=selected.State,generation=selected.Generation,
                connectionPassword=selected.Password,
                members=selected.Members.ToArray(),ready=selected.Ready.ToArray(),messages=selected.Messages.ToArray(),
                transport=selected.Transport,
                relay=selected.Transport=="relay-wss-v1"&&relayGrants.TryGetValue(p.Id,out var grant)?new {path="/v1/station/online/relay",protocol="station-relay.v1",ticket=grant.Token,expiresInSeconds=Math.Max(0,(grant.Expires-clock())/1000)}:null,
                directEndpoint=selected.Transport=="direct"&&selected.State is "starting" or "connecting" ? new { address=selected.Address,port=selected.Port } : null },
            engines = engines.Values.Select(e => new { engineId=e.Id,platform=e.Platform,coreSha256=e.CoreSha256,runtimeSha256=e.RuntimeSha256 }).ToArray()
        };
    }
    public object Command(OnlineIdentity identity, OnlineCommand cmd)
    {
        Require(!string.IsNullOrWhiteSpace(identity.LicenseId) && !string.IsNullOrWhiteSpace(identity.DeviceId),401,"STATION_SESSION_INVALID");
        Require(Guid.TryParseExact(cmd.RequestId,"D",out _),400,"STATION_ONLINE_REQUEST_INVALID");
        Require(cmd.Page>=0 && cmd.Page<=40,400,"STATION_ONLINE_PAGE_INVALID");
        lock (gate)
        {
            Sweep();
            if(cmd.Action=="offline" && !identities.ContainsKey(identity))
                return new { schemaVersion=1,instance,revision,offline=true };
            if (cmd.Action=="enter" && !identities.ContainsKey(identity))
            {
                Require(peers.Count<4096,503,"STATION_ONLINE_FULL");
                string nickname=Text(cmd.Nickname,24),id=Id();
                peers[id]=new Peer(id,identity,nickname,clock());identities[identity]=id;Changed();
            }
            var p=Get(identity);p.Seen=clock();
            string requestJson=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cmd)));
            if (p.Receipts.TryGetValue(cmd.RequestId,out var receipt))
            { Require(receipt==requestJson,409,"STATION_ONLINE_REQUEST_REUSED"); return View(p,cmd.Page); }
            Rate(p);
            switch (cmd.Action)
            {
                case "enter": {var nickname=Text(cmd.Nickname,24);if(p.Nickname!=nickname){p.Nickname=nickname;Changed();}break;}
                case "heartbeat": break;
                case "leave": Leave(p); break;
                case "offline": Remove(p); return new { schemaVersion=1,instance,revision,offline=true };
                case "create":
                {
                    Require(p.Room is null,409,"STATION_ONLINE_ALREADY_IN_ROOM");
                    Require(rooms.Count<1024,503,"STATION_ONLINE_FULL");
                    Require(cmd.EngineId is not null && engines.ContainsKey(cmd.EngineId),409,"STATION_ONLINE_ENGINE_UNAVAILABLE");
                    var engine=engines[cmd.EngineId!];
                    Require(cmd.ItemId is not null && platformForItem(cmd.ItemId)==engine.Platform,404,"STATION_ONLINE_ITEM_UNAVAILABLE");
                    Require(Hash(cmd.CoreSha256)==engine.CoreSha256 && Hash(cmd.RuntimeSha256)==engine.RuntimeSha256,409,"STATION_ONLINE_BUILD_MISMATCH");
                    var room=new Room(Id(),p.Id,cmd.ItemId!,engine,Hash(cmd.ContentSha256),Hash(cmd.OptionsSha256));
                    rooms[room.Id]=room;p.Room=room.Id;Changed();break;
                }
                case "join":
                {
                    Require(p.Room is null,409,"STATION_ONLINE_ALREADY_IN_ROOM");
                    Require(cmd.RoomId is not null && rooms.ContainsKey(cmd.RoomId),404,"STATION_ONLINE_ROOM_NOT_FOUND");
                    var r=rooms[cmd.RoomId!];Require(r.State=="waiting" && r.Members.Count<2,409,"STATION_ONLINE_ROOM_FULL");
                    Require(!Blocked(p,peers[r.Host]),403,"STATION_ONLINE_BLOCKED");
                    Require(Hash(cmd.ContentSha256)==r.Content && Hash(cmd.OptionsSha256)==r.Options &&
                        Hash(cmd.CoreSha256)==r.Engine.CoreSha256 && Hash(cmd.RuntimeSha256)==r.Engine.RuntimeSha256,
                        409,"STATION_ONLINE_BUILD_MISMATCH");
                    r.Members.Add(p.Id);r.Ready.Clear();r.Generation++;p.Room=r.Id;
                    foreach(var request in joinRequests.Values.Where(x=>x.From==p.Id).ToArray())joinRequests.Remove(request.Id);
                    foreach(var invite in invites.Values.Where(x=>x.To==p.Id&&x.Room==r.Id).ToArray())invites.Remove(invite.Id);
                    Changed();break;
                }
                case "ready":
                {
                    var r=Member(p,cmd.RoomId);Require(r.State=="waiting",409,"STATION_ONLINE_ROOM_STARTED");
                    if(cmd.Value)r.Ready.Add(p.Id);else r.Ready.Remove(p.Id);Changed();break;
                }
                case "start":
                {
                    var r=Member(p,cmd.RoomId);Require(r.Host==p.Id,403,"STATION_ONLINE_HOST_REQUIRED");
                    Require(r.State=="waiting" && r.Members.Count==2 && r.Ready.Count==2,409,"STATION_ONLINE_NOT_READY");
                    if(cmd.Transport=="relay-wss-v1"){
                        Require(relayEnabled,503,"STATION_ONLINE_RELAY_DISABLED");r.Transport="relay-wss-v1";
                    }else{
                        Require(cmd.Transport is null or "direct",400,"STATION_ONLINE_TRANSPORT_INVALID");
                    Require(IPAddress.TryParse(cmd.Address,out var ip) && !IPAddress.IsLoopback(ip) &&
                        !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any) && !ip.IsIPv6Multicast &&
                        (ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork || ip.GetAddressBytes()[0]<224) &&
                        cmd.Port is >=1024 and <=65535,
                        400,"STATION_ONLINE_ENDPOINT_INVALID");
                    r.Address=ip!.ToString();r.Port=cmd.Port;
                    }
                    r.State="starting";r.StartingAt=clock();r.Generation++;Changed();break;
                }
                case "host-listening":
                {
                    var r=Member(p,cmd.RoomId);Require(r.Host==p.Id,403,"STATION_ONLINE_HOST_REQUIRED");
                    Require(r.State is "starting" or "connecting",409,"STATION_ONLINE_NOT_READY");
                    if(r.State=="starting"){r.State="connecting";if(r.Transport=="direct")r.Generation++;Changed();}break;
                }
                case "relay-ticket":
                {
                    var r=Member(p,cmd.RoomId);
                    Require(relayEnabled&&r.Transport=="relay-wss-v1",503,"STATION_ONLINE_RELAY_DISABLED");
                    Require(r.State is "starting" or "connecting",409,"STATION_ONLINE_NOT_READY");
                    Require(!usedRelayPeers.Contains(p.Id),409,"STATION_ONLINE_RELAY_ALREADY_ATTACHED");
                    string ticket=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
                    relayGrants[p.Id]=new RelayGrant(ticket,new RelayLease(r.Id,p.Id,r.Generation,r.Host==p.Id),clock()+60000);break;
                }
                case "invite":
                {
                    var r=Member(p,cmd.RoomId);Require(r.Host==p.Id && r.State=="waiting",403,"STATION_ONLINE_HOST_REQUIRED");
                    Require(cmd.PeerId is not null && peers.ContainsKey(cmd.PeerId) && cmd.PeerId!=p.Id,404,"STATION_ONLINE_PEER_NOT_FOUND");
                    var other=peers[cmd.PeerId!];Require(!Blocked(p,other),403,"STATION_ONLINE_BLOCKED");
                    Require(invites.Values.Count(i=>i.From==p.Id)<10 && invites.Values.Count(i=>i.To==other.Id)<20,429,"STATION_ONLINE_INVITE_LIMIT");
                    Require(!invites.Values.Any(i=>i.From==p.Id&&i.To==other.Id&&i.Room==r.Id),409,"STATION_ONLINE_INVITE_EXISTS");
                    var invitation=new Invite(Id(),p.Id,other.Id,r.Id,clock()+60000);invites[invitation.Id]=invitation;Changed();break;
                }
                case "dismiss-invite":
                {
                    Require(cmd.Text is not null && invites.TryGetValue(cmd.Text,out var invite) && invite.To==p.Id,404,"STATION_ONLINE_INVITE_NOT_FOUND");
                    invites.Remove(cmd.Text!);Changed();break;
                }
                case "direct-chat":
                {
                    Require(socialEnabled,503,"STATION_ONLINE_SOCIAL_DISABLED");
                    Require(cmd.PeerId is not null && peers.ContainsKey(cmd.PeerId) && cmd.PeerId!=p.Id,404,"STATION_ONLINE_PEER_NOT_FOUND");
                    var other=peers[cmd.PeerId!];Require(!Blocked(p,other),403,"STATION_ONLINE_BLOCKED");
                    string text=Text(cmd.Text,500);Require(clock()-p.LastChat>=1000,429,"STATION_ONLINE_CHAT_LIMIT");p.LastChat=clock();
                    var message=new DirectMessage(Id(),p.Id,other.Id,p.Nickname,text,DateTimeOffset.UtcNow);
                    p.DirectMessages.Enqueue(message);other.DirectMessages.Enqueue(message);
                    while(p.DirectMessages.Count>32)p.DirectMessages.Dequeue();while(other.DirectMessages.Count>32)other.DirectMessages.Dequeue();
                    Changed();break;
                }
                case "request-join":
                {
                    Require(socialEnabled,503,"STATION_ONLINE_SOCIAL_DISABLED");
                    Require(cmd.RoomId is not null && rooms.ContainsKey(cmd.RoomId),404,"STATION_ONLINE_ROOM_NOT_FOUND");
                    var target=rooms[cmd.RoomId!];Require(target.Host!=p.Id && target.State=="waiting" && target.Members.Count<2,409,"STATION_ONLINE_ROOM_FULL");
                    Require(!Blocked(p,peers[target.Host]),403,"STATION_ONLINE_BLOCKED");
                    Require(!joinRequests.Values.Any(x=>x.From==p.Id&&x.Room==target.Id),409,"STATION_ONLINE_REQUEST_EXISTS");
                    Require(joinRequests.Values.Count(x=>x.From==p.Id)<5 && joinRequests.Values.Count(x=>x.Room==target.Id)<20,429,"STATION_ONLINE_INVITE_LIMIT");
                    var request=new JoinRequest(Id(),p.Id,target.Id,clock()+60000);joinRequests.Add(request.Id,request);Changed();break;
                }
                case "accept-request":
                case "dismiss-request":
                {
                    Require(socialEnabled,503,"STATION_ONLINE_SOCIAL_DISABLED");
                    Require(cmd.Text is not null && joinRequests.ContainsKey(cmd.Text),404,"STATION_ONLINE_JOIN_REQUEST_NOT_FOUND");
                    var request=joinRequests[cmd.Text!];var target=Member(p,request.Room);Require(target.Host==p.Id,403,"STATION_ONLINE_HOST_REQUIRED");
                    if(cmd.Action=="accept-request"){
                        Require(target.State=="waiting"&&target.Members.Count<2,409,"STATION_ONLINE_ROOM_FULL");
                        Require(peers.ContainsKey(request.From),404,"STATION_ONLINE_PEER_NOT_FOUND");
                        Require(!Blocked(p,peers[request.From]),403,"STATION_ONLINE_BLOCKED");
                        if(!invites.Values.Any(x=>x.From==p.Id&&x.To==request.From&&x.Room==target.Id)){
                            Require(invites.Values.Count(x=>x.From==p.Id)<10 && invites.Values.Count(x=>x.To==request.From)<20,429,"STATION_ONLINE_INVITE_LIMIT");
                            var invite=new Invite(Id(),p.Id,request.From,target.Id,clock()+60000);invites.Add(invite.Id,invite);
                        }
                    }
                    joinRequests.Remove(request.Id);Changed();break;
                }
                case "chat":
                {
                    var r=Member(p,cmd.RoomId);string text=Text(cmd.Text,500);
                    Require(clock()-p.LastChat>=1000,429,"STATION_ONLINE_CHAT_LIMIT");p.LastChat=clock();
                    r.Messages.Enqueue(new { messageId=Id(),fromPeerId=p.Id,nickname=p.Nickname,text,utc=DateTimeOffset.UtcNow });
                    while(r.Messages.Count>50)r.Messages.Dequeue();Changed();break;
                }
                case "block":
                {
                    Require(cmd.PeerId is not null && peers.ContainsKey(cmd.PeerId) && cmd.PeerId!=p.Id,404,"STATION_ONLINE_PEER_NOT_FOUND");
                    Require(p.Blocks.Count<128,409,"STATION_ONLINE_BLOCK_LIMIT");
                    p.Blocks.Add(cmd.PeerId!);
                    foreach(var request in joinRequests.Values.Where(x=>rooms.TryGetValue(x.Room,out var target)&&((x.From==p.Id&&target.Host==cmd.PeerId)||(x.From==cmd.PeerId&&target.Host==p.Id))).ToArray())joinRequests.Remove(request.Id);
                    foreach(var participant in new[]{p,peers[cmd.PeerId!]}){
                        var kept=participant.DirectMessages.Where(x=>!((x.fromPeerId==p.Id&&x.toPeerId==cmd.PeerId)||(x.fromPeerId==cmd.PeerId&&x.toPeerId==p.Id))).ToArray();
                        participant.DirectMessages.Clear();foreach(var message in kept)participant.DirectMessages.Enqueue(message);
                    }
                    if(p.Room is not null && peers[cmd.PeerId!].Room==p.Room)Leave(p);
                    foreach(var i in invites.Values.Where(i=>(i.From==p.Id&&i.To==cmd.PeerId)||(i.To==p.Id&&i.From==cmd.PeerId)).ToArray())invites.Remove(i.Id);
                    Changed();break;
                }
                default: throw new OnlineFailure(400,"STATION_ONLINE_ACTION_INVALID");
            }
            var result=View(p,cmd.Page);p.Receipts[cmd.RequestId]=requestJson;p.ReceiptOrder.Enqueue(cmd.RequestId);
            while(p.ReceiptOrder.Count>64)p.Receipts.Remove(p.ReceiptOrder.Dequeue());
            return result;
        }
    }
    public RelayLease TakeRelayTicket(string token)
    {
        lock(gate){
            Sweep();Require(relayEnabled,503,"STATION_ONLINE_RELAY_DISABLED");
            Require(Regex.IsMatch(token,"\\A[A-Za-z0-9_-]{43}\\z"),401,"STATION_ONLINE_RELAY_TICKET_INVALID");
            var grant=relayGrants.Values.FirstOrDefault(g=>CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(g.Token),System.Text.Encoding.ASCII.GetBytes(token)));
            Require(grant is not null && grant.Expires>clock() && RelayCurrentLocked(grant.Lease),401,"STATION_ONLINE_RELAY_TICKET_INVALID");
            relayGrants.Remove(grant!.Lease.PeerId);Require(usedRelayPeers.Add(grant.Lease.PeerId),409,"STATION_ONLINE_RELAY_ALREADY_ATTACHED");
            return grant.Lease;
        }
    }
    private bool RelayCurrentLocked(RelayLease lease)=>rooms.TryGetValue(lease.RoomId,out var room)&&room.Transport=="relay-wss-v1"&&room.Generation==lease.Generation&&room.Members.Contains(lease.PeerId)&&peers.TryGetValue(lease.PeerId,out var peer)&&clock()-peer.Seen<60000;
    public bool RelayCurrent(RelayLease lease){lock(gate){Sweep();return RelayCurrentLocked(lease);}}
    public void CloseRelay(RelayLease lease){lock(gate){if(RelayCurrentLocked(lease)&&peers.TryGetValue(lease.PeerId,out var peer))Leave(peer);}}
    public async Task<object> Events(OnlineIdentity identity, string? clientInstance,long after,int page,CancellationToken token)
    {
        Require(page>=0 && page<=40,400,"STATION_ONLINE_PAGE_INVALID");
        Task wait;
        lock(gate)
        {
            Sweep();var p=Get(identity);
            Require(after>=0,400,"STATION_ONLINE_CURSOR_INVALID");
            if(clientInstance!=instance || after!=revision)return View(p,page);
            Require(polls.Add(identity),429,"STATION_ONLINE_POLL_EXISTS");
            wait=pulse.Task;
        }
        try { try { await wait.WaitAsync(TimeSpan.FromSeconds(10),token); } catch(TimeoutException) { } }
        finally { lock(gate)polls.Remove(identity); }
        lock(gate){Sweep();return View(Get(identity),page);}
    }
}
