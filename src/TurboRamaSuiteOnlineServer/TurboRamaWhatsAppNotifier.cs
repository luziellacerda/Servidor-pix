using System.Net.Http.Json;

namespace TurboRamaSuiteOnlineServer;

public sealed record TurboRamaConnectionNotice(string LicenseId, string Phone, string? DeviceId);

/// <summary>Notificador independente do TurboRama. Fail-closed quando não configurado.</summary>
public sealed class TurboRamaWhatsAppNotifier
{
    private readonly HttpClient _http;
    private readonly string _endpoint = (Environment.GetEnvironmentVariable("TURBORAMA_MENUIA_ENDPOINT")?.Trim() is { Length: > 0 } configured ? configured : "https://chatbot.menuia.com/api/create-message");
    private readonly string _appKey = Environment.GetEnvironmentVariable("TURBORAMA_MENUIA_APPKEY")?.Trim() ?? "";
    private readonly string _authKey = Environment.GetEnvironmentVariable("TURBORAMA_MENUIA_AUTHKEY")?.Trim() ?? "";

    public TurboRamaWhatsAppNotifier(HttpClient http) => _http = http;

    public async Task<bool> NotifyConnectionAsync(string phone, string licenseId, string deviceId, CancellationToken ct)
    {
        if (_endpoint.Length == 0 || _appKey.Length == 0 || _authKey.Length == 0) return false;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 12) return false;
        var message = $"🎮 TurboRama Suite\n\nSua licença {licenseId} foi conectada com sucesso.\nDispositivo: {deviceId[..Math.Min(8, deviceId.Length)]}…\n\nAcesso autorizado.";
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        { Content = JsonContent.Create(new { appkey = _appKey, authkey = _authKey, to = digits, message, sandbox = false }) };
        using var response = await _http.SendAsync(request, ct);
        return (int)response.StatusCode is >= 200 and < 300;
    }
}
