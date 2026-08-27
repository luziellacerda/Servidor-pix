using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        MaxDepth = 32
    };
}

sealed record OnlineServerConfiguration(string StateFile, byte[] StateIntegrityKey,
    byte[] StateEncryptionKey, int PaymentExpirationMinutes, bool AllowHttpLoopback)
{
    public static OnlineServerConfiguration Load()
    {
        var stateFile = Environment.GetEnvironmentVariable("TURBORAMA_SERVER_STATE_FILE");
        if (string.IsNullOrWhiteSpace(stateFile))
            throw new SecurityException("TURBORAMA_SERVER_STATE_FILE precisa apontar para o banco de estado protegido.");
        var keyText = Environment.GetEnvironmentVariable("TURBORAMA_SERVER_STATE_KEY") ?? "";
        byte[] key;
        try { key = Convert.FromBase64String(keyText); }
        catch (FormatException ex) { throw new SecurityException("TURBORAMA_SERVER_STATE_KEY e invalida.", ex); }
        if (key.Length != 32) throw new SecurityException("TURBORAMA_SERVER_STATE_KEY deve possuir exatamente 32 bytes.");
        var encryptionText = Environment.GetEnvironmentVariable("TURBORAMA_SERVER_SECRET_KEY") ?? "";
        byte[] encryptionKey;
        try { encryptionKey = Convert.FromBase64String(encryptionText); }
        catch (FormatException ex) { throw new SecurityException("TURBORAMA_SERVER_SECRET_KEY e invalida.", ex); }
        if (encryptionKey.Length != 32)
            throw new SecurityException("TURBORAMA_SERVER_SECRET_KEY deve possuir exatamente 32 bytes.");
        if (CryptographicOperations.FixedTimeEquals(key, encryptionKey))
            throw new SecurityException("As chaves de integridade e de segredos do servidor devem ser diferentes.");
        var expiration = int.TryParse(Environment.GetEnvironmentVariable("TURBORAMA_PAYMENT_EXPIRATION_MINUTES"), out var minutes)
            ? Math.Clamp(minutes, 1, 60) : 15;
        var allowLoopback = string.Equals(Environment.GetEnvironmentVariable("TURBORAMA_ALLOW_HTTP_LOOPBACK"),
            "true", StringComparison.Ordinal);
        return new OnlineServerConfiguration(Path.GetFullPath(stateFile), key, encryptionKey, expiration, allowLoopback);
    }
}

sealed class OnlineServerException : Exception
{
    public OnlineServerException(int statusCode, string internalReason, string diagnostic)
        : base(diagnostic) => (StatusCode, InternalReason) = (statusCode, internalReason);
    public int StatusCode { get; }
    public string InternalReason { get; }
}

sealed class OnlineServerState
{
    public int SchemaVersion { get; set; } = 1;
    public List<OnlineCustomerEntry> Customers { get; set; } = [];
    public List<OnlineLicenseEntry> Licenses { get; set; } = [];
    public List<OnlinePaymentEntry> Payments { get; set; } = [];
    public List<OnlineAuditEntry> Audit { get; set; } = [];
}

sealed class OnlineCustomerEntry
{
    public string CustomerId { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public OnlineMercadoPagoConnection? MercadoPago { get; set; }
}

sealed class OnlineMercadoPagoConnection
{
    public int SchemaVersion { get; set; } = 1;
    public string ExternalPosId { get; set; } = "";
    public string Nonce { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Ciphertext { get; set; } = "";
    public long UpdatedAtUnixSeconds { get; set; }
}

sealed record DecryptedMercadoPagoConnection(string ExternalPosId, string AccessToken);

sealed class OnlineLicenseEntry
{
    public string CustomerId { get; set; } = "";
    public string LicenseId { get; set; } = "";
    public string BindingType { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public bool PixEnabled { get; set; } = true;
    public int MaximumDevices { get; set; } = 1;
    public string ActivationSalt { get; set; } = "";
    public string ActivationHash { get; set; } = "";
    public Dictionary<int, long> PackagePricesCents { get; set; } = [];
    public long ConfigurationVersion { get; set; }
    public long ConfigurationUpdatedAtUnixSeconds { get; set; }
    public List<OnlineDeviceEntry> Devices { get; set; } = [];
}

sealed class OnlineDeviceEntry
{
    public OnlineDeviceDescriptor Descriptor { get; set; } = new(1, "", "", "", "", "", "");
    public string Status { get; set; } = "ACTIVE";
    public long ActivatedAtUnixSeconds { get; set; }
    public long LastContactUnixSeconds { get; set; }
    public string ActiveSessionId { get; set; } = "";
    public long SessionExpiresAtUnixSeconds { get; set; }
    public int RejectedAttempts { get; set; }
    public bool CanManageConfiguration { get; set; }
}

sealed class OnlinePaymentEntry
{
    public string CustomerId { get; set; } = "";
    public string LicenseId { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string ExternalReference { get; set; } = "";
    public long AmountCents { get; set; }
    public int Minutes { get; set; }
    public string ProviderOrderId { get; set; } = "";
    public string QrData { get; set; } = "";
    public string Status { get; set; } = "pending";
    public long CreatedAtUnixSeconds { get; set; }
    public long UpdatedAtUnixSeconds { get; set; }
}

sealed record OnlineAuditEntry(long AtUnixSeconds, string Event, string LicenseId,
    string DeviceId, string Detail);

sealed record AdminDeviceSnapshot(string DeviceId, string Status, string BindingType,
    string AgentVersion, long ActivatedAtUnixSeconds, long LastContactUnixSeconds,
    long SessionExpiresAtUnixSeconds, bool Online, int RejectedAttempts,
    bool CanManageConfiguration);

sealed record AdminLicenseSnapshot(string CustomerId, string LicenseId, string Status,
    bool PixEnabled, string BindingType, int MaximumDevices, long ConfigurationVersion,
    long ConfigurationUpdatedAtUnixSeconds, IReadOnlyDictionary<int, long> PackagePricesCents,
    IReadOnlyList<AdminDeviceSnapshot> Devices, bool MercadoPagoConfigured, string ExternalPosId);

sealed record AdminDashboardSnapshot(long GeneratedAtUnixSeconds,
    int RejectedMachineAttempts, IReadOnlyList<AdminLicenseSnapshot> Licenses,
    IReadOnlyList<OnlineAuditEntry> RecentAudit);

sealed record StateEnvelope(int SchemaVersion, string Payload, string Hmac);

sealed class OnlineStateRepository : IDisposable
{
    private const int ActivationIterations = 210_000;
    private readonly string _path;
    private readonly byte[] _integrityKey;
    private readonly byte[] _encryptionKey;
    private readonly FileStream _processLock;
    private readonly object _gate = new();

