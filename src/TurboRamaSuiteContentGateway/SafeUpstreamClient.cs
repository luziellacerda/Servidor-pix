using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurboRamaSuiteContentPublisher;

namespace TurboRamaSuiteContentGateway;

public sealed class SafeUpstreamClient : IDisposable
{
    private readonly HashSet<string> _allowedHosts;
    private readonly byte[] _allowlistFingerprint;
    private readonly HttpClient _client;

    public SafeUpstreamClient(IEnumerable<string> allowedHosts)
    {
        _allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in allowedHosts)
        {
            var canonical = CanonicalHost(host);
            if (!_allowedHosts.Add(canonical))
                throw new InvalidOperationException("Upstream host allowlist is invalid.");
        }
        if (_allowedHosts.Count == 0)
            throw new InvalidOperationException("Upstream host allowlist is empty.");
        _allowlistFingerprint = ContentDeploymentProofProtocol.AllowlistFingerprint(
            _allowedHosts);

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            MaxConnectionsPerServer = 32,
            ConnectCallback = ConnectAsync,
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online
            }
        };
        _client = new HttpClient(handler, true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("TurboRamaSuite/1.0");
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/octet-stream"));
    }

    public async Task<HttpResponseMessage> SendAsync(
        Uri uri,
        long rangeStart,
        CancellationToken cancellationToken)
    {
        ValidateUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (rangeStart > 0)
            request.Headers.Range = new RangeHeaderValue(rangeStart, null);
        using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        headerTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return await _client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("The upstream response timed out.");
        }
    }

    public void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            Encoding.UTF8.GetByteCount(uri.AbsoluteUri) > ContentUrlKeyRing.MaximumUrlUtf8Bytes ||
            !_allowedHosts.Contains(CanonicalHost(uri.IdnHost)))
            throw new InvalidOperationException("Upstream destination is not authorized.");
    }

    public byte[] CopyAllowlistFingerprint() => _allowlistFingerprint.ToArray();

    public void Dispose()
    {
        _client.Dispose();
        CryptographicOperations.ZeroMemory(_allowlistFingerprint);
    }

    private async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = CanonicalHost(context.DnsEndPoint.Host);
        if (context.DnsEndPoint.Port != 443 || !_allowedHosts.Contains(host))
            throw new HttpRequestException("Upstream destination is not authorized.");
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsForbiddenAddress))
            throw new HttpRequestException("Upstream destination is not authorized.");

        Exception? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream,
                ProtocolType.Tcp)
            { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, 443), cancellationToken);
                return new NetworkStream(socket, true);
            }
            catch (Exception exception) when (exception is SocketException or
                                               OperationCanceledException)
            {
                last = exception;
                socket.Dispose();
                if (exception is OperationCanceledException) throw;
            }
        }
        throw new HttpRequestException("The upstream destination is unavailable.", last);
    }

    private static string CanonicalHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 ||
            IPAddress.TryParse(host, out _))
            throw new InvalidOperationException("Upstream host allowlist is invalid.");
        string ascii;
        try { ascii = new IdnMapping().GetAscii(host.TrimEnd('.')).ToLowerInvariant(); }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("Upstream host allowlist is invalid.",
                exception);
        }
        if (ascii.Length == 0 || ascii.Split('.').Any(label => label.Length is < 1 or > 63 ||
                label[0] == '-' || label[^1] == '-' || label.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) || character == '-'))))
            throw new InvalidOperationException("Upstream host allowlist is invalid.");
        return ascii;
    }

    private static bool IsForbiddenAddress(IPAddress address)
        => !PublicNetworkPolicy.IsGloballyRoutable(address);
}
