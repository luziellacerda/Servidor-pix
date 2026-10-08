using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteOnlineServer;

/// <summary>Join client requests to private index IDs without logging credentials or paths.</summary>
public static class StationRequestDiagnostics
{
    private const string ItemKey = "StationDiagnosticItemTag";

    public static string IdentityTag(string? id) =>
        id is not null && StationProtocol.IsSafeLibraryId(id)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant()
            : "";

    public static void SelectedItem(HttpContext context, string? id) =>
        context.Items[ItemKey] = IdentityTag(id);

    public static RouteHandlerBuilder TraceStation(this RouteHandlerBuilder route, string operation) =>
        route.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);
            var http = context.HttpContext;
            var status = result is IStatusCodeHttpResult typed
                ? typed.StatusCode ?? http.Response.StatusCode : http.Response.StatusCode;
            var itemTag = http.Items.TryGetValue(ItemKey, out var selected) ? selected as string ?? "" : "";
            var coverTag = operation == "cover" && http.Request.RouteValues.TryGetValue("coverId", out var cover)
                ? IdentityTag(cover as string) : "";
            http.RequestServices.GetRequiredService<ILogger<StationService>>().LogInformation(
                "Station trace operation={Operation} status={StatusCode} correlation={CorrelationId} itemTag={ItemTag} coverTag={CoverTag}",
                operation, status, http.Response.Headers["X-Correlation-ID"].ToString(), itemTag, coverTag);
            return result;
        });
}
