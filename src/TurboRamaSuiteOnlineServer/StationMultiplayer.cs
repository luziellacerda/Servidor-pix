using System.Security.Cryptography;
using System.Text.Json;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

// Exact allowlist, never an inference from a catalog's descriptive player count.
public sealed record StationMultiplayerProfile(string ItemId,string ContentSha256,string Platform,
    string EngineId,string CoreSha256,string RuntimeSha256,string ProfileId,string ProfileSha256,
    int MaximumPlayers,bool Approved,string ControllerProfile,string Mode,int[] AllowedPlayerCounts);
public sealed record StationMultiplayerCommand(string Action,string RequestId,string? RoomId=null,
    string? ItemId=null,string? ContentSha256=null,string? EngineId=null,string? CoreSha256=null,
    string? RuntimeSha256=null,string? ProfileId=null,string? ProfileSha256=null,int Capacity=0,
    long Generation=0,string? LinkId=null,bool Value=false,string? Nickname=null,string? Text=null);
public sealed record StationMultiplayerMessage(string MessageId,string FromPeerId,string Nickname,string Text,long UtcMs);

// Shared with v2 when the candidate is enabled: reservations survive a network loss.
public sealed class StationReplayBudget(long maximumBytes=33554432)
{
    private readonly object gate=new();private readonly Dictionary<string,long> reservations=[];
    public long MaximumBytes {get;}=maximumBytes is >=524288 and <=33554432?maximumBytes:throw new ArgumentOutOfRangeException(nameof(maximumBytes));
    public long UsedBytes {get{lock(gate)return reservations.Values.Sum();}}
    public bool TryReserve(string key,long bytes){lock(gate){
        if(reservations.TryGetValue(key,out long old))return old==bytes;
        if(bytes<=0||bytes>MaximumBytes-reservations.Values.Sum())return false;
        reservations.Add(key,bytes);return true;}}
    public void Release(string key){lock(gate)reservations.Remove(key);}
}

