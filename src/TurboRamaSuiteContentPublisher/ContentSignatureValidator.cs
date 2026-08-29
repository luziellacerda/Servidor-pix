using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace TurboRamaSuiteContentPublisher;

internal static class ContentSignatureValidator
{
    internal const int CaptureLimit = 64 * 1024;
    internal const long MinimumGenericBinLength = 4 * 1024;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.Ordinal)
    {
        ".rar", ".7z", ".iso", ".pbp", ".zip", ".nsp", ".chd", ".cso", ".3ds",
        ".xci", ".rvz", ".wux", ".bin", ".wbfs", ".xml"
    };

    internal static bool IsSupportedExtension(string extension)
        => SupportedExtensions.Contains(extension);

    internal static void ValidateContentType(string extension, string contentType)
    {
        var mediaType = contentType.Trim().ToLowerInvariant();
        var html = mediaType is "text/html" or "application/xhtml+xml";
        var json = mediaType is "application/json" or "text/json" || mediaType.EndsWith("+json", StringComparison.Ordinal);
        var xml = mediaType is "application/xml" or "text/xml" || mediaType.EndsWith("+xml", StringComparison.Ordinal);
        if (html || json || extension != ".xml" && (xml || mediaType.StartsWith("text/", StringComparison.Ordinal)))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        if (extension == ".xml" && mediaType.StartsWith("text/", StringComparison.Ordinal) &&
            mediaType is not ("text/xml" or "text/plain"))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
    }

    internal static void ValidateBinary(
        string extension,
        string contentType,
        long contentLength,
        ReadOnlySpan<byte> captured)
    {
        ValidateContentType(extension, contentType);
        if (!IsSupportedExtension(extension) || extension == ".xml" ||
            LooksLikeMarkupOrJson(captured) || !HasExpectedSignature(extension, contentLength, captured))
            throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
    }

    internal static async Task ValidateXmlAsync(
        Stream stream,
        long contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength <= 0) throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        var settings = new XmlReaderSettings
        {
            Async = true,
            CloseInput = false,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = contentLength >= 512L * 1024 * 1024
                ? 512L * 1024 * 1024
                : contentLength + 1
        };
        try
        {
            using var reader = XmlReader.Create(stream, settings);
            var rootSeen = false;
            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || rootSeen) continue;
                if (!reader.LocalName.Equals("gameList", StringComparison.Ordinal) ||
                    reader.NamespaceURI.Length != 0)
                    throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
                rootSeen = true;
            }
            if (!rootSeen) throw new PublisherFailure("ORIGIN_CONTENT_INVALID");
        }
        catch (PublisherFailure) { throw; }
        catch (XmlException) { throw new PublisherFailure("ORIGIN_CONTENT_INVALID"); }
    }

    private static bool HasExpectedSignature(string extension, long length, ReadOnlySpan<byte> data)
        => extension switch
        {
            ".rar" => length >= 8 && (At(data, 0, "Rar!\x1a\x07\x00"u8) || At(data, 0, "Rar!\x1a\x07\x01\x00"u8)),
            ".7z" => length >= 32 && At(data, 0, new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c }),
            ".iso" => length >= 32 * 1024 && HasIsoVolumeDescriptor(data),
            ".pbp" => length >= 40 && At(data, 0, "\0PBP"u8),
            ".zip" => length >= 22 && (At(data, 0, "PK\x03\x04"u8) || At(data, 0, "PK\x05\x06"u8) || At(data, 0, "PK\x07\x08"u8)),
            ".nsp" => length >= 16 && At(data, 0, "PFS0"u8),
            ".chd" => length >= 16 && At(data, 0, "MComprHD"u8),
            ".cso" => length >= 24 && At(data, 0, "CISO"u8),
            ".3ds" => length >= 0x104 && (At(data, 0x100, "NCSD"u8) || At(data, 0x100, "NCCH"u8)),
            ".xci" => length >= 0x104 && At(data, 0x100, "HEAD"u8),
            ".rvz" => length >= 0x48 && At(data, 0, "RVZ\x01"u8),
            ".wux" => length >= 0x20 && At(data, 0, "WUX0"u8),
            ".bin" => length >= MinimumGenericBinLength && !LooksLikeUnicodeText(data),
            ".wbfs" => length >= 12 && At(data, 0, "WBFS"u8),
            _ => false
        };

    private static bool HasIsoVolumeDescriptor(ReadOnlySpan<byte> data)
    {
        for (var sector = 16; sector < 32; sector++)
        {
            var offset = sector * 2048 + 1;
            if (At(data, offset, "CD001"u8) || At(data, offset, "BEA01"u8) ||
                At(data, offset, "NSR02"u8) || At(data, offset, "NSR03"u8))
                return true;
        }
        return false;
    }

    private static bool At(ReadOnlySpan<byte> data, int offset, ReadOnlySpan<byte> expected)
        => offset >= 0 && data.Length - offset >= expected.Length && data.Slice(offset, expected.Length).SequenceEqual(expected);

    private static bool LooksLikeMarkupOrJson(ReadOnlySpan<byte> data)
    {
        var value = TrimBomAndAsciiWhitespace(data);
        if (value.IsEmpty) return false;
        return value[0] is (byte)'{' or (byte)'[' or (byte)'<';
    }

    private static bool LooksLikeUnicodeText(ReadOnlySpan<byte> data)
        => data.Length >= 2 && (data[..2].SequenceEqual(new byte[] { 0xff, 0xfe }) ||
                                data[..2].SequenceEqual(new byte[] { 0xfe, 0xff }));

    private static ReadOnlySpan<byte> TrimBomAndAsciiWhitespace(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) data = data[3..];
        while (!data.IsEmpty && data[0] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') data = data[1..];
        return data;
    }

}

