using System.Net;
using System.Net.Sockets;

namespace TurboRamaSuiteContentPublisher;

public static class PublicNetworkPolicy
{
    public static bool IsGloballyRoutable(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsGloballyRoutableV4(bytes),
            AddressFamily.InterNetworkV6 => IsGloballyRoutableV6(bytes),
            _ => false
        };
    }

    private static bool IsGloballyRoutableV4(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4) return false;
        var first = bytes[0];
        var second = bytes[1];
        var third = bytes[2];
        if (first is 0 or 10 or 127 || first >= 224 ||
            first == 100 && second is >= 64 and <= 127 ||
            first == 169 && second == 254 ||
            first == 172 && second is >= 16 and <= 31 ||
            first == 192 && second == 168 ||
            first == 198 && second is 18 or 19)
            return false;
        if (first == 192 &&
            (second == 0 && third is 0 or 2 ||
             second == 31 && third == 196 ||
             second == 52 && third == 193 ||
             second == 88 && third == 99 ||
             second == 175 && third == 48))
            return false;
        return !(first == 198 && second == 51 && third == 100 ||
                 first == 203 && second == 0 && third == 113);
    }

    private static bool IsGloballyRoutableV6(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16 || (bytes[0] & 0xe0) != 0x20)
            return false; // Current global-unicast allocation is 2000::/3.
        if (HasPrefix(bytes, [0x20, 0x01, 0x00], 23) ||
            HasPrefix(bytes, [0x20, 0x01, 0x0d, 0xb8], 32) ||
            HasPrefix(bytes, [0x20, 0x02], 16) ||
            HasPrefix(bytes, [0x3f, 0xfe], 16) ||
            HasPrefix(bytes, [0x3f, 0xff, 0x00], 20))
            return false;
        // ISATAP embeds an IPv4 endpoint in the interface identifier.
        return !(bytes[8] == 0 && bytes[9] == 0 &&
                 bytes[10] == 0x5e && bytes[11] == 0xfe);
    }

    private static bool HasPrefix(
        ReadOnlySpan<byte> address,
        ReadOnlySpan<byte> prefix,
        int prefixBits)
    {
        var completeBytes = prefixBits / 8;
        var remainingBits = prefixBits % 8;
        if (!address[..completeBytes].SequenceEqual(prefix[..completeBytes]))
            return false;
        if (remainingBits == 0) return true;
        var mask = (byte)(0xff << (8 - remainingBits));
        return (address[completeBytes] & mask) == (prefix[completeBytes] & mask);
    }
}
