using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using TurboRamaSuiteOnlineServer;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = Protocol.MaximumBodyBytes);
builder.Services.Configure<ForwardedHeadersOptions>(SuiteTrustedProxyPolicy.Configure);
var enabled = builder.Configuration.GetValue("Suite:Enabled", false);
var contentRequested = enabled && builder.Configuration.GetValue("Suite:Content:Enabled", false);
var connection = builder.Configuration.GetConnectionString("SuiteStore");
var pepper = ReadProtected("Suite:ActivationPepper", "Suite:ActivationPepperFile");
var signingPem = ReadProtected("Suite:OnlineAssertionPrivateKeyPem", "Suite:OnlineAssertionPrivateKeyPemFile");
string? contentConnection = null;
string? contentSigningPem = null;
string? contentGrantPepper = null;
Uri? gatewayPepperProofUri = null;
string? contentAssertionKeyId = null;
var contentStartupStage = "not-started";
if (enabled && (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(pepper) || string.IsNullOrWhiteSpace(signingPem)))
    throw new InvalidOperationException("Suite is enabled but protected dependencies are unavailable.");
var contentAvailable = contentRequested && ContentStartupIsolation.TryInitialize(() =>
{
    contentStartupStage = "protected-inputs";
    contentConnection = ReadContentProtected("ConnectionStrings:SuiteContentApiStore",
        "ConnectionStrings:SuiteContentApiStoreFile", 4096);
    contentSigningPem = ReadContentProtected("Suite:ContentAssertionPrivateKeyPem",
        "Suite:ContentAssertionPrivateKeyPemFile", 32 * 1024);
    contentGrantPepper = ReadContentProtected("Suite:ContentGrantTokenPepper",
        "Suite:ContentGrantTokenPepperFile", 1024, "content-grant-token-pepper");
    var gatewayPepperProofUriText =
        builder.Configuration["Suite:Content:GatewayGrantPepperProofUri"];
    if (string.IsNullOrWhiteSpace(contentConnection) ||
        string.IsNullOrWhiteSpace(contentSigningPem) ||
        string.IsNullOrWhiteSpace(contentGrantPepper) ||
        string.IsNullOrWhiteSpace(gatewayPepperProofUriText) ||
        !Uri.TryCreate(gatewayPepperProofUriText, UriKind.Absolute,
            out var parsedProofUri))
        throw new InvalidOperationException("Content dependencies are unavailable.");
    contentStartupStage = "connection-role";
    contentConnection = ContentConnectionPolicy.RequireRole(contentConnection,
        "turborama-suite-content-api");
    gatewayPepperProofUri = ContentGatewayPepperVerifier.RequireLoopbackProofUri(
        parsedProofUri);
    contentStartupStage = "assertion-key";
    using var validationRsa = RSA.Create();
    validationRsa.ImportFromPem(contentSigningPem);
    using var validationSigner = new RsaContentAssertionSigner(validationRsa);
    contentAssertionKeyId = validationSigner.KeyId;
    ContentAssertionKeyPolicy.RequireExpectedKeyId(contentAssertionKeyId,
        builder.Configuration["Suite:ContentAssertionExpectedKeyId"],
        builder.Environment.IsProduction());
    contentStartupStage = "pepper";
    using var validationHasher = new ContentGrantTokenHasher(contentGrantPepper);
    if (SamePublicKey(signingPem!, contentSigningPem))
        throw new InvalidOperationException(
            "Suite online and content assertion keys must be independent.");
    contentStartupStage = "complete";
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SuiteRateLimiter>();
if (enabled)
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connection!));
    builder.Services.AddSingleton<ISuiteStore, PostgresSuiteStore>();
    builder.Services.AddSingleton<IAssertionSigner>(_ => { var rsa = RSA.Create(); rsa.ImportFromPem(signingPem); return new RsaAssertionSigner(rsa); });
    builder.Services.AddSingleton(sp => new SuiteService(sp.GetRequiredService<ISuiteStore>(), sp.GetRequiredService<IAssertionSigner>(), sp.GetRequiredService<TimeProvider>(), pepper!));
    if (contentAvailable)
    {
        builder.Services.AddSingleton(_ => new PostgresContentStore(contentConnection!));
        builder.Services.AddSingleton<IContentControlStore>(sp =>
            sp.GetRequiredService<PostgresContentStore>());
        builder.Services.AddSingleton<IContentAssertionSigner>(_ =>
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(contentSigningPem);
            return new RsaContentAssertionSigner(rsa);
        });
        builder.Services.AddSingleton(_ => new ContentGrantTokenHasher(contentGrantPepper!));
        builder.Services.AddSingleton<IContentGatewayPepperVerifier>(sp =>
            new ContentGatewayPepperVerifier(gatewayPepperProofUri!,
                sp.GetRequiredService<ContentGrantTokenHasher>(),
                sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new ContentService(
            sp.GetRequiredService<ISuiteStore>(),
            sp.GetRequiredService<IContentControlStore>(),
            sp.GetRequiredService<IContentAssertionSigner>(),
            sp.GetRequiredService<ContentGrantTokenHasher>(),
            sp.GetRequiredService<IContentGatewayPepperVerifier>(),
            sp.GetRequiredService<TimeProvider>()));
    }
}
var app = builder.Build();
if (contentRequested && !contentAvailable)
    app.Logger.LogError(
        "Suite content startup validation failed at {Stage}; licensing v1 remains available and content is fail-closed.",
        contentStartupStage);
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var correlation = context.Request.Headers["X-Correlation-ID"].ToString();
    if (correlation.Length is < 8 or > 64 || correlation.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) correlation = ActivityTraceId.CreateRandom().ToString();
    context.Response.Headers["X-Correlation-ID"] = correlation;
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.MapGet("/health", () => Results.Json(new { status = "ok", service = "turborama-suite-api" }));
app.MapGet("/ready", () => enabled ? Results.Json(new { status = "ready" }) : Results.Json(new ErrorResponse(1, "SUITE_DISABLED", "Suite is disabled."), statusCode: 503));
app.MapGet("/ready/content", async (HttpContext context) =>
{
    if (!contentAvailable)
        return ContentUnavailable();
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await context.RequestServices.GetRequiredService<IContentGatewayPepperVerifier>()
            .RequireMatchingAsync(timeout.Token);
        var ready = await context.RequestServices.GetRequiredService<IContentControlStore>()
            .IsProductionCatalogReadyAsync(timeout.Token);
        return ready
            ? Results.Json(new
            {
                status = "ready",
                expectedItemCount = ContentProtocol.ExpectedProductionItemCount,
                contentAssertionKeyValidated = true,
                contentAssertionKeyId
            }, StrictJson.Options)
            : Results.Json(new ErrorResponse(1, "CONTENT_NOT_READY",
                "The production content catalog is not ready."), StrictJson.Options,
                statusCode: 503);
    }
    catch (OperationCanceledException)
    {
        return Results.Json(new ErrorResponse(1, "CONTENT_NOT_READY",
            "The production content catalog is not ready."), StrictJson.Options,
            statusCode: 503);
    }
    catch (Exception)
    {
        // Content datastore/proof failures are logged without exception text or secret paths.
        app.Logger.LogError(
            "Suite content readiness failed. Correlation {CorrelationId}",
            context.Response.Headers["X-Correlation-ID"].ToString());
        return Results.Json(new ErrorResponse(1, "CONTENT_NOT_READY",
            "The production content catalog is not ready."), StrictJson.Options,
            statusCode: 503);
    }
});
Map<ActivationChallengeRequest>("/v1/suite/activations/challenge", (s, r, c) => s.ActivationChallengeAsync(r, c));
Map<ActivationProof>("/v1/suite/activations/complete", (s, r, c) => s.CompleteActivationAsync(r, c));
Map<ChallengeRequest>("/v1/suite/challenges", ChallengeAsync);
Map<SessionProof>("/v1/suite/sessions", (s, r, c) => s.SessionAsync(r, c));
MapContent<CatalogPageProof>("/v1/suite-content/catalog/current",
    (service, request, _, token) => service.CatalogAsync(request, token));