internal sealed class HashingCaptureStream : Stream
{
    private static readonly TimeSpan ReadInactivityTimeout = TimeSpan.FromSeconds(90);
    private readonly Stream source;
    private readonly IncrementalHash hash;
    private readonly byte[] capture;
    private readonly CancellationToken operationToken;
    private readonly CancellationToken callerToken;
    private readonly CancellationToken totalTimeoutToken;

    public HashingCaptureStream(
        Stream source,
        IncrementalHash hash,
        byte[] capture,
        CancellationToken operationToken,
        CancellationToken callerToken,
        CancellationToken totalTimeoutToken) =>
        (this.source, this.hash, this.capture, this.operationToken, this.callerToken, this.totalTimeoutToken) =
        (source, hash, capture, operationToken, callerToken, totalTimeoutToken);

    public long TotalBytes { get; private set; }
    public int CapturedBytes { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => TotalBytes; set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(operationToken, cancellationToken);
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        idle.CancelAfter(ReadInactivityTimeout);
        int read;
        try { read = await source.ReadAsync(buffer, idle.Token); }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested && totalTimeoutToken.IsCancellationRequested)
        { throw new PublisherFailure("ORIGIN_TOTAL_TIMEOUT"); }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested && idle.IsCancellationRequested)
        { throw new PublisherFailure("ORIGIN_READ_IDLE_TIMEOUT"); }
        if (read <= 0) return read;
        if (read > ContentArtifactLimits.MaximumContentLength - TotalBytes)
            throw new PublisherFailure("ORIGIN_CONTENT_TOO_LARGE");
        hash.AppendData(buffer.Span[..read]);
        var copy = Math.Min(read, capture.Length - CapturedBytes);
        if (copy > 0)
        {
            buffer.Span[..copy].CopyTo(capture.AsSpan(CapturedBytes));
            CapturedBytes += copy;
        }
        TotalBytes = checked(TotalBytes + read);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override int Read(Span<byte> buffer) => throw new NotSupportedException();
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
