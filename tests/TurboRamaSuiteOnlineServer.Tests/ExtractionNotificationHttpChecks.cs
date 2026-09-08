using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using TurboRamaSuiteNotifications;
using TurboRamaSuiteOnlineServer;

internal static class ExtractionNotificationHttpChecks
{
    internal static async Task RunAsync()
    {
        // Ephemeral loopback only; no real configuration, database, account,
        // credentials, provider or outbound Internet call is loaded.
        foreach (var enabled in new[] { false, true })
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32 * 1024);
            await using var app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            ExtractionNotificationEndpoints.Map(app, enabled);
            await app.StartAsync();
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(10) };
                using var malformed = await client.PostAsync(ExtractionCompletionProtocol.Route,
                    new StringContent("{}", Encoding.UTF8, "application/json"));
                Check(malformed.StatusCode == (enabled ? HttpStatusCode.BadRequest : HttpStatusCode.NotFound),
                    "Feature gate or malformed request handling failed.");
                if (!enabled) continue;
                foreach (var chunked in new[] { false, true })
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, ExtractionCompletionProtocol.Route)
                    {
                        Content = new ByteArrayContent(new byte[ExtractionCompletionProtocol.MaximumBodyBytes + 1])
                    };
                    if (chunked) request.Headers.TransferEncodingChunked = true;
                    using var response = await client.SendAsync(request);
                    Check(response.StatusCode == HttpStatusCode.RequestEntityTooLarge,
                        "Oversized request was not rejected: chunked=" + chunked);
                }
                using var unexpected = await client.PostAsync(ExtractionCompletionProtocol.Route,
                    new StringContent("{\"phone\":\"forbidden\"}", Encoding.UTF8, "application/json"));
                Check(unexpected.StatusCode == HttpStatusCode.BadRequest, "Unknown field was accepted.");
            }
            finally { await app.StopAsync(); }
        }
        Console.WriteLine("PASS: extraction HTTP feature gate, invalid JSON, forbidden field and fixed/chunked size limits (loopback, no database or provider).");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
