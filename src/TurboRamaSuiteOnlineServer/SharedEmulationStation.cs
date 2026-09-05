using Npgsql;

namespace TurboRamaSuiteOnlineServer;

public static class EmulationStationScope
{
    public const string Header = "X-TurboRama-Client";
    public const string Value = "EMULATIONSTATION";
    public const string ChallengeRoute = "/v1/suite/challenges";
    public const string SessionRoute = "/v1/suite/sessions";

    public static bool IsShared(HttpContext context) => context.Request.Headers.ContainsKey(Header);

    public static async Task Guard(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Headers.TryGetValue(Header, out var values) &&
            (values.Count != 1 || !string.Equals(values[0], Value, StringComparison.Ordinal) ||
             context.Request.Path.Value is not (ChallengeRoute or SessionRoute) ||
             !HttpMethods.IsPost(context.Request.Method)))
        {
            context.Response.StatusCode = 400;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new ErrorResponse(1, "CLIENT_SCOPE_INVALID",
                "Client scope is invalid."), StrictJson.Options, context.RequestAborted);
            return;
        }
        await next(context);
    }
}

public sealed class EmulationStationAssertionSigner(IAssertionSigner inner) : IAssertionSigner
{
    public const string OpenChallengeKind = "TURBORAMA_SUITE_ES_SESSION_OPEN_CHALLENGE";
    public const string HeartbeatChallengeKind = "TURBORAMA_SUITE_ES_SESSION_HEARTBEAT_CHALLENGE";
    public const string OpenKind = "TURBORAMA_SUITE_ES_SESSION_OPEN";
    public const string HeartbeatKind = "TURBORAMA_SUITE_ES_SESSION_HEARTBEAT";
    public string KeyId => inner.KeyId;

    public SignedAssertionEnvelope Sign(object assertion) => inner.Sign(assertion switch
    {
        OperationChallengeAssertion a when a.Action == "session.open" => a with { Kind = OpenChallengeKind },
        OperationChallengeAssertion a when a.Action == "session.heartbeat" => a with { Kind = HeartbeatChallengeKind },
        SessionAssertion a when a.Action == "session.open" => a with { Kind = OpenKind },
        SessionAssertion a when a.Action == "session.heartbeat" => a with { Kind = HeartbeatKind },
        _ => throw new InvalidOperationException("Assertion is outside the ES shared contract.")
    });
}

public interface ISharedEmulationStationStore : IEmulationStationStore;

public sealed class PostgresSharedEmulationStationStore(NpgsqlDataSource data)
    : PostgresEmulationStationStore(data, true), ISharedEmulationStationStore;

// Raised only after proof validation, transactional eligibility checks and one-use
// challenge consumption. It carries no identifier of the other active instance.
public sealed class EmulationStationSessionConflict(long serverTime) : Exception
{
    public long ServerTime { get; } = serverTime;
}

public sealed class SharedEmulationStationService
{
    private readonly EmulationStationService _sessions;
    private readonly EmulationStationAssertionSigner _signer;

    public SharedEmulationStationService(ISharedEmulationStationStore store,
        IAssertionSigner signer, TimeProvider time)
    {
        _signer = new(signer);
        _sessions = new(store, _signer, time);
    }

    public Task<SignedAssertionEnvelope> ChallengeAsync(ChallengeRequest request, CancellationToken token) =>
        _sessions.ChallengeAsync(request, token);

    public async Task<SignedAssertionEnvelope> SessionAsync(SessionProof request, CancellationToken token)
    {
        try { return await _sessions.SessionAsync(request, token); }
        catch (EmulationStationSessionConflict conflict)
        {
            // A signed CONFLICT is an outcome of this request, never an access grant.
            // Its authorization interval is empty; the existing ES session is untouched.
            return _signer.Sign(new SessionAssertion(1, EmulationStationAssertionSigner.OpenKind,
                Protocol.ProductId, request.Proof.LicenseId, request.Proof.DeviceId,
                request.Proof.SessionId, "session.open", request.Proof.ContextHash,
                request.Proof.ChallengeId, "CONFLICT", conflict.ServerTime, conflict.ServerTime, 5));
        }
    }
}
