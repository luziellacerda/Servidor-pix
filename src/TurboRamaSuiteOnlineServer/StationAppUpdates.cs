using System.Security.Cryptography;
using System.Text.Json;

namespace TurboRamaSuiteOnlineServer;

// Uses the existing Station session, request-proof middleware, signer, encrypted
// grant store and atomic one-use consumption. No directory is exposed by HTTP.
public sealed record StationAppUpdateRequest(int SchemaVersion, string Domain,
    string ProductId, string ApplicationId, string DeviceId, string RequestId,
    string PackageName, long VersionCode, int AndroidSdk, string Abi,
    string CertificateSha256, string ReleaseId = "");

public sealed record StationAppRelease(int SchemaVersion, string PackageName,
    long VersionCode, string VersionName, string ReleaseId, string Sha256,
    long SizeBytes, string CertificateSha256, int MinimumSdk, string Abi,
    string Notes);

public sealed record StationAppUpdateFile(StationAppRelease Release, string Path,
    long LastWriteTicks);

public sealed class StationAppUpdateCatalog(string? directory, bool enabled)
{
    public const string Package = "org.turboramastation.frontend";
    public const long MaximumApkBytes = 8L * 1024 * 1024 * 1024;
    public bool Enabled => enabled && !string.IsNullOrWhiteSpace(directory);

