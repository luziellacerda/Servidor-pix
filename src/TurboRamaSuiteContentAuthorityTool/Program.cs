using TurboRamaSuiteContentAuthorityTool;

return CommandLine.Run(args);

internal static class CommandLine
{
    private static readonly string[] GenerateOptions =
    [
        "--base-url",
        "--content-assertion-public-spki",
        "--tls-pin-current",
        "--tls-pin-next",
        "--issued-at-unix",
        "--expires-at-unix",
        "--issuer-private-key-pem",
        "--output-directory"
    ];

    private static readonly string[] VerifyOptions =
    [
        "--envelope",
        "--issuer-public-spki",
        "--envelope-sha256",
        "--issuer-spki-sha256"
    ];

    internal static int Run(string[] arguments)
    {
        try
        {
            if (arguments.Length == 0 ||
                arguments[0] is "--help" or "-h" or "help")
            {
                PrintHelp();
                return arguments.Length == 0 ? 2 : 0;
            }

            return arguments[0] switch
            {
                "generate" => Generate(ParseOptions(
                    arguments.AsSpan(1), GenerateOptions)),
                "verify" => Verify(ParseOptions(
                    arguments.AsSpan(1), VerifyOptions)),
                _ => throw new AuthorityArtifactException(
                    "The command must be 'generate' or 'verify'.")
            };
        }
        catch (AuthorityArtifactException exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
        catch
        {
            Console.Error.WriteLine(
                "ERROR: The content-authority operation failed safely.");
            return 3;
        }
    }

    private static int Generate(IReadOnlyDictionary<string, string> options)
    {
        var result = AuthorityArtifactGenerator.Generate(
            new AuthorityGenerationRequest(
                Required(options, "--base-url"),
                Required(options, "--content-assertion-public-spki"),
                Required(options, "--tls-pin-current"),
                Optional(options, "--tls-pin-next"),
                RequiredInt64(options, "--issued-at-unix"),
                RequiredInt64(options, "--expires-at-unix"),
                Required(options, "--issuer-private-key-pem"),
                Required(options, "--output-directory")));
        Console.WriteLine(result.ReusedExistingArtifact
            ? "OK: the existing content-authority artifact set is valid and unchanged."
            : "OK: the content-authority artifact set was written atomically.");
        return 0;
    }

    private static int Verify(IReadOnlyDictionary<string, string> options)
    {
        _ = AuthorityArtifactVerifier.Verify(
            new AuthorityVerificationRequest(
                Required(options, "--envelope"),
                Required(options, "--issuer-public-spki"),
                Required(options, "--envelope-sha256"),
                Required(options, "--issuer-spki-sha256")));
        Console.WriteLine(
            "OK: content-authority envelope, hashes, signature and validity are verified.");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(
        ReadOnlySpan<string> arguments,
        IReadOnlyCollection<string> allowedOptions)
    {
        if (arguments.Length % 2 != 0)
        {
            throw new AuthorityArtifactException(
                "Every option must have exactly one value.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
        {
            var name = arguments[index];
            if (!allowedOptions.Contains(name, StringComparer.Ordinal) ||
                !options.TryAdd(name, arguments[index + 1]))
            {
                throw new AuthorityArtifactException(
                    "An option is unknown or repeated.");
            }
        }

        return options;
    }

    private static string Required(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out var value) ||
            string.IsNullOrWhiteSpace(value) ||
            value.Length > 4096)
        {
            throw new AuthorityArtifactException("A required option is missing.");
        }

        return value;
    }

    private static string Optional(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        if (!options.TryGetValue(name, out var value))
        {
            return string.Empty;
        }

        if (value.Length > 4096)
        {
            throw new AuthorityArtifactException("An option is too long.");
        }

        return value;
    }

    private static long RequiredInt64(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        var text = Required(options, name);
        if (!long.TryParse(
                text,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value) ||
            !string.Equals(value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                text,
                StringComparison.Ordinal))
        {
            throw new AuthorityArtifactException(
                "A Unix timestamp option is not canonical decimal.");
        }

        return value;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            TurboRama Suite content-authority offline tool

            Generate an immutable artifact directory:
              generate --base-url <canonical-https-url/> \
                --content-assertion-public-spki <public-spki.der> \
                --tls-pin-current <64-lowercase-hex> \
                [--tls-pin-next <64-lowercase-hex>] \
                --issued-at-unix <seconds> --expires-at-unix <seconds> \
                --issuer-private-key-pem <offline-private.pem> \
                --output-directory <new-directory>

            Verify through independently approved hashes, without a private key:
              verify --envelope <content-authority-envelope.json> \
                --issuer-public-spki <content-authority-issuer.spki.der> \
                --envelope-sha256 <64-lowercase-hex> \
                --issuer-spki-sha256 <64-lowercase-hex>

            The issuer private key is read only by 'generate', is never copied,
            and must remain outside Git and the online server.
            """);
    }
}
