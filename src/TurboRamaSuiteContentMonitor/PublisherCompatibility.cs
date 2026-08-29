using System.Security.Cryptography;

namespace TurboRamaSuiteContentPublisher;

internal sealed class PublisherFailure : Exception
{
    public PublisherFailure(string code) : base(code) => Code = code;
    public string Code { get; }
}

internal static partial class CatalogLoader
{
    public static string Hex(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(value).ToLowerInvariant();
}

internal static class ProtectedFile
{
    public static void Require(string path, string code, bool requirePrivateMode = true)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length is <= 0 or > 64 * 1024 * 1024 ||
                (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget is not null)
                throw new PublisherFailure(code);
            if (!OperatingSystem.IsLinux() || !requirePrivateMode) return;
            var forbidden = UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            var credentialDirectory = Environment.GetEnvironmentVariable("CREDENTIALS_DIRECTORY");
            var isCredential = !string.IsNullOrWhiteSpace(credentialDirectory) &&
                Path.IsPathFullyQualified(credentialDirectory) &&
                string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(credentialDirectory)),
                    StringComparison.Ordinal);
            if (!isCredential) forbidden |= UnixFileMode.GroupRead;
            if ((File.GetUnixFileMode(path) & forbidden) != 0) throw new PublisherFailure(code);
        }
        catch (PublisherFailure) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PublisherFailure(code);
        }
    }
}
