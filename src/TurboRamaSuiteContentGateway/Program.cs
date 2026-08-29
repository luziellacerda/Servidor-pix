using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpOverrides;
using TurboRamaSuiteContentGateway;
using TurboRamaSuiteOnlineServer;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 4096);
builder.Services.Configure<ForwardedHeadersOptions>(SuiteTrustedProxyPolicy.Configure);
var enabled = builder.Configuration.GetValue("Suite:Content:Enabled", false);
var connection = ReadContentProtected("ConnectionStrings:SuiteContentGatewayStore",
    "ConnectionStrings:SuiteContentGatewayStoreFile", 4096);
var tokenPepper = ReadContentProtected("Suite:ContentGrantTokenPepper",
    "Suite:ContentGrantTokenPepperFile", 1024, "content-grant-token-pepper");
var keyRingPath = builder.Configuration["Suite:ContentUrlKeyRingFile"];
var allowedHosts = ReadAllowedHosts();

if (enabled && (string.IsNullOrWhiteSpace(connection) ||
                string.IsNullOrWhiteSpace(tokenPepper) ||
                string.IsNullOrWhiteSpace(keyRingPath) ||
                allowedHosts.Length == 0))
    throw new InvalidOperationException(
        "Suite content gateway is enabled but protected dependencies are unavailable.");
if (enabled)
    connection = ContentConnectionPolicy.RequireRole(connection!,
        "turborama-suite-gateway");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GatewayRateLimiter>();
if (enabled)
{
    builder.Services.AddSingleton(_ => new PostgresContentStore(connection!));
    builder.Services.AddSingleton<IContentGatewayStore>(sp =>
        sp.GetRequiredService<PostgresContentStore>());
    builder.Services.AddSingleton(_ => new ContentGrantTokenHasher(tokenPepper!));
    builder.Services.AddSingleton(_ => ContentUrlKeyRing.Load(keyRingPath!));
    builder.Services.AddSingleton(_ => new SafeUpstreamClient(allowedHosts));
    builder.Services.AddSingleton<ContentGatewayDeploymentGuard>();
    builder.Services.AddSingleton<ContentGatewayService>();
}

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var correlation = context.Request.Headers["X-Correlation-ID"].ToString();
    if (correlation.Length is < 8 or > 64 || correlation.Any(character =>
            !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        correlation = ActivityTraceId.CreateRandom().ToString();
    context.Response.Headers["X-Correlation-ID"] = correlation;
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    service = "turborama-suite-content-gateway"
}));

app.MapGet("/ready", async (HttpContext context) =>
{
    if (!enabled)
        return Results.Json(new ErrorResponse(1, "CONTENT_DISABLED",
            "Content access is disabled."), StrictJson.Options, statusCode: 503);
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await context.RequestServices
            .GetRequiredService<ContentGatewayDeploymentGuard>()
            .RequireReadyAsync(timeout.Token);
        return Results.Json(new
        {
            status = "ready",
            expectedItemCount = ContentProtocol.ExpectedProductionItemCount
        }, StrictJson.Options);
    }
    catch (Exception exception) when (exception is not OperationCanceledException ||
                                      !context.RequestAborted.IsCancellationRequested)
    {
        // Never attach the exception: DNS/HTTP failures may contain a private host or URL.
        app.Logger.LogError(
            "Content gateway readiness failed. Correlation {CorrelationId}",
            context.Response.Headers["X-Correlation-ID"].ToString());
        return Results.Json(new ErrorResponse(1, "CONTENT_NOT_READY",
            "The production content catalog is not ready."), StrictJson.Options,
            statusCode: 503);
    }
});

app.MapGet("/ready/keyring", (HttpContext context) =>
{
    if (!enabled)
        return Results.Json(new ErrorResponse(1, "CONTENT_DISABLED",
            "Content access is disabled."), StrictJson.Options, statusCode: 503);
    var keyRing = context.RequestServices.GetRequiredService<ContentUrlKeyRing>();
    return Results.Json(new
    {
        status = "ready",
        activeKeyVersion = keyRing.ActiveKeyVersion
    }, StrictJson.Options);
});

app.MapPost("/ready/keyring/prove", async (HttpContext context) =>
{
    if (!enabled)
        return Results.NotFound();
    var origin = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var limiter = context.RequestServices.GetRequiredService<GatewayRateLimiter>();
    if (!limiter.AllowKeyRingProof(origin) || context.Request.ContentLength is > 4096)
        return Results.NotFound();
    try
    {
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, context.RequestAborted);
        if (body.Length is < 1 or > 4096) return Results.NotFound();
        var request = StrictJson.Parse<KeyRingProofRequest>(body.ToArray());
        if (request.SchemaVersion != Protocol.SchemaVersion)
            return Results.NotFound();
        var keySetFingerprint = ContentDeploymentProofProtocol.DecodeFingerprint(
            request.KeySetFingerprint);
        var suppliedAllowlistFingerprint =
            ContentDeploymentProofProtocol.DecodeFingerprint(
                request.AllowlistFingerprint);
        var loadedAllowlistFingerprint = context.RequestServices
            .GetRequiredService<SafeUpstreamClient>().CopyAllowlistFingerprint();
        var nonce = Convert.FromBase64String(request.Nonce);
        var proof = Convert.FromBase64String(request.Proof);
        try
        {
            if (Convert.ToBase64String(nonce) != request.Nonce ||
                Convert.ToBase64String(proof) != request.Proof)
                return Results.NotFound();
            return context.RequestServices.GetRequiredService<ContentUrlKeyRing>()
                .VerifyDeploymentProof(request.ActiveKeyVersion,
                    keySetFingerprint, suppliedAllowlistFingerprint,
                    loadedAllowlistFingerprint, nonce, proof)
                ? Results.NoContent()
                : Results.NotFound();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keySetFingerprint);
            CryptographicOperations.ZeroMemory(suppliedAllowlistFingerprint);
            CryptographicOperations.ZeroMemory(loadedAllowlistFingerprint);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(proof);
        }
    }
    catch (Exception exception) when (exception is SuiteException or FormatException)
    {
        return Results.NotFound();
    }
}).DisableAntiforgery();

