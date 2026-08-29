using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;

static class ContentManagementEndpoints
{
    public static void Map(WebApplication app, ContentManagementRuntime? runtime, bool configured)
    {
        if (runtime is null)
        {
            if (configured)
                app.MapMethods("/content/{**path}", ["GET", "POST"], () => Failure(
                    StatusCodes.Status503ServiceUnavailable, "SERVICO_INDISPONIVEL"));
            return;
        }

        app.MapGet("/content/items", async (HttpContext context, CancellationToken ct) =>
        {
            var security = ContentAdminSecurity.Authorize(context, "suite.content.read", false, false);
            if (!security.Allowed) return await Denied(runtime, security, null, ct);
            if (!ContentQuery.TryParse(context.Request.Query, out var query))
                return Failure(StatusCodes.Status400BadRequest, "CONSULTA_INVALIDA");
            return Results.Json(await runtime.ListItemsAsync(query!, ct));
        });

        app.MapGet("/content/jobs/{candidateId}", async (
            HttpContext context, string candidateId, CancellationToken ct) =>
        {
            var security = ContentAdminSecurity.Authorize(context, "suite.content.read", false, false);
            if (!security.Allowed) return await Denied(runtime, security, null, ct);
            if (!ContentValidation.IsLowerHex(candidateId, 64))
                return Failure(StatusCodes.Status400BadRequest, "IDENTIFICADOR_INVALIDO");
            var job = await runtime.GetJobAsync(candidateId, ct);
            return job is null ? Results.NotFound(new ContentError("NAO_ENCONTRADO",
                "O trabalho solicitado não foi encontrado.")) : Results.Json(job);
        });

        app.MapGet("/content/audit", async (HttpContext context, CancellationToken ct) =>
        {
            var security = ContentAdminSecurity.Authorize(context, "suite.content.read", false, false);
            if (!security.Allowed) return await Denied(runtime, security, null, ct);
            if (!ContentAuditQuery.TryParse(context.Request.Query, out var query))
                return Failure(StatusCodes.Status400BadRequest, "CONSULTA_INVALIDA");
            return Results.Json(await runtime.ListAuditAsync(query!, ct));
        });

        app.MapPost("/content/items/{itemId}/origin-candidates", async (
            HttpContext context, string itemId, ContentCandidateRequest request, CancellationToken ct) =>
        {
            var security = ContentAdminSecurity.Authorize(context,
                "suite.content.origin.replace", true, false);
            if (!security.Allowed) return await Denied(runtime, security, SafeItem(itemId), ct);
            if (!ContentValidation.IsLowerHex(itemId, 32) ||
                !ContentValidation.ValidRequest(request.RequestId) ||
                !ContentValidation.ValidActor(request.Actor) ||
                !string.Equals(request.Actor, security.Actor, StringComparison.Ordinal))
                return Failure(StatusCodes.Status400BadRequest, "SOLICITACAO_INVALIDA");
            if (!runtime.RateLimiter.TryAcquire(security.RateIdentity, itemId, "replace", 6))
                return await RateLimited(runtime, security, itemId, ct);
            try
            {
                var result = await runtime.SubmitCandidateAsync(itemId, request.CandidateUrl,
                    request.RequestId, request.Actor, "AUTO_REPLACEMENT", null, null, ct);
                request.CandidateUrl = string.Empty;
                return Results.Accepted(value: result);
            }
            catch (ContentAdminException)
            {
                request.CandidateUrl = string.Empty;
                return Failure(StatusCodes.Status409Conflict, "SUBSTITUICAO_RECUSADA");
            }
            catch (PostgresException)
            {
                request.CandidateUrl = string.Empty;
                return Failure(StatusCodes.Status409Conflict, "SUBSTITUICAO_RECUSADA");
            }
        });

        app.MapPost("/content/items/{itemId}/versions", async (
            HttpContext context, string itemId, ContentVersionRequest request, CancellationToken ct) =>
        {
            var security = ContentAdminSecurity.Authorize(context,
                "suite.content.version.publish", true, true);
            if (!security.Allowed) return await Denied(runtime, security, SafeItem(itemId), ct);
            if (!ContentValidation.IsLowerHex(itemId, 32) ||
                !ContentValidation.ValidRequest(request.RequestId) ||
                !ContentValidation.ValidActor(request.Actor) ||
                !string.Equals(request.Actor, security.Actor, StringComparison.Ordinal) ||
                !ContentValidation.ValidChangeReason(request.ChangeReason) ||
                !ContentAdminSecurity.ConfirmsVersion(context, itemId, request.ConfirmedArtifactVersion))
                return Failure(StatusCodes.Status400BadRequest, "CONFIRMACAO_INVALIDA");
            if (!runtime.RateLimiter.TryAcquire(security.RateIdentity, itemId, "version", 2))
                return await RateLimited(runtime, security, itemId, ct);
            try
            {
                var result = await runtime.SubmitCandidateAsync(itemId, request.CandidateUrl,
                    request.RequestId, request.Actor, "NEW_ARTIFACT_VERSION", request.ChangeReason,
                    request.ConfirmedArtifactVersion, ct);
                request.CandidateUrl = string.Empty;
                return Results.Accepted(value: result);
            }
            catch (ContentAdminException)
            {
                request.CandidateUrl = string.Empty;
                return Failure(StatusCodes.Status409Conflict, "NOVA_VERSAO_RECUSADA");
            }
            catch (PostgresException)
            {
                request.CandidateUrl = string.Empty;
                return Failure(StatusCodes.Status409Conflict, "NOVA_VERSAO_RECUSADA");
            }
        });

        app.MapPost("/content/items/{itemId}/checks", async (
            HttpContext context, string itemId, ContentCheckRequest request, CancellationToken ct) =>
        {
            var security = ContentAdminSecurity.Authorize(context, "suite.content.check", true, false);
            if (!security.Allowed) return await Denied(runtime, security, SafeItem(itemId), ct);
            if (!ContentValidation.IsLowerHex(itemId, 32) ||
                !ContentValidation.ValidRequest(request.RequestId) ||
                !ContentValidation.ValidActor(request.Actor) ||
                !string.Equals(request.Actor, security.Actor, StringComparison.Ordinal))
                return Failure(StatusCodes.Status400BadRequest, "SOLICITACAO_INVALIDA");
            if (!runtime.RateLimiter.TryAcquire(security.RateIdentity, itemId, "check", 12))
                return await RateLimited(runtime, security, itemId, ct);
            try
            {
                await runtime.RequestCheckAsync(itemId, request.RequestId, request.Actor, ct);
                return Results.Accepted(value: new { state = "QUEUED" });
            }
            catch (ContentAdminException)
            {
                return Failure(StatusCodes.Status409Conflict, "VERIFICACAO_RECUSADA");
            }
        });
    }