MapContent<DownloadAuthorizationProof>("/v1/suite-content/downloads/authorize",
    (service, request, correlationId, token) =>
        service.AuthorizeDownloadAsync(request, correlationId, token));
app.Run();

Task<SignedAssertionEnvelope> ChallengeAsync(
    SuiteService service,
    ChallengeRequest request,
    CancellationToken cancellationToken)
{
    if (!contentAvailable && ContentProtocol.IsContentAction(request.Action))
        throw new SuiteException(503,
            contentRequested ? "CONTENT_NOT_READY" : "CONTENT_DISABLED",
            contentRequested ? "Content access is not ready." : "Content access is disabled.");
    return service.ChallengeAsync(request, cancellationToken);
}

string? ReadProtected(string valueKey, string fileKey)
{
    var direct = builder.Configuration[valueKey];
    if (!string.IsNullOrWhiteSpace(direct)) return direct;
    var path = builder.Configuration[fileKey];
    return string.IsNullOrWhiteSpace(path) ? null : File.ReadAllText(path).Trim();
}

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

static bool SamePublicKey(string firstPem, string secondPem)
{
    using var first = RSA.Create();
    using var second = RSA.Create();
    first.ImportFromPem(firstPem);
    second.ImportFromPem(secondPem);
    var firstSpki = first.ExportSubjectPublicKeyInfo();
    var secondSpki = second.ExportSubjectPublicKeyInfo();
    try
    {
        return firstSpki.Length == secondSpki.Length &&
               CryptographicOperations.FixedTimeEquals(firstSpki, secondSpki);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(firstSpki);
        CryptographicOperations.ZeroMemory(secondSpki);
    }
}

