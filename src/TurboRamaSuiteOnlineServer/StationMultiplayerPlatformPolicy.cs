namespace TurboRamaSuiteOnlineServer;

// Maintainer limits. A platform ceiling never grants a game, mode or engine approval.
public static class StationMultiplayerPlatformPolicy
{
    public static IReadOnlyList<string> SupportedPlatforms { get; } = Array.AsReadOnly(new[]{
        "snes","megadrive","n64","dreamcast","gamecube","wii","wiiu","switch",
        "neogeo","neogeocd","psx","fbneo","cps1","cps2","cps3","ps2","saturn"});

    public static bool ServerReady(string? platform) => platform is not null && SupportedPlatforms.Contains(Normalize(platform));

    public static string Normalize(string value) => value.ToLowerInvariant() switch
    {
        "snesbr" or "super nintendo" or "super nintendo - br" => "snes",
        "megadrivebr" or "megadrive - br" => "megadrive",
        "neo geo" => "neogeo",
        "neo geo cd" => "neogeocd",
        "n64br" or "nintendo 64" or "nintendo 64 - br" => "n64",
        "playstation 1" => "psx",
        "ps2br" or "playstation 2" or "playstation 2 - br" => "ps2",
        "sega saturn" or "sega-saturn" or "segasaturn" => "saturn",
        _ => value.ToLowerInvariant()
    };

    public static int MaximumPlayers(string? platform) => platform is null ? 0 : Normalize(platform) switch
    {
        "snes" => 5,
        "dreamcast" or "n64" or "gamecube" or "wii" or "wiiu" => 4,
        _ => 2
    };
}
