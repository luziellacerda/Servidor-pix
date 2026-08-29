using System.Security.Cryptography;

namespace TurboRamaSuiteContentPublisher;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            return SelfTest.Run();

        try
        {
            var options = PublisherOptions.Parse(args);
            using var shutdown = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                shutdown.Cancel();
            };

            return options.Command switch
            {
                PublisherCommand.Validate => await CatalogPublisher.ValidateAsync(options, shutdown.Token),
                PublisherCommand.Probe => await CatalogPublisher.ProbeAsync(options, shutdown.Token),
                PublisherCommand.Publish => await CatalogPublisher.PublishAsync(options, shutdown.Token),
                PublisherCommand.ReconcileEntitlements =>
                    await CatalogPublisher.ReconcileEntitlementsAsync(options, shutdown.Token),
                _ => throw new PublisherFailure("COMMAND_INVALID")
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("SUITE CONTENT PUBLISHER: CANCELLED");
            return 130;
        }
        catch (PublisherFailure ex)
        {
            Console.Error.WriteLine($"SUITE CONTENT PUBLISHER: FAILED code={ex.Code}");
            return 2;
        }
        catch (Exception)
        {
            // Deliberately do not print exception messages: network exceptions commonly
            // contain the upstream URL, which is private catalog material.
            Console.Error.WriteLine("SUITE CONTENT PUBLISHER: FAILED code=UNEXPECTED_FAILURE");
            return 3;
        }
    }
}

internal enum PublisherCommand { Validate, Probe, Publish, ReconcileEntitlements }

internal sealed record PublisherOptions(
    PublisherCommand Command,
    string? CatalogPath,
    string? VisualCatalogPath,
    string? JournalPath,
    string? KeyRingPath,
    Uri? GatewayKeyRingReadinessUri,
    string? AllowedHostsPath,
    string? ConnectionFilePath,
    string? ExpectedInventorySha256,
    string? ExpectedVisualCatalogSha256,
    int ExpectedItemCount,
    int ExpectedRejectedExtraCount,
    int MaximumConcurrency)
{
    public static PublisherOptions Parse(string[] args)
    {
        if (args.Length == 0) throw new PublisherFailure("COMMAND_REQUIRED");
        var command = args[0].ToLowerInvariant() switch
        {
            "validate" => PublisherCommand.Validate,
            "probe" => PublisherCommand.Probe,
            "publish" => PublisherCommand.Publish,
            "reconcile-entitlements" => PublisherCommand.ReconcileEntitlements,
            _ => throw new PublisherFailure("COMMAND_INVALID")
        };
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new PublisherFailure("ARGUMENT_INVALID");
            if (!values.TryAdd(args[index][2..], args[index + 1]))
                throw new PublisherFailure("ARGUMENT_DUPLICATE");
        }

        string? Value(string name, string? environment = null)
            => values.GetValueOrDefault(name) is { Length: > 0 } value
                ? value
                : environment is null ? null : Environment.GetEnvironmentVariable(environment)?.Trim();
        int Number(string name, int fallback, int minimum, int maximum)
        {
            var text = Value(name);
            if (text is null) return fallback;
            if (!int.TryParse(text, out var result) || result < minimum || result > maximum)
                throw new PublisherFailure("ARGUMENT_INVALID");
            return result;
        }

        var options = new PublisherOptions(
            command,
            Value("catalog"),
            Value("visual-catalog"),
            Value("journal"),
            Value("key-ring", "SUITE_CONTENT_KEYRING_FILE"),
            LoopbackGatewayUri(Value("gateway-keyring-readiness-url",
                "SUITE_CONTENT_GATEWAY_KEYRING_READINESS_URL")),
            Value("allowed-hosts", "SUITE_CONTENT_ALLOWED_HOSTS_FILE"),
            Value("connection-file", "SUITE_CONTENT_PUBLISHER_CONNECTION_FILE"),
            Value("expected-inventory-sha256", "SUITE_CONTENT_EXPECTED_INVENTORY_SHA256"),
            Value("expected-visual-sha256", "SUITE_CONTENT_EXPECTED_VISUAL_SHA256"),
            850,
            2,
            Number("max-concurrency", 2, 1, 4));

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "catalog","visual-catalog","journal","key-ring","allowed-hosts","connection-file",
            "gateway-keyring-readiness-url","expected-inventory-sha256",
            "expected-visual-sha256","max-concurrency"
        };
        if (values.Keys.Any(key => !known.Contains(key))) throw new PublisherFailure("ARGUMENT_INVALID");

        if (command is PublisherCommand.Validate or PublisherCommand.Probe or PublisherCommand.Publish)
        {
            Require(options.CatalogPath, "CATALOG_PATH_REQUIRED");
            Require(options.VisualCatalogPath, "VISUAL_CATALOG_PATH_REQUIRED");
            Require(options.AllowedHostsPath, "ALLOWED_HOSTS_PATH_REQUIRED");
        }
        if (command is PublisherCommand.Probe or PublisherCommand.Publish)
        {
            Require(options.ExpectedInventorySha256, "EXPECTED_INVENTORY_DIGEST_REQUIRED");
            Require(options.ExpectedVisualCatalogSha256, "EXPECTED_VISUAL_DIGEST_REQUIRED");
        }
        if (command == PublisherCommand.Publish)
        {
            Require(options.JournalPath, "JOURNAL_PATH_REQUIRED");
            Require(options.KeyRingPath, "KEY_RING_PATH_REQUIRED");
            if (options.GatewayKeyRingReadinessUri is null)
                throw new PublisherFailure("GATEWAY_KEY_RING_READINESS_REQUIRED");
            Require(options.ConnectionFilePath, "CONNECTION_FILE_REQUIRED");
        }
        if (command == PublisherCommand.ReconcileEntitlements)
            Require(options.ConnectionFilePath, "CONNECTION_FILE_REQUIRED");
        return options;
    }

    private static void Require(string? value, string code)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new PublisherFailure(code);
    }

    private static Uri? LoopbackGatewayUri(string? value)
    {
        if (value is null) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || uri.Host != "127.0.0.1" ||
            uri.Port != 5191 || uri.AbsolutePath != "/ready/keyring/prove" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new PublisherFailure("GATEWAY_KEY_RING_READINESS_URL_INVALID");
        return uri;
    }
}

internal sealed class PublisherFailure : Exception
{
    public PublisherFailure(string code) : base(code) => Code = code;
    public string Code { get; }
}
