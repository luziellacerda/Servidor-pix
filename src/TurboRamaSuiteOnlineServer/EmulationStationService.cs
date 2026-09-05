namespace TurboRamaSuiteOnlineServer;

/// <summary>
/// Reuses the activated Suite identity and frozen signed protocol, but never the
/// Suite application's challenge/session namespace. No activation or content API
/// is exposed through this service.
/// </summary>
public sealed class EmulationStationService
{
    public const string ChallengeRoute = "/v1/suite/emulationstation/challenges";
    public const string SessionRoute = "/v1/suite/emulationstation/sessions";
    private readonly SuiteService _sessions;

    public EmulationStationService(IEmulationStationStore store,
        IAssertionSigner signer, TimeProvider time)
    {
        // The activation pepper is intentionally unavailable to this adapter.
        _sessions = new SuiteService(store, signer, time, string.Empty);
    }

    public Task<SignedAssertionEnvelope> ChallengeAsync(ChallengeRequest request,
        CancellationToken cancellationToken)
    {
        RequireSessionAction(request.Action);
        return _sessions.ChallengeAsync(request, cancellationToken);
    }

    public Task<SignedAssertionEnvelope> SessionAsync(SessionProof request,
        CancellationToken cancellationToken)
    {
        RequireSessionAction(request.Proof.Action);
        RequireSessionAction(request.Context.Action);
        return _sessions.SessionAsync(request, cancellationToken);
    }

    public static void RequireSessionAction(string action)
    {
        if (action is not ("session.open" or "session.heartbeat"))
            throw new SuiteException(400, "ACTION_INVALID",
                "Action is invalid for EmulationStation.");
    }
}

public interface IEmulationStationStore : ISuiteStore;

public static class EmulationStationEndpoints
{
    public static void MapEmulationStation(this WebApplication app, bool enabled)
    {
        Map<ChallengeRequest>(app, enabled, EmulationStationService.ChallengeRoute,
            (service, request, token) => service.ChallengeAsync(request, token));
        Map<SessionProof>(app, enabled, EmulationStationService.SessionRoute,
            (service, request, token) => service.SessionAsync(request, token));
    }

    private static void Map<T>(WebApplication app, bool enabled, string route,
        Func<EmulationStationService, T, CancellationToken,
            Task<SignedAssertionEnvelope>> operation) where T : class
    {
        app.MapPost(route, async (HttpContext context) =>
        {
            if (!enabled)
                return Results.Json(new ErrorResponse(1, "EMULATIONSTATION_DISABLED",
                    "EmulationStation licensing is disabled."), StrictJson.Options,
                    statusCode: 503);
            try
            {
                using var body = new MemoryStream();
                await context.Request.Body.CopyToAsync(body, context.RequestAborted);
                var request = StrictJson.Parse<T>(body.ToArray());
                var limiter = context.RequestServices.GetRequiredService<SuiteRateLimiter>();
                if (!limiter.Allow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        route, request))
                    return Results.Json(new ErrorResponse(1, "RATE_LIMITED",
                        "Too many requests."), StrictJson.Options, statusCode: 429);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    context.RequestAborted);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var response = await operation(context.RequestServices
                    .GetRequiredService<EmulationStationService>(), request, timeout.Token);
                return Results.Json(response, StrictJson.Options,
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
                return Results.Json(new ErrorResponse(1, "REQUEST_TIMEOUT",
                    "Request timed out."), StrictJson.Options, statusCode: 504);
            }
            catch (Exception)
            {
                app.Logger.LogError("EmulationStation licensing request failed.");
                return Results.Json(new ErrorResponse(1, "INTERNAL_ERROR",
                    "Request could not be completed."), StrictJson.Options, statusCode: 500);
            }
        }).DisableAntiforgery();
    }
}
