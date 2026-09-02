using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteOnlineServer;

internal static class SuiteMotherboardInventoryNormalizer
{
    // Frozen by MotherboardIdentity/v1 compatibility vectors. Any semantic
    // change to this set requires a new identity domain and inventory schema.
    internal const int PlaceholderSetVersion = 1;
    private const string FingerprintDomain = "TurboRamaMotherboardIdentity/v1\0";

    internal static string NormalizeDisplay(string? value, int maxUtf8Bytes)
        => NormalizeCore(value, NormalizationForm.FormC, uppercase: false, maxUtf8Bytes);

    internal static string NormalizeIdentity(string? value, int maxUtf8Bytes)
        => NormalizeCore(value, NormalizationForm.FormKC, uppercase: true, maxUtf8Bytes);

    internal static string NormalizeHardwareDisplay(string? value, int maxUtf8Bytes)
    {
        var display = NormalizeDisplay(value, maxUtf8Bytes);
        if (display.Length == 0) return "";

        var identity = NormalizeIdentity(display, maxUtf8Bytes);
        return identity.Length == 0 || IsPlaceholderIdentity(identity) ? "" : display;
    }

    internal static string NormalizeSerial(string? value, int maxUtf8Bytes)
    {
        var serial = NormalizeHardwareDisplay(value, maxUtf8Bytes);
        if (serial.Length == 0) return "";

        var identity = NormalizeIdentity(serial, maxUtf8Bytes);
        var significant = new string(identity
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray());
        if (significant.Length >= 4
            && (significant.All(static character => character == '0')
                || significant.All(static character => character == 'F')))
            return "";

        return serial;
    }

    internal static string NormalizeUuid(string? value)
    {
        var display = NormalizeHardwareDisplay(value, 64);
        if (!Guid.TryParse(display, out var parsed)
            || parsed == Guid.Empty
            || parsed == AllBitsSetGuid)
            return "";

        return parsed.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
    }

    internal static string ComputeFingerprint(
        string? baseboardManufacturer,
        string? baseboardProduct,
        string? baseboardVersion,
        string? baseboardSerial,
        string? systemManufacturer,
        string? systemModel,
        string? systemUuid)
    {
        const int identityLimit = 128;
        var canonical = string.Join('\n',
            "baseboardManufacturer=" + NormalizeHardwareIdentity(
                baseboardManufacturer, identityLimit),
            "baseboardProduct=" + NormalizeHardwareIdentity(
                baseboardProduct, identityLimit),
            "baseboardVersion=" + NormalizeHardwareIdentity(
                baseboardVersion, identityLimit),
            "baseboardSerial=" + NormalizeIdentity(
                NormalizeSerial(baseboardSerial, identityLimit), identityLimit),
            "systemManufacturer=" + NormalizeHardwareIdentity(
                systemManufacturer, identityLimit),
            "systemModel=" + NormalizeHardwareIdentity(systemModel, identityLimit),
            "systemUuid=" + NormalizeIdentity(NormalizeUuid(systemUuid), identityLimit));
        var bytes = Encoding.UTF8.GetBytes(FingerprintDomain + canonical);
        try
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    internal static bool IsPlaceholderIdentity(string identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return identity is
            "TO BE FILLED BY O.E.M."
            or "TO BE FILLED BY O.E.M"
            or "TO BE FILLED BY OEM"
            or "DEFAULT STRING"
            or "SYSTEM PRODUCT NAME"
            or "UNKNOWN"
            or "NONE"
            or "NOT SPECIFIED"
            or "00000000"
            or "FFFFFFFF";
    }

    private static string NormalizeHardwareIdentity(string? value, int maxUtf8Bytes)
        => NormalizeIdentity(
            NormalizeHardwareDisplay(value, maxUtf8Bytes), maxUtf8Bytes);

    private static string NormalizeCore(
        string? value,
        NormalizationForm normalizationForm,
        bool uppercase,
        int maxUtf8Bytes)
    {
        if (string.IsNullOrWhiteSpace(value) || maxUtf8Bytes <= 0) return "";

        string normalized;
        try
        {
            normalized = value.Normalize(normalizationForm);
        }
        catch (ArgumentException)
        {
            return "";
        }

        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length != 0;
                continue;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned)
                continue;

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(rune.ToString());
        }

        if (builder.Length == 0) return "";
        var result = builder.ToString();
        if (uppercase) result = result.ToUpperInvariant();
        return Encoding.UTF8.GetByteCount(result) <= maxUtf8Bytes ? result : "";
    }

    private static Guid AllBitsSetGuid { get; }
        = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
}

