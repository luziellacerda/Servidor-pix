using System.Net;
using System.Security.Cryptography;
using System.Text;
using TurboRamaSuiteOnlineServer;

namespace TurboRamaSuiteOnlineServer;

public static class StationEndpoints
{
    public static void MapStation(this WebApplication app, bool enabled)
    {
        var limiter = new StationRateLimiter(originRequestsPerMinute:
            app.Configuration.GetValue("Station:OriginRequestsPerMinute",30),
            maximumWindows:app.Configuration.GetValue("Station:MaximumRateWindows",4096));
        Post<StationActivationChallengeRequest>(app, enabled, limiter,
            "/v1/station/activations/challenge",
            (service, request, token) => service.ActivationChallengeAsync(request, token));
        Post<StationDeviceEnvelope>(app, enabled, limiter,
            "/v1/station/activations/complete",
            (service, request, token) => service.CompleteActivationAsync(request, token));
        Post<StationSessionChallengeRequest>(app, enabled, limiter,
            "/v1/station/challenges",
            (service, request, token) => service.SessionChallengeAsync(request, token));
        Post<StationDeviceEnvelope>(app, enabled, limiter,
            "/v1/station/sessions",
            (service, request, token) => service.OpenSessionAsync(request, token));
        app.MapGet("/v1/station/me", async (HttpContext context) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "/v1/station/me"))
                return Limited();
            return await Handle(context, (service, token) =>
                service.ProfileAsync(Bearer(context), token));
        }).TraceStation("profile");
        app.MapGet("/v1/station/catalog", async (HttpContext context) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "/v1/station/catalog"))
                return Limited();
            return await Handle(context, (service, token) =>
                service.CatalogAsync(Bearer(context), token, context.Request.Query["metadata"] == "1"));
        }).TraceStation("catalog");
        app.MapGet("/v1/station/covers/{coverId}", async (HttpContext context, string coverId) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "/v1/station/covers"))
                return Limited();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var cover = await context.RequestServices.GetRequiredService<StationService>()
                    .CoverAsync(Bearer(context), coverId, timeout.Token,
                        session => limiter.AllowCoverDevice(session.LicenseId, session.DeviceId));
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return Results.File(cover.Bytes, cover.ContentType);
            }
            catch (SuiteException exception)
            {
                return Results.Json(new ErrorResponse(1, exception.Code, exception.Message),
                    StrictJson.Options, statusCode: exception.StatusCode);
            }
        }).TraceStation("cover");
        app.MapPost("/v1/station/downloads/authorize", async (HttpContext context) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress,
                    "/v1/station/downloads/authorize")) return Limited();
            return await Handle(context, async (service, token) =>
            {
                var request = await Read<StationDownloadRequest>(context, token);
                StationRequestDiagnostics.SelectedItem(context, request.ItemId);
                return await service.AuthorizeDownloadAsync(request, Bearer(context), token);
            });
        }).TraceStation("authorize");
        app.MapGet("/v1/station/artifacts/{grantId}", async (HttpContext context, string grantId) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "/v1/station/artifacts"))
                return Limited();
            try
            {
                var header = context.Request.Headers.Authorization.ToString();
                string? bearer = header.StartsWith("Bearer ", StringComparison.Ordinal) &&
                    header.Length <= 128 ? header[7..] : null;
                var artifact = await context.RequestServices.GetRequiredService<StationService>()
                    .ConsumeArtifactAsync(grantId, bearer, context.RequestAborted);
                StationRequestDiagnostics.SelectedItem(context, artifact.Entry.ItemId);
                await using var file = new FileStream(artifact.FilePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read, 64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (file.Length != artifact.Descriptor.SizeBytes || !artifact.MatchesFile())
                    throw new SuiteException(404, "STATION_GRANT_NOT_FOUND",
                        "Station grant is not found.");
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.ContentType = "application/octet-stream";
                context.Response.ContentLength = artifact.Descriptor.SizeBytes;
                context.Response.StatusCode = 200;
                await file.CopyToAsync(context.Response.Body, context.RequestAborted);
                return Results.Empty;
            }
            catch (SuiteException exception)
            {
                return Results.Json(new ErrorResponse(1, exception.Code, exception.Message),
                    StrictJson.Options, statusCode: exception.StatusCode);
            }
            catch (IOException) when (!context.Response.HasStarted)
            {
                return Results.Json(new ErrorResponse(1, "STATION_GRANT_NOT_FOUND",
                    "Station grant is not found."), StrictJson.Options, statusCode: 404);
            }
            catch (UnauthorizedAccessException) when (!context.Response.HasStarted)
            {
                return Results.Json(new ErrorResponse(1, "STATION_GRANT_NOT_FOUND",
                    "Station grant is not found."), StrictJson.Options, statusCode: 404);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
        }).TraceStation("artifact");
    }

    private static void Post<T>(WebApplication app, bool enabled,
        StationRateLimiter limiter, string route,
        Func<StationService, T, CancellationToken, Task<object>> action) where T : class
    {
        app.MapPost(route, async (HttpContext context) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress, route))
                return Limited();
            return await Handle(context, async (service, token) =>
                await action(service, await Read<T>(context, token), token));
        }).DisableAntiforgery();
    }

    private static async Task<T> Read<T>(HttpContext context,
        CancellationToken token) where T : class
    {
        var maximum = typeof(T) == typeof(StationDeviceEnvelope) ? 64 * 1024 : StationProtocol.MaximumBodyBytes;
        if (context.Request.ContentLength > maximum)
            throw new SuiteException(413, "STATION_BODY_INVALID",
                "Station request body is invalid.");
        using var memory = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(chunk, token);
            if (read == 0) break;
            if (memory.Length + read > maximum)
                throw new SuiteException(413, "STATION_BODY_INVALID",
                    "Station request body is invalid.");
            memory.Write(chunk, 0, read);
        }
        if (memory.Length == 0 || memory.Length > maximum)
            throw new SuiteException(413, "STATION_BODY_INVALID",
                "Station request body is invalid.");
        if (typeof(T) != typeof(StationDeviceEnvelope)) return StrictJson.Parse<T>(memory.ToArray());
        // Station-only optional fields; Suite's required-member contract is unchanged.
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(memory.ToArray(),
                new System.Text.Json.JsonDocumentOptions { MaxDepth = 16 });
            StationProtocol.RejectDuplicates(json.RootElement);
            var envelope = System.Text.Json.JsonSerializer.Deserialize<StationDeviceEnvelope>(
                json.RootElement, StrictJson.Options);
            if (envelope?.Payload is null || envelope.Signature is null) throw new System.Text.Json.JsonException();
            return (T)(object)envelope;
        }
        catch (System.Text.Json.JsonException ex)
        { throw new SuiteException(400, "JSON_INVALID", "Request JSON is invalid.", ex); }
    }

    private static async Task<IResult> Handle(HttpContext context,
        Func<StationService, CancellationToken, Task<object>> action)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await action(context.RequestServices
                .GetRequiredService<StationService>(), timeout.Token);
            return Results.Json(result, StrictJson.Options,
                contentType: "application/json; charset=utf-8");
        }
        catch (SuiteException exception)
        {
            return Results.Json(new ErrorResponse(1, exception.Code, exception.Message),
                StrictJson.Options, statusCode: exception.StatusCode);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return Results.StatusCode(499);
        }
        catch (OperationCanceledException)
        {
            return Results.Json(new ErrorResponse(1, "STATION_TIMEOUT", "Request timed out."),
                StrictJson.Options, statusCode: 504);
        }
        catch (Exception)
        {
            context.RequestServices.GetRequiredService<ILogger<StationService>>()
                .LogError("Station request failed. Correlation {CorrelationId}",
                    context.Response.Headers["X-Correlation-ID"].ToString());
            return Results.Json(new ErrorResponse(1, "STATION_INTERNAL_ERROR",
                "Station request could not be completed."), StrictJson.Options,
                statusCode: 500);
        }
    }

    private static string Bearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal) ||
            header.Length > 128)
            throw new SuiteException(401, "STATION_SESSION_INVALID",
                "Station session is invalid.");
        return header[7..];
    }

    private static IResult Disabled() => Results.Json(new ErrorResponse(1,
        "STATION_DISABLED", "Station Android is disabled."), StrictJson.Options,
        statusCode: 503);
    private static IResult Limited() => Results.Json(new ErrorResponse(1,
        "STATION_RATE_LIMITED", "Too many requests."), StrictJson.Options,
        statusCode: 429);
}