public sealed class StationMultiplayer
{
    public const string Protocol="station-stream.v3",CommandPath="/v1/station/online/multiplayer/command",RelayPath="/v1/station/online/multiplayer/relay";
    public const int WindowBytes=262144;
    private sealed class Peer(string id,OnlineIdentity identity,string nickname,long seen)
    {
        public string Id=id;public readonly OnlineIdentity Identity=identity;
        public string Nickname=nickname;public string? Room;public long Seen=seen;
        public long LastChat=long.MinValue/2;
        public readonly Dictionary<string,string> Receipts=[];public readonly Queue<string> ReceiptOrder=[];
        public readonly Queue<long> Actions=[];
    }
    private sealed class Member(Peer peer,int slot){public Peer Peer=peer;public int Slot=slot;public bool Ready;}
    public sealed record Link(string LinkId,string GuestPeerId,int GuestSlot);
    private sealed class Room(string id,Peer host,StationMultiplayerProfile profile,int capacity)
    {
        public readonly string Id=id,Host=host.Id,Password=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();public readonly StationMultiplayerProfile Profile=profile;
        public readonly int Capacity=capacity;public long Generation=1;public readonly List<Member> Members=[new(host,1)];
        public Link[] Links=[];public StationMultiplayerStream? Stream;
        public readonly Queue<StationMultiplayerMessage> Messages=[];
    }
    public sealed record Lease(string RoomId,long Generation,string LinkId,string OwnerPeerId,int OwnerSlot,
        bool HostSide,string GuestPeerId,int GuestSlot,string AttachmentId,OnlineIdentity Identity,
        string ProofMode,string? ProofKeySpki)
    {
        // Only the existing device/license authorizer consumes this adapter. It is not sent to the v2 hub.
        public StationOnline.RelayLease AuthorizationLease=>new(RoomId,OwnerPeerId,Generation,HostSide,
            ProofMode,ProofKeySpki,Protocol,AttachmentId,Identity);
    }
    private sealed record Grant(string Token,Lease Lease,long Expires);
    private readonly object gate=new();private readonly Dictionary<OnlineIdentity,Peer> peers=[];
    private readonly Dictionary<string,Room> rooms=[];private readonly Dictionary<string,Grant> grants=[];
    private readonly Dictionary<string,string> attachments=[];private readonly Dictionary<string,StationMultiplayerProfile> profiles;
    private readonly Dictionary<string,StationMultiplayerProfile[]> gameProfiles;
    private readonly Func<string,string?> platform;private readonly Func<string,string?>? currentContentHash;
    private readonly Func<long> clock;private readonly StationReplayBudget budget;
    private readonly string instance=Id();private long revision;
    public object AdmissionGate {get;}=new();
    public StationMultiplayer(IEnumerable<StationMultiplayerProfile> entries,Func<string,string?> platform,
        StationReplayBudget budget,Func<long>? clock=null,Func<string,string?>? currentContentHash=null)
    {
        this.platform=platform;this.budget=budget;this.clock=clock??(()=>Environment.TickCount64);
        this.currentContentHash=currentContentHash;
        profiles=[];
        foreach(var p in entries){
            Need(Text(p.ItemId,256)&&Text(p.Platform,64)&&Text(p.EngineId,128)&&Text(p.ProfileId,128)&&Text(p.ControllerProfile,128)
                &&Hash(p.ContentSha256)&&Hash(p.CoreSha256)&&Hash(p.RuntimeSha256)&&Hash(p.ProfileSha256)
                &&p.MaximumPlayers is >=1 and <=4&&Text(p.Mode,64)&&p.AllowedPlayerCounts is not null
                &&p.AllowedPlayerCounts.SequenceEqual(p.AllowedPlayerCounts.Distinct().Order())
                &&p.AllowedPlayerCounts.All(n=>n is >=2 and <=4&&n<=p.MaximumPlayers)
                &&(p.MaximumPlayers==1?p.AllowedPlayerCounts.Length==0:p.AllowedPlayerCounts.Contains(p.MaximumPlayers)),400,"STATION_MULTIPLAYER_PROFILE_INVALID");
            // Explicitly selected different controller/mode profiles may coexist. A duplicate
            // complete identity remains ambiguous and cannot be resolved by choosing the first row.
            Need(profiles.TryAdd(ProfileKey(p.ItemId,p.ContentSha256,p.EngineId,p.CoreSha256,p.RuntimeSha256,p.ProfileId,p.ProfileSha256),p with{AllowedPlayerCounts=p.AllowedPlayerCounts!.ToArray()}),400,"STATION_MULTIPLAYER_PROFILE_AMBIGUOUS");
        }
        Need(profiles.Count<=50000,400,"STATION_MULTIPLAYER_PROFILE_LIMIT");
        Need(profiles.Values.GroupBy(p=>p.ItemId).All(g=>g.Count()<=32),400,"STATION_MULTIPLAYER_PROFILE_LIMIT");
        gameProfiles=profiles.Values.GroupBy(p=>Key(p.ItemId,p.ContentSha256,p.EngineId,p.CoreSha256,p.RuntimeSha256)).ToDictionary(g=>g.Key,g=>g.ToArray());
    }
    private static string Key(string? item,string? content,string? engine,string? core,string? runtime)=>string.Join('\n',item,content,engine,core,runtime);
    private static string ProfileKey(string? item,string? content,string? engine,string? core,string? runtime,string? profile,string? hash)=>Key(item,content,engine,core,runtime)+"\n"+profile+"\n"+hash;
    private static bool Text(string? value,int max)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=max&&!value.Any(char.IsControl);
    private static bool Hash(string? value)=>value is {Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Need(bool ok,int status,string code){if(!ok)throw new OnlineFailure(status,code);}
    private static string Id()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    private static string Token()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string AttachmentKey(Lease lease)=>lease.RoomId+":"+lease.LinkId+":"+lease.OwnerPeerId;
    private StationMultiplayerProfile Approved(StationMultiplayerCommand c,int count)
    {
        Need(gameProfiles.ContainsKey(Key(c.ItemId,c.ContentSha256,c.EngineId,c.CoreSha256,c.RuntimeSha256)),409,"STATION_MULTIPLAYER_GAME_UNCLASSIFIED");
        Need(profiles.TryGetValue(ProfileKey(c.ItemId,c.ContentSha256,c.EngineId,c.CoreSha256,c.RuntimeSha256,c.ProfileId,c.ProfileSha256),out var p),409,"STATION_MULTIPLAYER_PROFILE_UNAPPROVED");
        Need(p!.Approved&&p.ProfileId==c.ProfileId&&p.ProfileSha256==c.ProfileSha256&&CatalogMatches(p),409,"STATION_MULTIPLAYER_PROFILE_UNAPPROVED");
        Need(count is >=2 and <=4&&p.AllowedPlayerCounts.Contains(count),409,"STATION_MULTIPLAYER_CAPACITY_UNSUPPORTED");return p;
    }
    private void Check(Room room,int count)
    {
        var p=room.Profile;
        _=Approved(new("capabilities",Guid.NewGuid().ToString(),ItemId:p.ItemId,ContentSha256:p.ContentSha256,
            EngineId:p.EngineId,CoreSha256:p.CoreSha256,RuntimeSha256:p.RuntimeSha256,ProfileId:p.ProfileId,ProfileSha256:p.ProfileSha256),count);
    }
    public bool LegacyAllowed(string item,string content,string engine,string core,string runtime)
    {return gameProfiles.TryGetValue(Key(item,content,engine,core,runtime),out var matches)&&matches.Count(p=>p.Approved&&p.AllowedPlayerCounts.Contains(2)&&p.ControllerProfile=="standard-2p-v1"&&CatalogMatches(p))==1;}

    private bool CatalogMatches(StationMultiplayerProfile profile) =>
        platform(profile.ItemId) == profile.Platform &&
        (currentContentHash is null || currentContentHash(profile.ItemId) == profile.ContentSha256);
    public object Command(OnlineIdentity identity,StationMultiplayerCommand c,StationSessionSecurity security,(string Id,string Nickname)? authority=null,IReadOnlySet<string>? blocked=null)
    {
        lock(gate){
            Need(Text(identity.LicenseId,256)&&Text(identity.DeviceId,256),401,"STATION_SESSION_INVALID");
            Need(Guid.TryParseExact(c.RequestId,"D",out _),400,"STATION_MULTIPLAYER_REQUEST_INVALID");
            Need(c.Action is "failed" or "chat" or "snapshot" or "capabilities" or "heartbeat" or "create" or "join" or "ready" or "start" or "leave" or "ticket" or "resume",400,"STATION_MULTIPLAYER_ACTION_INVALID");
            foreach(var k in grants.Where(x=>x.Value.Expires<=clock()).Select(x=>x.Key).ToArray())grants.Remove(k);
            foreach(var old in peers.Where(x=>x.Value.Room is null&&clock()-x.Value.Seen>=60000).Select(x=>x.Key).ToArray())peers.Remove(old);
            if(!peers.TryGetValue(identity,out var peer)){
                Need(peers.Count<4096,503,"STATION_MULTIPLAYER_FULL");peer=new(authority?.Id??Id(),identity,authority?.Nickname??"Jogador",clock());peers.Add(identity,peer);
            }
            peer.Seen=clock();
            if(authority is {} verified){Need(peer.Room is null||peer.Id==verified.Id,409,"STATION_MULTIPLAYER_IDENTITY_CHANGED");peer.Id=verified.Id;peer.Nickname=verified.Nickname;}
            else if(c.Nickname is not null){Need(Text(c.Nickname,40),400,"STATION_MULTIPLAYER_NICKNAME_INVALID");peer.Nickname=c.Nickname;}
            string fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(c,StrictJson.Options)));
            if(peer.Receipts.TryGetValue(c.RequestId,out var previous)){
                Need(previous==fingerprint,409,"STATION_MULTIPLAYER_REQUEST_REUSED");return View(peer,c.ItemId,GrantView(peer,c.LinkId),blocked);
            }
            while(peer.Actions.TryPeek(out var at)&&clock()-at>=1000)peer.Actions.Dequeue();
            Need(peer.Actions.Count<20,429,"STATION_MULTIPLAYER_RATE_LIMIT");peer.Actions.Enqueue(clock());
            object? ticket=null;
            switch(c.Action){
                case "create":{
                    Need(peer.Room is null,409,"STATION_MULTIPLAYER_ALREADY_IN_ROOM");Need(rooms.Count<100,503,"STATION_MULTIPLAYER_FULL");
                    var p=Approved(c,c.Capacity);var room=new Room(Id(),peer,p,c.Capacity);rooms.Add(room.Id,room);peer.Room=room.Id;break;}
                case "join":{
                    Need(peer.Room is null,409,"STATION_MULTIPLAYER_ALREADY_IN_ROOM");var room=Find(c.RoomId);
                    Need(blocked is null||room.Members.All(m=>!blocked.Contains(m.Peer.Id)),403,"STATION_ONLINE_BLOCKED");
                    Need(room.Stream is null&&room.Members.Count<room.Capacity,409,"STATION_MULTIPLAYER_ROOM_FULL");
                    Need(c.Generation==room.Generation,409,"STATION_MULTIPLAYER_GENERATION_MISMATCH");
                    var p=Approved(c,room.Capacity);Need(p==room.Profile,409,"STATION_MULTIPLAYER_GAME_MISMATCH");
                    int slot=Enumerable.Range(2,3).First(s=>room.Members.All(m=>m.Slot!=s));room.Members.Add(new(peer,slot));peer.Room=room.Id;Bump(room);break;}
                case "ready":{
                    var room=Own(peer,c);Need(room.Stream is null,409,"STATION_MULTIPLAYER_ALREADY_STARTED");Check(room,room.Capacity);
                    room.Members.Single(m=>m.Peer==peer).Ready=c.Value;break;}
                case "start":{
                    var room=Own(peer,c);Need(room.Host==peer.Id,403,"STATION_MULTIPLAYER_HOST_REQUIRED");
                    Need(room.Stream is null,409,"STATION_MULTIPLAYER_ALREADY_STARTED");Check(room,room.Members.Count);
                    Need(room.Members.All(m=>m.Ready),409,"STATION_MULTIPLAYER_NOT_READY");
                    var links=room.Members.Where(m=>m.Slot!=1).OrderBy(m=>m.Slot).Select(m=>new Link(Id(),m.Peer.Id,m.Slot)).ToArray();
                    Need(budget.TryReserve("v3:"+room.Id,(long)links.Length*2*WindowBytes),503,"STATION_MULTIPLAYER_REPLAY_FULL");
                    try{room.Links=links;room.Stream=new StationMultiplayerStream(links.Select(l=>l.LinkId).ToArray(),WindowBytes,()=>budget.Release("v3:"+room.Id));room.Generation++;}
                    catch{budget.Release("v3:"+room.Id);throw;}break;}
                case "leave":{var room=Own(peer,c);Leave(peer,room);break;}
                case "failed":{var room=Own(peer,c);Need(room.Stream is not null,409,"STATION_MULTIPLAYER_START_REQUIRED");room.Stream!.End();break;}
                case "chat":{
                    var room=Own(peer,c);Need(Text(c.Text,500),400,"STATION_MULTIPLAYER_TEXT_INVALID");Need(clock()-peer.LastChat>=1000,429,"STATION_MULTIPLAYER_CHAT_RATE_LIMIT");peer.LastChat=clock();
                    room.Messages.Enqueue(new(Id(),peer.Id,peer.Nickname,c.Text!,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                    while(room.Messages.Count>32||JsonSerializer.SerializeToUtf8Bytes(room.Messages,StrictJson.Options).Length>65536)room.Messages.Dequeue();break;}
                case "ticket":case "resume":{
                    var room=Own(peer,c);Check(room,room.Members.Count);Need(room.Stream is not null,409,"STATION_MULTIPLAYER_START_REQUIRED");
                    Need(security.Mode is "rsa-pss-v1" or "ec-p256-v1"&&security.PublicKeySpki is not null,401,"STATION_REQUEST_PROOF_REQUIRED");
                    Need(Approved(c,room.Members.Count)==room.Profile,409,"STATION_MULTIPLAYER_GAME_MISMATCH");
                    Need(room.Stream!.CurrentState!=StationStreamSession.Unrecoverable,409,"STATION_MULTIPLAYER_UNRECOVERABLE");
                    var link=room.Links.SingleOrDefault(l=>l.LinkId==c.LinkId);Need(link is not null,404,"STATION_MULTIPLAYER_LINK_UNKNOWN");
                    Need(peer.Id==room.Host||peer.Id==link!.GuestPeerId,403,"STATION_MULTIPLAYER_LINK_FORBIDDEN");
                    int slot=room.Members.Single(m=>m.Peer==peer).Slot;
                    var lease=new Lease(room.Id,room.Generation,link!.LinkId,peer.Id,slot,peer.Id==room.Host,link.GuestPeerId,link.GuestSlot,Id(),identity,security.Mode,security.PublicKeySpki);
                    string key=AttachmentKey(lease);Need(!attachments.ContainsKey(key),409,"STATION_MULTIPLAYER_ALREADY_ATTACHED");
                    var grant=new Grant(Token(),lease,clock()+60000);grants[key]=grant;ticket=TicketView(room,grant);break;}
            }
            peer.Receipts.Add(c.RequestId,fingerprint);peer.ReceiptOrder.Enqueue(c.RequestId);
            while(peer.ReceiptOrder.Count>64)peer.Receipts.Remove(peer.ReceiptOrder.Dequeue());revision++;
            return View(peer,c.ItemId,ticket,blocked);
        }
    }
    private Room Find(string? id){Need(id is not null&&rooms.ContainsKey(id),404,"STATION_MULTIPLAYER_ROOM_UNKNOWN");return rooms[id!];}
    private Room Own(Peer peer,StationMultiplayerCommand c){var room=Find(c.RoomId);Need(peer.Room==room.Id&&room.Members.Any(m=>m.Peer==peer),403,"STATION_MULTIPLAYER_MEMBERSHIP_REQUIRED");Need(c.Generation==room.Generation,409,"STATION_MULTIPLAYER_GENERATION_MISMATCH");return room;}
    private void Bump(Room room){room.Generation++;int slot=1;foreach(var m in room.Members.OrderBy(m=>m.Slot)){m.Slot=slot++;m.Ready=false;}}
    private void Leave(Peer peer,Room room)
    {
        if(room.Host==peer.Id||room.Stream is not null){
            room.Stream?.RetireRoom();foreach(var m in room.Members)m.Peer.Room=null;rooms.Remove(room.Id);
            foreach(var k in grants.Where(x=>x.Value.Lease.RoomId==room.Id).Select(x=>x.Key).ToArray())grants.Remove(k);
        }else{room.Members.RemoveAll(m=>m.Peer==peer);peer.Room=null;Bump(room);}
    }
    public void Revoke(OnlineIdentity identity){lock(gate){if(peers.TryGetValue(identity,out var peer)){if(peer.Room is {} id&&rooms.TryGetValue(id,out var room))Leave(peer,room);peers.Remove(identity);revision++;}}}
    // Only called after the existing social authority accepts a human block command,
    // while AdmissionGate is held. Network loss never invokes this membership change.
    public void ApplyBlock(OnlineIdentity identity,string otherPeerId){lock(gate){if(peers.TryGetValue(identity,out var peer)&&peer.Room is {} id&&rooms.TryGetValue(id,out var room)&&room.Members.Any(m=>m.Peer.Id==otherPeerId)){Leave(peer,room);revision++;}}}
    public bool HasRoom(OnlineIdentity identity){lock(gate)return peers.TryGetValue(identity,out var p)&&p.Room is not null;}
    private object? GrantView(Peer peer,string? linkId){var grant=grants.Values.FirstOrDefault(g=>g.Lease.OwnerPeerId==peer.Id&&g.Lease.LinkId==linkId);return grant is null?null:TicketView(Find(grant.Lease.RoomId),grant);}
    private object TicketView(Room room,Grant g)=>new{path=RelayPath,protocol=Protocol,ticket=g.Token,expiresInSeconds=Math.Max(0,(g.Expires-clock())/1000),linkId=g.Lease.LinkId,ownerSlot=g.Lease.OwnerSlot,hostSide=g.Lease.HostSide,windowBytes=WindowBytes,epoch=room.Stream!.Snapshot().Epoch};
    private object View(Peer peer,string? item,object? ticket,IReadOnlySet<string>? blocked)
    {
        var own=peer.Room is {} id?rooms.GetValueOrDefault(id):null;item??=own?.Profile.ItemId;
        var matches=profiles.Values.Where(p=>p.ItemId==item&&CatalogMatches(p)).Take(32).ToArray();
        var visible=rooms.Values.Where(r=>blocked is null||r.Members.All(m=>!blocked.Contains(m.Peer.Id))).OrderBy(r=>r.Id,StringComparer.Ordinal).Take(100).ToArray();
        return new{schemaVersion=1,multiplayerVersion=3,capability="station-multiplayer.v3",selfId=peer.Id,instance,revision,serverTimeMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),status="ok",
            profiles=matches,profileCount=profiles.Count,classification=matches.Length==0?"pending":"classified",
            totalRooms=visible.Length,rooms=visible.Select(r=>RoomView(r,false)).ToArray(),
            room=own is null?null:RoomView(own,true),ticket};
    }
    private object RoomView(Room room,bool member)
    {
        var p=room.Profile;var stream=room.Stream?.Snapshot();
        string state=stream is null?"waiting":stream.Value.State switch{StationStreamSession.Playing=>"playing",StationStreamSession.Synchronizing=>"synchronizing",StationStreamSession.Unrecoverable=>"unrecoverable",_=>stream.Value.Epoch==1?"starting":"waiting-reconnect"};
        return new{id=room.Id,roomId=room.Id,itemId=p.ItemId,contentSha256=p.ContentSha256,platform=p.Platform,engineId=p.EngineId,coreSha256=p.CoreSha256,runtimeSha256=p.RuntimeSha256,
            profileId=p.ProfileId,profileSha256=p.ProfileSha256,controllerProfile=p.ControllerProfile,mode=p.Mode,allowedPlayerCounts=p.AllowedPlayerCounts,maximumPlayers=p.MaximumPlayers,capacity=room.Capacity,hostPeerId=room.Host,hostId=room.Host,
            generation=room.Generation,epoch=stream?.Epoch??0,state,status=state,transport="relay-wss-v3",recoveryProtocol=Protocol,recoveryStarted=room.Stream?.HasPlayed??false,players=room.Members.Count,
            members=room.Members.Select(m=>m.Peer.Id).ToArray(),ready=room.Members.Where(m=>m.Ready).Select(m=>m.Peer.Id).ToArray(),
            roster=room.Members.OrderBy(m=>m.Slot).Select(m=>new{peerId=m.Peer.Id,nickname=m.Peer.Nickname,slot=m.Slot,ready=m.Ready}).ToArray(),
            links=room.Links,connectionPassword=member?room.Password:null,messages=member?room.Messages.ToArray():[]};
    }
    public Lease TakeTicket(string token,Action<Lease> verify)
    {
        lock(gate){Need(StationProtocol.IsCanonicalBase64Url(token,32),401,"STATION_MULTIPLAYER_TICKET_INVALID");
            var grant=grants.Values.FirstOrDefault(g=>CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(g.Token),System.Text.Encoding.ASCII.GetBytes(token)));
            Need(grant is not null&&grant.Expires>clock(),401,"STATION_MULTIPLAYER_TICKET_INVALID");var lease=grant!.Lease;
            Need(CurrentMembership(lease),409,"STATION_MULTIPLAYER_LEASE_STALE");
            if(lease.ProofMode!="none")verify(lease);string key=AttachmentKey(lease);
            Need(!attachments.ContainsKey(key),409,"STATION_MULTIPLAYER_ALREADY_ATTACHED");grants.Remove(key);attachments.Add(key,lease.AttachmentId);return lease;
        }
    }
    private bool CurrentMembership(Lease lease)=>rooms.TryGetValue(lease.RoomId,out var r)&&r.Generation==lease.Generation&&r.Stream is not null&&r.Stream.CurrentState!=StationStreamSession.Unrecoverable&&
        r.Members.Any(m=>m.Peer.Id==lease.OwnerPeerId&&m.Peer.Identity==lease.Identity&&m.Slot==lease.OwnerSlot)&&
        r.Links.Any(l=>l.LinkId==lease.LinkId&&l.GuestPeerId==lease.GuestPeerId&&l.GuestSlot==lease.GuestSlot)&&
        (lease.HostSide?lease.OwnerPeerId==r.Host:lease.OwnerPeerId==lease.GuestPeerId);
    public bool Current(Lease lease){lock(gate)return CurrentMembership(lease)&&attachments.GetValueOrDefault(AttachmentKey(lease))==lease.AttachmentId;}
    public StationMultiplayerStream Stream(Lease lease){lock(gate){Need(Current(lease),409,"STATION_MULTIPLAYER_LEASE_STALE");return rooms[lease.RoomId].Stream!;}}
    public long PresenceAge(Lease lease){lock(gate)return peers.TryGetValue(lease.Identity,out var p)?Math.Max(0,clock()-p.Seen):long.MaxValue;}
    public void Close(Lease lease){lock(gate){string key=AttachmentKey(lease);if(attachments.GetValueOrDefault(key)==lease.AttachmentId)attachments.Remove(key);revision++;}}
}