app.MapPost("/ready/grant-pepper/prove", async (HttpContext context) =>
{
    if (!enabled) return Results.NotFound();
    var origin = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var limiter = context.RequestServices.GetRequiredService<GatewayRateLimiter>();
    if (!limiter.AllowGrantPepperProof(origin) || context.Request.ContentLength is > 4096)
        return Results.NotFound();
    try
    {
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body, context.RequestAborted);
        if (body.Length is < 1 or > 4096) return Results.NotFound();
        var request = StrictJson.Parse<GrantPepperProofRequest>(body.ToArray());
        if (request.SchemaVersion != Protocol.SchemaVersion) return Results.NotFound();
        var nonce = Convert.FromBase64String(request.Nonce);
        var proof = Convert.FromBase64String(request.Proof);
        try
        {
            if (Convert.ToBase64String(nonce) != request.Nonce ||
                Convert.ToBase64String(proof) != request.Proof)
                return Results.NotFound();
            return context.RequestServices.GetRequiredService<ContentGrantTokenHasher>()
                .VerifyGatewayReadinessProof(nonce, proof)
                ? Results.NoContent()
                : Results.NotFound();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(proof);
        }
    }
    catch (Exception exception) when (exception is SuiteException or FormatException)
    {
        return Results.NotFound();
    }
}).DisableAntiforgery();

app.MapGet("/v1/suite-content/artifacts/{grantId}", async (
    HttpContext context,
    string grantId) =>
{
    if (!enabled)
        return Results.Json(new ErrorResponse(1, "CONTENT_DISABLED",
            "Content access is disabled."), StrictJson.Options, statusCode: 503);
    var limiter = context.RequestServices.GetRequiredService<GatewayRateLimiter>();
    if (!limiter.Allow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", grantId))
        return Results.Json(new ErrorResponse(1, "RATE_LIMITED",
            "Too many requests."), StrictJson.Options, statusCode: 429);
    try
    {
        var directUri = await context.RequestServices.GetRequiredService<ContentGatewayService>()
            .AuthorizeDirectAsync(context, grantId, context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = directUri.AbsoluteUri;
        context.Response.Headers.CacheControl = "no-store";
        return Results.Empty;
    }
    catch (SuiteException exception) when (!context.Response.HasStarted)
    {
        return Results.Json(new ErrorResponse(1, exception.Code, exception.Message),
            StrictJson.Options, statusCode: exception.StatusCode);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        if (context.Response.HasStarted) context.Abort();
        return Results.Empty;
    }
    catch (Exception)
    {
        // Never attach the exception: upstream failures may contain a private host or URL.
        app.Logger.LogError(
            "Content transfer failed. Correlation {CorrelationId}",
            context.Response.Headers["X-Correlation-ID"].ToString());
        if (context.Response.HasStarted)
        {
            context.Abort();
            return Results.Empty;
        }
        return Results.Json(new ErrorResponse(1, "CONTENT_UPSTREAM_FAILURE",
            "The content transfer could not be completed."), StrictJson.Options,
            statusCode: 502);
    }
}).DisableAntiforgery();

app.Run();

string? ReadContentProtected(
    string valueKey,
    string fileKey,
    long maximumBytes,
    string? systemdCredentialName = null)
{
    var direct = builder.Configuration[valueKey];
    if (!string.IsNullOrWhiteSpace(direct))
    {
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException(
                "Direct content secrets are allowed only in Development.");
        return direct.Trim();
    }
    var path = builder.Configuration[fileKey];
    if (string.IsNullOrWhiteSpace(path) && systemdCredentialName is not null)
    {
        var credentialDirectory = Environment.GetEnvironmentVariable(
            "CREDENTIALS_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(credentialDirectory) &&
            Path.IsPathFullyQualified(credentialDirectory))
            path = Path.Combine(credentialDirectory, systemdCredentialName);
    }
    return string.IsNullOrWhiteSpace(path)
        ? null
        : ContentProtectedSecret.ReadFile(path, maximumBytes);
}

string[] ReadAllowedHosts()
{
    var filePath = builder.Configuration["Suite:Content:AllowedUpstreamHostsFile"];
    if (!string.IsNullOrWhiteSpace(filePath))
    {
        var content = ContentProtectedSecret.ReadFile(filePath, 16 * 1024);
        return content.Split(['\r', '\n'], StringSplitOptions.TrimEntries |
                                             StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#'))
            .ToArray();
    }

    var configured = builder.Configuration
        .GetSection("Suite:Content:AllowedUpstreamHosts").Get<string[]>() ?? [];
    if (configured.Length > 0 && !builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Inline upstream hosts are allowed only in Development.");
    return configured;
}

public partial class Program;

public sealed record KeyRingProofRequest(
    int SchemaVersion,
    int ActiveKeyVersion,
    string KeySetFingerprint,
    string AllowlistFingerprint,
    string Nonce,
    string Proof);

public sealed record GrantPepperProofRequest(
    int SchemaVersion,
    string Nonce,
    string Proof);