    public OnlineStateRepository(string path, ReadOnlySpan<byte> integrityKey,
        ReadOnlySpan<byte> encryptionKey = default)
    {
        _path = Path.GetFullPath(path);
        if (integrityKey.Length != 32) throw new SecurityException("A chave de integridade do estado e invalida.");
        _integrityKey = integrityKey.ToArray();
        if (encryptionKey.IsEmpty) encryptionKey = integrityKey;
        if (encryptionKey.Length != 32) throw new SecurityException("A chave de segredos do estado e invalida.");
        _encryptionKey = encryptionKey.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Pasta de estado invalida."));
        try
        {
            _processLock = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.None, 1, FileOptions.WriteThrough);
        }
        catch (IOException ex)
        {
            CryptographicOperations.ZeroMemory(_integrityKey);
            CryptographicOperations.ZeroMemory(_encryptionKey);
            throw new InvalidOperationException(
                "O estado ja esta aberto por outro processo. Pare o servico antes de executar comandos administrativos.", ex);
        }
        try
        {
            lock (_gate)
            {
                if (!File.Exists(_path)) SaveUnlocked(new OnlineServerState());
                else
                {
                    var state = LoadUnlocked();
                    if (NormalizeState(state)) SaveUnlocked(state);
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _processLock.Dispose();
        CryptographicOperations.ZeroMemory(_integrityKey);
        CryptographicOperations.ZeroMemory(_encryptionKey);
    }

    public string CreateLicense(string customerId, string licenseId, OnlineProtectionProfile profile, int maximumDevices)
    {
        customerId = OnlineLicenseProtocol.RequireIdentifier(customerId, "CustomerId", 4, 64);
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        OnlineProtectionProfileCodec.RequireImplemented(profile);
        if (maximumDevices is < 1 or > 100) throw new SecurityException("Quantidade de maquinas invalida.");
        var activation = Base64Url(RandomNumberGenerator.GetBytes(24));
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashActivation(activation, salt);
        lock (_gate)
        {
            var state = LoadUnlocked();
            if (state.Licenses.Any(item => item.LicenseId.Equals(licenseId, StringComparison.Ordinal)))
                throw new InvalidOperationException("A licenca ja existe.");
            if (!state.Customers.Any(item => item.CustomerId.Equals(customerId, StringComparison.Ordinal)))
                state.Customers.Add(new OnlineCustomerEntry { CustomerId = customerId, Status = "ACTIVE" });
            state.Licenses.Add(new OnlineLicenseEntry
            {
                CustomerId = customerId,
                LicenseId = licenseId,
                BindingType = OnlineProtectionProfileCodec.Format(profile),
                Status = "ACTIVE",
                PixEnabled = true,
                MaximumDevices = maximumDevices,
                ActivationSalt = Convert.ToBase64String(salt),
                ActivationHash = Convert.ToBase64String(hash)
            });
            state.Audit.Add(new OnlineAuditEntry(Now(), "LICENSE_CREATED", licenseId, "", "profile=" + OnlineProtectionProfileCodec.Format(profile)));
            TrimAudit(state);
            SaveUnlocked(state);
        }
        CryptographicOperations.ZeroMemory(salt);
        CryptographicOperations.ZeroMemory(hash);
        return activation;
    }

    public IReadOnlyList<OnlineLicenseEntry> ListLicenses()
    {
        lock (_gate) return LoadUnlocked().Licenses;
    }

    public IReadOnlyList<OnlineDeviceEntry> ListDevices(string licenseId)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        lock (_gate)
        {
            var license = LoadUnlocked().Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            return license.Devices;
        }
    }

    public void SetLicenseStatus(string licenseId, string status)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        status = RequireStatus(status, allowTransfer: true);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            license.Status = status;
            if (status != "ACTIVE")
                foreach (var device in license.Devices) ClearSession(device);
            state.Audit.Add(new OnlineAuditEntry(Now(), "LICENSE_STATUS_CHANGED", licenseId, "", status));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public void SetPixEnabled(string licenseId, bool enabled)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            license.PixEnabled = enabled;
            if (!enabled)
                foreach (var device in license.Devices) ClearSession(device);
            state.Audit.Add(new OnlineAuditEntry(Now(), enabled ? "PIX_ENABLED" : "PIX_DISABLED",
                licenseId, "", "admin_panel"));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public void SetDeviceStatus(string licenseId, string deviceId, string status)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        deviceId = OnlineLicenseProtocol.RequireHex(deviceId, "DeviceId", 64);
        status = RequireStatus(status, allowTransfer: false);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            var device = license.Devices.SingleOrDefault(item => item.Descriptor.DeviceId == deviceId)
                ?? throw new InvalidOperationException("A maquina nao existe.");
            device.Status = status;
            if (status != "ACTIVE") ClearSession(device);
            state.Audit.Add(new OnlineAuditEntry(Now(), "DEVICE_STATUS_CHANGED", licenseId, deviceId, status));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public void ForceReauthentication(string licenseId, string deviceId)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        deviceId = OnlineLicenseProtocol.RequireHex(deviceId, "DeviceId", 64);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            var device = license.Devices.SingleOrDefault(item => item.Descriptor.DeviceId == deviceId)
                ?? throw new InvalidOperationException("A maquina nao existe.");
            ClearSession(device);
            state.Audit.Add(new OnlineAuditEntry(Now(), "FORCE_REAUTH", licenseId, deviceId, "session_cleared"));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public void SetDeviceConfigurationPermission(string licenseId, string deviceId, bool allowed)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        deviceId = OnlineLicenseProtocol.RequireHex(deviceId, "DeviceId", 64);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            var device = license.Devices.SingleOrDefault(item => item.Descriptor.DeviceId == deviceId)
                ?? throw new InvalidOperationException("A maquina nao existe.");
            device.CanManageConfiguration = allowed;
            state.Audit.Add(new OnlineAuditEntry(Now(), "DEVICE_CONFIGURATION_PERMISSION_CHANGED",
                licenseId, deviceId, allowed ? "allowed" : "denied"));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public string IssueActivationCode(string licenseId)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        var activation = Base64Url(RandomNumberGenerator.GetBytes(24));
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashActivation(activation, salt);
        try
        {
            lock (_gate)
            {
                var state = LoadUnlocked();
                var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                    ?? throw new InvalidOperationException("A licenca nao existe.");
                if (license.Status != "ACTIVE") throw new InvalidOperationException("A licenca nao esta ativa.");
                license.ActivationSalt = Convert.ToBase64String(salt);
                license.ActivationHash = Convert.ToBase64String(hash);
                state.Audit.Add(new OnlineAuditEntry(Now(), "ACTIVATION_CODE_ISSUED", licenseId, "", "one_time"));
                TrimAudit(state);
                SaveUnlocked(state);
            }
            return activation;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public string PrepareDeviceTransfer(string licenseId)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        var activation = Base64Url(RandomNumberGenerator.GetBytes(24));
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashActivation(activation, salt);
        try
        {
            lock (_gate)
            {
                var state = LoadUnlocked();
                var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                    ?? throw new InvalidOperationException("A licenca nao existe.");
                if (license.Status is not ("ACTIVE" or "TRANSFER_PENDING"))
                    throw new InvalidOperationException("A licenca nao pode ser transferida neste estado.");

                license.Status = "TRANSFER_PENDING";
                foreach (var device in license.Devices)
                {
                    device.Status = "SUSPENDED";
                    device.CanManageConfiguration = false;
                    ClearSession(device);
                }
                license.ActivationSalt = Convert.ToBase64String(salt);
                license.ActivationHash = Convert.ToBase64String(hash);
                state.Audit.Add(new OnlineAuditEntry(Now(), "DEVICE_TRANSFER_PREPARED", licenseId, "", "one_time"));
                TrimAudit(state);
                SaveUnlocked(state);
            }
            return activation;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public void SetPackagePrices(string licenseId, IReadOnlyDictionary<int, long> prices)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        var required = new[] { 15, 30, 45, 60, 120 };
        if (prices.Count != required.Length || required.Any(minutes => !prices.TryGetValue(minutes, out var cents)
                || cents is < 50 or > 100_000_000))
            throw new SecurityException("A tabela deve conter os cinco pacotes e valores validos em centavos.");
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)
                ?? throw new InvalidOperationException("A licenca nao existe.");
            license.PackagePricesCents = required.ToDictionary(minutes => minutes, minutes => prices[minutes]);
            license.ConfigurationVersion = Math.Max(1, license.ConfigurationVersion + 1);
            license.ConfigurationUpdatedAtUnixSeconds = Now();
            state.Audit.Add(new OnlineAuditEntry(Now(), "PRICE_TABLE_UPDATED", licenseId, "", "admin"));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public OnlinePriceConfigurationResponse ReadPriceConfiguration(string licenseId, string deviceId,
        string sessionId)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        deviceId = OnlineLicenseProtocol.RequireHex(deviceId, "DeviceId", 64);
        sessionId = OnlineLicenseProtocol.RequireHex(sessionId, "SessionId", 64);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = RequireConfigurationSession(state, licenseId, deviceId, sessionId);
            ValidatePrices(license.PackagePricesCents);
            return ToPriceConfiguration(license);
        }
    }

