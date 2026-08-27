using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Features;
using Npgsql;
using TurboRamaSuiteOnlineServer;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = Protocol.MaximumBodyBytes);
var enabled = builder.Configuration.GetValue("Suite:Enabled", false);
var connection = builder.Configuration.GetConnectionString("SuiteStore");
var pepper = builder.Configuration["Suite:ActivationPepper"];
var signingPem = builder.Configuration["Suite:OnlineAssertionPrivateKeyPem"];
if (enabled && (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(pepper) || string.IsNullOrWhiteSpace(signingPem)))
    throw new InvalidOperationException("Suite is enabled but protected dependencies are unavailable.");

builder.Services.AddSingleton(TimeProvider.System);
if (enabled)
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connection!));
    builder.Services.AddSingleton<ISuiteStore, PostgresSuiteStore>();
    builder.Services.AddSingleton<IAssertionSigner>(_ => { var rsa = RSA.Create(); rsa.ImportFromPem(signingPem); return new RsaAssertionSigner(rsa); });
    builder.Services.AddSingleton(sp => new SuiteService(sp.GetRequiredService<ISuiteStore>(), sp.GetRequiredService<IAssertionSigner>(), sp.GetRequiredService<TimeProvider>(), pepper!));
}
var app = builder.Build();
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
Map<ActivationChallengeRequest>("/v1/suite/activations/challenge", (s, r, c) => s.ActivationChallengeAsync(r, c));
Map<ActivationProof>("/v1/suite/activations/complete", (s, r, c) => s.CompleteActivationAsync(r, c));
Map<ChallengeRequest>("/v1/suite/challenges", (s, r, c) => s.ChallengeAsync(r, c));
Map<SessionProof>("/v1/suite/sessions", (s, r, c) => s.SessionAsync(r, c));
app.Run();

void Map<T>(string route, Func<SuiteService, T, CancellationToken, Task<SignedAssertionEnvelope>> action) where T : class
{
    app.MapPost(route, async (HttpContext context) =>
    {
        if (!enabled) return Results.Json(new ErrorResponse(1, "SUITE_DISABLED", "Suite is disabled."), StrictJson.Options, statusCode: 503);
        try
        {
            using var memory = new MemoryStream(); await context.Request.Body.CopyToAsync(memory, context.RequestAborted);
            var request = StrictJson.Parse<T>(memory.ToArray()); var response = await action(context.RequestServices.GetRequiredService<SuiteService>(), request, context.RequestAborted);
            return Results.Json(response, StrictJson.Options, contentType: "application/json; charset=utf-8");
        }
        catch (SuiteException ex) { return Results.Json(new ErrorResponse(1, ex.Code, ex.Message), StrictJson.Options, statusCode: ex.StatusCode); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(499); }
        catch (Exception ex) { app.Logger.LogError(ex, "Suite request failed. Correlation {CorrelationId}", context.Response.Headers["X-Correlation-ID"].ToString()); return Results.Json(new ErrorResponse(1, "INTERNAL_ERROR", "Request could not be completed."), StrictJson.Options, statusCode: 500); }
    }).DisableAntiforgery();
}

public partial class Program;
