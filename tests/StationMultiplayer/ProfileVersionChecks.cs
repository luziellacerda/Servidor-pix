using System.Text.Json;
using TurboRamaSuiteOnlineServer;
using TurboRamaSuiteOnlineServer.Online;

static class ProfileVersionChecks
{
    public static void Run(Action<bool,string> check)
    {
        string Hash(char value)=>new(value,64);
        var security=new StationSessionSecurity("rsa-pss-v1","synthetic-key-only");
        StationMultiplayerProfile Mode(int owner,int mode)=>new("synthetic-n64-content",Hash('a'),"n64",
            "synthetic-n64-owner-"+owner,Hash('b'),owner.ToString("x64"),
            "synthetic-n64-mode-"+mode,Hash('f'),4,true,"direct-four-ports-v1","battle-"+mode,[2,3,4]);
        var existing=Enumerable.Range(0,3).SelectMany(owner=>Enumerable.Range(0,8).Select(mode=>Mode(owner,mode))).ToArray();
        var renewed=Enumerable.Range(0,2).SelectMany(owner=>Enumerable.Range(0,8).Select(mode=>Mode(owner+3,mode))).ToArray();
        var person=new OnlineIdentity("synthetic-version-license","synthetic-version-device");
        var hub=new StationMultiplayer(existing,_=>"n64",new());
        var original=existing[0];
        var created=JsonSerializer.SerializeToElement(hub.Command(person,new("create",Guid.NewGuid().ToString(),
            ItemId:original.ItemId,ContentSha256:original.ContentSha256,EngineId:original.EngineId,
            CoreSha256:original.CoreSha256,RuntimeSha256:original.RuntimeSha256,ProfileId:original.ProfileId,
            ProfileSha256:original.ProfileSha256,Capacity:4,ClientMaximumPlayers:4),security),StrictJson.Options);
        string roomId=created.GetProperty("room").GetProperty("roomId").GetString()!;
        hub.ReplaceProfiles(existing.Concat(renewed));
        var capabilities=JsonSerializer.SerializeToElement(hub.Command(person,new("capabilities",Guid.NewGuid().ToString(),
            ItemId:original.ItemId,ClientMaximumPlayers:4),security),StrictJson.Options);
        var all=capabilities.GetProperty("profiles").EnumerateArray().ToArray();
        check(all.Length==40,"N64 immutable versions can exceed 32 total profiles without hiding modes");
        foreach(int owner in Enumerable.Range(0,5))
            check(all.Count(p=>p.GetProperty("engineId").GetString()=="synthetic-n64-owner-"+owner)==8,
                "every N64 owner's eight modes remain advertised: "+owner);
        check(capabilities.GetProperty("room").GetProperty("roomId").GetString()==roomId,
            "profile renewal preserves an existing four-player room");
        check(JsonSerializer.SerializeToUtf8Bytes(capabilities,StrictJson.Options).Length<524288,
            "complete N64 version listing fits the existing Android envelope bound");
        bool refused=false;
        try{_=new StationMultiplayer(Enumerable.Range(0,33).Select(mode=>Mode(0,mode)),_=>"n64",new());}
        catch(OnlineFailure failure){refused=failure.Code=="STATION_MULTIPLAYER_PROFILE_LIMIT";}
        check(refused,"33 modes in a single exact engine binding are still refused");
        var oversized=Enumerable.Range(0,8).Select(owner=>Mode(owner,0) with{
            Instructions=Enumerable.Repeat(new string('\u03a9',500),8).ToArray()}).ToArray();
        refused=false;
        try{hub.ReplaceProfiles(oversized);}
        catch(OnlineFailure failure){refused=failure.Code=="STATION_MULTIPLAYER_PROFILE_LIMIT";}
        check(refused,"encoded profile bytes remain bounded across engine versions");
        var after=JsonSerializer.SerializeToElement(hub.Command(person,new("capabilities",Guid.NewGuid().ToString(),
            ItemId:original.ItemId,ClientMaximumPlayers:4),security),StrictJson.Options);
        check(after.GetProperty("profiles").GetArrayLength()==40&&after.GetProperty("room").GetProperty("roomId").GetString()==roomId,
            "oversized replacement keeps the previous profiles and room intact");
    }
}