    public OnlinePriceConfigurationResponse UpdatePriceConfigurationFromDevice(string licenseId,
        string deviceId, string sessionId, long expectedVersion, IReadOnlyDictionary<int, long> prices)
    {
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        deviceId = OnlineLicenseProtocol.RequireHex(deviceId, "DeviceId", 64);
        sessionId = OnlineLicenseProtocol.RequireHex(sessionId, "SessionId", 64);
        ValidatePrices(prices);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var license = RequireConfigurationSession(state, licenseId, deviceId, sessionId);
            var device = license.Devices.Single(item => item.Descriptor.DeviceId == deviceId);
            if (!device.CanManageConfiguration)
                throw new OnlineServerException(403, "CONFIGURATION_WRITE_DENIED", "CONFIGURATION_WRITE_DENIED");
            if (license.ConfigurationVersion != expectedVersion)
                throw new OnlineServerException(409, "CONFIGURATION_CONFLICT", "CONFIGURATION_CONFLICT");
            license.PackagePricesCents = RequiredPriceMinutes.ToDictionary(
                minutes => minutes, minutes => prices[minutes]);
            license.ConfigurationVersion = Math.Max(1, license.ConfigurationVersion + 1);
            license.ConfigurationUpdatedAtUnixSeconds = Now();
            state.Audit.Add(new OnlineAuditEntry(Now(), "PRICE_TABLE_UPDATED", licenseId, deviceId,
                "registered_device"));
            TrimAudit(state);
            SaveUnlocked(state);
            return ToPriceConfiguration(license);
        }
    }

    public AdminDashboardSnapshot ReadAdminDashboard(int auditLimit = 100)
    {
        auditLimit = Math.Clamp(auditLimit, 1, 500);
        lock (_gate)
        {
            var state = LoadUnlocked();
            var now = Now();
            var licenses = state.Licenses
                .OrderBy(item => item.LicenseId, StringComparer.Ordinal)
                .Select(license =>
                {
                    var customer = state.Customers.SingleOrDefault(item => item.CustomerId == license.CustomerId);
                    var connection = customer?.MercadoPago;
                    var devices = license.Devices
                        .OrderBy(item => item.Descriptor.DeviceId, StringComparer.Ordinal)
                        .Select(device => new AdminDeviceSnapshot(
                            device.Descriptor.DeviceId,
                            device.Status,
                            device.Descriptor.BindingType,
                            device.Descriptor.AgentVersion,
                            device.ActivatedAtUnixSeconds,
                            device.LastContactUnixSeconds,
                            device.SessionExpiresAtUnixSeconds,
                            device.Status == "ACTIVE" && device.SessionExpiresAtUnixSeconds >= now,
                            device.RejectedAttempts,
                            device.CanManageConfiguration))
                        .ToArray();
                    return new AdminLicenseSnapshot(
                        license.CustomerId,
                        license.LicenseId,
                        license.Status,
                        license.PixEnabled,
                        license.BindingType,
                        license.MaximumDevices,
                        license.ConfigurationVersion,
                        license.ConfigurationUpdatedAtUnixSeconds,
                        new Dictionary<int, long>(license.PackagePricesCents),
                        devices,
                        connection is not null,
                        connection?.ExternalPosId ?? "");
                })
                .ToArray();
            var audit = state.Audit.TakeLast(auditLimit).Reverse().ToArray();
            var rejectedMachineAttempts = state.Audit.Count(item => IsRejectedMachineEvent(item.Event));
            return new AdminDashboardSnapshot(now, rejectedMachineAttempts, licenses, audit);
        }
    }

    public IReadOnlyList<OnlineAuditEntry> ReadAdminAudit(int limit = 20_000)
    {
        limit = Math.Clamp(limit, 1, 20_000);
        lock (_gate)
        {
            var state = LoadUnlocked();
            return state.Audit.TakeLast(limit).Reverse().ToArray();
        }
    }

    public int PurgeAuditOlderThan(int retentionDays, string scope)
    {
        if (retentionDays is not (0 or 30 or 90 or 180))
            throw new SecurityException("Período de retenção inválido.");
        if (scope is not ("security" or "audit"))
            throw new SecurityException("Categoria de histórico inválida.");
        lock (_gate)
        {
            var state = LoadUnlocked();
            var cutoff = retentionDays == 0 ? long.MaxValue : Now() - retentionDays * 86_400L;
            var removed = state.Audit.RemoveAll(item =>
                item.AtUnixSeconds < cutoff && (scope == "security") == IsSecurityEvent(item.Event));
            TrimAudit(state);
            SaveUnlocked(state);
            return removed;
        }
    }

