namespace TurboRamaSuiteOnlineServer;

public static class StationAppUpdateEndpoints
{
    public static void MapStationAppUpdates(this WebApplication app, bool enabled)
    {
        var limiter = new StationRateLimiter(originRequestsPerMinute:
            Math.Clamp(app.Configuration.GetValue("Station:Updates:OriginRequestsPerMinute", 300), 30, 3000));
        foreach (var authorize in new[] { false, true })
        {
            var route = StationAppUpdates.Root + (authorize ? "/authorize" : "/check");
            app.MapPost(route, async (HttpContext context) =>
            {
                NoStore(context);
                try
                {
                    if (!enabled) throw new SuiteException(503, "STATION_DISABLED", "Station is disabled.");
                    if (context.Request.QueryString.HasValue)
                        throw new SuiteException(400, "STATION_UPDATE_REQUEST_INVALID", "Station app update request is invalid.");
                    if (!limiter.Allow(context.Connection.RemoteIpAddress, route))
                        throw new SuiteException(429, "STATION_RATE_LIMITED", "Too many requests.");
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    using var bytes = new MemoryStream();
                    var buffer = new byte[2048];
                    int count;
                    while ((count = await context.Request.Body.ReadAsync(buffer, timeout.Token)) != 0)
                    {
                        if (bytes.Length + count > StationProtocol.MaximumBodyBytes)
                            throw new SuiteException(413, "STATION_BODY_INVALID", "Station request body is invalid.");
                        bytes.Write(buffer, 0, count);
                    }
                    var request = StrictJson.Parse<StationAppUpdateRequest>(bytes.ToArray());
                    var service = context.RequestServices.GetRequiredService<StationAppUpdates>();
                    var result = authorize ? await service.AuthorizeAsync(Bearer(context), request, timeout.Token)
                        : await service.CheckAsync(Bearer(context), request, timeout.Token);
                    return Results.Json(result, StrictJson.Options);
                }
                catch (SuiteException ex) { return Error(ex); }
                catch (OperationCanceledException) { return Results.StatusCode(context.RequestAborted.IsCancellationRequested ? 499 : 504); }
            }).DisableAntiforgery().TraceStation(authorize ? "update-authorize" : "update-check");
        }
        app.MapGet(StationAppUpdates.Root + "/artifacts/{grantId}", async (HttpContext context, string grantId) =>
        {
            NoStore(context);
            try
            {
                if (!enabled) throw new SuiteException(503, "STATION_DISABLED", "Station is disabled.");
                if (!limiter.Allow(context.Connection.RemoteIpAddress, StationAppUpdates.Root + "/artifacts"))
                    throw new SuiteException(429, "STATION_RATE_LIMITED", "Too many requests.");
                // A grant authorizes one complete response. A failed/cancelled
                // transfer must request a fresh grant; no implicit Range retry.
                if (context.Request.QueryString.HasValue || context.Request.Headers.ContainsKey("Range"))
                    throw new SuiteException(400, "STATION_UPDATE_REQUEST_INVALID", "Station app update request is invalid.");
                var file = await context.RequestServices.GetRequiredService<StationAppUpdates>()
                    .ConsumeAsync(Bearer(context), grantId, context.RequestAborted);
                await using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (stream.Length != file.Release.SizeBytes || File.GetLastWriteTimeUtc(file.Path).Ticks != file.LastWriteTicks)
                    throw new SuiteException(404, "STATION_GRANT_NOT_FOUND", "Station grant is not found.");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/vnd.android.package-archive";
                context.Response.ContentLength = file.Release.SizeBytes;
                await stream.CopyToAsync(context.Response.Body, 256 * 1024, context.RequestAborted);
                return Results.Empty;
            }
            catch (SuiteException ex) when (!context.Response.HasStarted) { return Error(ex); }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && !context.Response.HasStarted)
            { return Error(new SuiteException(404, "STATION_GRANT_NOT_FOUND", "Station grant is not found.")); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.Empty; }
        }).TraceStation("update-artifact");
    }

    private static string Bearer(HttpContext context)
    {
        var value = context.Request.Headers.Authorization.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.Ordinal) || value.Length > 128)
            throw new SuiteException(401, "STATION_SESSION_INVALID", "Station session is invalid.");
        return value[7..];
    }
    private static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    private static IResult Error(SuiteException ex) => Results.Json(new ErrorResponse(1, ex.Code, ex.Message),
        StrictJson.Options, statusCode: ex.StatusCode);
}