void Map<T>(string route, Func<SuiteService, T, CancellationToken, Task<SignedAssertionEnvelope>> action) where T : class
{
    app.MapPost(route, async (HttpContext context) =>
    {
        if (!enabled) return Results.Json(new ErrorResponse(1, "SUITE_DISABLED", "Suite is disabled."), StrictJson.Options, statusCode: 503);
        try
        {
            using var memory = new MemoryStream(); await context.Request.Body.CopyToAsync(memory, context.RequestAborted);
            var request = StrictJson.Parse<T>(memory.ToArray());
            var limiter = context.RequestServices.GetRequiredService<SuiteRateLimiter>();
            if (!limiter.Allow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", route, request))
                return Results.Json(new ErrorResponse(1, "RATE_LIMITED", "Too many requests."), StrictJson.Options, statusCode: 429);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var response = await action(context.RequestServices.GetRequiredService<SuiteService>(), request, timeout.Token);
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

void MapContent<T>(
    string route,
    Func<ContentService, T, string, CancellationToken, Task<SignedAssertionEnvelope>> action)
    where T : class
{
    app.MapPost(route, async (HttpContext context) =>
    {
        if (!contentAvailable)
            return ContentUnavailable();
        try
        {
            using var memory = new MemoryStream();
            await context.Request.Body.CopyToAsync(memory, context.RequestAborted);
            var request = StrictJson.Parse<T>(memory.ToArray());
            var limiter = context.RequestServices.GetRequiredService<SuiteRateLimiter>();
            if (!limiter.Allow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    route, request))
                return Results.Json(new ErrorResponse(1, "RATE_LIMITED",
                    "Too many requests."), StrictJson.Options, statusCode: 429);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();
            var response = await action(
                context.RequestServices.GetRequiredService<ContentService>(), request,
                correlationId, timeout.Token);
            return Results.Json(response, StrictJson.Options,
                contentType: "application/json; charset=utf-8");
        }
        catch (SuiteException ex)
        {
            return Results.Json(new ErrorResponse(1, ex.Code, ex.Message),
                StrictJson.Options, statusCode: ex.StatusCode);
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
            // Content failures may carry database/private-origin detail; keep the log sanitized.
            app.Logger.LogError(
                "Suite content request failed. Correlation {CorrelationId}",
                context.Response.Headers["X-Correlation-ID"].ToString());
            return Results.Json(new ErrorResponse(1, "INTERNAL_ERROR",
                "Request could not be completed."), StrictJson.Options, statusCode: 500);
        }
    }).DisableAntiforgery();
}

IResult ContentUnavailable() => Results.Json(new ErrorResponse(1,
        contentRequested ? "CONTENT_NOT_READY" : "CONTENT_DISABLED",
        contentRequested ? "Content access is not ready." : "Content access is disabled."),
    StrictJson.Options, statusCode: 503);

public partial class Program;
