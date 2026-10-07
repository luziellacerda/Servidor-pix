namespace TurboRamaSuiteOnlineServer;
public sealed record StationSessionSecurity(string Mode,string? PublicKeySpki)
{
    public static readonly StationSessionSecurity Legacy=new("none",null);
}