    public void RecordSecurityAttempt(string eventName, string licenseId, string deviceId, string detail)
    {
        eventName = OnlineLicenseProtocol.RequireIdentifier(eventName, "Event", 3, 64);
        licenseId = OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        deviceId = OnlineLicenseProtocol.RequireHex(deviceId, "DeviceId", 64);
        detail = (detail ?? "").Trim();
        if (detail.Length > 128 || detail.Any(char.IsControl))
            throw new SecurityException("O detalhe de seguranca e invalido.");
        lock (_gate)
        {
            var state = LoadUnlocked();
            var knownDevice = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId)?
                .Devices.SingleOrDefault(item => item.Descriptor.DeviceId == deviceId);
            if (knownDevice is not null) knownDevice.RejectedAttempts++;
            state.Audit.Add(new OnlineAuditEntry(Now(), eventName, licenseId, deviceId, detail));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public void RecordAdministrativeEvent(string eventName, string detail)
    {
        eventName = OnlineLicenseProtocol.RequireIdentifier(eventName, "Event", 3, 64);
        detail = (detail ?? "").Trim();
        if (detail.Length > 128 || detail.Any(character => char.IsControl(character)))
            throw new SecurityException("O detalhe administrativo e invalido.");
        lock (_gate)
        {
            var state = LoadUnlocked();
            state.Audit.Add(new OnlineAuditEntry(Now(), eventName, "", "", detail));
            TrimAudit(state);
            SaveUnlocked(state);
        }
    }

    public void SetMercadoPagoConnection(string customerId, string externalPosId, string accessToken)
    {
        customerId = OnlineLicenseProtocol.RequireIdentifier(customerId, "CustomerId", 4, 64);
        externalPosId = (externalPosId ?? "").Trim();
        accessToken = (accessToken ?? "").Trim();
        if (externalPosId.Length is < 1 or > 40 || !externalPosId.All(char.IsAsciiLetterOrDigit))
            throw new SecurityException("O ExternalPosId e invalido.");
        if (accessToken.Length is < 40 or > 384 || !accessToken.StartsWith("APP_USR-", StringComparison.Ordinal)
            || accessToken.Any(char.IsWhiteSpace))
            throw new SecurityException("O Access Token possui formato invalido.");
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintext = Encoding.UTF8.GetBytes(accessToken);
        var ciphertext = new byte[plaintext.Length];
        var aad = Encoding.UTF8.GetBytes("TurboRamaMercadoPagoConnection/v1\0" + customerId);
        try
        {
            using (var aes = new AesGcm(_encryptionKey, tag.Length))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
            lock (_gate)
            {
                var state = LoadUnlocked();
                var customer = state.Customers.SingleOrDefault(item => item.CustomerId == customerId);
                if (customer is null)
                {
                    customer = new OnlineCustomerEntry { CustomerId = customerId, Status = "ACTIVE" };
                    state.Customers.Add(customer);
                }
                customer.MercadoPago = new OnlineMercadoPagoConnection
                {
                    ExternalPosId = externalPosId,
                    Nonce = Convert.ToBase64String(nonce),
                    Tag = Convert.ToBase64String(tag),
                    Ciphertext = Convert.ToBase64String(ciphertext),
                    UpdatedAtUnixSeconds = Now()
                };
                state.Audit.Add(new OnlineAuditEntry(Now(), "MERCADOPAGO_CONNECTION_UPDATED", "", "", customerId));
                TrimAudit(state);
                SaveUnlocked(state);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    public DecryptedMercadoPagoConnection GetMercadoPagoConnection(string customerId)
    {
        customerId = OnlineLicenseProtocol.RequireIdentifier(customerId, "CustomerId", 4, 64);
        OnlineMercadoPagoConnection connection;
        lock (_gate)
        {
            var customer = LoadUnlocked().Customers.SingleOrDefault(item => item.CustomerId == customerId);
            if (customer is null || customer.Status != "ACTIVE" || customer.MercadoPago is null)
                throw new OnlineServerException(503, "PAYMENT_ACCOUNT_UNAVAILABLE", "MERCADOPAGO_NOT_CONFIGURED");
            connection = customer.MercadoPago;
        }
        byte[] nonce;
        byte[] tag;
        byte[] ciphertext;
        try
        {
            nonce = Convert.FromBase64String(connection.Nonce);
            tag = Convert.FromBase64String(connection.Tag);
            ciphertext = Convert.FromBase64String(connection.Ciphertext);
        }
        catch (FormatException ex) { throw new SecurityException("A conexao Mercado Pago protegida esta corrompida.", ex); }
        var plaintext = new byte[ciphertext.Length];
        var aad = Encoding.UTF8.GetBytes("TurboRamaMercadoPagoConnection/v1\0" + customerId);
        try
        {
            if (nonce.Length != 12 || tag.Length != 16 || ciphertext.Length is < 40 or > 384)
                throw new SecurityException("A conexao Mercado Pago protegida possui tamanho invalido.");
            using (var aes = new AesGcm(_encryptionKey, tag.Length))
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            var token = Encoding.UTF8.GetString(plaintext);
            if (!token.StartsWith("APP_USR-", StringComparison.Ordinal) || token.Any(char.IsWhiteSpace))
                throw new SecurityException("A conexao Mercado Pago protegida nao pode ser validada.");
            return new DecryptedMercadoPagoConnection(connection.ExternalPosId, token);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(aad);
        }
    }

    public T Read<T>(Func<OnlineServerState, T> action)
    {
        lock (_gate) return action(LoadUnlocked());
    }

    public T Update<T>(Func<OnlineServerState, T> action)
    {
        lock (_gate)
        {
            var state = LoadUnlocked();
            var result = action(state);
            TrimAudit(state);
            SaveUnlocked(state);
            return result;
        }
    }

    public bool VerifyActivationCode(OnlineLicenseEntry license, string activationCode)
    {
        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(license.ActivationSalt);
            expected = Convert.FromBase64String(license.ActivationHash);
        }
        catch (FormatException) { return false; }
        var actual = HashActivation(activationCode, salt);
        try { return expected.Length == 32 && CryptographicOperations.FixedTimeEquals(expected, actual); }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private OnlineServerState LoadUnlocked()
    {
        var bytes = File.ReadAllBytes(_path);
        if (bytes.Length is < 32 or > 32 * 1024 * 1024) throw new SecurityException("O estado do servidor possui tamanho invalido.");
        var envelope = JsonSerializer.Deserialize<StateEnvelope>(bytes, Json.Options)
            ?? throw new SecurityException("O estado do servidor esta vazio.");
        if (envelope.SchemaVersion != 1) throw new SecurityException("A versao do estado do servidor e invalida.");
        byte[] payload;
        byte[] expected;
        try { payload = Convert.FromBase64String(envelope.Payload); expected = Convert.FromBase64String(envelope.Hmac); }
        catch (FormatException ex) { throw new SecurityException("O estado do servidor esta corrompido.", ex); }
        using var hmac = new HMACSHA256(_integrityKey);
        var actual = hmac.ComputeHash(payload);
        try
        {
            if (expected.Length != 32 || !CryptographicOperations.FixedTimeEquals(expected, actual))
                throw new SecurityException("A integridade do estado do servidor foi violada.");
            var state = JsonSerializer.Deserialize<OnlineServerState>(payload, Json.Options)
                ?? throw new SecurityException("O estado do servidor nao pode ser lido.");
            if (state.SchemaVersion != 1) throw new SecurityException("A versao interna do estado e invalida.");
            return state;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private static bool NormalizeState(OnlineServerState state)
    {
        var changed = false;
        foreach (var license in state.Licenses)
        {
            license.PackagePricesCents ??= [];
            license.Devices ??= [];
            if (license.ConfigurationVersion < 1 && PricesAreValid(license.PackagePricesCents))
            {
                license.ConfigurationVersion = 1;
                license.ConfigurationUpdatedAtUnixSeconds = Math.Max(1, Now());
                state.Audit.Add(new OnlineAuditEntry(Now(), "CONFIGURATION_MIGRATED",
                    license.LicenseId, "", "existing_prices_preserved"));
                changed = true;
            }
            if (license.Devices.Count != 0 && !license.Devices.Any(device => device.CanManageConfiguration))
            {
                var firstActive = license.Devices
                    .Where(device => device.Status == "ACTIVE")
                    .OrderBy(device => device.ActivatedAtUnixSeconds)
                    .FirstOrDefault();
                if (firstActive is not null)
                {
                    firstActive.CanManageConfiguration = true;
                    state.Audit.Add(new OnlineAuditEntry(Now(), "DEVICE_CONFIGURATION_PERMISSION_MIGRATED",
                        license.LicenseId, firstActive.Descriptor.DeviceId, "first_active_device"));
                    changed = true;
                }
            }
        }
        if (changed) TrimAudit(state);
        return changed;
    }

    private void SaveUnlocked(OnlineServerState state)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(state, Json.Options);
        using var hmac = new HMACSHA256(_integrityKey);
        var mac = hmac.ComputeHash(payload);
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new StateEnvelope(1,
            Convert.ToBase64String(payload), Convert.ToBase64String(mac)), Json.Options);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(envelope);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, _path, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
            CryptographicOperations.ZeroMemory(mac);
            CryptographicOperations.ZeroMemory(envelope);
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    private static byte[] HashActivation(string code, byte[] salt)
    {
        var bytes = Encoding.UTF8.GetBytes(code);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(bytes, salt, ActivationIterations,
                HashAlgorithmName.SHA256, 32);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string Base64Url(byte[] bytes)
    {
        try { return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static readonly int[] RequiredPriceMinutes = [15, 30, 45, 60, 120];
    private static readonly HashSet<string> RejectedMachineEvents = new(StringComparer.Ordinal)
    {
        "ACTIVATION_INVALID", "BINDING_DOWNGRADE_DENIED", "DEVICE_LIMIT_REACHED",
        "DEVICE_UNKNOWN", "MACHINE_PROOF_INVALID", "MACHINE_BINDING_MISMATCH",
        "DUPLICATE_SESSION_DENIED"
    };
    private static bool IsRejectedMachineEvent(string eventName) => RejectedMachineEvents.Contains(eventName);
    private static bool IsSecurityEvent(string eventName)
        => eventName.Contains("MISMATCH", StringComparison.Ordinal)
            || eventName.Contains("INVALID", StringComparison.Ordinal)
            || eventName.Contains("DENIED", StringComparison.Ordinal)
            || eventName.Contains("REVOKED", StringComparison.Ordinal)
            || eventName.Contains("FAILED", StringComparison.Ordinal)
            || eventName.Contains("SUSPENDED", StringComparison.Ordinal)
            || eventName.Contains("DISABLED", StringComparison.Ordinal)
            || eventName.Contains("BLOCKED", StringComparison.Ordinal)
            || eventName.Contains("REAUTH", StringComparison.Ordinal)
            || eventName.Contains("TRANSFER", StringComparison.Ordinal);
    private static bool PricesAreValid(IReadOnlyDictionary<int, long> prices)
        => prices.Count == RequiredPriceMinutes.Length
            && RequiredPriceMinutes.All(minutes => prices.TryGetValue(minutes, out var cents)
                && cents is >= 50 and <= 100_000_000);
    private static void ValidatePrices(IReadOnlyDictionary<int, long> prices)
    {
        if (!PricesAreValid(prices))
            throw new SecurityException("A tabela deve conter os cinco pacotes e valores validos em centavos.");
    }
    private static OnlinePriceConfigurationResponse ToPriceConfiguration(OnlineLicenseEntry license)
        => new(OnlineLicenseProtocol.SchemaVersion, license.LicenseId, license.ConfigurationVersion,
            license.PixEnabled, new Dictionary<int, long>(license.PackagePricesCents),
            license.ConfigurationUpdatedAtUnixSeconds);
    private static OnlineLicenseEntry RequireConfigurationSession(OnlineServerState state,
        string licenseId, string deviceId, string sessionId)
    {
        var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId);
        var customer = license is null ? null
            : state.Customers.SingleOrDefault(item => item.CustomerId == license.CustomerId);
        if (license is null || license.Status != "ACTIVE" || customer is null || customer.Status != "ACTIVE")
            throw new OnlineServerException(403, "LICENSE_REVOKED", "LICENSE_REVOKED");
        var device = license.Devices.SingleOrDefault(item => item.Descriptor.DeviceId == deviceId);
        if (device is null || device.Status != "ACTIVE")
            throw new OnlineServerException(403, "MACHINE_BINDING_MISMATCH", "MACHINE_BINDING_MISMATCH");
        if (device.SessionExpiresAtUnixSeconds < Now()
            || !device.ActiveSessionId.Equals(sessionId, StringComparison.Ordinal))
            throw new OnlineServerException(409, "SESSION_EXPIRED", "SESSION_EXPIRED");
        return license;
    }
    private static string RequireStatus(string? status, bool allowTransfer)
    {
        var normalized = (status ?? "").Trim().ToUpperInvariant();
        if (normalized is "ACTIVE" or "SUSPENDED" or "REVOKED" or "MAINTENANCE") return normalized;
        if (allowTransfer && normalized == "TRANSFER_PENDING") return normalized;
        throw new SecurityException("O status administrativo e invalido.");
    }
    private static void ClearSession(OnlineDeviceEntry device)
    {
        device.ActiveSessionId = "";
        device.SessionExpiresAtUnixSeconds = 0;
    }
    private static void TrimAudit(OnlineServerState state)
    {
        if (state.Audit.Count > 20_000) state.Audit.RemoveRange(0, state.Audit.Count - 20_000);
    }
}

sealed record StoredChallenge(OnlineChallengeResponse Response, string LicenseId, string DeviceId,
    string SessionId, string Action, string ContextHash, string ActivationVerifier);

sealed class OnlineLicensingService
{
    private const int ChallengeLifetimeSeconds = 60;
    private const int SessionLifetimeSeconds = 180;
    private readonly OnlineStateRepository _repository;
    private readonly IPixPaymentGateway _payments;
    private readonly ConcurrentDictionary<string, StoredChallenge> _challenges = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _paymentLocks = new(StringComparer.Ordinal);

    public OnlineLicensingService(OnlineStateRepository repository, IPixPaymentGateway payments)
        => (_repository, _payments) = (repository, payments);

    public object Readiness() => new { schemaVersion = 1, ready = _payments.IsReady, service = "turborama-online" };

    public Task<OnlineChallengeResponse> CreateActivationChallengeAsync(OnlineActivationChallengeRequest request,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        token.ThrowIfCancellationRequested();
        var activationCode = request.ActivationCode ?? "";
        if (request.SchemaVersion != 1 || activationCode.Length is < 16 or > 128
            || activationCode.Any(char.IsWhiteSpace)) Deny("ACTIVATION_INVALID");
        var spki = OnlineLicenseProtocol.ParseAndValidateSpki(request.Device);
        CryptographicOperations.ZeroMemory(spki);
        var contextHash = OnlineLicenseProtocol.ActivationContextHash(request.LicenseId, request.Device);
        string activationVerifier;
        try
        {
            activationVerifier = _repository.Read(state =>
            {
                var license = RequireLicenseForActivation(state, request.LicenseId);
                if (!license.BindingType.Equals(request.Device.BindingType, StringComparison.Ordinal))
                    Deny("BINDING_DOWNGRADE_DENIED");
                if (string.IsNullOrEmpty(license.ActivationHash)
                    || !_repository.VerifyActivationCode(license, activationCode))
                    Deny("ACTIVATION_INVALID");
                if (license.Devices.Count(device => device.Status == "ACTIVE") >= license.MaximumDevices
                    && !license.Devices.Any(device => device.Descriptor.DeviceId == request.Device.DeviceId))
                    Deny("DEVICE_LIMIT_REACHED");
                return license.ActivationHash;
            });
        }
        catch (OnlineServerException ex) when (IsRejectedMachineReason(ex.InternalReason))
        {
            _repository.RecordSecurityAttempt(ex.InternalReason, request.LicenseId,
                request.Device.DeviceId, "activation_challenge_denied");
            throw;
        }
        return Task.FromResult(CreateChallenge(request.LicenseId, request.Device.DeviceId, "",
            "device.activate", contextHash, activationVerifier));
    }

    public Task<OnlineActivationResult> CompleteActivationAsync(OnlineActivationProof proof, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(proof.Device);
        token.ThrowIfCancellationRequested();
        var challenge = ConsumeChallenge(proof.ChallengeId, "device.activate");
        var contextHash = OnlineLicenseProtocol.ActivationContextHash(proof.LicenseId, proof.Device);
        RequireChallengeMatch(challenge, proof.LicenseId, proof.Device.DeviceId, "", "device.activate", contextHash);
        try
        {
            if (!OnlineLicenseProtocol.VerifyProof(proof.Device, challenge.Response, proof.LicenseId,
                    "", "device.activate", contextHash, proof.Signature))
                Deny("MACHINE_PROOF_INVALID");
            var result = _repository.Update(state =>
            {
                var license = RequireLicenseForActivation(state, proof.LicenseId);
                var transferPending = license.Status == "TRANSFER_PENDING";
                if (!license.BindingType.Equals(proof.Device.BindingType, StringComparison.Ordinal))
                    Deny("BINDING_DOWNGRADE_DENIED");
                if (string.IsNullOrEmpty(challenge.ActivationVerifier)
                    || !FixedBase64Equals(license.ActivationHash, challenge.ActivationVerifier))
                    Deny("ACTIVATION_INVALID");
                var device = license.Devices.SingleOrDefault(item => item.Descriptor.DeviceId == proof.Device.DeviceId);
                if (device is null)
                {
                    if (license.Devices.Count(item => item.Status == "ACTIVE") >= license.MaximumDevices)
                        Deny("DEVICE_LIMIT_REACHED");
                    device = new OnlineDeviceEntry
                    {
                        Descriptor = proof.Device,
                        Status = "ACTIVE",
                        ActivatedAtUnixSeconds = Now(),
                        LastContactUnixSeconds = Now(),
                        CanManageConfiguration = transferPending || license.Devices.Count == 0
                    };
                    license.Devices.Add(device);
                }
                else
                {
                    if (!DescriptorsEqual(device.Descriptor, proof.Device))
                    {
                        if (!transferPending) Deny("MACHINE_BINDING_MISMATCH");
                        device.Descriptor = proof.Device;
                        device.ActivatedAtUnixSeconds = Now();
                        device.RejectedAttempts = 0;
                    }
                    device.Status = "ACTIVE";
                    device.LastContactUnixSeconds = Now();
                    if (transferPending) device.CanManageConfiguration = true;
                }
                if (transferPending) license.Status = "ACTIVE";
                // O codigo validado ao criar o desafio e de uso unico. Um novo
                // cadastro ou transferencia exige emissao administrativa de outro.
                license.ActivationSalt = "";
                license.ActivationHash = "";
                state.Audit.Add(new OnlineAuditEntry(Now(), "DEVICE_ACTIVATED", license.LicenseId,
                    proof.Device.DeviceId, transferPending ? "transfer:" + proof.Device.BindingType : proof.Device.BindingType));
                return new OnlineActivationResult(1, "ACTIVE", proof.Device.DeviceId, proof.Device.BindingType);
            });
            return Task.FromResult(result);
        }
        catch (OnlineServerException ex) when (IsRejectedMachineReason(ex.InternalReason))
        {
            _repository.RecordSecurityAttempt(ex.InternalReason, proof.LicenseId,
                proof.Device.DeviceId, "activation_completion_denied");
            throw;
        }
    }

    public Task<OnlineChallengeResponse> CreateOperationChallengeAsync(OnlineChallengeRequest request,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        token.ThrowIfCancellationRequested();
        if (request.SchemaVersion != 1) Deny("INVALID_PROTOCOL");
        if (request.Action is not ("session.open" or "session.heartbeat" or "payment.create" or "payment.read"
            or "configuration.read" or "configuration.write"))
            Deny("ACTION_INVALID");
        OnlineLicenseProtocol.RequireHex(request.DeviceId, "DeviceId", 64);
        OnlineLicenseProtocol.RequireHex(request.SessionId, "SessionId", 64);
        OnlineLicenseProtocol.RequireHex(request.ContextHash, "ContextHash", 64);
        try
        {
            _repository.Read(state =>
            {
                var license = RequireActiveLicense(state, request.LicenseId);
                var device = RequireActiveDevice(license, request.DeviceId);
                if (request.Action != "session.open") RequireActiveSession(device, request.SessionId);
                return 0;
            });
        }
        catch (OnlineServerException ex) when (IsRejectedMachineReason(ex.InternalReason))
        {
            _repository.RecordSecurityAttempt(ex.InternalReason, request.LicenseId,
                request.DeviceId, "operation_challenge_denied");
            throw;
        }
        return Task.FromResult(CreateChallenge(request.LicenseId, request.DeviceId, request.SessionId,
            request.Action, request.ContextHash, ""));
    }

    public Task<OnlineActivationResult> CompleteSessionAsync(OnlineSessionProof request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proof);
        ArgumentNullException.ThrowIfNull(request.Context);
        token.ThrowIfCancellationRequested();
        var contextHash = OnlineLicenseProtocol.ContextHash(request.Context);
        var challenge = VerifyOperationProof(request.Proof, contextHash);
        if (request.Context.SessionId != request.Proof.SessionId) Deny("SESSION_MISMATCH");
        var outcome = _repository.Update(state =>
        {
            var license = RequireActiveLicense(state, request.Proof.LicenseId);
            var device = RequireActiveDevice(license, request.Proof.DeviceId);
            var now = Now();
            if (!HardwareFingerprintMatches(device, request.Context.HardwareFingerprint))
            {
                device.RejectedAttempts++;
                state.Audit.Add(new OnlineAuditEntry(now, "MACHINE_BINDING_MISMATCH", license.LicenseId,
                    device.Descriptor.DeviceId, "original_session_preserved"));
                return (Accepted: false, Result: (OnlineActivationResult?)null,
                    Reason: "MACHINE_BINDING_MISMATCH", StatusCode: 403);
            }
            if (request.Proof.Action == "session.open"
                && device.SessionExpiresAtUnixSeconds > now
                && !device.ActiveSessionId.Equals(request.Proof.SessionId, StringComparison.Ordinal))
            {
                device.RejectedAttempts++;
                state.Audit.Add(new OnlineAuditEntry(now, "DUPLICATE_SESSION_DENIED", license.LicenseId,
                    device.Descriptor.DeviceId, "original_session_preserved"));
                return (Accepted: false, Result: (OnlineActivationResult?)null,
                    Reason: "DUPLICATE_DEVICE", StatusCode: 409);
            }
            if (request.Proof.Action == "session.heartbeat")
                RequireActiveSession(device, request.Proof.SessionId);
            device.ActiveSessionId = request.Proof.SessionId;
            device.SessionExpiresAtUnixSeconds = now + SessionLifetimeSeconds;
            device.LastContactUnixSeconds = now;
            return (Accepted: true, Result: (OnlineActivationResult?)new OnlineActivationResult(
                1, "ACTIVE", device.Descriptor.DeviceId, device.Descriptor.BindingType),
                Reason: "", StatusCode: 200);
        });
        if (!outcome.Accepted) Deny(outcome.Reason, outcome.StatusCode);
        _ = challenge;
        return Task.FromResult(outcome.Result!);
    }

    public async Task<OnlineOrderResponse> CreateOrderAsync(OnlinePaymentCreateProof request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proof);
        ArgumentNullException.ThrowIfNull(request.Context);
        var contextHash = OnlineLicenseProtocol.ContextHash(request.Context);
        _ = VerifyOperationProof(request.Proof, contextHash);
        if (request.Proof.Action != "payment.create" || request.Context.SessionId != request.Proof.SessionId)
            Deny("ACTION_MISMATCH");
        var now = Now();
        if (request.Context.RequestExpiresAtUnixSeconds <= now
            || request.Context.RequestExpiresAtUnixSeconds > now + 3660)
            Deny("PAYMENT_REQUEST_EXPIRED");
        var paymentKey = request.Proof.LicenseId + "\0" + request.Context.ExternalReference;
        var paymentLock = _paymentLocks.GetOrAdd(paymentKey, _ => new SemaphoreSlim(1, 1));
        await paymentLock.WaitAsync(token);
        try
        {
            var customerId = _repository.Read(state =>
            {
                var license = RequireActiveLicense(state, request.Proof.LicenseId);
                if (!license.PixEnabled) Deny("PIX_DISABLED");
                var device = RequireActiveDevice(license, request.Proof.DeviceId);
                RequireActiveSession(device, request.Proof.SessionId);
                if (!license.PackagePricesCents.TryGetValue(request.Context.Minutes, out var expectedCents)
                    || expectedCents != request.Context.AmountCents)
                    Deny("PRICE_MISMATCH");
                return license.CustomerId;
            });
            var existing = _repository.Read(state => state.Payments.SingleOrDefault(payment =>
                payment.LicenseId == request.Proof.LicenseId
                && payment.ExternalReference == request.Context.ExternalReference));
            if (existing is not null) return ToResponse(existing);
            var idempotencyKey = PaymentIdempotencyKey(request.Proof.LicenseId,
                request.Proof.DeviceId, request.Context.ExternalReference);
            var created = await _payments.CreateAsync(customerId, request.Context, idempotencyKey, token);
            if (created.ExternalReference != request.Context.ExternalReference
                || created.AmountCents != request.Context.AmountCents || created.Currency != "BRL"
                || created.Status != "pending") Deny("PROVIDER_RESPONSE_MISMATCH", 502);
            return _repository.Update(state =>
            {
                var duplicate = state.Payments.SingleOrDefault(payment => payment.LicenseId == request.Proof.LicenseId
                    && payment.ExternalReference == request.Context.ExternalReference);
                if (duplicate is not null) return ToResponse(duplicate);
                var entry = new OnlinePaymentEntry
                {
                    CustomerId = customerId,
                    LicenseId = request.Proof.LicenseId,
                    DeviceId = request.Proof.DeviceId,
                    ExternalReference = request.Context.ExternalReference,
                    AmountCents = request.Context.AmountCents,
                    Minutes = request.Context.Minutes,
                    ProviderOrderId = created.ProviderOrderId,
                    QrData = created.QrData,
                    Status = created.Status,
                    CreatedAtUnixSeconds = Now(),
                    UpdatedAtUnixSeconds = Now()
                };
                state.Payments.Add(entry);
                state.Audit.Add(new OnlineAuditEntry(Now(), "PAYMENT_CREATED", entry.LicenseId,
                    entry.DeviceId, entry.ExternalReference));
                return ToResponse(entry);
            });
        }
        finally { paymentLock.Release(); }
    }

    public Task<OnlinePriceConfigurationResponse> ReadConfigurationAsync(
        OnlineConfigurationReadProof request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proof);
        ArgumentNullException.ThrowIfNull(request.Context);
        token.ThrowIfCancellationRequested();
        var contextHash = OnlineLicenseProtocol.ContextHash(request.Context);
        _ = VerifyOperationProof(request.Proof, contextHash);
        if (request.Proof.Action != "configuration.read"
            || request.Context.SessionId != request.Proof.SessionId)
            Deny("ACTION_MISMATCH");
        _repository.Read(state =>
        {
            var license = RequireActiveLicense(state, request.Proof.LicenseId);
            var device = RequireActiveDevice(license, request.Proof.DeviceId);
            RequireActiveSession(device, request.Proof.SessionId);
            return 0;
        });
        return Task.FromResult(_repository.ReadPriceConfiguration(
            request.Proof.LicenseId, request.Proof.DeviceId, request.Proof.SessionId));
    }

    public Task<OnlinePriceConfigurationResponse> WriteConfigurationAsync(
        OnlineConfigurationWriteProof request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proof);
        ArgumentNullException.ThrowIfNull(request.Context);
        token.ThrowIfCancellationRequested();
        var contextHash = OnlineLicenseProtocol.ContextHash(request.Context);
        _ = VerifyOperationProof(request.Proof, contextHash);
        if (request.Proof.Action != "configuration.write"
            || request.Context.SessionId != request.Proof.SessionId)
            Deny("ACTION_MISMATCH");
        _repository.Read(state =>
        {
            var license = RequireActiveLicense(state, request.Proof.LicenseId);
            var device = RequireActiveDevice(license, request.Proof.DeviceId);
            RequireActiveSession(device, request.Proof.SessionId);
            return 0;
        });
        return Task.FromResult(_repository.UpdatePriceConfigurationFromDevice(
            request.Proof.LicenseId, request.Proof.DeviceId, request.Proof.SessionId,
            request.Context.ExpectedVersion, request.Context.PackagePricesCents));
    }

    public async Task<OnlineOrderResponse> ReadOrderAsync(OnlinePaymentReadProof request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proof);
        ArgumentNullException.ThrowIfNull(request.Context);
        var contextHash = OnlineLicenseProtocol.ContextHash(request.Context);
        _ = VerifyOperationProof(request.Proof, contextHash);
        if (request.Proof.Action != "payment.read" || request.Context.SessionId != request.Proof.SessionId)
            Deny("ACTION_MISMATCH");
        var stored = _repository.Read(state =>
        {
            var license = RequireActiveLicense(state, request.Proof.LicenseId);
            var device = RequireActiveDevice(license, request.Proof.DeviceId);
            RequireActiveSession(device, request.Proof.SessionId);
            return state.Payments.SingleOrDefault(payment => payment.LicenseId == request.Proof.LicenseId
                && payment.ExternalReference == request.Context.ExternalReference
                && payment.ProviderOrderId == request.Context.ProviderOrderId
                && payment.AmountCents == request.Context.AmountCents)
                ?? throw new OnlineServerException(404, "PAYMENT_NOT_FOUND", "PAYMENT_NOT_FOUND");
        });
        var remote = await _payments.ReadAsync(stored.CustomerId, request.Context, token);
        if (remote.ExternalReference != stored.ExternalReference || remote.ProviderOrderId != stored.ProviderOrderId
            || remote.AmountCents != stored.AmountCents || remote.Currency != "BRL")
            Deny("PROVIDER_RESPONSE_MISMATCH", 502);
        return _repository.Update(state =>
        {
            var entry = state.Payments.Single(payment => payment.LicenseId == request.Proof.LicenseId
                && payment.ExternalReference == request.Context.ExternalReference);
            entry.Status = remote.Status;
            entry.UpdatedAtUnixSeconds = Now();
            return ToResponse(entry);
        });
    }

    private StoredChallenge VerifyOperationProof(OnlineOperationProof proof, string expectedContextHash)
    {
        var challenge = ConsumeChallenge(proof.ChallengeId, proof.Action);
        RequireChallengeMatch(challenge, proof.LicenseId, proof.DeviceId, proof.SessionId,
            proof.Action, expectedContextHash);
        var descriptor = _repository.Read(state => RequireActiveDevice(
            RequireActiveLicense(state, proof.LicenseId), proof.DeviceId).Descriptor);
        if (!OnlineLicenseProtocol.VerifyProof(descriptor, challenge.Response, proof.LicenseId,
                proof.SessionId, proof.Action, expectedContextHash, proof.Signature))
        {
            _repository.RecordSecurityAttempt("MACHINE_PROOF_INVALID", proof.LicenseId,
                proof.DeviceId, "operation_proof_denied");
            Deny("MACHINE_PROOF_INVALID");
        }
        return challenge;
    }

    private OnlineChallengeResponse CreateChallenge(string licenseId, string deviceId, string sessionId,
        string action, string contextHash, string activationVerifier)
    {
        CleanupChallenges();
        var response = new OnlineChallengeResponse(1,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), Now() + ChallengeLifetimeSeconds);
        var stored = new StoredChallenge(response, licenseId, deviceId, sessionId, action, contextHash,
            activationVerifier);
        if (!_challenges.TryAdd(response.ChallengeId, stored)) throw new CryptographicException("Colisao de desafio.");
        return response;
    }

    private StoredChallenge ConsumeChallenge(string challengeId, string action)
    {
        OnlineLicenseProtocol.RequireHex(challengeId, "ChallengeId", 64);
        if (!_challenges.TryRemove(challengeId, out var challenge)
            || challenge.Response.ExpiresAtUnixSeconds < Now() || challenge.Action != action)
            Deny("CHALLENGE_INVALID");
        return challenge!;
    }

    private static void RequireChallengeMatch(StoredChallenge challenge, string licenseId, string deviceId,
        string sessionId, string action, string contextHash)
    {
        if (challenge.LicenseId != licenseId || challenge.DeviceId != deviceId
            || challenge.SessionId != sessionId || challenge.Action != action
            || !OnlineLicenseProtocol.FixedHexEquals(challenge.ContextHash, contextHash))
            Deny("CHALLENGE_CONTEXT_MISMATCH");
    }

    private static OnlineLicenseEntry RequireActiveLicense(OnlineServerState state, string licenseId)
    {
        OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId);
        if (license is null || license.Status != "ACTIVE") Deny("LICENSE_REVOKED");
        var customer = state.Customers.SingleOrDefault(item => item.CustomerId == license!.CustomerId);
        if (customer is null || customer.Status != "ACTIVE") Deny("CUSTOMER_SUSPENDED");
        return license!;
    }

