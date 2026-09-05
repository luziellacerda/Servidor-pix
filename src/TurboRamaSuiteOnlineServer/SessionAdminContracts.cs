namespace TurboRamaSuite.Management;

public sealed record SessionQuery(string[] LicenseIds);
public sealed record ManagedSession(string LicenseId, string DeviceId, string SessionId,
    string AppScope, string State, long LastContactAtUnixSeconds, long AuthorizedUntilUnixSeconds);
public sealed record ManagedSessions(ManagedSession[] Sessions);
public sealed record MaskedNetworkInterface(string Mac, string InterfaceType, bool LocallyAdministered, bool Virtual);
public sealed record ManagedNetworkReport(string LicenseId, string DeviceId, string AppScope,
    string SessionId, string IpMasked, MaskedNetworkInterface[] Interfaces,
    long CollectedAtUnixSeconds, long ReceivedAtUnixSeconds);
public sealed record ManagedNetworkReports(ManagedNetworkReport[] Reports);
public sealed record RevokeEsSessionRequest(string LicenseId, string DeviceId, string AppScope,
    string TargetSessionId, string ExpectedSessionId, string RequestId);
public sealed record RevokeEsSessionResult(string Code);

public static class SessionManagementPermissions
{
    public const string Read = "suite.sessions.read";
    public const string Revoke = "suite.sessions.revoke";
    public const string NetworkRead = "suite.network.read";
}