public sealed class StationRateLimiter
{
    public const int CoverDeviceRequestsPerMinute = StationLibrary.MaximumItems;
    public const int CoverOriginRequestsPerMinute = CoverDeviceRequestsPerMinute * 4;
    private readonly object _sync = new();
    private readonly Dictionary<string, (long Minute, int Count)> _windows = new();
    private readonly TimeProvider _clock;
    private readonly int originMaximum;
    private readonly int maximumWindows;

    public StationRateLimiter(TimeProvider? clock = null, int originRequestsPerMinute = 30,
        int maximumWindows = 4096)
    {
        _clock = clock ?? TimeProvider.System;
        originMaximum = originRequestsPerMinute is >=30 and <=65536
            ? originRequestsPerMinute : throw new ArgumentOutOfRangeException(nameof(originRequestsPerMinute));
        this.maximumWindows = maximumWindows is >=4096 and <=131072
            ? maximumWindows : throw new ArgumentOutOfRangeException(nameof(maximumWindows));
    }

    public bool Allow(IPAddress? address, string route)
    {
        var origin = address?.ToString() ?? "unknown";
        var key = origin + "\0" + route;
        return AllowWindow(key, route == "/v1/station/covers" ? CoverOriginRequestsPerMinute : originMaximum);
    }

    // Bound to the authenticated device rather than its temporary session or shared NAT.
    public bool AllowCoverDevice(string licenseId, string deviceId)
    {
        if (licenseId.Length is < 6 or > 64 ||
            licenseId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) ||
            !StationProtocol.IsCanonicalBase64Url(deviceId, 32))
            return false;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(licenseId + "\n" + deviceId)));
        return AllowWindow("cover-device\0" + identity, CoverDeviceRequestsPerMinute);
    }

    private bool AllowWindow(string key, int maximum)
    {
        var minute = _clock.GetUtcNow().ToUnixTimeSeconds() / 60;
        lock (_sync)
        {
            if (_windows.TryGetValue(key, out var window) && window.Minute == minute)
            {
                if (window.Count >= maximum) return false;
                _windows[key] = (minute, window.Count + 1);
                return true;
            }
            if (_windows.Count >= maximumWindows)
            {
                foreach (var stale in _windows.Where(pair => pair.Value.Minute != minute)
                             .Select(pair => pair.Key).ToArray()) _windows.Remove(stale);
                if (_windows.Count >= maximumWindows) return false;
            }
            _windows[key] = (minute, 1);
            return true;
        }
    }
}