    private static OnlineLicenseEntry RequireLicenseForActivation(OnlineServerState state, string licenseId)
    {
        OnlineLicenseProtocol.RequireIdentifier(licenseId, "LicenseId", 6, 64);
        var license = state.Licenses.SingleOrDefault(item => item.LicenseId == licenseId);
        if (license is null || license.Status is not ("ACTIVE" or "TRANSFER_PENDING"))
            Deny("LICENSE_REVOKED");
        var customer = state.Customers.SingleOrDefault(item => item.CustomerId == license!.CustomerId);
        if (customer is null || customer.Status != "ACTIVE") Deny("CUSTOMER_SUSPENDED");
        return license!;
    }

    private static OnlineDeviceEntry RequireActiveDevice(OnlineLicenseEntry license, string deviceId)
    {
        var device = license.Devices.SingleOrDefault(item => item.Descriptor.DeviceId == deviceId);
        if (device is null || device.Status != "ACTIVE") Deny("MACHINE_BINDING_MISMATCH");
        return device!;
    }

    private static void RequireActiveSession(OnlineDeviceEntry device, string sessionId)
    {
        if (device.SessionExpiresAtUnixSeconds < Now()
            || !device.ActiveSessionId.Equals(sessionId, StringComparison.Ordinal))
            Deny("SESSION_EXPIRED", 409);
    }