    public static bool Hex(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static void Validate(StationAppRelease r)
    {
        if (r.SchemaVersion != 1 || r.PackageName != Package ||
            r.VersionCode is < 1 or > 2100000000 ||
            string.IsNullOrWhiteSpace(r.VersionName) || r.VersionName.Length > 100 ||
            r.VersionName.Any(char.IsControl) || !Hex(r.Sha256) || r.ReleaseId != r.Sha256 ||
            r.SizeBytes is < 1024 or > MaximumApkBytes || !Hex(r.CertificateSha256) ||
            r.MinimumSdk is < 26 or > 1000 || r.Abi != "arm64-v8a" ||
            r.Notes is null || r.Notes.Length > 2000 || r.Notes.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
            throw new InvalidDataException("Invalid Station app release.");
    }

    public static void RequireRegularPath(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        for (var current = full; !string.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Station update links are not allowed.");
    }

    public StationAppUpdateFile? Read()
    {
        if (!Enabled) return null;
        try
        {
            var root = System.IO.Path.GetFullPath(directory!);
            var index = System.IO.Path.Combine(root, "current.json");
            RequireRegularPath(index);
            if (!File.Exists(index)) return null;
            var info = new FileInfo(index);
            if (info.Length is < 2 or > 16384) throw new InvalidDataException();
            var bytes = File.ReadAllBytes(index);
            if (bytes.Length > 16384) throw new InvalidDataException();
            using var parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            StationProtocol.RejectDuplicates(parsed.RootElement);
            var release = JsonSerializer.Deserialize<StationAppRelease>(bytes, StrictJson.Options) ?? throw new InvalidDataException();
            Validate(release);
            var path = System.IO.Path.Combine(root, "apks", release.Sha256 + ".apk");
            RequireRegularPath(path);
            var apk = new FileInfo(path);
            if (!apk.Exists || apk.Length != release.SizeBytes) throw new InvalidDataException();
            return new(release, path, apk.LastWriteTimeUtc.Ticks);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or SuiteException or ArgumentException)
        {
            throw new SuiteException(503, "STATION_UPDATE_NOT_READY", "Station app update is not ready.");
        }
    }
}

public interface IStationAppUpdateAccess
{
    Task<StationSession> SessionAsync(string bearer, CancellationToken token);
    Task InsertAsync(StationGrantRecord grant, int seconds, CancellationToken token);
    Task<StationGrantRecord?> PeekAsync(string id, CancellationToken token);
    Task<StationGrantRecord?> ConsumeAsync(string id, string license, string device, CancellationToken token);
}

public sealed class StationAppUpdateAccess(StationService service, PostgresStationStore store) : IStationAppUpdateAccess
{
    public Task<StationSession> SessionAsync(string bearer, CancellationToken token) => service.Session(bearer, token);
    public Task InsertAsync(StationGrantRecord grant, int seconds, CancellationToken token) => store.InsertGrantAsync(grant, seconds, token);
    public Task<StationGrantRecord?> PeekAsync(string id, CancellationToken token) => store.PeekGrantAsync(id, token);
    public Task<StationGrantRecord?> ConsumeAsync(string id, string license, string device, CancellationToken token) => store.ConsumeGrantAsync(id, license, device, token);
}

public sealed class StationAppUpdates(StationAppUpdateCatalog catalog,
    IStationAppUpdateAccess access, StationResponseSigner signer, StationGrantCipher? cipher)
{
    public const string Root = "/v1/station/updates";
    public const int GrantSeconds = 60;
    private sealed record Bound(string ReleaseId, long VersionCode, long Bytes, long Ticks);
    private static SuiteException Missing() => new(404, "STATION_GRANT_NOT_FOUND", "Station grant is not found.");
    private static string Item(string release) => "update_" + release[..57];
    private static string Associated(StationSession s, string item, string grant) =>
        "station-app-update/v1\n" + s.LicenseId + "\n" + s.DeviceId + "\n" + s.SessionId + "\n" + item + "\n" + grant;

    private static void Request(StationAppUpdateRequest r, StationSession s, bool authorize)
    {
        StationProtocol.RequireIdentity(r.SchemaVersion, r.Domain,
            authorize ? "request-app-update-download" : "request-app-update-check",
            r.ProductId, r.ApplicationId, r.DeviceId);
        if (r.DeviceId != s.DeviceId) throw new SuiteException(403, "STATION_DEVICE_DENIED", "Device is not authorized.");
        if (r.RequestId is null || r.RequestId.Length != 32 ||
            !r.RequestId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            r.PackageName != StationAppUpdateCatalog.Package || r.VersionCode is < 1 or > 2100000000 ||
            r.AndroidSdk is < 21 or > 1000 || r.Abi is null || r.Abi.Length > 32 ||
            !StationAppUpdateCatalog.Hex(r.CertificateSha256) ||
            authorize && !StationAppUpdateCatalog.Hex(r.ReleaseId) || !authorize && r.ReleaseId != "")
            throw new SuiteException(400, "STATION_UPDATE_REQUEST_INVALID", "Station app update request is invalid.");
    }

    private static bool Eligible(StationAppRelease release, StationAppUpdateRequest r) =>
        release.VersionCode > r.VersionCode && release.MinimumSdk <= r.AndroidSdk &&
        release.Abi == r.Abi && release.CertificateSha256 == r.CertificateSha256;

    public async Task<object> CheckAsync(string bearer, StationAppUpdateRequest request, CancellationToken token)
    {
        var session = await access.SessionAsync(bearer, token);
        Request(request, session, false);
        var current = catalog.Read();
        var release = current is not null && Eligible(current.Release, request) ? current.Release : null;
        return signer.Sign(new { schemaVersion = 1, domain = StationProtocol.Prefix + "app-update/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            session.LicenseId, session.DeviceId, session.SessionId, request.RequestId,
            expiresInSeconds = GrantSeconds, available = release is not null, release });
    }

    public async Task<object> AuthorizeAsync(string bearer, StationAppUpdateRequest request, CancellationToken token)
    {
        var session = await access.SessionAsync(bearer, token);
        Request(request, session, true);
        var file = catalog.Read();
        if (file is null || cipher is null) throw new SuiteException(503, "STATION_UPDATE_NOT_READY", "Station app update is not ready.");
        if (file.Release.ReleaseId != request.ReleaseId || !Eligible(file.Release, request))
            throw new SuiteException(409, "STATION_UPDATE_CHANGED", "Refresh the Station app update.");
        var id = StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
        var item = Item(file.Release.ReleaseId);
        var body = JsonSerializer.Serialize(new Bound(file.Release.ReleaseId, file.Release.VersionCode,
            file.Release.SizeBytes, file.LastWriteTicks), StrictJson.Options);
        var sealedBody = cipher.Seal(Associated(session, item, id), body);
        await access.InsertAsync(new(id, session.LicenseId, session.DeviceId, item, cipher.KeyVersion,
            sealedBody.Nonce, sealedBody.Ciphertext, sealedBody.Tag), GrantSeconds, token);
        return signer.Sign(new { schemaVersion = 1, domain = StationProtocol.Prefix + "app-update-grant/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            session.LicenseId, session.DeviceId, session.SessionId, request.RequestId,
            expiresInSeconds = GrantSeconds, grantId = id, release = file.Release });
    }

    public async Task<StationAppUpdateFile> ConsumeAsync(string bearer, string grantId, CancellationToken token)
    {
        if (cipher is null || !StationProtocol.IsCanonicalBase64Url(grantId, 32)) throw Missing();
        StationSession session;
        try { session = await access.SessionAsync(bearer, token); }
        catch (SuiteException) { throw Missing(); }
        var peek = await access.PeekAsync(grantId, token);
        if (peek is null || peek.LicenseId != session.LicenseId || peek.DeviceId != session.DeviceId ||
            !peek.ItemId.StartsWith("update_", StringComparison.Ordinal) || peek.KeyVersion != cipher.KeyVersion) throw Missing();
        try
        {
            var bound = JsonSerializer.Deserialize<Bound>(cipher.Open(Associated(session, peek.ItemId, grantId),
                peek.Nonce, peek.Ciphertext, peek.Tag), StrictJson.Options);
            var file = catalog.Read();
            if (file is null || bound is null || bound.ReleaseId != file.Release.ReleaseId ||
                peek.ItemId != Item(bound.ReleaseId) || bound.VersionCode != file.Release.VersionCode ||
                bound.Bytes != file.Release.SizeBytes || bound.Ticks != file.LastWriteTicks) throw Missing();
            if (await access.ConsumeAsync(grantId, session.LicenseId, session.DeviceId, token) is null) throw Missing();
            return file;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { throw Missing(); }
    }
}
