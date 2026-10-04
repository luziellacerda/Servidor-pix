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
        builder.Services.AddSingleton(new StationOnline(engines, id =>
        {
            var item = (monitor?.Current ?? library).Catalog.FirstOrDefault(e => e.ItemId == id);
            return item is null ? null : Normalize(item.Platform);
        }));
        builder.Services.AddSingleton<IStationOnlineAccess,StationOnlineAccess>();
    }
    private static string Normalize(string value)=>value.ToLowerInvariant() switch {
        "snesbr" or "super nintendo" or "super nintendo - br"=>"snes",
        "megadrivebr" or "megadrive - br"=>"megadrive",
        "neo geo"=>"neogeo", _=>value.ToLowerInvariant()
    };
}
