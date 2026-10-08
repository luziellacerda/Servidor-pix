using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

internal static class CatalogChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var directory=Path.Combine(Path.GetTempPath(),"station-content-contract-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            var index=Path.Combine(directory,"index.json");
            var game=Path.Combine(directory,"game.bin");
            var cover=Path.Combine(directory,"cover.png");
            File.WriteAllBytes(game,[1,2,3,4]);File.WriteAllBytes(cover,[9,8,7]);
            var document=JsonSerializer.SerializeToNode(new {revision=1,items=new[]{new {
                itemId="synthetic-content",name="Synthetic",platform="snes",revision=1,
                coverId="synthetic-cover",filePath=game,coverPath=cover,
                metadata=new {description="Synopsis",developer="",publisher="",genre="",players="",releaseDate=""},
                artifact=new {fileName="game.bin",sizeBytes=4,sha256=new string('a',64),format="raw",launchPath="game.bin",expandedSizeBytes=4,fileCount=1}
            }}})!;
            void Save()=>File.WriteAllText(index,document.ToJsonString());
            string Wire(object value)=>JsonSerializer.Serialize(value,StrictJson.Options);
            Save();var initial=StationLibrary.TryLoad(index,verifyContent:false)!;
            var entry=initial.Catalog.Single();
            check(entry.ContentSha256 is null,"absent identity stays unknown");
            check(Wire(entry.PublicValue(false))==Wire(new {itemId=entry.ItemId,name=entry.Name,platform=entry.Platform,revision=entry.Revision,coverId=entry.CoverId,folderPath=entry.FolderPath}),"legacy catalog bytes without metadata preserved");
            check(Wire(entry.PublicValue(true))==Wire(new {itemId=entry.ItemId,name=entry.Name,platform=entry.Platform,revision=entry.Revision,coverId=entry.CoverId,metadata=entry.Metadata,folderPath=entry.FolderPath}),"legacy catalog bytes with metadata preserved");
            var payloadHash=new string('b',64);
            document["revision"]=2;document["items"]![0]!["contentSha256"]=payloadHash;Save();
            var monitor=new StationLibraryMonitor(index,StationLibrary.TryLoad(index,verifyContent:false)!,false);
            var current=monitor.Current.Catalog.Single();
            check(current.ContentSha256==payloadHash,"publisher payload identity loaded");
            check(monitor.Current.TryResolveArtifact(entry.ItemId,out var artifact)&&artifact.Descriptor.Sha256!=payloadHash,"payload and container identities remain separate");
            foreach(var withMetadata in new[]{false,true}) {
                var serialized=Wire(current.PublicValue(withMetadata));
                using var parsed=JsonDocument.Parse(serialized);
                check(parsed.RootElement.GetProperty("contentSha256").GetString()==payloadHash,"known identity serialized independently of metadata flag");
                check(parsed.RootElement.TryGetProperty("metadata",out _)==withMetadata,"metadata opt-in preserved");
                check(!serialized.Contains(directory,StringComparison.Ordinal)&&!serialized.Contains("filePath",StringComparison.Ordinal)&&!serialized.Contains("coverPath",StringComparison.Ordinal),"private paths remain private");
            }
            using var key=RSA.Create(2048);using var signer=new StationResponseSigner(key.ExportRSAPrivateKeyPem());
            using var envelope=JsonDocument.Parse(Wire(signer.Sign(new {items=new[]{current.PublicValue(true)}})));
            var signedBytes=StationProtocol.Decode(envelope.RootElement.GetProperty("payload").GetString()!,8192);
            var signature=StationProtocol.Decode(envelope.RootElement.GetProperty("signature").GetString()!,512);
            check(key.VerifyData(signedBytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"catalog identity covered by existing response signature");
            using var signedJson=JsonDocument.Parse(signedBytes);
            check(signedJson.RootElement.GetProperty("items")[0].GetProperty("contentSha256").GetString()==payloadHash,"signed identity matches publisher value");
            signedBytes[10]^=1;
            check(!key.VerifyData(signedBytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"tampered catalog rejected");
            foreach(JsonNode? bad in new JsonNode?[]{null,JsonValue.Create("invalid"),JsonValue.Create(new string('B',64)),JsonValue.Create(123)}) {
                document["revision"]=3;document["items"]![0]!["contentSha256"]=bad?.DeepClone();Save();
                bool rejected=false;
                try {monitor.Reload();}catch(InvalidOperationException){rejected=true;}
                check(rejected&&monitor.Current.Revision==2&&monitor.Current.Catalog.Single().ContentSha256==payloadHash,"invalid publisher hash retains last valid catalog");
            }
            document["revision"]=3;document["items"]![0]!.AsObject().Remove("contentSha256");Save();
            check(monitor.Reload()&&monitor.Current.Revision==3&&monitor.Current.Catalog.Single().ContentSha256 is null,"later catalog can withdraw an identity without inventing one");
            document["items"]![0]!["contentSha256"]=payloadHash;Save();
            var enginesPath=Path.Combine(directory,"engines.json");
            var profilesPath=Path.Combine(directory,"profiles.json");
            var engine=new OnlineEngine("synthetic-engine","snes",new string('c',64),new string('d',64),"station-stream.v2");
            var profile=new StationMultiplayerProfile(entry.ItemId,payloadHash,"snes",engine.Id,engine.CoreSha256,engine.RuntimeSha256,
                "synthetic-approved-mode",new string('e',64),4,true,"snes-port2-multitap-v1","battle",[2,3,4]);
            File.WriteAllText(enginesPath,Wire(new[]{engine}));File.WriteAllText(profilesPath,Wire(new[]{profile}));
            foreach(bool gated in new[]{false,true}){
                var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
                builder.Configuration["Station:Online:Enabled"]="true";
                builder.Configuration["Station:Online:EngineRegistryFile"]=enginesPath;
                builder.Configuration["Station:Online:RecoveryEnabled"]="true";
                builder.Configuration["Station:Online:RelayEnabled"]="true";
                builder.Configuration["Station:Online:MultiplayerEnabled"]="true";
                builder.Configuration["Station:Online:MultiplayerLegacyCapacityGate"]=gated.ToString();
                builder.Configuration["Station:Online:MultiplayerProfileRegistryFile"]=profilesPath;
                builder.AddStationOnline(StationLibrary.TryLoad(index,verifyContent:false)!);
                using var services=builder.Services.BuildServiceProvider();
                var legacy=services.GetRequiredService<StationOnline>();
                var person=new OnlineIdentity("synthetic-registration","synthetic-registration");
                legacy.Command(person,new("enter",Guid.NewGuid().ToString(),Nickname:"Registration"));
                var create=new OnlineCommand("create",Guid.NewGuid().ToString(),ItemId:entry.ItemId,EngineId:engine.Id,
                    ContentSha256:payloadHash,OptionsSha256:new string('f',64),CoreSha256:engine.CoreSha256,RuntimeSha256:engine.RuntimeSha256,RecoveryProtocol:"station-stream.v2");
                if(gated){
                    bool denied=false;
                    try{legacy.Command(person,create);}catch(OnlineFailure e){denied=e.Code=="STATION_MULTIPLAYER_PROFILE_REQUIRED";}
                    check(denied,"explicit legacy gate still rejects unqualified legacy mode");
                }else{
                    var created=JsonSerializer.SerializeToElement(legacy.Command(person,create),StrictJson.Options);
                    check(created.GetProperty("room").ValueKind==JsonValueKind.Object,"enabling v3 without gate preserves legacy admission");
                    legacy.Command(person,new("leave",Guid.NewGuid().ToString()));
                }
                var multi=services.GetRequiredService<StationMultiplayer>();
                var command=new StationMultiplayerCommand("create",Guid.NewGuid().ToString(),ItemId:profile.ItemId,ContentSha256:profile.ContentSha256,
                    EngineId:profile.EngineId,CoreSha256:profile.CoreSha256,RuntimeSha256:profile.RuntimeSha256,ProfileId:profile.ProfileId,ProfileSha256:profile.ProfileSha256,Capacity:4);
                var security=new StationSessionSecurity("rsa-pss-v1","synthetic-key-only");
                bool wrongProfileDenied=false;
                try{multi.Command(person,command with{ProfileSha256=new string('f',64)},security);}catch(OnlineFailure e){wrongProfileDenied=e.Code=="STATION_MULTIPLAYER_PROFILE_UNAPPROVED";}
                check(wrongProfileDenied,"optional legacy gate does not weaken exact v3 profile admission");
                check(JsonSerializer.SerializeToElement(multi.Command(person,command,security),StrictJson.Options).GetProperty("room").GetProperty("capacity").GetInt32()==4,"actual registration admits approved v3 mode");
                check(JsonSerializer.SerializeToElement(multi.Snapshot(),StrictJson.Options).GetProperty("activeRooms").GetInt32()==1,"operator telemetry sees v3 rooms before a restart");
                multi.Command(person,command with{Action="leave",RequestId=Guid.NewGuid().ToString(),RoomId=JsonSerializer.SerializeToElement(multi.Command(person,command,security),StrictJson.Options).GetProperty("room").GetProperty("roomId").GetString(),Generation=1},security);
                check(JsonSerializer.SerializeToElement(multi.Snapshot(),StrictJson.Options).GetProperty("activeRooms").GetInt32()==0,"operator telemetry clears ended v3 room");
            }
            File.Delete(game);
            check(JsonSerializer.SerializeToElement(current.PublicValue(true),StrictJson.Options).GetProperty("contentSha256").GetString()==payloadHash,"catalog serialization performs no ROM read");
        } finally {Directory.Delete(directory,true);}
    }
}