    private static bool HardwareFingerprintMatches(OnlineDeviceEntry device, string fingerprint)
    {
        var profile = OnlineProtectionProfileCodec.Parse(device.Descriptor.BindingType);
        if (profile is OnlineProtectionProfile.SoftwareBoundOnline or OnlineProtectionProfile.UsbTokenBound)
            return OnlineLicenseProtocol.FixedHexEquals(device.Descriptor.HardwareFingerprint, fingerprint);
        return true;
    }

    private static bool DescriptorsEqual(OnlineDeviceDescriptor left, OnlineDeviceDescriptor right)
        => left.DeviceId == right.DeviceId && left.BindingType == right.BindingType
            && left.Algorithm == right.Algorithm && left.PublicKeySpki == right.PublicKeySpki
            && left.HardwareFingerprint == right.HardwareFingerprint;

    private static OnlineOrderResponse ToResponse(OnlinePaymentEntry entry)
        => new(1, "turborama-online", entry.ExternalReference, entry.AmountCents, "BRL",
            entry.ProviderOrderId, entry.QrData, entry.Status);

    private static string PaymentIdempotencyKey(string licenseId, string deviceId, string reference)
    {
        var bytes = Encoding.UTF8.GetBytes("TurboRamaPaymentIdempotency/v1\0" + licenseId + "\0"
            + deviceId + "\0" + reference);
        var hash = SHA256.HashData(bytes);
        try { return new Guid(hash.AsSpan(0, 16)).ToString("D"); }
        finally { CryptographicOperations.ZeroMemory(bytes); CryptographicOperations.ZeroMemory(hash); }
    }