    private static string? SafeItem(string itemId) =>
        ContentValidation.IsLowerHex(itemId, 32) ? itemId : null;

    private static async Task<IResult> Denied(ContentManagementRuntime runtime,
        ContentSecurityResult security, string? itemId, CancellationToken ct)
    {
        await runtime.TryAuditDenialAsync(security.EventType, security.Actor, itemId,
            security.DetailCode, ct);
        return Failure(security.StatusCode, "ACESSO_NEGADO");
    }

    private static async Task<IResult> RateLimited(ContentManagementRuntime runtime,
        ContentSecurityResult security, string itemId, CancellationToken ct)
    {
        await runtime.TryAuditDenialAsync("CONTENT_RATE_LIMITED", security.Actor, itemId,
            "RATE_LIMITED", ct);
        return Failure(StatusCodes.Status429TooManyRequests, "LIMITE_ATINGIDO");
    }

    private static IResult Failure(int status, string code) => Results.Json(
        new ContentError(code, "A solicitação não pôde ser concluída."), statusCode: status);
}

static class ContentManagementBootstrap
{
    public static async Task<ContentManagementRuntime?> TryLoadAsync(bool enabled,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<ContentManagementRuntime>>? loader = null)
    {
        if (!enabled) return null;
        try
        {
            return await (loader ?? ContentManagementRuntime.LoadAsync)(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            Console.Error.WriteLine(
                "SUITE CONTENT ADMIN: UNAVAILABLE code=CONTENT_CONFIGURATION_INVALID");
            return null;
        }
    }

    internal static async Task<bool> FailsClosedForSelfTestAsync()
    {
        var disabledLoaderCalled = false;
        var disabled = await TryLoadAsync(false, CancellationToken.None, _ =>
        {
            disabledLoaderCalled = true;
            throw new InvalidOperationException();
        });
        var unavailable = await TryLoadAsync(true, CancellationToken.None,
            _ => throw new InvalidOperationException());
        return disabled is null && !disabledLoaderCalled && unavailable is null;
    }
}

sealed class ContentManagementRuntime : IDisposable
{
    private const string ReadinessSql = """
        SELECT current_user='turborama-suite-content-admin',
          EXISTS(SELECT 1 FROM suite.schema_migrations
            WHERE version='013_suite_content_management'),
          EXISTS(SELECT 1 FROM suite.schema_migration_checksums
            WHERE version='013_suite_content_management'),
          (SELECT count(*)>=0 FROM
            (SELECT 1 FROM suite.suite_content_management_items LIMIT 1) sample),
          has_function_privilege(current_user,
            'suite.get_suite_content_candidate_context(character,character varying)'::regprocedure,
            'EXECUTE') AND
          has_function_privilege(current_user,
            'suite.submit_suite_content_origin_candidate(character,character,character,character varying,bytea,bytea,bytea,integer,character varying,character varying,character varying)'::regprocedure,
            'EXECUTE') AND
          has_function_privilege(current_user,
            'suite.request_suite_content_check(character,character varying,character varying)'::regprocedure,
            'EXECUTE') AND
          has_function_privilege(current_user,
            'suite.audit_suite_content_management_denial(character varying,character varying,character,character varying,character varying)'::regprocedure,
            'EXECUTE')
        """;
    private readonly NpgsqlDataSource dataSource;
    private readonly ContentCandidateProtector protector;
    public ContentAdminRateLimiter RateLimiter { get; } = new();

    private ContentManagementRuntime(NpgsqlDataSource dataSource,
        ContentCandidateProtector protector) =>
        (this.dataSource, this.protector) = (dataSource, protector);

