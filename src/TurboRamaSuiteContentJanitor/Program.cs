using Npgsql;

namespace TurboRamaSuiteContentJanitor;

internal static class Program
{
    private const int MaximumBatchSize = 10_000;
    private const int MaximumDrainBatches = 100;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
            return SelfTest();
        if (args.Length != 1 || args[0] != "run-once")
        {
            Console.Error.WriteLine("SUITE CONTENT JANITOR: FAILED code=COMMAND_INVALID");
            return 2;
        }

        try
        {
            var options = JanitorOptions.FromEnvironment();
            RequireProtectedFile(options.ConnectionFile, 16 * 1024);
            var connection = (await File.ReadAllTextAsync(options.ConnectionFile)).Trim();
            try
            {
                ValidateConnectionRole(connection);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                await using var dataSource = NpgsqlDataSource.Create(connection);
                var aggregate = RetentionResult.Empty;
                var batches = 0;
                do
                {
                    var batch = await RunBatchAsync(dataSource, options.BatchSize,
                        timeout.Token);
                    aggregate = aggregate.Add(batch);
                    batches++;
                    if (!batch.HasBacklog)
                    {
                        Console.WriteLine($"SUITE CONTENT JANITOR: OK batches={batches} " +
                                          Summary(aggregate));
                        return 0;
                    }
                } while (batches < MaximumDrainBatches);

                Console.Error.WriteLine(
                    $"SUITE CONTENT JANITOR: FAILED code=RETENTION_BACKLOG batches={batches} " +
                    Summary(aggregate));
                return 4;
            }
            finally { connection = string.Empty; }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("SUITE CONTENT JANITOR: FAILED code=RETENTION_TIMEOUT");
            return 3;
        }
        catch (JanitorFailure exception)
        {
            Console.Error.WriteLine($"SUITE CONTENT JANITOR: FAILED code={exception.Code}");
            return 2;
        }
        catch (Exception exception) when (exception is NpgsqlException or IOException or
                                           UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine("SUITE CONTENT JANITOR: FAILED code=RETENTION_UNAVAILABLE");
            return 3;
        }
    }

    private static async Task<RetentionResult> RunBatchAsync(
        NpgsqlDataSource dataSource,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT * FROM suite.run_suite_content_retention($1)");
        command.Parameters.AddWithValue(batchSize);
        command.CommandTimeout = 60;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new JanitorFailure("RETENTION_RESULT_INVALID");
        var result = new RetentionResult(reader.GetInt64(0), reader.GetInt64(1),
            reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4),
            reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7));
        if (result.Values.Any(value => value < 0) ||
            await reader.ReadAsync(cancellationToken))
            throw new JanitorFailure("RETENTION_RESULT_INVALID");
        return result;
    }

    private static string Summary(RetentionResult result) =>
        $"expired_issued={result.ExpiredIssued} stale_claims={result.StaleClaims} " +
        $"deleted_terminal={result.DeletedTerminal} " +
        $"shredded_candidates={result.ShreddedCandidates} " +
        $"deleted_challenges={result.DeletedChallenges} " +
        $"grant_backlog={result.GrantBacklog} " +
        $"candidate_backlog={result.CandidateBacklog} " +
        $"challenge_backlog={result.ChallengeBacklog}";

    private static int SelfTest()
    {
        if (JanitorOptions.ParseBatchSize(null) != 5_000 ||
            JanitorOptions.ParseBatchSize("1") != 1 ||
            JanitorOptions.ParseBatchSize(MaximumBatchSize.ToString(
                System.Globalization.CultureInfo.InvariantCulture)) != MaximumBatchSize)
            throw new InvalidOperationException("janitor batch parser failed");
        var firstBatch = new RetentionResult(1, 2, 3, 4, 5, 9, 8, 7);
        var finalBatch = new RetentionResult(6, 5, 4, 3, 2, 0, 0, 0);
        var drained = firstBatch.Add(finalBatch);
        if (drained.ExpiredIssued != 7 || drained.DeletedChallenges != 7 ||
            drained.HasBacklog || MaximumDrainBatches != 100)
            throw new InvalidOperationException("janitor bounded drain failed");
        try
        {
            _ = JanitorOptions.ParseBatchSize("10001");
            throw new InvalidOperationException("oversized janitor batch accepted");
        }
        catch (JanitorFailure) { }
        ValidateConnectionRole(
            "Host=/var/run/postgresql;Database=turborama;Username=turborama-suite-content-maintenance");
        try
        {
            ValidateConnectionRole(
                "Host=/var/run/postgresql;Database=turborama;Username=postgres");
            throw new InvalidOperationException("privileged janitor role accepted");
        }
        catch (JanitorFailure) { }
        Console.WriteLine("SUITE CONTENT JANITOR SELF-TEST: OK");
        return 0;
    }

    private static void ValidateConnectionRole(string connection)
    {
        NpgsqlConnectionStringBuilder builder;
        try { builder = new NpgsqlConnectionStringBuilder(connection); }
        catch (ArgumentException) { throw new JanitorFailure("CONNECTION_FILE_INVALID"); }
        if (!string.Equals(builder.Username, "turborama-suite-content-maintenance",
                StringComparison.Ordinal))
            throw new JanitorFailure("CONNECTION_ROLE_INVALID");
    }

    private static void RequireProtectedFile(string path, long maximumBytes)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new JanitorFailure("CONNECTION_FILE_INVALID");
        var info = new FileInfo(Path.GetFullPath(path));
        info.Refresh();
        if (!info.Exists || (info.Attributes & (FileAttributes.Directory |
                                                FileAttributes.ReparsePoint)) != 0 ||
            info.LinkTarget is not null || info.Length is < 1 || info.Length > maximumBytes)
            throw new JanitorFailure("CONNECTION_FILE_INVALID");
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(info.FullName);
            var allowed = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            if ((mode & ~allowed) != 0 || (mode & UnixFileMode.UserRead) == 0)
                throw new JanitorFailure("CONNECTION_FILE_PERMISSIONS_UNSAFE");
        }
    }

    private sealed record JanitorOptions(string ConnectionFile, int BatchSize)
    {
        public static JanitorOptions FromEnvironment()
        {
            var connection = Environment.GetEnvironmentVariable(
                "SUITE_CONTENT_JANITOR_CONNECTION_FILE")?.Trim();
            if (string.IsNullOrEmpty(connection))
                throw new JanitorFailure("CONFIGURATION_MISSING");
            return new JanitorOptions(connection, ParseBatchSize(
                Environment.GetEnvironmentVariable("SUITE_CONTENT_JANITOR_BATCH_SIZE")?.Trim()));
        }

        internal static int ParseBatchSize(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 5_000;
            if (!int.TryParse(value, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var result) ||
                result is < 1 or > MaximumBatchSize)
                throw new JanitorFailure("CONFIGURATION_INVALID");
            return result;
        }
    }

    private sealed record RetentionResult(
        long ExpiredIssued,
        long StaleClaims,
        long DeletedTerminal,
        long ShreddedCandidates,
        long DeletedChallenges,
        long GrantBacklog,
        long CandidateBacklog,
        long ChallengeBacklog)
    {
        public static RetentionResult Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

        public long[] Values =>
            [ExpiredIssued, StaleClaims, DeletedTerminal, ShreddedCandidates,
                DeletedChallenges, GrantBacklog, CandidateBacklog,
                ChallengeBacklog];

        public bool HasBacklog =>
            GrantBacklog != 0 || CandidateBacklog != 0 || ChallengeBacklog != 0;

        public RetentionResult Add(RetentionResult next) => new(
            checked(ExpiredIssued + next.ExpiredIssued),
            checked(StaleClaims + next.StaleClaims),
            checked(DeletedTerminal + next.DeletedTerminal),
            checked(ShreddedCandidates + next.ShreddedCandidates),
            checked(DeletedChallenges + next.DeletedChallenges),
            next.GrantBacklog,
            next.CandidateBacklog,
            next.ChallengeBacklog);
    }

    private sealed class JanitorFailure(string code) : Exception
    {
        public string Code { get; } = code;
    }
}