    private static bool FixedBase64Equals(string left, string right)
    {
        byte[] a;
        byte[] b;
        try { a = Convert.FromBase64String(left); b = Convert.FromBase64String(right); }
        catch (FormatException) { return false; }
        try { return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
        finally { CryptographicOperations.ZeroMemory(a); CryptographicOperations.ZeroMemory(b); }
    }

    private void CleanupChallenges()
    {
        var now = Now();
        foreach (var pair in _challenges)
            if (pair.Value.Response.ExpiresAtUnixSeconds < now) _challenges.TryRemove(pair.Key, out _);
        if (_challenges.Count > 10_000) throw new OnlineServerException(503, "SERVER_BUSY", "CHALLENGE_LIMIT");
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static bool IsRejectedMachineReason(string reason)
        => reason is "ACTIVATION_INVALID" or "BINDING_DOWNGRADE_DENIED" or "DEVICE_LIMIT_REACHED"
            or "MACHINE_PROOF_INVALID" or "MACHINE_BINDING_MISMATCH";
    private static void Deny(string reason, int statusCode = 403)
        => throw new OnlineServerException(statusCode, reason, reason);
}

interface IPixPaymentGateway
{
    bool IsReady { get; }
    Task<OnlineOrderResponse> CreateAsync(string customerId, OnlinePaymentCreateContext context,
        string idempotencyKey, CancellationToken token);
    Task<OnlineOrderResponse> ReadAsync(string customerId, OnlinePaymentReadContext context, CancellationToken token);
}

sealed class MercadoPagoServerGateway : IPixPaymentGateway, IDisposable
{
    private readonly HttpClient _http;
    private readonly OnlineStateRepository _repository;
    private readonly int _expirationMinutes;
    public bool IsReady => true;

    public MercadoPagoServerGateway(OnlineStateRepository repository, int expirationMinutes,
        HttpMessageHandler? handler = null)
    {
        _repository = repository;
        _expirationMinutes = expirationMinutes;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, true);
        _http.BaseAddress = new Uri("https://api.mercadopago.com/");
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TurboRamaPixOnlineServer/1.0");
    }

    public async Task ValidateStoredConnectionAsync(string customerId, CancellationToken token)
    {
        var connection = _repository.GetMercadoPagoConnection(customerId);
        await ValidateConnectionAsync(connection.ExternalPosId, connection.AccessToken, token);
    }

    public async Task ValidateConnectionAsync(string externalPosId, string accessToken, CancellationToken token)
    {
        externalPosId = (externalPosId ?? "").Trim();
        accessToken = (accessToken ?? "").Trim();
        if (externalPosId.Length is < 1 or > 40 || !externalPosId.All(char.IsAsciiLetterOrDigit))
            throw new SecurityException("O ExternalPosId e invalido.");
        if (accessToken.Length is < 40 or > 384 || !accessToken.StartsWith("APP_USR-", StringComparison.Ordinal)
            || accessToken.Any(char.IsWhiteSpace))
            throw new SecurityException("O Access Token possui formato invalido.");

        using var message = Authorized(HttpMethod.Get,
            "pos?external_id=" + Uri.EscapeDataString(externalPosId), accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        using var root = await ReadJsonAsync(response, token);
        if (!root.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
            throw new SecurityException("O Mercado Pago retornou uma lista de caixas invalida.");

        var matches = results.EnumerateArray().Count(item =>
            String(item, "external_id").Equals(externalPosId, StringComparison.Ordinal));
        if (matches != 1)
            throw new SecurityException("O caixa informado nao pertence a credencial do Mercado Pago.");
    }

    public void Dispose() => _http.Dispose();

    public async Task<OnlineOrderResponse> CreateAsync(string customerId,
        OnlinePaymentCreateContext context, string idempotencyKey, CancellationToken token)
    {
        var connection = _repository.GetMercadoPagoConnection(customerId);
        var amount = (context.AmountCents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        using var message = Authorized(HttpMethod.Post, "v1/orders", connection.AccessToken);
        message.Headers.Add("X-Idempotency-Key", idempotencyKey);
        message.Content = JsonContent.Create(new
        {
            type = "qr",
            total_amount = amount,
            external_reference = context.ExternalReference,
            expiration_time = $"PT{_expirationMinutes}M",
            description = $"Tempo TurboRama - {context.Minutes} min",
            config = new { qr = new { mode = "dynamic", external_pos_id = connection.ExternalPosId } },
            transactions = new { payments = new[] { new { amount } } }
        }, options: Json.Options);
        using var response = await _http.SendAsync(message, token);
        var root = await ReadJsonAsync(response, token);
        using (root)
        {
            var orderId = String(root.RootElement, "id");
            var qr = root.RootElement.TryGetProperty("type_response", out var typeResponse)
                ? String(typeResponse, "qr_data") : "";
            ValidateRemote(root.RootElement, context.ExternalReference, context.AmountCents, orderId);
            if (!String(root.RootElement, "status").Equals("created", StringComparison.OrdinalIgnoreCase))
                throw new OnlineServerException(502, "PROVIDER_INVALID", "CREATE_STATUS_MISMATCH");
            if (qr.Length is < 20 or > 4096) throw new OnlineServerException(502, "PROVIDER_INVALID", "QR_INVALID");
            return new OnlineOrderResponse(1, "turborama-online", context.ExternalReference,
                context.AmountCents, "BRL", orderId, qr, "pending");
        }
    }

    public async Task<OnlineOrderResponse> ReadAsync(string customerId,
        OnlinePaymentReadContext context, CancellationToken token)
    {
        var connection = _repository.GetMercadoPagoConnection(customerId);
        using var message = Authorized(HttpMethod.Get, "v1/orders/" + Uri.EscapeDataString(context.ProviderOrderId),
            connection.AccessToken);
        using var response = await _http.SendAsync(message, token);
        var root = await ReadJsonAsync(response, token);
        using (root)
        {
            ValidateRemote(root.RootElement, context.ExternalReference, context.AmountCents, context.ProviderOrderId);
            var status = DetermineStatus(root.RootElement);
            return new OnlineOrderResponse(1, "turborama-online", context.ExternalReference,
                context.AmountCents, "BRL", context.ProviderOrderId, "", status);
        }
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string route, string accessToken)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken token)
    {
        var bytes = await ReadBoundedAsync(response.Content, 64 * 1024, token);
        try
        {
            if (bytes.Length < 2) throw new OnlineServerException(502, "PROVIDER_INVALID", "BODY_SIZE");
            if (!response.IsSuccessStatusCode)
                throw new OnlineServerException((int)response.StatusCode, "PROVIDER_DENIED", "MERCADOPAGO_DENIED");
            // JsonDocument can retain the supplied buffer. Give it an owned copy before clearing the
            // transport buffer, otherwise the parsed provider response becomes corrupted after return.
            try { return JsonDocument.Parse(bytes.ToArray()); }
            catch (JsonException ex) { throw new OnlineServerException(502, "PROVIDER_INVALID", ex.Message); }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximumBytes,
        CancellationToken token)
    {
        await using var input = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var remaining = maximumBytes + 1 - checked((int)output.Length);
                if (remaining <= 0)
                    throw new OnlineServerException(502, "PROVIDER_INVALID", "BODY_SIZE");
                var read = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), token);
                if (read == 0) return output.ToArray();
                output.Write(buffer, 0, read);
                if (output.Length > maximumBytes)
                    throw new OnlineServerException(502, "PROVIDER_INVALID", "BODY_SIZE");
            }
        }
        finally { CryptographicOperations.ZeroMemory(buffer); }
    }