    public static async Task<ContentManagementRuntime> LoadAsync(CancellationToken cancellationToken)
    {
        var stage = "CONFIGURATION";
        var connectionPath = Required("SUITE_CONTENT_ADMIN_CONNECTION_FILE");
        var keyRingPath = ProtectedPath("SUITE_CONTENT_ADMIN_CANDIDATE_KEYRING_FILE",
            "SUITE_CONTENT_ADMIN_CANDIDATE_KEYRING_CREDENTIAL");
        var allowedHostsPath = ProtectedPath("SUITE_CONTENT_ADMIN_ALLOWED_HOSTS_FILE",
            "SUITE_CONTENT_ADMIN_ALLOWED_HOSTS_CREDENTIAL");
        stage = "CONNECTION_FILE";
        var connection = await ContentAdminProtectedFile.ReadTextAsync(connectionPath, 16 * 1024,
            cancellationToken);
        ContentCandidateProtector? protector = null;
        try
        {
            stage = "CONNECTION_ROLE";
            var parsed = new NpgsqlConnectionStringBuilder(connection);
            if (!string.Equals(parsed.Username, "turborama-suite-content-admin",
                    StringComparison.Ordinal))
                throw new InvalidOperationException();
            stage = "PROTECTOR";
            protector = await ContentCandidateProtector.LoadAsync(keyRingPath,
                allowedHostsPath, cancellationToken);
            stage = "DATASOURCE";
            var dataSource = NpgsqlDataSource.Create(connection);
            connection = string.Empty;
            var runtime = new ContentManagementRuntime(dataSource, protector);
            stage = "DATABASE_READINESS";
            if (!await runtime.IsReadyAsync(cancellationToken))
            {
                runtime.Dispose();
                protector = null;
                throw new InvalidOperationException();
            }
            return runtime;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            protector?.Dispose();
            connection = string.Empty;
            throw;
        }
        catch
        {
            Console.Error.WriteLine($"SUITE CONTENT ADMIN: INIT_FAILED stage={stage}");
            protector?.Dispose();
            connection = string.Empty;
            throw new InvalidOperationException("Content administration configuration is invalid.");
        }
    }

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await using var command = dataSource.CreateCommand(ReadinessSql);
            await using var reader = await command.ExecuteReaderAsync(timeout.Token);
            if (!await reader.ReadAsync(timeout.Token)) return false;
            var checks = Enumerable.Range(0, 5).Select(reader.GetBoolean).ToArray();
            if (checks.Any(value => !value))
                Console.Error.WriteLine("SUITE CONTENT ADMIN: READINESS_CHECKS=" +
                    string.Join(',', checks.Select(value => value ? '1' : '0')));
            return checks.All(value => value);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (PostgresException ex)
        {
            Console.Error.WriteLine($"SUITE CONTENT ADMIN: READINESS_DATABASE_{ex.SqlState}");
            return false;
        }
        catch (NpgsqlException ex)
        {
            Console.Error.WriteLine($"SUITE CONTENT ADMIN: READINESS_DRIVER_{ex.GetType().Name}");
            return false;
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine("SUITE CONTENT ADMIN: READINESS_STATE_INVALID");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SUITE CONTENT ADMIN: READINESS_EXCEPTION_{ex.GetType().Name}");
            return false;
        }
    }

    internal static bool ReadinessChecksMutationPrivilegesForSelfTest()
    {
        string[] signatures =
        [
            "suite.get_suite_content_candidate_context(character,character varying)",
            "suite.submit_suite_content_origin_candidate(character,character,character,character varying,bytea,bytea,bytea,integer,character varying,character varying,character varying)",
            "suite.request_suite_content_check(character,character varying,character varying)",
            "suite.audit_suite_content_management_denial(character varying,character varying,character,character varying,character varying)"
        ];
        return signatures.All(signature => ReadinessSql.Contains(
            $"'{signature}'::regprocedure", StringComparison.Ordinal)) &&
            ReadinessSql.Split("has_function_privilege", StringSplitOptions.None).Length - 1 ==
            signatures.Length;
    }

