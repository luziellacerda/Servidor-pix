namespace TurboRamaSuiteOnlineServer;

public static class SuiteSessionEndpoints
{
public static void Map<T>(WebApplication app, bool enabled, bool emulationStationEnabled, string route, Func<SuiteService, T, CancellationToken, Task<SignedAssertionEnvelope>> action) where T : class
{
    app.MapPost(route, async (HttpContext context) =>
    {
        var sharedEs = EmulationStationScope.IsShared(context);
        if (sharedEs && !emulationStationEnabled)
            return Results.Json(new ErrorResponse(1, "EMULATIONSTATION_DISABLED",
                "EmulationStation licensing is disabled."), StrictJson.Options, statusCode: 503);
        if (!enabled) return Results.Json(new ErrorResponse(1, "SUITE_DISABLED", "Suite is disabled."), StrictJson.Options, statusCode: 503);
        try
        {
            using var memory = new MemoryStream(); await context.Request.Body.CopyToAsync(memory, context.RequestAborted);
            var request = StrictJson.Parse<T>(memory.ToArray());
            var limiter = context.RequestServices.GetRequiredService<SuiteRateLimiter>();
            if (!limiter.Allow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    sharedEs ? route + "/emulationstation" : route, request))
                return Results.Json(new ErrorResponse(1, "RATE_LIMITED", "Too many requests."), StrictJson.Options, statusCode: 429);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            SignedAssertionEnvelope response;
            if (sharedEs)
            {
                var es = context.RequestServices.GetRequiredService<SharedEmulationStationService>();
                response = request switch
                {
                    ChallengeRequest challenge => await es.ChallengeAsync(challenge, timeout.Token),
                    SessionProof proof => await es.SessionAsync(proof, timeout.Token),
                    _ => throw new SuiteException(400, "CLIENT_SCOPE_INVALID", "Client scope is invalid.")
                };
            }
            else response = await action(context.RequestServices.GetRequiredService<SuiteService>(), request, timeout.Token);
            return Results.Json(response, StrictJson.Options, contentType: "application/json; charset=utf-8");
        }
        catch (SuiteException ex) { return Results.Json(new ErrorResponse(1, ex.Code, ex.Message), StrictJson.Options, statusCode: ex.StatusCode); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(499); }
        catch (OperationCanceledException) { return Results.Json(new ErrorResponse(1, "REQUEST_TIMEOUT", "Request timed out."), StrictJson.Options, statusCode: 504); }
        catch (Exception)
        {
            // This shared endpoint also carries content actions. Do not attach an
            // exception that could contain private datastore/origin detail.
            app.Logger.LogError(
                "Suite request failed. Correlation {CorrelationId}",
                context.Response.Headers["X-Correlation-ID"].ToString());
            return Results.Json(new ErrorResponse(1, "INTERNAL_ERROR",
                "Request could not be completed."), StrictJson.Options,
                statusCode: 500);
        }
    }).DisableAntiforgery();
}

}
