using System.Net;
using TurboRamaSuiteOnlineServer;

namespace TurboRamaSuiteOnlineServer;

public static class StationEndpoints
{
    public static void MapStation(this WebApplication app, bool enabled)
    {
        var limiter = new StationRateLimiter();
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
        });
        app.MapGet("/v1/station/catalog", async (HttpContext context) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress, "/v1/station/catalog"))
                return Limited();
            return await Handle(context, (service, token) =>
                service.CatalogAsync(Bearer(context), token));
        });
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
                    .CoverAsync(Bearer(context), coverId, timeout.Token);
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                return Results.File(cover.Bytes, cover.ContentType);
            }
            catch (SuiteException exception)
            {
                return Results.Json(new ErrorResponse(1, exception.Code, exception.Message),
                    StrictJson.Options, statusCode: exception.StatusCode);
            }
        });
        app.MapPost("/v1/station/downloads/authorize", async (HttpContext context) =>
        {
            if (!enabled) return Disabled();
            if (!limiter.Allow(context.Connection.RemoteIpAddress,
                    "/v1/station/downloads/authorize")) return Limited();
            return await Handle(context, async (service, token) =>
                await service.AuthorizeDownloadAsync(
                    await Read<StationDownloadRequest>(context, token),
                    Bearer(context), token));
        });
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
                var path = await context.RequestServices.GetRequiredService<StationService>()
                    .ConsumeArtifactPathAsync(grantId, bearer, context.RequestAborted);
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.ContentType = "application/octet-stream";
                await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                context.Response.ContentLength = file.Length;
                context.Response.StatusCode = 200;
                await file.CopyToAsync(context.Response.Body, context.RequestAborted);
                return Results.Empty;
            }
            catch (SuiteException exception)
            {
                return Results.Json(new ErrorResponse(1, exception.Code, exception.Message),
                    StrictJson.Options, statusCode: exception.StatusCode);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
        });
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
        if (context.Request.ContentLength is > StationProtocol.MaximumBodyBytes)
            throw new SuiteException(413, "STATION_BODY_INVALID",
                "Station request body is invalid.");
        using var memory = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(chunk, token);
            if (read == 0) break;
            if (memory.Length + read > StationProtocol.MaximumBodyBytes)
                throw new SuiteException(413, "STATION_BODY_INVALID",
                    "Station request body is invalid.");
            memory.Write(chunk, 0, read);
        }
        if (memory.Length is 0 or > StationProtocol.MaximumBodyBytes)
            throw new SuiteException(413, "STATION_BODY_INVALID",
                "Station request body is invalid.");
        return StrictJson.Parse<T>(memory.ToArray());
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

internal sealed class StationRateLimiter
{
    private readonly object _sync = new();
    private readonly Dictionary<string, (long Minute, int Count)> _windows = new();

    public bool Allow(IPAddress? address, string route)
    {
        var origin = address?.ToString() ?? "unknown";
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var key = origin + "\0" + route;
        lock (_sync)
        {
            if (_windows.TryGetValue(key, out var window) && window.Minute == minute)
            {
                _windows[key] = (minute, window.Count + 1);
                return window.Count < 30;
            }
            if (_windows.Count >= 4096)
            {
                foreach (var stale in _windows.Where(pair => pair.Value.Minute != minute)
                             .Select(pair => pair.Key).ToArray()) _windows.Remove(stale);
                if (_windows.Count >= 4096) return false;
            }
            _windows[key] = (minute, 1);
            return true;
        }
    }
}