    public async Task<ContentItemPage> ListItemsAsync(ContentQuery query, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT btrim(item_id),display_name,availability,artifact_version,
              CASE WHEN last_checked_at IS NULL THEN NULL ELSE
                to_char(last_checked_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"') END,
              last_result_code,job_state,btrim(candidate_id),
              CASE WHEN job_updated_at IS NULL THEN NULL ELSE
                to_char(job_updated_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"') END
            FROM suite.suite_content_management_items
            WHERE ($1::varchar='' OR item_id::varchar>$1)
              AND ($2='' OR availability=$2)
              AND ($3='' OR last_result_code=$3)
              AND ($4='' OR job_state=$4)
              AND ($5='' OR item_id::varchar LIKE $5||'%')
              AND ($6='' OR position(
                translate(lower(btrim($6)),
                  'áàâãäéèêëíìîïóòôõöúùûüçñ',
                  'aaaaaeeeeiiiiooooouuuucn')
                in translate(lower(display_name),
                  'áàâãäéèêëíìîïóòôõöúùûüçñ',
                  'aaaaaeeeeiiiiooooouuuucn'))>0)
            ORDER BY item_id LIMIT $7
            """);
        command.Parameters.AddWithValue(query.AfterItemId);
        command.Parameters.AddWithValue(query.Availability ?? string.Empty);
        command.Parameters.AddWithValue(query.ResultCode ?? string.Empty);
        command.Parameters.AddWithValue(query.JobState ?? string.Empty);
        command.Parameters.AddWithValue(query.ItemPrefix ?? string.Empty);
        command.Parameters.AddWithValue(query.Name ?? string.Empty);
        command.Parameters.AddWithValue(query.Limit + 1);
        var rows = new List<ContentItemView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new ContentItemView(reader.GetString(0), reader.GetString(1),
                DisplayAvailability(reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        var more = rows.Count > query.Limit;
        if (more) rows.RemoveAt(rows.Count - 1);
        return new ContentItemPage(rows, more && rows.Count > 0
            ? ContentCursor.Encode(rows[^1].ItemId) : null);
    }

    public async Task<ContentJobView?> GetJobAsync(string candidateId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT btrim(candidate_id),btrim(item_id),state,change_intent,internal_result_code,
              to_char(submitted_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
              to_char(updated_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
              CASE WHEN verified_at IS NULL THEN NULL ELSE
                to_char(verified_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"') END,
              CASE WHEN published_at IS NULL THEN NULL ELSE
                to_char(published_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"') END
            FROM suite.suite_content_origin_candidates WHERE candidate_id=$1
            """);
        command.Parameters.AddWithValue(candidateId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new ContentJobView(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    public async Task<ContentAuditPage> ListAuditAsync(ContentAuditQuery query, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT audit_id,event_type,actor,btrim(item_id),btrim(candidate_id),btrim(job_id),
              correlation_id,coalesce(request_id,''),outcome,detail_code,
              btrim(previous_catalog_identity),btrim(new_catalog_identity),
              to_char(occurred_at AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"')
            FROM suite.suite_content_management_audit
            WHERE audit_id<$1 AND ($2::varchar='' OR item_id::varchar=$2)
            ORDER BY audit_id DESC LIMIT $3
            """);
        command.Parameters.AddWithValue(query.BeforeAuditId);
        command.Parameters.AddWithValue(query.ItemId ?? string.Empty);
        command.Parameters.AddWithValue(query.Limit + 1);
        var rows = new List<ContentAuditView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new ContentAuditView(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                reader.GetString(7), reader.GetString(8), reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetString(12)));
        var more = rows.Count > query.Limit;
        if (more) rows.RemoveAt(rows.Count - 1);
        return new ContentAuditPage(rows, more && rows.Count > 0
            ? ContentCursor.Encode(rows[^1].AuditId.ToString(CultureInfo.InvariantCulture)) : null);
    }

    public async Task<ContentCandidateAccepted> SubmitCandidateAsync(string itemId,
        string candidateUrl, string requestId, string actor, string intent, string? changeReason,
        int? confirmedVersion, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        ContentCandidateContext context;
        await using (var getContext = new NpgsqlCommand("""
            SELECT btrim(base_catalog_identity),current_availability,expected_content_length,
              btrim(expected_sha256),expected_artifact_version,expected_file_extension,
              expected_extract_policy,resolved_change_intent
            FROM suite.get_suite_content_candidate_context($1,$2)
            """, connection, transaction))
        {
            getContext.Parameters.AddWithValue(itemId);
            getContext.Parameters.AddWithValue(intent);
            await using var reader = await getContext.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ContentAdminException();
            context = new ContentCandidateContext(reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7));
        }
        if (intent == "NEW_ARTIFACT_VERSION" && confirmedVersion != context.ExpectedArtifactVersion)
            throw new ContentAdminException();
        var candidateId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        using var encrypted = protector.Encrypt(candidateUrl, candidateId, itemId,
            context.BaseCatalogIdentity);
        await using var submit = new NpgsqlCommand("""
            SELECT btrim(candidate_id),state FROM suite.submit_suite_content_origin_candidate(
              $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            """, connection, transaction);
        submit.Parameters.AddWithValue(candidateId);
        submit.Parameters.AddWithValue(itemId);
        submit.Parameters.AddWithValue(context.BaseCatalogIdentity);
        submit.Parameters.AddWithValue(requestId);
        submit.Parameters.AddWithValue(encrypted.Ciphertext);
        submit.Parameters.AddWithValue(encrypted.Nonce);
        submit.Parameters.AddWithValue(encrypted.Tag);
        submit.Parameters.AddWithValue(encrypted.KeyVersion);
        submit.Parameters.AddWithValue(context.ResolvedChangeIntent);
        submit.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Varchar,
            Value = (object?)changeReason ?? DBNull.Value
        });
        submit.Parameters.AddWithValue(actor);
        await using var result = await submit.ExecuteReaderAsync(ct);
        if (!await result.ReadAsync(ct)) throw new ContentAdminException();
        var accepted = new ContentCandidateAccepted(result.GetString(0), result.GetString(1));
        await result.DisposeAsync();
        await transaction.CommitAsync(ct);
        return accepted;
    }

    public async Task RequestCheckAsync(string itemId, string requestId, string actor,
        CancellationToken ct)
    {
        try
        {
            await using var command = dataSource.CreateCommand(
                "SELECT suite.request_suite_content_check($1,$2,$3)");
            command.Parameters.AddWithValue(itemId);
            command.Parameters.AddWithValue(requestId);
            command.Parameters.AddWithValue(actor);
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException) { throw new ContentAdminException(); }
    }

    public async Task TryAuditDenialAsync(string eventType, string actor, string? itemId,
        string detailCode, CancellationToken ct)
    {
        try
        {
            await using var command = dataSource.CreateCommand(
                "SELECT suite.audit_suite_content_management_denial($1,$2,$3,$4,$5)");
            command.Parameters.AddWithValue(eventType);
            command.Parameters.AddWithValue(ContentValidation.ValidActor(actor) ? actor : "unknown");
            command.Parameters.Add(new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Char,
                Value = (object?)itemId ?? DBNull.Value
            });
            command.Parameters.AddWithValue(Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32)).ToLowerInvariant());
            command.Parameters.AddWithValue(detailCode);
            await command.ExecuteNonQueryAsync(ct);
        }
        catch { /* A denial remains denied even if its best-effort audit sink is unavailable. */ }
    }

    public void Dispose()
    {
        protector.Dispose();
        dataSource.Dispose();
    }

    private static string DisplayAvailability(string value) => value == "READY"
        ? "ONLINE" : "EM_MANUTENCAO";
    private static string Required(string key) => Environment.GetEnvironmentVariable(key)?.Trim()
        is { Length: > 0 } value ? value : throw new InvalidOperationException(
        "Content administration configuration is incomplete.");
    private static string ProtectedPath(string fileKey, string credentialKey)
    {
        var file = Environment.GetEnvironmentVariable(fileKey)?.Trim();
        var credential = Environment.GetEnvironmentVariable(credentialKey)?.Trim();
        if (!string.IsNullOrEmpty(file) && !string.IsNullOrEmpty(credential))
            throw new InvalidOperationException("Content administration configuration is invalid.");
        if (string.IsNullOrEmpty(credential))
            return !string.IsNullOrEmpty(file) ? file : throw new InvalidOperationException(
                "Content administration configuration is incomplete.");
        if (credential.Length > 128 || credential.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new InvalidOperationException("Content administration configuration is invalid.");
        var directory = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY")?.Trim();
        if (string.IsNullOrEmpty(directory) || !Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException("Content administration configuration is invalid.");
        return Path.Combine(directory, credential);
    }
}

sealed class ContentCandidateProtector : IDisposable
{
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
        "TurboRamaSuiteContentCandidateUrl/v1\0");
    private readonly byte[] key;
    private readonly HashSet<string> allowedHosts;
    private bool disposed;

    private ContentCandidateProtector(int activeVersion, byte[] key, HashSet<string> allowedHosts) =>
        (ActiveVersion, this.key, this.allowedHosts) = (activeVersion, key, allowedHosts);
    public int ActiveVersion { get; }

    public static async Task<ContentCandidateProtector> LoadAsync(string keyRingPath,
        string allowedHostsPath, CancellationToken ct)
    {
        var keyRingText = await ContentAdminProtectedFile.ReadTextAsync(keyRingPath, 64 * 1024, ct);
        var hostsText = await ContentAdminProtectedFile.ReadTextAsync(allowedHostsPath, 64 * 1024, ct);
        try
        {
            var ring = JsonSerializer.Deserialize<ContentAdminKeyRing>(keyRingText,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
                    MaxDepth = 8
                }) ?? throw new ContentAdminException();
            if (ring.SchemaVersion != 1 || ring.ActiveKeyVersion <= 0 || ring.Keys is null ||
                ring.Keys.Select(item => item.Version).Distinct().Count() != ring.Keys.Count)
                throw new ContentAdminException();
            var active = ring.Keys.SingleOrDefault(item => item.Version == ring.ActiveKeyVersion)
                ?? throw new ContentAdminException();
            var key = Convert.FromBase64String(active.Key ?? string.Empty);
            if (key.Length != 32 || !string.Equals(Convert.ToBase64String(key), active.Key,
                    StringComparison.Ordinal))
            {
                CryptographicOperations.ZeroMemory(key);
                throw new ContentAdminException();
            }
            var hosts = ParseHosts(hostsText);
            return new ContentCandidateProtector(active.Version, key, hosts);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ContentAdminException)
        {
            throw new InvalidOperationException("Content administration configuration is invalid.");
        }
        finally
        {
            keyRingText = string.Empty;
            hostsText = string.Empty;
        }
    }

    internal static ContentCandidateProtector ForTest(byte[] key, params string[] hosts) =>
        new(1, key.ToArray(), new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase));

    public ContentCandidateCipher Encrypt(string candidateUrl, string candidateId,
        string itemId, string baseCatalogIdentity)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrEmpty(candidateUrl) || Encoding.UTF8.GetByteCount(candidateUrl) > 4096 ||
            candidateUrl.Any(character => character is '\r' or '\n' or '\0') ||
            !Uri.TryCreate(candidateUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || uri.HostNameType != UriHostNameType.Dns ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            !allowedHosts.Contains(new IdnMapping().GetAscii(uri.DnsSafeHost).ToLowerInvariant()))
            throw new ContentAdminException();
        var plaintext = Encoding.UTF8.GetBytes(uri.AbsoluteUri);
        if (plaintext.Length is < 1 or > 4096)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new ContentAdminException();
        }
        var aad = CandidateAad(candidateId, itemId, baseCatalogIdentity, ActiveVersion);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
            return new ContentCandidateCipher(ciphertext, nonce, tag, ActiveVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    internal static byte[] CandidateAad(string candidateId, string itemId,
        string baseCatalogIdentity, int keyVersion)
    {
        using var output = new MemoryStream();
        output.Write(Domain);
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteString("candidateId", candidateId);
            writer.WriteString("itemId", itemId);
            writer.WriteString("baseCatalogIdentity", baseCatalogIdentity);
            writer.WriteNumber("keyVersion", keyVersion);
            writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static HashSet<string> ParseHosts(string text)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var value = raw.Trim();
            if (value.Length == 0 || value.StartsWith('#')) continue;
            if (value.Contains('/') || value.Contains(':') || value.Contains('@') ||
                value.Contains('*') || Uri.CheckHostName(value) != UriHostNameType.Dns)
                throw new ContentAdminException();
            hosts.Add(new IdnMapping().GetAscii(value).ToLowerInvariant());
        }
        if (hosts.Count == 0) throw new ContentAdminException();
        return hosts;
    }

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(key);
        disposed = true;
    }
}

sealed class ContentCandidateCipher(byte[] ciphertext, byte[] nonce, byte[] tag,
    int keyVersion) : IDisposable
{
    public byte[] Ciphertext { get; } = ciphertext;
    public byte[] Nonce { get; } = nonce;
    public byte[] Tag { get; } = tag;
    public int KeyVersion { get; } = keyVersion;
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Ciphertext);
        CryptographicOperations.ZeroMemory(Nonce);
        CryptographicOperations.ZeroMemory(Tag);
    }
}

static class ContentAdminSecurity
{
    public static ContentSecurityResult Authorize(HttpContext context, string requiredClaim,
        bool requirePostControls, bool requireSecondStepUp)
    {
        var actor = context.Request.Headers["X-Suite-Admin-Actor"].ToString();
        if (!ContentValidation.ValidActor(actor))
            return ContentSecurityResult.Deny("CONTENT_AUTH_DENIED", "unknown", "AUTH_REQUIRED", 401);
        var claimsText = context.Request.Headers["X-Suite-Admin-Claims"].ToString();
        if (claimsText.Length > 1024)
            return ContentSecurityResult.Deny("CONTENT_AUTH_DENIED", actor, "CLAIM_DENIED", 403);
        var claims = claimsText
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (claims.Length != 1 || !string.Equals(claims[0], requiredClaim, StringComparison.Ordinal))
            return ContentSecurityResult.Deny("CONTENT_AUTH_DENIED", actor, "CLAIM_DENIED", 403);
        var digest = context.Request.Headers["X-Suite-Client-Ip-Digest"].ToString();
        if (!ContentValidation.IsLowerHex(digest, 64))
            return ContentSecurityResult.Deny("CONTENT_AUTH_DENIED", actor, "AUTH_REQUIRED", 401);
        if (requirePostControls && context.Request.Headers["X-Suite-Csrf-Verified"] != "1")
            return ContentSecurityResult.Deny("CONTENT_CSRF_DENIED", actor, "CSRF_DENIED", 403);
        if (requirePostControls && !Fresh(context.Request.Headers["X-Suite-Step-Up-At"].ToString()))
            return ContentSecurityResult.Deny("CONTENT_STEP_UP_DENIED", actor, "STEP_UP_REQUIRED", 403);
        if (requireSecondStepUp &&
            !Fresh(context.Request.Headers["X-Suite-Version-Step-Up-At"].ToString()))
            return ContentSecurityResult.Deny("CONTENT_STEP_UP_DENIED", actor, "STEP_UP_REQUIRED", 403);
        return ContentSecurityResult.Allow(actor, actor + ":" + digest);
    }

    public static bool ConfirmsVersion(HttpContext context, string itemId, int version) =>
        string.Equals(context.Request.Headers["X-Suite-Version-Confirmation"].ToString(),
            itemId + ":" + version.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static bool Fresh(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
            return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return timestamp >= now - 300 && timestamp <= now + 300;
    }
}

sealed class ContentAdminRateLimiter
{
    private readonly ConcurrentDictionary<string, RateBucket> buckets = new();
    private const int MaximumBuckets = 4096;

    public bool TryAcquire(string identity, string itemId, string action, int limit)
    {
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var raw = Encoding.UTF8.GetBytes(identity + "\0" + itemId + "\0" + action);
        var key = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        CryptographicOperations.ZeroMemory(raw);
        if (buckets.Count >= MaximumBuckets && !buckets.ContainsKey(key))
            Sweep(minute);
        if (buckets.Count >= MaximumBuckets && !buckets.ContainsKey(key)) return false;
        var bucket = buckets.GetOrAdd(key, _ => new RateBucket(minute));
        lock (bucket)
        {
            if (bucket.Minute != minute) { bucket.Minute = minute; bucket.Count = 0; }
            if (bucket.Count >= limit) return false;
            bucket.Count++;
            return true;
        }
    }

    internal int Count => buckets.Count;
    private void Sweep(long minute)
    {
        foreach (var pair in buckets)
            if (pair.Value.Minute < minute - 1) buckets.TryRemove(pair.Key, out _);
    }
    private sealed class RateBucket(long minute) { public long Minute = minute; public int Count; }
}

static class ContentAdminProtectedFile
{
    public static async Task<string> ReadTextAsync(string path, long maximum, CancellationToken ct)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 || info.Length > maximum ||
                (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                throw new InvalidOperationException();
            if (OperatingSystem.IsLinux())
            {
                var forbidden = UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                if ((File.GetUnixFileMode(path) & forbidden) != 0) throw new InvalidOperationException();
            }
            var value = await File.ReadAllTextAsync(path, ct);
            if (value.Length == 0 || value.Contains('\0')) throw new InvalidOperationException();
            return value.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new InvalidOperationException("Protected content administration file is invalid.");
        }
    }

    private static bool IsSystemdCredential(string path)
    {
        var directory = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY");
        return !string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory) &&
            string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), StringComparison.Ordinal);
    }
}

static class ContentValidation
{
    private static readonly HashSet<string> ChangeReasons = new(StringComparer.Ordinal)
    { "VENDOR_RELEASE", "SECURITY_UPDATE", "CONTENT_CORRECTION", "PLATFORM_UPDATE" };
    public static bool IsLowerHex(string? value, int length) => value?.Length == length &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ValidActor(string? value) => value is { Length: >= 1 and <= 64 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '@' or '.' or '_' or '-');
    public static bool ValidRequest(string? value) => value is { Length: >= 16 and <= 128 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    public static bool ValidChangeReason(string? value) => value is not null && ChangeReasons.Contains(value);
}

sealed class ContentQuery
{
    public required string AfterItemId { get; init; }
    public required int Limit { get; init; }
    public string? Availability { get; init; }
    public string? ResultCode { get; init; }
    public string? JobState { get; init; }
    public string? ItemPrefix { get; init; }
    public string? Name { get; init; }

    public static bool TryParse(IQueryCollection values, out ContentQuery? query)
    {
        query = null;
        if (!int.TryParse(values["limit"].FirstOrDefault() ?? "100", out var limit) || limit is < 1 or > 1000)
            return false;
        var cursor = values["cursor"].FirstOrDefault();
        var after = cursor is null ? string.Empty : ContentCursor.Decode(cursor);
        if (after is null || after.Length != 0 && !ContentValidation.IsLowerHex(after, 32)) return false;
        var availability = values["availability"].FirstOrDefault() switch
        { null or "" => null, "ONLINE" or "READY" => "READY", "EM_MANUTENCAO" or "MAINTENANCE" => "MAINTENANCE", _ => "!" };
        var result = Empty(values["resultCode"].FirstOrDefault());
        var job = Empty(values["jobState"].FirstOrDefault());
        var prefix = Empty(values["item"].FirstOrDefault());
        var name = Empty(values["name"].FirstOrDefault());
        if (availability == "!" || result?.Length > 64 || job?.Length > 16 ||
            prefix is { Length: > 32 } || prefix?.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')) == true ||
            name is { Length: > 100 } || name?.Any(char.IsControl) == true)
            return false;
        query = new ContentQuery
        {
            AfterItemId = after,
            Limit = limit,
            Availability = availability,
            ResultCode = result,
            JobState = job,
            ItemPrefix = prefix,
            Name = name
        };
        return true;
    }
    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

sealed class ContentAuditQuery
{
    public required long BeforeAuditId { get; init; }
    public required int Limit { get; init; }
    public string? ItemId { get; init; }
    public static bool TryParse(IQueryCollection values, out ContentAuditQuery? query)
    {
        query = null;
        if (!int.TryParse(values["limit"].FirstOrDefault() ?? "100", out var limit) || limit is < 1 or > 100)
            return false;
        var cursor = values["cursor"].FirstOrDefault();
        var decoded = cursor is null ? null : ContentCursor.Decode(cursor);
        if (decoded is not null && !long.TryParse(decoded, NumberStyles.None,
                CultureInfo.InvariantCulture, out _)) return false;
        var before = decoded is null ? long.MaxValue : long.Parse(decoded, CultureInfo.InvariantCulture);
        var item = values["itemId"].FirstOrDefault();
        if (item is not null && !ContentValidation.IsLowerHex(item, 32)) return false;
        query = new ContentAuditQuery { BeforeAuditId = before, Limit = limit, ItemId = item };
        return true;
    }
}

static class ContentCursor
{
    public static string Encode(string value) => Convert.ToBase64String(Encoding.ASCII.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string? Decode(string value)
    {
        if (value.Length is < 1 or > 128 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            return null;
        try
        {
            var canonical = value.Replace('-', '+').Replace('_', '/');
            canonical += new string('=', (4 - canonical.Length % 4) % 4);
            var bytes = Convert.FromBase64String(canonical);
            return bytes.All(b => b is >= 0x20 and <= 0x7e) ? Encoding.ASCII.GetString(bytes) : null;
        }
        catch (FormatException) { return null; }
    }
}

static class ContentManagementSelfTest
{
    public static void Run()
    {
        if (!ContentManagementBootstrap.FailsClosedForSelfTestAsync().GetAwaiter().GetResult())
            throw new InvalidOperationException("content bootstrap did not fail closed");
        if (!ContentManagementRuntime.ReadinessChecksMutationPrivilegesForSelfTest())
            throw new InvalidOperationException("content readiness does not prove mutation privileges");
        var testKey = new byte[32];
        using var protector = ContentCandidateProtector.ForTest(testKey, "example.invalid");
        using var encrypted = protector.Encrypt("https://example.invalid/file.zip",
            new string('a', 64), new string('b', 32), new string('c', 64));
        var aad = ContentCandidateProtector.CandidateAad(new string('a', 64),
            new string('b', 32), new string('c', 64), 1);
        if (!Encoding.ASCII.GetString(aad).StartsWith("TurboRamaSuiteContentCandidateUrl/v1\0{\"candidateId\":", StringComparison.Ordinal) ||
            encrypted.Nonce.Length != 12 || encrypted.Tag.Length != 16)
            throw new InvalidOperationException("candidate envelope self-test failed");
        var plaintext = new byte[encrypted.Ciphertext.Length];
        using (var aes = new AesGcm(testKey, 16))
            aes.Decrypt(encrypted.Nonce, encrypted.Ciphertext, encrypted.Tag, plaintext, aad);
        if (Encoding.UTF8.GetString(plaintext) != "https://example.invalid/file.zip")
            throw new InvalidOperationException("candidate encryption self-test failed");
        CryptographicOperations.ZeroMemory(plaintext);
        CryptographicOperations.ZeroMemory(testKey);
        CryptographicOperations.ZeroMemory(aad);
        var limiter = new ContentAdminRateLimiter();
        for (var index = 0; index < 2; index++)
            if (!limiter.TryAcquire("actor:" + new string('d', 64), new string('e', 32), "version", 2))
                throw new InvalidOperationException("rate limiter rejected valid request");
        if (limiter.TryAcquire("actor:" + new string('d', 64), new string('e', 32), "version", 2) ||
            limiter.Count != 1) throw new InvalidOperationException("rate limiter did not enforce bound");
        var bounded = new ContentAdminRateLimiter();
        var accepted = 0;
        for (var index = 0; index < 4200; index++)
            if (bounded.TryAcquire("actor-" + index.ToString(CultureInfo.InvariantCulture),
                    new string('f', 32), "check", 1)) accepted++;
        if (accepted != 4096 || bounded.Count != 4096)
            throw new InvalidOperationException("rate limiter cardinality is unbounded");
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Suite-Admin-Actor"] = "admin@example.invalid";
        context.Request.Headers["X-Suite-Admin-Claims"] = "suite.content.read";
        context.Request.Headers["X-Suite-Client-Ip-Digest"] = new string('a', 64);
        if (!ContentAdminSecurity.Authorize(context, "suite.content.read", false, false).Allowed ||
            ContentAdminSecurity.Authorize(context, "suite.content.check", true, false).Allowed)
            throw new InvalidOperationException("content claim/CSRF policy self-test failed");
        context.Request.Headers["X-Suite-Admin-Claims"] =
            "suite.content.read,suite.content.version.publish";
        if (ContentAdminSecurity.Authorize(context, "suite.content.read", false, false).Allowed)
            throw new InvalidOperationException("content authorization accepted multiple claims");
        context.Request.Headers["X-Suite-Admin-Claims"] = "suite.content.check";
        context.Request.Headers["X-Suite-Csrf-Verified"] = "1";
        context.Request.Headers["X-Suite-Step-Up-At"] = DateTimeOffset.UtcNow
            .ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        if (!ContentAdminSecurity.Authorize(context, "suite.content.check", true, false).Allowed)
            throw new InvalidOperationException("content step-up policy self-test failed");
        foreach (var extreme in new[] { long.MinValue, long.MaxValue })
        {
            context.Request.Headers["X-Suite-Step-Up-At"] = extreme.ToString(CultureInfo.InvariantCulture);
            if (ContentAdminSecurity.Authorize(context, "suite.content.check", true, false).Allowed)
                throw new InvalidOperationException("content step-up accepted extreme timestamp");
        }
        const string urlPrefix = "https://example.invalid/";
        var exactLimit = urlPrefix + new string('a', 4096 - urlPrefix.Length);
        using (var exact = protector.Encrypt(exactLimit, new string('a', 64),
                   new string('b', 32), new string('c', 64)))
            if (exact.Ciphertext.Length != 4096)
                throw new InvalidOperationException("candidate URL 4096-byte boundary failed");
        var oversizedDenied = false;
        try
        {
            using var ignored = protector.Encrypt(exactLimit + "a", new string('a', 64),
                new string('b', 32), new string('c', 64));
        }
        catch (ContentAdminException) { oversizedDenied = true; }
        if (!oversizedDenied)
            throw new InvalidOperationException("candidate URL 4097-byte boundary accepted");
        try
        {
            _ = protector.Encrypt("https://127.0.0.1/file.zip", new string('a', 64),
            new string('b', 32), new string('c', 64));
        }
        catch (ContentAdminException) { return; }
        throw new InvalidOperationException("candidate host policy self-test failed");
    }
}

sealed class ContentAdminException : Exception { }
sealed class ContentAdminKeyRing
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("activeKeyVersion")] public int ActiveKeyVersion { get; init; }
    [JsonPropertyName("keys")] public List<ContentAdminKey>? Keys { get; init; }
}
sealed class ContentAdminKey
{
    [JsonPropertyName("version")] public int Version { get; init; }
    [JsonPropertyName("key")] public string? Key { get; init; }
}
class ContentCandidateRequest { public string CandidateUrl { get; set; } = ""; public string RequestId { get; set; } = ""; public string Actor { get; set; } = ""; }
sealed class ContentVersionRequest : ContentCandidateRequest { public string ChangeReason { get; set; } = ""; public int ConfirmedArtifactVersion { get; set; } }
sealed record ContentCheckRequest(string RequestId, string Actor);
sealed record ContentCandidateContext(string BaseCatalogIdentity, string CurrentAvailability,
    long? ExpectedContentLength, string? ExpectedSha256, int? ExpectedArtifactVersion,
    string? ExpectedFileExtension, string? ExpectedExtractPolicy, string ResolvedChangeIntent);
sealed record ContentCandidateAccepted(string CandidateId, string State);
sealed record ContentError(string Code, string Message);
sealed record ContentItemView(string ItemId, string DisplayName, string Availability,
    int? ArtifactVersion,
    string? LastCheckedAt,
    string LastResultCode, string? JobState, string? CandidateId, string? JobUpdatedAt);
sealed record ContentItemPage(IReadOnlyList<ContentItemView> Items, string? NextCursor);
sealed record ContentJobView(string CandidateId, string ItemId, string State, string ChangeIntent,
    string ResultCode, string SubmittedAt, string UpdatedAt, string? VerifiedAt, string? PublishedAt);
sealed record ContentAuditView(long AuditId, string EventType, string Actor, string? ItemId,
    string? CandidateId, string? JobId, string CorrelationId, string RequestId, string Outcome,
    string DetailCode, string? PreviousCatalogIdentity, string? NewCatalogIdentity, string OccurredAt);
sealed record ContentAuditPage(IReadOnlyList<ContentAuditView> Events, string? NextCursor);
sealed record ContentSecurityResult(bool Allowed, string EventType, string Actor, string DetailCode,
    int StatusCode, string RateIdentity)
{
    public static ContentSecurityResult Allow(string actor, string rateIdentity) =>
        new(true, "", actor, "", 200, rateIdentity);
    public static ContentSecurityResult Deny(string eventType, string actor, string detailCode, int status) =>
        new(false, eventType, actor, detailCode, status, "");
}
