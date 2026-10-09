using System.Text.Json;
using TurboRamaSuiteOnlineServer.Online;

namespace TurboRamaSuiteOnlineServer;

public static class StationOnlineRegistration
{
    public static void AddStationOnline(this WebApplicationBuilder builder,StationLibrary? library, StationLibraryMonitor? monitor = null)
    {
        if(!builder.Configuration.GetValue("Station:Online:Enabled",false))return;
        if(library is null)throw new InvalidOperationException("Online requires the Station library.");
        string? path=builder.Configuration["Station:Online:EngineRegistryFile"];
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||!File.Exists(path)||new FileInfo(path).Length>32768)
            throw new InvalidOperationException("Online requires a bounded, absolute engine registry file.");
        var engines=JsonSerializer.Deserialize<OnlineEngine[]>(File.ReadAllBytes(path),StrictJson.Options);
        if(engines is null || engines.Length is <1 or >32 || engines.Any(e=>string.IsNullOrEmpty(e.Id)||e.Id.Length>64||string.IsNullOrEmpty(e.Platform)))
            throw new InvalidOperationException("Invalid online engine registry.");
        // Index metadata only: never open a ROM merely to enter a room.
        bool recoveryEnabled=builder.Configuration.GetValue("Station:Online:RecoveryEnabled",false);
        int recoveryRooms=builder.Configuration.GetValue("Station:Online:RecoveryMaxRooms",64);
        int recoveryWindow=builder.Configuration.GetValue("Station:Online:RecoveryWindowBytes",262144);
        if(recoveryRooms is <1 or >512||recoveryWindow is <32768 or >1048576 ||
            (long)recoveryRooms*recoveryWindow*2>128L*1024*1024||engines.Any(e=>e.RecoveryProtocol is not null and not "station-stream.v2"))
            throw new InvalidOperationException("Invalid or excessive recovery bounds.");
        bool multiplayerEnabled=builder.Configuration.GetValue("Station:Online:MultiplayerEnabled",false);
        bool legacyGate=builder.Configuration.GetValue("Station:Online:MultiplayerLegacyCapacityGate",false);
        // v3 always checks its exact approved profiles. The optional legacy gate
        // can be rolled out separately without blocking existing v1/v2 clients.
        if(multiplayerEnabled&&!recoveryEnabled)throw new InvalidOperationException("Multiplayer requires recovery to be enabled.");
        if(multiplayerEnabled||legacyGate){
            string? profilePath=builder.Configuration["Station:Online:MultiplayerProfileRegistryFile"];
            if(string.IsNullOrWhiteSpace(profilePath)||!Path.IsPathFullyQualified(profilePath)||!File.Exists(profilePath)||new FileInfo(profilePath).Length>16*1024*1024)
                throw new InvalidOperationException("Multiplayer requires an explicit bounded profile registry.");
            var profiles=JsonSerializer.Deserialize<StationMultiplayerProfile[]>(File.ReadAllBytes(profilePath),StrictJson.Options)??throw new InvalidOperationException("Profile array required.");
            builder.Services.AddSingleton(new StationReplayBudget());
            builder.Services.AddSingleton(sp=>new StationMultiplayer(profiles,
                id=>{var item=(monitor?.Current??library).Catalog.FirstOrDefault(e=>e.ItemId==id);return item is null?null:Normalize(item.Platform);},
                sp.GetRequiredService<StationReplayBudget>(),
                currentContentHash:id=>(monitor?.Current??library).Catalog.FirstOrDefault(e=>e.ItemId==id)?.ContentSha256));
            builder.Services.AddSingleton<StationMultiplayerRelay>();
        }
        builder.Services.AddSingleton(sp=>new StationOnline(engines, id =>
        {
            var item = (monitor?.Current ?? library).Catalog.FirstOrDefault(e => e.ItemId == id);
            return item is null ? null : Normalize(item.Platform);
        },relayEnabled:builder.Configuration.GetValue("Station:Online:RelayEnabled",false),socialEnabled:builder.Configuration.GetValue("Station:Online:SocialEnabled",false),
            recoveryEnabled:recoveryEnabled,recoveryMaximumRooms:recoveryRooms,recoveryWindowBytes:recoveryWindow,
            trace:eventValue=>sp.GetRequiredService<ILogger<StationOnline>>().LogInformation("Station online control utc={Utc} requestId={RequestId} correlation={Correlation} generation={Generation} role={Role} action={Action} proofMode={ProofMode} hostHeartbeatAgeMs={HostAge} clientHeartbeatAgeMs={ClientAge} commandElapsedMs={ElapsedMs}",
                eventValue.Utc,eventValue.RequestId,eventValue.Correlation,eventValue.Generation,eventValue.Role,eventValue.Action,eventValue.ProofMode,eventValue.HostAgeMs,eventValue.ClientAgeMs,eventValue.ElapsedMs),
            legacyAdmission:legacyGate?(item,content,engine,core,runtime)=>sp.GetRequiredService<StationMultiplayer>().LegacyAllowed(item,content,engine,core,runtime):null,
            externalMembership:(multiplayerEnabled||legacyGate)?identity=>sp.GetRequiredService<StationMultiplayer>().HasRoom(identity):null));
        builder.Services.AddSingleton(sp=>new StationRelay(sp.GetRequiredService<StationOnline>(),
            builder.Configuration.GetValue("Station:Online:RelayMaxRooms",128),sp.GetRequiredService<ILogger<StationRelay>>()));
        builder.Services.AddSingleton<IStationOnlineAccess,StationOnlineAccess>();
        if(recoveryEnabled){
            builder.Services.AddSingleton(sp=>new StationRecoveryRelay(sp.GetRequiredService<StationOnline>(),
                sp.GetRequiredService<IStationOnlineAccess>(),sp.GetRequiredService<ILogger<StationRecoveryRelay>>(),recoveryRooms,recoveryWindow,
                builder.Configuration.GetValue("Station:Online:RecoveryDiagnosticsEnabled",true),sp.GetService<StationReplayBudget>()));
            builder.Services.AddHostedService(sp=>sp.GetRequiredService<StationRecoveryRelay>());
        }
    }
    private static string Normalize(string value)=>StationMultiplayerPlatformPolicy.Normalize(value);
}