    private static void ValidateRemote(JsonElement root, string reference, long cents, string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId) || String(root, "id") != orderId
            || String(root, "external_reference") != reference
            || !String(root, "currency").Equals("BRL", StringComparison.Ordinal))
            throw new OnlineServerException(502, "PROVIDER_INVALID", "IDENTITY_MISMATCH");
        if (!TryCents(root, "total_amount", out var total) || total != cents)
            throw new OnlineServerException(502, "PROVIDER_INVALID", "AMOUNT_MISMATCH");
    }

    private static string DetermineStatus(JsonElement root)
    {
        var status = String(root, "status").ToLowerInvariant();
        var detail = String(root, "status_detail").ToLowerInvariant();
        var accredited = root.TryGetProperty("transactions", out var transactions)
            && transactions.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array
            && payments.EnumerateArray().Any(item => String(item, "status_detail") == "accredited");
        if (status == "processed" && (detail == "accredited" || accredited)) return "approved";
        return status is "canceled" or "expired" or "refunded" ? "cancelled" : "pending";
    }

    private static string String(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? ""
            : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "";
    }

    private static bool TryCents(JsonElement item, string property, out long cents)
    {
        cents = 0;
        var raw = String(item, property);
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0) return false;
        var scaled = amount * 100m;
        if (scaled != decimal.Truncate(scaled) || scaled > long.MaxValue) return false;
        cents = (long)scaled;
        return true;
    }
}
