using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

sealed record OnlineAdminConfiguration(string Username, string PasswordHash, string DataProtectionDirectory,
    string PublicHostname)
{
    public bool Enabled => Username.Length != 0 && PasswordHash.Length != 0;

    public static OnlineAdminConfiguration Load(string stateFile)
    {
        var username = (Environment.GetEnvironmentVariable("TURBORAMA_ADMIN_USERNAME") ?? "").Trim();
        var passwordHash = (Environment.GetEnvironmentVariable("TURBORAMA_ADMIN_PASSWORD_HASH") ?? "").Trim();
        if ((username.Length == 0) != (passwordHash.Length == 0))
            throw new SecurityException("Usuario e hash administrativo devem ser configurados juntos.");
        if (username.Length != 0 && (username.Length is < 3 or > 64
            || username.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))))
            throw new SecurityException("O usuario administrativo possui formato invalido.");
        if (passwordHash.Length != 0 && !AdminPasswordHash.IsValidFormat(passwordHash))
            throw new SecurityException("O hash da senha administrativa possui formato invalido.");
        var keyDirectory = Environment.GetEnvironmentVariable("TURBORAMA_ADMIN_KEY_DIRECTORY");
        if (string.IsNullOrWhiteSpace(keyDirectory))
            keyDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(stateFile))
                ?? throw new SecurityException("Diretorio de estado invalido."), "admin-data-protection");
        var publicHostname = (Environment.GetEnvironmentVariable("TURBORAMA_ADMIN_PUBLIC_HOST") ?? "").Trim();
        if (publicHostname.Length != 0)
        {
            if (publicHostname.Length > 253 || publicHostname.EndsWith(".", StringComparison.Ordinal)
                || publicHostname.Contains(":", StringComparison.Ordinal)
                || publicHostname.Contains("/", StringComparison.Ordinal)
                || publicHostname.Contains("*", StringComparison.Ordinal)
                || Uri.CheckHostName(publicHostname) != UriHostNameType.Dns)
                throw new SecurityException("O hostname publico do painel possui formato invalido.");
            publicHostname = publicHostname.ToLowerInvariant();
        }
        return new OnlineAdminConfiguration(username, passwordHash, Path.GetFullPath(keyDirectory), publicHostname);
    }
}

static class AdminPasswordHash
{
    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "pbkdf2-sha256";

    public static string Create(string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            var hash = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Iterations,
                HashAlgorithmName.SHA256, HashBytes);
            try { return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}"; }
            finally { CryptographicOperations.ZeroMemory(hash); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static bool Verify(string storedHash, string password)
    {
        if (!TryParse(storedHash, out var iterations, out var salt, out var expected)) return false;
        var passwordBytes = Encoding.UTF8.GetBytes(password ?? "");
        try
        {
            var actual = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations,
                HashAlgorithmName.SHA256, expected.Length);
            try { return CryptographicOperations.FixedTimeEquals(expected, actual); }
            finally { CryptographicOperations.ZeroMemory(actual); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static bool IsValidFormat(string storedHash)
    {
        if (!TryParse(storedHash, out _, out var salt, out var hash)) return false;
        CryptographicOperations.ZeroMemory(salt);
        CryptographicOperations.ZeroMemory(hash);
        return true;
    }

    private static bool TryParse(string storedHash, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];
        var parts = (storedHash ?? "").Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1],
                NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations is < Iterations or > 2_000_000) return false;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltBytes || hash.Length != HashBytes)
            {
                CryptographicOperations.ZeroMemory(salt);
                CryptographicOperations.ZeroMemory(hash);
                salt = [];
                hash = [];
                return false;
            }
            return true;
        }
        catch (FormatException)
        {
            if (salt.Length != 0) CryptographicOperations.ZeroMemory(salt);
            if (hash.Length != 0) CryptographicOperations.ZeroMemory(hash);
            salt = [];
            hash = [];
            return false;
        }
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length is < 14 or > 256 || password.Any(character => character == '\0'))
            throw new SecurityException("A senha administrativa deve ter entre 14 e 256 caracteres.");
    }
}

sealed class AdminLoginGuard
{
    private sealed record FailureState(int Count, long BlockedUntilUnixSeconds);
    private readonly ConcurrentDictionary<string, FailureState> _failures = new(StringComparer.Ordinal);

    public bool IsBlocked(string key)
        => _failures.TryGetValue(key, out var state)
            && state.BlockedUntilUnixSeconds > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void Failed(string key)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _failures.AddOrUpdate(key, _ => new FailureState(1, 0), (_, current) =>
        {
            var count = current.BlockedUntilUnixSeconds > now ? current.Count + 1 : Math.Min(current.Count + 1, 5);
            return count >= 5 ? new FailureState(count, now + 15 * 60) : new FailureState(count, 0);
        });
    }

    public void Succeeded(string key) => _failures.TryRemove(key, out _);
}

static class AdminPanel
{
    public const string AuthenticationScheme = "TurboRamaAdmin";

    public static void ConfigureServices(WebApplicationBuilder builder, OnlineAdminConfiguration configuration)
    {
        Directory.CreateDirectory(configuration.DataProtectionDirectory);
        builder.Services.AddDataProtection()
            .SetApplicationName("TurboRamaPixAdmin/v1")
            .PersistKeysToFileSystem(new DirectoryInfo(configuration.DataProtectionDirectory));
        builder.Services.AddAuthentication(AuthenticationScheme)
            .AddCookie(AuthenticationScheme, options =>
            {
                options.Cookie.Name = "__Secure-TurboRamaAdmin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/admin";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = false;
                options.LoginPath = "/admin/login";
                options.AccessDeniedPath = "/admin/login";
            });
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Secure-TurboRamaAdminCsrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/admin";
            options.FormFieldName = "__RequestVerificationToken";
        });
        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton<AdminLoginGuard>();
        builder.Services.AddSingleton<SuiteAdminBff>();
        builder.Services.AddSingleton<SuiteIssueGuard>();
        builder.Services.AddSingleton<SuiteContentAdminGuard>();
    }

    public static void UseSecurityHeaders(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.IsHttps)
                context.Response.Headers.StrictTransportSecurity = "max-age=31536000";
            if (context.Request.Path.StartsWithSegments("/admin"))
            {
                context.Response.Headers.CacheControl = "no-store, max-age=0";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'none'; style-src 'self'; script-src 'self'; connect-src 'self'; img-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
                context.Response.Headers["Permissions-Policy"] =
                    "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            }
            await next();
        });
    }

    public static void UseHostIsolation(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/admin"))
            {
                await next();
                return;
            }

            var configuration = context.RequestServices.GetRequiredService<OnlineAdminConfiguration>();
            if (IsHostAllowed(context.Request.Host.Host, context.Request.IsHttps,
                    configuration.PublicHostname))
            {
                await next();
                return;
            }

            // Falha fechada e sem redirecionamento: no hostname da API o painel
            // deve parecer inexistente, mesmo quando as credenciais estao configuradas.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.Headers.CacheControl = "no-store, max-age=0";
            context.Response.ContentLength = 0;
        });
    }

    public static bool IsHostAllowed(string host, bool isHttps, string publicHostname)
    {
        return publicHostname.Length != 0 && isHttps
            && string.Equals(host, publicHostname, StringComparison.OrdinalIgnoreCase);
    }

    public static void Map(WebApplication app, OnlineServerConfiguration serverConfiguration)
    {
        app.MapGet("/admin/assets/admin.css", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            return Results.Text(Css, "text/css; charset=utf-8");
        });
        app.MapGet("/admin/assets/admin.js", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            return Results.Text(AdminJavascript, "text/javascript; charset=utf-8");
        });

        app.MapGet("/admin/login", (HttpContext context, IAntiforgery antiforgery,
            OnlineAdminConfiguration configuration) =>
        {
            if (context.User.Identity?.IsAuthenticated == true) return Results.Redirect("/admin");
            var token = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
            var message = configuration.Enabled ? "Acesso restrito" : "Painel ainda não configurado no servidor";
            return Html(LoginPage(token, message, configuration.Enabled));
        });

        app.MapPost("/admin/login", async (HttpContext context, IAntiforgery antiforgery,
            OnlineAdminConfiguration configuration, AdminLoginGuard guard,
            OnlineStateRepository repository) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Html(LoginPage("", "Sessão inválida. Atualize a página.", false), 400); }
            if (!configuration.Enabled) return Html(LoginPage("", "Painel ainda não configurado no servidor", false), 503);
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
            var remote = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var guardKey = remote + "\0" + username;
            if (guard.IsBlocked(guardKey))
            {
                repository.RecordAdministrativeEvent("ADMIN_LOGIN_BLOCKED", "rate_limit");
                return Html(LoginPage("", "Acesso temporariamente bloqueado. Tente mais tarde.", false), 429);
            }
            var validUser = FixedTextEquals(configuration.Username, username);
            var validPassword = AdminPasswordHash.Verify(configuration.PasswordHash, password);
            if (!validUser || !validPassword)
            {
                guard.Failed(guardKey);
                repository.RecordAdministrativeEvent("ADMIN_LOGIN_FAILED", "invalid_credentials");
                var retryToken = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
                return Html(LoginPage(retryToken, "Usuário ou senha inválidos.", true), 401);
            }
            guard.Succeeded(guardKey);
            var identity = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, configuration.Username),
                new Claim(ClaimTypes.Role, "Administrator"),
                new Claim("permission", "suite.read"),
                new Claim("permission", "suite.activation.issue"),
                new Claim("permission", "suite.audit.export"),
                new Claim("permission", "suite.content.read"),
                new Claim("permission", "suite.content.origin.replace"),
                new Claim("permission", "suite.content.version.publish"),
                new Claim("permission", "suite.content.check")
            ], AuthenticationScheme);
            await context.SignInAsync(AuthenticationScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
            repository.RecordAdministrativeEvent("ADMIN_LOGIN_SUCCEEDED", "authenticated");
            return Results.Redirect("/admin");
        });

        app.MapPost("/admin/logout", async (HttpContext context, IAntiforgery antiforgery,
            OnlineStateRepository repository) =>
        {
            await antiforgery.ValidateRequestAsync(context);
            await context.SignOutAsync(AuthenticationScheme);
            repository.RecordAdministrativeEvent("ADMIN_LOGOUT", "completed");
            return Results.Redirect("/admin/login");
        }).RequireAuthorization();

        app.MapGet("/admin", async (HttpContext context, IAntiforgery antiforgery,
            OnlineStateRepository repository, SuiteAdminBff suiteBff, CancellationToken ct) =>
        {
            var token = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
            var customers = await SuiteAdminPanel.ActiveCustomerPanelAsync(suiteBff, ct);
            return Html(DashboardPage(repository.ReadAdminDashboard(), token,
                context.Request.Query["ok"].ToString(), context.Request.Query["error"].ToString(),customers));
        }).RequireAuthorization();

        app.MapGet("/admin/fragments/suite-clients", async (HttpContext context,
            SuiteAdminBff suiteBff, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers["X-Turborama-Fragment"] = "suite-clients";
            return Html(await SuiteAdminPanel.ActiveCustomerPanelAsync(suiteBff, ct));
        }).RequireAuthorization();

        app.MapGet("/admin/export/audit.csv", (OnlineStateRepository repository) =>
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
                AuditCsv(repository.ReadAdminAudit()))).ToArray();
            var fileName = "turborama-auditoria-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture) + ".csv";
            return Results.File(bytes, "text/csv; charset=utf-8", fileName);
        }).RequireAuthorization();

        MapAction(app, "/admin/actions/pix", (form, repository, _, _) =>
        {
            repository.SetPixEnabled(Required(form, "licenseId"), Required(form, "enabled") == "true");
            return Task.FromResult("pix");
        });
        MapAction(app, "/admin/actions/license-status", (form, repository, _, _) =>
        {
            repository.SetLicenseStatus(Required(form, "licenseId"), Required(form, "status"));
            return Task.FromResult("license");
        });
        MapAction(app, "/admin/actions/device-status", (form, repository, _, _) =>
        {
            repository.SetDeviceStatus(Required(form, "licenseId"), Required(form, "deviceId"),
                Required(form, "status"));
            return Task.FromResult("device");
        });
        MapAction(app, "/admin/actions/device-configuration", (form, repository, _, _) =>
        {
            repository.SetDeviceConfigurationPermission(Required(form, "licenseId"),
                Required(form, "deviceId"), Required(form, "allowed") == "true");
            return Task.FromResult("permission");
        });
        MapAction(app, "/admin/actions/force-reauth", (form, repository, _, _) =>
        {
            repository.ForceReauthentication(Required(form, "licenseId"), Required(form, "deviceId"));
            return Task.FromResult("reauth");
        });
        MapAction(app, "/admin/actions/prices", (form, repository, _, _) =>
        {
            repository.SetPackagePrices(Required(form, "licenseId"), ReadPrices(form));
            return Task.FromResult("prices");
        });
        MapAction(app, "/admin/actions/mercadopago", async (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            var customerId = Required(form, "customerId");
            var externalPosId = Required(form, "externalPosId");
            var accessToken = Required(form, "accessToken");
            try
            {
                using var gateway = new MercadoPagoServerGateway(repository,
                    serverConfiguration.PaymentExpirationMinutes);
                await gateway.ValidateConnectionAsync(externalPosId, accessToken, CancellationToken.None);
                repository.SetMercadoPagoConnection(customerId, externalPosId, accessToken);
                return "mercadopago";
            }
            finally { accessToken = ""; }
        });
        MapAction(app, "/admin/actions/create-license", (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            if (!int.TryParse(Required(form, "maximumDevices"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var maximumDevices))
                throw new SecurityException("Quantidade de máquinas inválida.");
            var code = repository.CreateLicense(Required(form, "customerId"), Required(form, "licenseId"),
                OnlineProtectionProfileCodec.Parse(Required(form, "bindingType")), maximumDevices);
            return Task.FromResult("activation:" + code);
        }, showActivationCode: true);
        MapAction(app, "/admin/actions/activation-code", (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            return Task.FromResult("activation:" + repository.IssueActivationCode(Required(form, "licenseId")));
        }, showActivationCode: true);
        MapAction(app, "/admin/actions/device-transfer", (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            return Task.FromResult("activation:" + repository.PrepareDeviceTransfer(
                Required(form, "licenseId")));
        }, showActivationCode: true);
        MapAction(app, "/admin/actions/audit-purge", (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            if (!int.TryParse(Required(form, "retentionDays"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var retentionDays))
                throw new SecurityException("Período de retenção inválido.");
            repository.PurgeAuditOlderThan(retentionDays, Required(form, "scope"));
            return Task.FromResult("audit-purge");
        });
    }

    private static void MapAction(WebApplication app, string path,
        Func<IFormCollection, OnlineStateRepository, OnlineAdminConfiguration, HttpContext, Task<string>> action,
        bool showActivationCode = false)
    {
        app.MapPost(path, async (HttpContext context, IAntiforgery antiforgery,
            OnlineStateRepository repository, OnlineAdminConfiguration admin) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var outcome = await action(form, repository, admin, context);
                if (showActivationCode && outcome.StartsWith("activation:", StringComparison.Ordinal))
                {
                    return Html(ActivationCodePage(outcome[11..]));
                }
                return Results.Redirect("/admin?ok=" + Uri.EscapeDataString(outcome));
            }
            catch (Exception ex) when (ex is SecurityException or InvalidOperationException
                or OnlineServerException or AntiforgeryValidationException or HttpRequestException)
            {
                return Results.Redirect("/admin?error=" + Uri.EscapeDataString(ErrorCode(ex)));
            }
        }).RequireAuthorization();
    }

    private static string DashboardPage(AdminDashboardSnapshot snapshot, string token, string ok, string error,
        string suiteCustomers = "")
    {
        var online = snapshot.Licenses.Sum(license => license.Devices.Count(device => device.Online));
        var devices = snapshot.Licenses.Sum(license => license.Devices.Count);
        var activeLicenses = snapshot.Licenses.Count(license => license.Status == "ACTIVE");
        var pixEnabled = snapshot.Licenses.Count(license => license.PixEnabled);
        var rejected = snapshot.RejectedMachineAttempts;
        var licenseRate = snapshot.Licenses.Count == 0 ? 0 : activeLicenses * 100 / snapshot.Licenses.Count;
        var onlineRate = devices == 0 ? 0 : online * 100 / devices;
        var pixRate = snapshot.Licenses.Count == 0 ? 0 : pixEnabled * 100 / snapshot.Licenses.Count;
        var html = new StringBuilder();
        html.Append(PageStart("Central TurboRama PIX", "dashboard-shell"));
        html.Append("<aside class=sidebar aria-label=\"Navegação principal\"><a class=side-brand href=#overview><span class=side-mark>TR</span><span><strong>TurboRama</strong><small>PIX CONTROL</small></span></a><nav class=side-nav><a href=#suite-clients><span>CL</span>Clientes SUITE</a><a href=/admin/suite><span>SU</span>SUITE</a><a href=/admin/suite/content/health><span>HL</span>Saúde dos links</a><a href=#overview><span>01</span>Visão geral</a><a href=#licenses><span>02</span>Licenças</a><a href=#machines><span>03</span>Máquinas</a><a href=#audit><span>04</span>Auditoria</a></nav><div class=side-status><i></i><span><strong>Servidor protegido</strong><small>Operação monitorada</small></span></div></aside>");
        html.Append("<header class=top><div><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central de licenças</h1><p>Máquinas autorizadas, disponibilidade e segurança em um só lugar.</p></div><nav class=compact-nav aria-label=\"Navegação do painel\"><a href=#overview>Visão geral</a><a href=/admin/suite/content/health>Saúde dos links</a><button type=button data-open-modal=licenses-modal>Licenças</button><button type=button data-open-modal=security-modal>Segurança</button><button type=button data-open-modal=audit-modal>Auditoria</button></nav><nav class=top-actions aria-label=\"Ações do painel\"><button type=button class=primary data-open-modal=licenses-modal>Licenças</button><a class='button ghost' href=/admin/export/audit.csv>Exportar</a>");
        html.Append(FormStart("/admin/logout", token)).Append("<button class=ghost>Sair</button></form></nav></header>");
        if (ok.Length != 0) html.Append("<div class='notice success' role=status>Alteração aplicada e registrada.</div>");
        if (error.Length != 0) html.Append("<div class='notice danger'>A operação foi recusada. Código: ")
            .Append(E(error)).Append("</div>");
        html.Append("<section class=summary id=overview aria-label=\"Resumo operacional\"><article><span class=metric-icon>LIC</span><strong>")
            .Append(activeLicenses).Append("<small>/").Append(snapshot.Licenses.Count)
            .Append("</small></strong><span>Licenças ativas</span></article><article><span class=metric-icon>PC</span><strong>")
            .Append(online).Append("<small>/").Append(devices)
            .Append("</small></strong><span>Máquinas online</span></article><article><span class=metric-icon>PIX</span><strong>")
            .Append(pixEnabled).Append("</strong><span>Licenças com PIX liberado</span></article><article class='")
            .Append(rejected > 0 ? "metric-alert" : "").Append("'><span class=metric-icon>SEG</span><strong>")
            .Append(rejected).Append("</strong><span>Tentativas recusadas</span></article></section>");
        html.Append("<p class=sr-only>Servidor de licença, gabinete autônomo. Preços, Mercado Pago, PDV, QR Code e créditos permanecem locais.</p><section class=front-grid><article class=operations><header><div><span class=eyebrow>DESEMPENHO</span><h2>Visão operacional</h2></div><span class='live'><i></i> ATUALIZAÇÃO ATIVA</span></header><div class=op-row><span>Licenciamento</span><div class=bar><i style=\"width:")
            .Append(licenseRate).Append("%\"></i></div><strong>").Append(licenseRate)
            .Append("%</strong></div><div class=op-row><span>Disponibilidade</span><div class=bar><i style=\"width:")
            .Append(onlineRate).Append("%\"></i></div><strong>").Append(onlineRate)
            .Append("%</strong></div><div class=op-row><span>PIX autorizado</span><div class=bar><i style=\"width:")
            .Append(pixRate).Append("%\"></i></div><strong>").Append(pixRate)
            .Append("%</strong></div><footer><span><i class=status-dot></i> Servidor de licenças operacional</span><time>")
            .Append(When(snapshot.GeneratedAtUnixSeconds)).Append("</time></footer></article>");
        html.Append("<article class=command-center><header><span class=eyebrow>ACESSO RÁPIDO</span><h2>Central de controle</h2></header><button type=button data-open-modal=licenses-modal><span class=command-icon>LC</span><span><strong>Licenças e máquinas</strong><small>Gerenciar operação e vínculos</small></span><b>").Append(snapshot.Licenses.Count)
            .Append("</b></button><button type=button data-open-modal=security-modal><span class=command-icon>SG</span><span><strong>Segurança</strong><small>Recusas e alertas recentes</small></span><b class='")
            .Append(rejected > 0 ? "attention" : "").Append("'>").Append(rejected)
            .Append("</b></button><button type=button data-open-modal=audit-modal><span class=command-icon>AU</span><span><strong>Auditoria</strong><small>Pesquisar, exportar e limpar</small></span><b>→</b></button></article></section>");
        html.Append(suiteCustomers);
        html.Append("<dialog class=app-modal id=licenses-modal><div class=modal-frame><header class=modal-head><div><span class=eyebrow>OPERAÇÃO</span><h2>Licenças e máquinas</h2></div><button type=button class=modal-close data-close-modal aria-label=\"Fechar\">×</button></header><div class=modal-body>");
        html.Append("<section class='panel toolbar' aria-label=\"Filtros de licenças\"><label>Localizar licença ou máquina<input id=license-search type=search placeholder=\"Cliente, licença, máquina ou versão\" autocomplete=off></label><label>Status<select id=license-status-filter><option value=all>Todos</option><option value=ACTIVE>Ativas</option><option value=SUSPENDED>Suspensas</option><option value=MAINTENANCE>Manutenção</option><option value=TRANSFER_PENDING>Transferência</option><option value=REVOKED>Revogadas</option></select></label><div class=filter-result id=license-filter-result role=status aria-live=polite></div></section>");
        html.Append("<details class='panel create-panel' id=new-license><summary><span><span class=eyebrow>NOVA INSTALAÇÃO</span><strong>Criar licença</strong></span><span class=summary-hint>Abrir formulário</span></summary>")
            .Append(FormStart("/admin/actions/create-license", token, "grid-form"))
            .Append(Input("customerId", "Cliente", "CLI-TURBORAMA-TESTE"))
            .Append(Input("licenseId", "Licença", "TR-TURBORAMA-TESTE-001"))
            .Append("<label>Proteção<select name=bindingType><option>SOFTWARE_BOUND_ONLINE</option><option>TPM_BOUND</option></select></label>")
            .Append(Input("maximumDevices", "Máquinas permitidas", "1", "number"))
            .Append(Input("adminPassword", "Confirme sua senha", "", "password"))
            .Append(CreateLicenseSubmitButton)
            .Append("<div class=submit-status role=status aria-live=polite></div></form></details>");
        html.Append("<div class=section-heading id=licenses><div><span class=eyebrow>OPERAÇÃO</span><h2>Licenças e máquinas</h2></div><span class=generated>Atualizado em ")
            .Append(When(snapshot.GeneratedAtUnixSeconds)).Append("</span></div><div id=machines>");
        foreach (var license in snapshot.Licenses) AppendLicense(html, license, token);
        if (snapshot.Licenses.Count == 0)
            html.Append("<section class='panel empty-state'><strong>Nenhuma licença cadastrada</strong><p>Use “Nova licença” para preparar a primeira instalação.</p></section>");
        html.Append("</div></div></div></dialog>");
        AppendSecurityModal(html, snapshot.RecentAudit, token);
        AppendAudit(html, snapshot.RecentAudit, token);
        html.Append(PageEnd());
        return html.ToString();
    }

    private static void AppendLicense(StringBuilder html, AdminLicenseSnapshot license, string token)
    {
        var activeDevices = license.Devices.Count(device => device.Status == "ACTIVE");
        var search = string.Join(' ', new[] { license.CustomerId, license.LicenseId, license.Status,
            license.BindingType }.Concat(license.Devices.SelectMany(device => new[]
            { device.DeviceId, device.AgentVersion, device.Status, device.BindingType })));
        html.Append("<article class='panel license-card' data-license-card data-status=\"")
            .Append(E(license.Status)).Append("\" data-search=\"").Append(E(search.ToLowerInvariant()))
            .Append("\"><div class=license-head><div><span class=eyebrow>")
            .Append(E(license.CustomerId)).Append("</span><h2>").Append(E(license.LicenseId))
            .Append("</h2></div><div class=license-pills><span class='pill state-").Append(E(license.Status.ToLowerInvariant()))
            .Append("'>").Append(E(StatusTitle(license.Status))).Append("</span><span class='pill ")
            .Append(license.PixEnabled ? "on'>PIX LIBERADO" : "off'>PIX BLOQUEADO").Append("</span></div></div>");
        html.Append("<div class=license-facts><div><span>Proteção</span><strong>").Append(E(BindingTitle(license.BindingType)))
            .Append("</strong></div><div><span>Ocupação</span><strong>").Append(activeDevices).Append('/')
            .Append(license.MaximumDevices).Append(" máquinas</strong></div><div><span>Online agora</span><strong>")
            .Append(license.Devices.Count(device => device.Online)).Append("</strong></div><div><span>Recusas</span><strong>")
            .Append(license.Devices.Sum(device => device.RejectedAttempts)).Append("</strong></div></div>");
        html.Append("<div class=operation-grid><section class=operation-card><span class=eyebrow>LICENÇA</span><h3>Estado operacional</h3><p>Suspender ou colocar em manutenção encerra as sessões ativas.</p>")
            .Append(FormStart("/admin/actions/license-status", token, "stack")).Append(Hidden("licenseId", license.LicenseId))
            .Append("<label>Novo estado<select name=status>").Append(StatusOptions(license.Status, true))
            .Append("</select></label><button data-busy=\"Aplicando...\" data-confirm=\"Confirma a alteração do estado desta licença?\">Aplicar estado</button></form></section>");
        html.Append("<section class=operation-card><span class=eyebrow>PIX</span><h3>Autorização remota</h3><p>Controla somente a permissão de novas cobranças; não altera preços nem credenciais locais.</p>")
            .Append(FormStart("/admin/actions/pix", token, "stack")).Append(Hidden("licenseId", license.LicenseId))
            .Append(Hidden("enabled", license.PixEnabled ? "false" : "true"))
            .Append("<button class='").Append(license.PixEnabled ? "danger" : "primary")
            .Append("' data-busy=\"Aplicando...\" data-confirm=\"")
            .Append(license.PixEnabled ? "Confirma o bloqueio de novas cobranças PIX?" : "Confirma a liberação de novas cobranças PIX?")
            .Append("\">").Append(license.PixEnabled ? "Bloquear novas cobranças" : "Liberar novas cobranças")
            .Append("</button></form></section>");
        html.Append("<section class='operation-card local-card'><span class=eyebrow>CONFIGURAÇÃO LOCAL</span><h3>Pagamento no gabinete</h3><p>Valores, Mercado Pago, PDV, QR e créditos são gerenciados no EmulationStation e nos configuradores instalados.</p><span class='pill neutral'>SEM DEPENDÊNCIA DO PAINEL</span></section></div>");
        html.Append("<div class=machines-title><div><h3>Máquinas autorizadas</h3><p class=muted>Identidade criptográfica, versão e último contato.</p></div></div><div class=table-wrap><table class=machine-table><thead><tr><th>Máquina</th><th>Proteção</th><th>Versão</th><th>Conexão</th><th>Segurança</th><th>Ações</th></tr></thead><tbody>");
        if (license.Devices.Count == 0) html.Append("<tr><td colspan=7 class=muted>Nenhuma máquina ativada.</td></tr>");
        foreach (var device in license.Devices)
        {
            html.Append("<tr><td><div class=id-line><code title=\"").Append(E(device.DeviceId)).Append("\">")
                .Append(E(Short(device.DeviceId))).Append("</code><button type=button class=copy-button data-copy=\"")
                .Append(E(device.DeviceId)).Append("\" aria-label=\"Copiar identificador da máquina\">Copiar</button></div><span class='pill compact state-")
                .Append(E(device.Status.ToLowerInvariant())).Append("'>").Append(E(StatusTitle(device.Status)))
                .Append("</span></td><td>").Append(E(BindingTitle(device.BindingType)))
                .Append("</td><td><strong>").Append(E(device.AgentVersion)).Append("</strong><br><span class=muted>Ativada ")
                .Append(When(device.ActivatedAtUnixSeconds)).Append("</span></td><td><span class='status ")
                .Append(device.Online ? "online'>ONLINE" : "offline'>OFFLINE").Append("</span><br><span class=muted>")
                .Append(When(device.LastContactUnixSeconds)).Append("</span></td><td><strong>").Append(device.RejectedAttempts)
                .Append(" recusas</strong><br><span class=muted>").Append(device.CanManageConfiguration ? "Configuração permitida" : "Configuração bloqueada")
                .Append("</span></td><td><div class=row-actions>");
            html.Append(FormStart("/admin/actions/device-status", token)).Append(Hidden("licenseId", license.LicenseId))
                .Append(Hidden("deviceId", device.DeviceId)).Append(Hidden("status", device.Status == "ACTIVE" ? "SUSPENDED" : "ACTIVE"))
                .Append("<button data-busy=\"Aplicando...\" data-confirm=\"")
                .Append(device.Status == "ACTIVE" ? "Confirma a suspensão desta máquina?" : "Confirma a reativação desta máquina?")
                .Append("\">").Append(device.Status == "ACTIVE" ? "Suspender" : "Reativar").Append("</button></form>");
            html.Append(FormStart("/admin/actions/device-configuration", token)).Append(Hidden("licenseId", license.LicenseId))
                .Append(Hidden("deviceId", device.DeviceId)).Append(Hidden("allowed", device.CanManageConfiguration ? "false" : "true"))
                .Append("<button data-busy=\"Aplicando...\" data-confirm=\"Confirma a alteração da permissão de configuração?\">")
                .Append(device.CanManageConfiguration ? "Bloquear configuração" : "Permitir configuração").Append("</button></form>");
            html.Append(FormStart("/admin/actions/force-reauth", token)).Append(Hidden("licenseId", license.LicenseId))
                .Append(Hidden("deviceId", device.DeviceId)).Append("<button data-busy=\"Encerrando...\" data-confirm=\"Confirma o encerramento da sessão desta máquina?\">Encerrar sessão</button></form></div></td></tr>");
        }
        html.Append("</tbody></table></div><details class=advanced><summary>Ativação e transferência de hardware</summary><div class=advanced-grid>")
            .Append(FormStart("/admin/actions/activation-code", token, "stack operation-card"))
            .Append(Hidden("licenseId", license.LicenseId))
            .Append(Input("adminPassword", "Senha para gerar novo código", "", "password"))
            .Append("<button data-busy=\"Gerando...\" data-confirm=\"O código anterior deixará de valer. Deseja continuar?\">Gerar novo código de ativação</button></form>")
            .Append(FormStart("/admin/actions/device-transfer", token, "stack operation-card danger-zone"))
            .Append(Hidden("licenseId", license.LicenseId))
            .Append(Input("adminPassword", "Senha para transferir esta licença", "", "password"))
            .Append("<button class=danger data-busy=\"Preparando...\" data-confirm=\"ATENÇÃO: isso suspenderá as máquinas atuais e preparará a licença para outro hardware. Continuar?\">Preparar transferência de hardware</button></form></div></details></article>");
    }

    private static void AppendSecurityModal(StringBuilder html, IReadOnlyList<OnlineAuditEntry> audit, string token)
    {
        var security = audit.Where(item => AuditSeverity(item.Event) is "critical" or "warning").Take(50).ToArray();
        html.Append("<dialog class=app-modal id=security-modal><div class=modal-frame><header class=modal-head><div><span class=eyebrow>SEGURANÇA</span><h2>Eventos e alertas</h2></div><div class=modal-head-actions><button type=button class=danger data-focus-cleanup>Limpar histórico</button><button type=button class=modal-close data-close-modal aria-label=\"Fechar\">×</button></div></header><div class=modal-body><div class=security-list>");
        if (security.Length == 0) html.Append("<p class=muted>Nenhum alerta de segurança recente.</p>");
        foreach (var item in security)
            html.Append("<article><span class='event-badge event-").Append(AuditSeverity(item.Event)).Append("'>")
                .Append(E(EventTitle(item.Event))).Append("</span><div><strong>").Append(E(item.LicenseId))
                .Append("</strong><small>").Append(E(item.Detail)).Append("</small></div><time>")
                .Append(When(item.AtUnixSeconds)).Append("</time></article>");
        html.Append("</div><details class=audit-cleanup><summary>Limpar histórico de Segurança</summary><p class=cleanup-note>Somente alertas e recusas de Segurança serão apagados. Licenças, máquinas e sessões permanecem intactas.</p>")
            .Append(FormStart("/admin/actions/audit-purge", token, "cleanup-form"))
            .Append(Hidden("scope", "security"))
            .Append("<label>Ação sobre o histórico<select name=retentionDays><option value=0 selected>Apagar todo o histórico</option><option value=30>Manter últimos 30 dias</option><option value=90>Manter últimos 90 dias</option><option value=180>Manter últimos 180 dias</option></select></label>")
            .Append(Input("adminPassword", "Confirme sua senha", "", "password"))
            .Append("<button class=danger data-busy=\"Limpando...\" data-confirm=\"Confirma a exclusão permanente do histórico anterior ao período escolhido? Somente o histórico deste modal será afetado.\">Apagar histórico antigo</button></form></details></div></div></dialog>");
    }

    private static void AppendAudit(StringBuilder html, IReadOnlyList<OnlineAuditEntry> audit, string token)
    {
        html.Append("<dialog class=app-modal id=audit-modal><div class=modal-frame><header class=modal-head><div><span class=eyebrow>AUDITORIA</span><h2>Histórico de eventos</h2></div><div class=modal-head-actions><button type=button class=danger data-focus-cleanup>Limpar histórico</button><button type=button class=modal-close data-close-modal aria-label=\"Fechar\">×</button></div></header><div class=modal-body><section class=panel id=audit><div class=section-title><p>Histórico administrativo e recusas criptográficas.</p><a class='button ghost' href=/admin/export/audit.csv>Baixar CSV</a></div><div class=audit-toolbar><label>Pesquisar eventos<input id=audit-search type=search placeholder=\"Evento, licença, máquina ou detalhe\" autocomplete=off></label><label>Tipo<select id=audit-severity><option value=all>Todos</option><option value=critical>Críticos</option><option value=warning>Atenção</option><option value=success>Sucesso</option><option value=info>Informativos</option></select></label><div class=filter-result id=audit-filter-result role=status aria-live=polite></div></div><div class=table-wrap><table class=audit-table><thead><tr><th>Quando</th><th>Evento</th><th>Licença / máquina</th><th>Detalhe</th></tr></thead><tbody>");
        var administrativeAudit = audit.Where(item => AuditSeverity(item.Event) is not ("critical" or "warning")).ToArray();
        if (administrativeAudit.Length == 0) html.Append("<tr><td colspan=4 class=muted>Nenhum evento de auditoria registrado.</td></tr>");
        foreach (var item in administrativeAudit)
        {
            var severity = AuditSeverity(item.Event);
            var search = (item.Event + " " + item.LicenseId + " " + item.DeviceId + " " + item.Detail).ToLowerInvariant();
            html.Append("<tr data-audit-row data-severity=\"").Append(severity).Append("\" data-search=\"")
                .Append(E(search)).Append("\"><td>").Append(When(item.AtUnixSeconds))
                .Append("</td><td><span class='event-badge event-").Append(severity).Append("'>")
                .Append(E(EventTitle(item.Event))).Append("</span><br><code class=event-code>").Append(E(item.Event))
                .Append("</code></td><td>").Append(E(item.LicenseId)).Append("<br><code>")
                .Append(E(Short(item.DeviceId))).Append("</code></td><td>").Append(E(item.Detail)).Append("</td></tr>");
        }
        html.Append("</tbody></table></div><details class=audit-cleanup><summary>Limpeza de registros antigos</summary>")
            .Append(FormStart("/admin/actions/audit-purge", token, "cleanup-form"))
            .Append(Hidden("scope", "audit"))
            .Append("<label>Ação sobre o histórico<select name=retentionDays><option value=0 selected>Apagar todo o histórico</option><option value=30>Manter últimos 30 dias</option><option value=90>Manter últimos 90 dias</option><option value=180>Manter últimos 180 dias</option></select></label>")
            .Append(Input("adminPassword", "Confirme sua senha", "", "password"))
            .Append("<button class=danger data-busy=\"Limpando...\" data-confirm=\"Confirma a exclusão permanente dos eventos anteriores ao período escolhido?\">Apagar registros antigos</button></form></details></section></div></div></dialog>");
    }

    private static string LoginPage(string token, string message, bool enabled)
        => PageStart("Acesso administrativo") + "<section class=login><div class=login-card><div class=login-mark>TR</div><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central de licenças</h1><p>"
            + E(message) + "</p>" + (enabled ? FormStart("/admin/login", token, "stack")
                + Input("username", "Usuário", "") + Input("password", "Senha", "", "password")
                + "<button class=primary data-busy=\"Autenticando...\">Entrar com segurança</button></form>" : "")
                + "<div class=login-security>Protegido por Cloudflare Access + autenticação TurboRama</div></div></section>" + PageEnd();

    private static string ActivationCodePage(string code)
        => PageStart("Código de ativação") + "<section class=login><div class=login-card><span class=eyebrow>USO ÚNICO</span><h1>Código de ativação</h1><p>Ele não será mostrado novamente. Transporte-o diretamente para o único gabinete autorizado.</p><code class=activation id=activation-code>"
            + E(code) + "</code><div class=activation-actions><button type=button class=ghost data-copy-target=\"#activation-code\">Copiar código</button><a class='button primary' href=/admin>Voltar ao painel</a></div><p class='muted compact-text'>Não salve este código no Git, em capturas ou mensagens.</p></div></section>" + PageEnd();

    private static Dictionary<int, long> ReadPrices(IFormCollection form)
    {
        var prices = new Dictionary<int, long>();
        foreach (var minutes in new[] { 15, 30, 45, 60, 120 })
        {
            var text = Required(form, "price" + minutes).Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var value)
                && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
                throw new SecurityException("Preço inválido.");
            var cents = decimal.ToInt64(decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));
            prices[minutes] = cents;
        }
        OnlineLicenseProtocol.ValidatePackagePrices(prices);
        return prices;
    }

    private static void RequireStepUp(OnlineAdminConfiguration admin, string password)
    {
        if (!AdminPasswordHash.Verify(admin.PasswordHash, password))
            throw new SecurityException("Confirmação administrativa inválida.");
    }

    private static string Required(IFormCollection form, string name)
    {
        var value = form[name].ToString();
        if (value.Length is < 1 or > 1024 || value.Any(character => character == '\0'))
            throw new SecurityException("Campo administrativo inválido.");
        return value;
    }

    private static bool FixedTextEquals(string expected, string actual)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        try { return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right); }
        finally { CryptographicOperations.ZeroMemory(left); CryptographicOperations.ZeroMemory(right); }
    }

    private static string ErrorCode(Exception exception) => exception switch
    {
        AntiforgeryValidationException => "ADMIN-CSRF",
        HttpRequestException => "PROVIDER-UNAVAILABLE",
        OnlineServerException online => online.InternalReason,
        SecurityException => "ADMIN-DENIED",
        InvalidOperationException => "ADMIN-CONFLICT",
        _ => "ADMIN-ERROR"
    };

    private static string StatusOptions(string selected, bool allowTransfer)
    {
        var options = allowTransfer
            ? new[] { "ACTIVE", "SUSPENDED", "MAINTENANCE", "TRANSFER_PENDING", "REVOKED" }
            : new[] { "ACTIVE", "SUSPENDED", "MAINTENANCE", "REVOKED" };
        var html = new StringBuilder();
        foreach (var status in options)
            html.Append("<option value=\"").Append(E(status)).Append("\"")
                .Append(status == selected ? " selected" : "").Append('>')
                .Append(E(StatusTitle(status))).Append("</option>");
        return html.ToString();
    }

    private static string StatusTitle(string status) => status switch
    {
        "ACTIVE" => "Ativa",
        "SUSPENDED" => "Suspensa",
        "MAINTENANCE" => "Manutenção",
        "TRANSFER_PENDING" => "Transferência pendente",
        "REVOKED" => "Revogada",
        _ => status
    };

    private static string BindingTitle(string binding) => binding switch
    {
        "SOFTWARE_BOUND_ONLINE" => "Software + servidor",
        "TPM_BOUND" => "TPM",
        "USB_TOKEN_BOUND" => "Token criptográfico",
        _ => binding
    };

    private static string AuditSeverity(string eventName)
    {
        if (eventName.Contains("MISMATCH", StringComparison.Ordinal)
            || eventName.Contains("INVALID", StringComparison.Ordinal)
            || eventName.Contains("DENIED", StringComparison.Ordinal)
            || eventName.Contains("REVOKED", StringComparison.Ordinal)
            || eventName.Contains("FAILED", StringComparison.Ordinal)) return "critical";
        if (eventName.Contains("SUSPENDED", StringComparison.Ordinal)
            || eventName.Contains("DISABLED", StringComparison.Ordinal)
            || eventName.Contains("BLOCKED", StringComparison.Ordinal)
            || eventName.Contains("REAUTH", StringComparison.Ordinal)
            || eventName.Contains("TRANSFER", StringComparison.Ordinal)) return "warning";
        if (eventName.Contains("SUCCEEDED", StringComparison.Ordinal)
            || eventName.Contains("ACTIVATED", StringComparison.Ordinal)
            || eventName.Contains("ENABLED", StringComparison.Ordinal)
            || eventName.Contains("CREATED", StringComparison.Ordinal)) return "success";
        return "info";
    }

    private static string EventTitle(string eventName) => eventName switch
    {
        "ADMIN_LOGIN_SUCCEEDED" => "Login administrativo",
        "ADMIN_LOGIN_FAILED" => "Falha de login",
        "ADMIN_LOGIN_BLOCKED" => "Login bloqueado",
        "ADMIN_LOGOUT" => "Saída administrativa",
        "LICENSE_CREATED" => "Licença criada",
        "LICENSE_STATUS_CHANGED" => "Estado da licença alterado",
        "PIX_ENABLED" => "PIX liberado",
        "PIX_DISABLED" => "PIX bloqueado",
        "DEVICE_ACTIVATED" => "Máquina ativada",
        "DEVICE_STATUS_CHANGED" => "Estado da máquina alterado",
        "DEVICE_CONFIGURATION_PERMISSION_CHANGED" => "Permissão alterada",
        "DEVICE_TRANSFER_PREPARED" => "Transferência preparada",
        "ACTIVATION_CODE_ISSUED" => "Código de ativação emitido",
        "FORCE_REAUTH" => "Nova autenticação exigida",
        "MACHINE_BINDING_MISMATCH" => "Máquina divergente recusada",
        "DUPLICATE_SESSION_DENIED" => "Sessão duplicada recusada",
        "ACTIVATION_INVALID" => "Ativação inválida",
        _ => eventName.Replace('_', ' ')
    };

    private static string AuditCsv(IReadOnlyList<OnlineAuditEntry> audit)
    {
        var csv = new StringBuilder("Quando;Evento;Licenca;Maquina;Detalhe\r\n");
        foreach (var item in audit)
            csv.Append(Csv(When(item.AtUnixSeconds))).Append(';').Append(Csv(item.Event)).Append(';')
                .Append(Csv(item.LicenseId)).Append(';').Append(Csv(item.DeviceId)).Append(';')
                .Append(Csv(item.Detail)).Append("\r\n");
        return csv.ToString();
    }

    private static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"",
        StringComparison.Ordinal) + "\"";

    private static IResult Html(string value, int statusCode = 200)
        => Results.Content(value, "text/html; charset=utf-8", Encoding.UTF8, statusCode);
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string Short(string value) => value.Length > 16 ? value[..8] + "…" + value[^8..] : value;
    private static string Reais(long cents) => (cents / 100m).ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
    private static string When(long unix) => unix <= 0 ? "Nunca" : DateTimeOffset.FromUnixTimeSeconds(unix)
        .ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    private static string Hidden(string name, string value) => "<input type=hidden name=\"" + E(name) + "\" value=\"" + E(value) + "\">";
    private static string Input(string name, string label, string value, string type = "text")
        => "<label>" + E(label) + "<input type=\"" + E(type) + "\" name=\"" + E(name)
            + "\" value=\"" + E(value) + "\" autocomplete=\"" + (type == "password" ? "new-password" : "off") + "\" required></label>";
    private static StringBuilder FormStart(string action, string token, string css = "inline-form")
        => new StringBuilder("<form method=post action=\"").Append(E(action)).Append("\" class=\"")
            .Append(E(css)).Append("\">").Append(Hidden("__RequestVerificationToken", token));
    private static string PageStart(string title, string pageClass = "")
        => "<!doctype html><html lang=pt-BR><head><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><title>"
            + E(title) + "</title><link rel=stylesheet href=/admin/assets/admin.css?v=r5-915><script defer src=/admin/assets/admin.js?v=r5-915></script></head><body><main class=\"shell " + E(pageClass) + "\">";
    private static string PageEnd() => "</main></body></html>";

    private const string Css = """
:root{color-scheme:dark;--bg:#061016;--panel:#0d1a21;--panel-2:#12232c;--line:#213943;--line-strong:#315563;--text:#f3f8f9;--muted:#94aab2;--cyan:#29c9e8;--green:#35df91;--red:#ff6e7f;--amber:#ffc857;--blue:#69a7ff;--shadow:0 20px 60px rgba(0,0,0,.28)}*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:radial-gradient(circle at 85% -10%,#164352 0,transparent 34%),linear-gradient(180deg,#07141a,#050c11 70%);color:var(--text);font:15px/1.5 Inter,ui-sans-serif,system-ui,-apple-system,Segoe UI,sans-serif}.shell{width:min(1480px,94vw);margin:auto;padding:32px 0 72px}.top,.top-actions,.section-title,.license-head,.license-pills,.row-actions,.id-line,.activation-actions{display:flex;align-items:center;gap:12px}.top,.section-title,.license-head{justify-content:space-between}.top{margin-bottom:24px}.top h1,.section-heading h2,.panel h2,.login h1,.scope-banner h2{margin:.2rem 0}.top p,.section-title p,.muted,.generated,.operation-card p,.scope-banner p{color:var(--muted)}.top-actions{justify-content:flex-end;flex-wrap:wrap}.top-actions form{display:inline-flex}.eyebrow{display:block;font-size:.7rem;font-weight:850;letter-spacing:.16em;color:var(--cyan)}.summary{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px;margin-bottom:18px}.summary article,.panel,.login-card,.scope-banner{background:linear-gradient(145deg,rgba(15,29,36,.98),rgba(8,18,24,.98));border:1px solid var(--line);border-radius:18px;box-shadow:var(--shadow)}.summary article{position:relative;padding:20px;min-height:122px}.summary strong{display:block;margin-top:10px;font-size:2rem;line-height:1;color:var(--green)}.summary strong small{font-size:.95rem;color:var(--muted);font-weight:700}.summary article>span:last-child{display:block;margin-top:10px;color:var(--muted)}.metric-icon{position:absolute;right:16px;top:16px;color:#53727e;font-size:.67rem;font-weight:900;letter-spacing:.12em}.summary .metric-alert strong{color:var(--amber)}.scope-banner{display:flex;align-items:center;justify-content:space-between;gap:28px;padding:22px 24px;margin-bottom:18px;border-color:rgba(41,201,232,.3);background:linear-gradient(120deg,rgba(17,43,52,.98),rgba(8,20,26,.98))}.scope-banner p{max-width:900px;margin:.45rem 0 0}.panel{padding:22px;margin:18px 0}.toolbar,.audit-toolbar{display:grid;grid-template-columns:minmax(280px,1fr) 220px auto;gap:14px;align-items:end}.filter-result{color:var(--muted);font-size:.82rem;text-align:right;padding-bottom:10px}.section-heading{display:flex;align-items:end;justify-content:space-between;margin:30px 2px 10px}.section-heading h2{font-size:1.35rem}.create-panel{scroll-margin-top:18px}.create-panel>summary,.advanced>summary{display:flex;align-items:center;justify-content:space-between;gap:20px;cursor:pointer;list-style:none}.create-panel>summary::-webkit-details-marker,.advanced>summary::-webkit-details-marker{display:none}.create-panel>summary strong{display:block;margin-top:5px;font-size:1.25rem}.summary-hint{color:var(--cyan);font-weight:800}.create-panel[open]>summary{padding-bottom:20px;border-bottom:1px solid var(--line)}.grid-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;align-items:end;margin-top:20px}.grid-form>.submit-status{grid-column:1/-1;color:#ffb4bd;font-weight:700}.license-card{scroll-margin-top:18px}.license-card[hidden],[data-audit-row][hidden]{display:none}.license-head{padding-bottom:18px;border-bottom:1px solid var(--line)}.license-head h2{overflow-wrap:anywhere}.license-pills{justify-content:flex-end;flex-wrap:wrap}.license-facts{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin:16px 0}.license-facts>div{padding:13px 14px;background:rgba(5,15,20,.65);border:1px solid var(--line);border-radius:12px}.license-facts span{display:block;color:var(--muted);font-size:.75rem}.license-facts strong{display:block;margin-top:4px}.operation-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin:18px 0}.operation-card{padding:17px;background:linear-gradient(145deg,rgba(18,35,44,.88),rgba(8,19,25,.88));border:1px solid var(--line);border-radius:14px}.operation-card h3{margin:.35rem 0}.operation-card p{min-height:45px;font-size:.84rem}.local-card{border-color:rgba(41,201,232,.25)}.machines-title{display:flex;align-items:end;justify-content:space-between;margin:24px 0 10px}.machines-title h3,.machines-title p{margin:.2rem 0}.pill,.status,.event-badge{display:inline-block;border-radius:999px;padding:6px 10px;font-weight:850;font-size:.69rem;letter-spacing:.045em;white-space:nowrap}.compact{padding:4px 8px;margin-top:7px}.pill.on,.status.online,.state-active,.event-success{background:rgba(53,223,145,.12);color:var(--green);border:1px solid rgba(53,223,145,.35)}.pill.off,.status.offline,.state-revoked,.event-critical{background:rgba(255,110,127,.11);color:#ff9baa;border:1px solid rgba(255,110,127,.36)}.state-suspended,.state-maintenance,.state-transfer_pending,.event-warning{background:rgba(255,200,87,.1);color:var(--amber);border:1px solid rgba(255,200,87,.35)}.neutral,.event-info{background:rgba(105,167,255,.1);color:#9fc6ff;border:1px solid rgba(105,167,255,.3)}button,.button,select,input{border-radius:10px;border:1px solid var(--line-strong);background:#07151b;color:var(--text);padding:10px 13px;font:inherit}button,.button{cursor:pointer;font-weight:780;text-decoration:none;text-align:center}button:hover,.button:hover{border-color:var(--cyan);transform:translateY(-1px)}button:disabled{opacity:.55;cursor:wait;transform:none}.primary{background:linear-gradient(135deg,#16bfe0,#2ad487);border:0;color:#031116}.danger,.danger-zone button{border-color:rgba(255,110,127,.55);color:#ff9baa}.ghost{background:transparent}.stack{display:grid;gap:11px}.stack button{width:100%}label{display:grid;gap:6px;color:var(--muted);font-size:.8rem;font-weight:750}input:focus,select:focus,button:focus,.button:focus,summary:focus{outline:2px solid var(--cyan);outline-offset:2px}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:13px;background:rgba(5,14,19,.45)}table{width:100%;border-collapse:collapse;min-width:980px}th,td{text-align:left;padding:12px;border-bottom:1px solid var(--line);vertical-align:top}tbody tr:last-child td{border-bottom:0}tbody tr:hover{background:rgba(41,201,232,.035)}th{color:var(--muted);font-size:.69rem;letter-spacing:.09em;text-transform:uppercase}code{color:#b9eff8}.id-line{align-items:flex-start}.copy-button{font-size:.68rem;padding:4px 7px}.row-actions{align-items:flex-start;flex-wrap:wrap}.row-actions form{display:inline-flex}.row-actions button{font-size:.72rem;padding:7px 9px}.advanced{margin-top:16px;padding-top:14px;border-top:1px solid var(--line)}.advanced>summary{color:var(--muted);font-weight:800}.advanced[open]>summary{color:var(--text);margin-bottom:14px}.advanced-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.danger-zone{border-color:rgba(255,110,127,.28)}.audit-toolbar{margin:18px 0 12px}.audit-table{min-width:900px}.event-code{font-size:.68rem;color:var(--muted)}.notice{padding:13px 16px;border-radius:11px;margin-bottom:15px;border:1px solid transparent}.success{background:rgba(53,223,145,.11);color:var(--green);border-color:rgba(53,223,145,.25)}.notice.danger{background:rgba(255,110,127,.11);color:#ffb4bd;border-color:rgba(255,110,127,.25)}.empty-state{text-align:center;padding:42px}.empty-state strong{font-size:1.2rem}.login{min-height:82vh;display:grid;place-items:center}.login-card{width:min(470px,92vw);padding:34px}.login-mark{width:52px;height:52px;display:grid;place-items:center;border-radius:15px;margin-bottom:20px;background:linear-gradient(135deg,var(--cyan),var(--green));color:#031116;font-weight:950;letter-spacing:.05em}.login-security{margin-top:22px;padding-top:17px;border-top:1px solid var(--line);color:var(--muted);font-size:.75rem}.activation{display:block;padding:16px;margin:18px 0;background:#061015;border:1px solid var(--line);border-radius:10px;overflow-wrap:anywhere;color:var(--amber);font-size:1rem}.activation-actions{align-items:stretch}.activation-actions>*{flex:1}.compact-text{font-size:.78rem}@media(max-width:1100px){.summary{grid-template-columns:repeat(2,1fr)}.operation-grid{grid-template-columns:1fr 1fr}.local-card{grid-column:1/-1}.license-facts{grid-template-columns:repeat(2,1fr)}}@media(max-width:760px){.shell{width:min(94vw,680px);padding-top:22px}.top,.scope-banner,.section-title,.license-head,.section-heading{align-items:flex-start;flex-direction:column}.top-actions{justify-content:flex-start}.summary,.operation-grid,.license-facts,.grid-form,.toolbar,.audit-toolbar,.advanced-grid{grid-template-columns:1fr}.local-card{grid-column:auto}.filter-result{text-align:left;padding:0}.license-pills{justify-content:flex-start}.scope-banner .pill{white-space:normal}.activation-actions{flex-direction:column}table{min-width:820px}}
.dashboard-shell{width:auto;max-width:none;margin:0;padding:88px 32px 72px 244px}.dashboard-shell .sidebar{position:fixed;z-index:30;inset:0 auto 0 0;width:212px;padding:22px 14px;display:flex;flex-direction:column;background:#071218;border-right:1px solid #1c323c;box-shadow:10px 0 35px rgba(0,0,0,.2)}.side-brand{display:flex;align-items:center;gap:11px;padding:5px 7px 24px;color:var(--text);text-decoration:none;border-bottom:1px solid var(--line)}.side-brand>span:last-child{display:grid}.side-brand strong{font-size:1rem}.side-brand small{color:var(--cyan);font-size:.58rem;font-weight:900;letter-spacing:.18em}.side-mark{width:39px;height:39px;display:grid;place-items:center;border-radius:11px;background:linear-gradient(135deg,var(--cyan),var(--green));color:#031116;font-weight:950}.side-nav{display:grid;gap:5px;margin-top:22px}.side-nav a{display:flex;align-items:center;gap:12px;padding:11px 12px;border-radius:10px;color:#a7bac1;text-decoration:none;font-weight:750}.side-nav a span{color:#58717a;font-size:.66rem}.side-nav a:hover,.side-nav a:focus{background:#10232c;color:#fff;outline:none}.side-status{display:flex;align-items:center;gap:10px;margin-top:auto;padding:13px 10px;border:1px solid #1d3a35;border-radius:11px;background:#0b1d1b}.side-status i{width:8px;height:8px;border-radius:50%;background:var(--green);box-shadow:0 0 13px var(--green)}.side-status span{display:grid}.side-status strong{font-size:.7rem}.side-status small{color:var(--muted);font-size:.62rem}.dashboard-shell>.top{position:fixed;z-index:20;top:0;left:212px;right:0;min-height:66px;margin:0;padding:10px 32px;background:rgba(7,18,24,.94);border-bottom:1px solid var(--line);backdrop-filter:blur(14px)}.dashboard-shell>.top h1{font-size:1.15rem}.dashboard-shell>.top p{display:none}.dashboard-shell>.top .eyebrow{font-size:.58rem}.dashboard-shell #overview,.dashboard-shell #licenses,.dashboard-shell #machines,.dashboard-shell #audit{scroll-margin-top:86px}.dashboard-shell .summary article{min-height:108px;box-shadow:0 10px 30px rgba(0,0,0,.18)}.dashboard-shell .panel,.dashboard-shell .scope-banner{box-shadow:0 12px 36px rgba(0,0,0,.18)}@media(max-width:760px){.dashboard-shell{width:94vw;padding:128px 0 60px}.dashboard-shell>.top{left:0;padding:9px 3vw}.dashboard-shell>.top h1{font-size:1rem}.dashboard-shell>.top .eyebrow{display:none}.dashboard-shell .sidebar{inset:64px 0 auto;width:auto;height:50px;padding:5px 3vw;display:block;overflow:hidden;border-right:0;border-bottom:1px solid var(--line)}.side-brand,.side-status{display:none}.side-nav{display:flex;gap:4px;margin:0;overflow:auto}.side-nav a{flex:0 0 auto;padding:8px 10px;font-size:.75rem}.dashboard-shell #overview,.dashboard-shell #licenses,.dashboard-shell #machines,.dashboard-shell #audit{scroll-margin-top:126px}}
/* Enterprise control-room visual layer. All geometry remains scoped to the dashboard. */
:root{--bg:#070b12;--panel:#101722;--panel-2:#151e2b;--line:#263244;--line-strong:#3a4b63;--text:#f5f7fb;--muted:#93a1b5;--cyan:#41d8ff;--green:#32d583;--red:#ff647c;--amber:#fdbb45;--blue:#78a9ff;--shadow:0 18px 50px rgba(0,0,0,.24)}
body{background:radial-gradient(900px 500px at 85% -180px,rgba(48,116,147,.22),transparent 64%),linear-gradient(180deg,#080d15 0%,#070b11 100%);font-size:14px}
.dashboard-shell{padding:94px 32px 80px 258px;background-image:linear-gradient(rgba(255,255,255,.012) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.012) 1px,transparent 1px);background-size:32px 32px}
.dashboard-shell .sidebar{width:226px;padding:24px 16px;background:linear-gradient(180deg,#0c131d 0%,#090f17 100%);border-right-color:#202b3b;box-shadow:16px 0 50px rgba(0,0,0,.22)}
.side-brand{padding:2px 7px 25px}.side-mark{width:42px;height:42px;border-radius:12px;background:linear-gradient(145deg,#45dcff,#2bd17f);box-shadow:0 8px 24px rgba(50,213,131,.15)}
.side-brand strong{font-size:1.04rem}.side-brand small{margin-top:1px;color:#62dfff}
.side-nav{gap:4px;margin-top:25px}.side-nav a{min-height:44px;padding:10px 12px;border:1px solid transparent;border-radius:10px;color:#a6b2c2}.side-nav a span{width:20px;color:#607087;font-variant-numeric:tabular-nums}.side-nav a:first-child{color:#fff;background:linear-gradient(90deg,rgba(65,216,255,.13),rgba(65,216,255,.04));border-color:rgba(65,216,255,.17);box-shadow:inset 3px 0 0 var(--cyan)}.side-nav a:hover,.side-nav a:focus-visible{border-color:#2d4055;background:#14202d}
.side-status{padding:13px 12px;background:rgba(50,213,131,.06);border-color:rgba(50,213,131,.2)}
.dashboard-shell>.top{left:226px;min-height:70px;padding:11px 32px;background:rgba(9,14,22,.92);border-bottom-color:#202c3d}.dashboard-shell>.top h1{font-size:1.16rem;letter-spacing:-.015em}.dashboard-shell>.top .top-actions{gap:8px}
.dashboard-shell>.top .button,.dashboard-shell>.top button{min-height:38px;padding:8px 12px;font-size:.78rem}
.summary{gap:12px;margin-bottom:14px}.summary article{min-height:112px;padding:18px 19px;border-radius:14px;background:linear-gradient(150deg,rgba(20,29,42,.98),rgba(12,18,28,.98));border-color:#263348;box-shadow:0 12px 32px rgba(0,0,0,.18)}
.summary article:before{content:"";position:absolute;inset:0 auto 0 0;width:3px;border-radius:14px 0 0 14px;background:var(--green)}.summary article:last-child:before{background:var(--amber)}
.summary strong{margin-top:12px;font-size:2.15rem;letter-spacing:-.05em}.summary article>span:last-child{margin-top:9px;font-size:.78rem}.metric-icon{right:15px;top:14px;padding:4px 6px;border:1px solid #2c3b50;border-radius:6px;color:#8391a5;background:#121b28}
.scope-banner{padding:18px 20px;margin-bottom:14px;border-radius:14px;background:linear-gradient(110deg,rgba(20,37,50,.98),rgba(12,23,33,.98));border-color:rgba(65,216,255,.22)}.scope-banner h2{font-size:1.12rem}.scope-banner p{font-size:.79rem;line-height:1.55}
.panel{padding:20px;margin:14px 0;border-radius:14px;background:linear-gradient(150deg,rgba(17,24,35,.98),rgba(11,17,26,.98));border-color:#253247;box-shadow:0 14px 38px rgba(0,0,0,.18)}
.toolbar{position:sticky;z-index:10;top:82px;grid-template-columns:minmax(300px,1fr) 210px auto;padding:15px 17px;background:rgba(16,23,34,.95);backdrop-filter:blur(16px)}
label{font-size:.72rem;letter-spacing:.015em}input,select{min-height:41px;background:#0b121c;border-color:#2d3b50}input:hover,select:hover{border-color:#43566f}
button,.button{min-height:38px;border-radius:9px;transition:border-color .16s,background .16s,transform .16s,box-shadow .16s}button:hover,.button:hover{box-shadow:0 8px 22px rgba(0,0,0,.16)}.primary{background:linear-gradient(135deg,#37cce9,#2bd17f);box-shadow:0 7px 20px rgba(43,209,127,.12)}
.section-heading{margin:27px 2px 10px}.section-heading h2{font-size:1.28rem;letter-spacing:-.02em}.generated{padding:5px 8px;border-radius:7px;background:#111a27;border:1px solid #253248;font-size:.7rem}
.create-panel{border-style:dashed;border-color:#33455e}.create-panel>summary{min-height:38px}.summary-hint{font-size:.75rem}
.license-card{padding:0;overflow:hidden}.license-head{padding:19px 20px;background:linear-gradient(90deg,rgba(31,44,60,.62),rgba(17,25,36,.15));border-bottom-color:#27354a}.license-head h2{font-size:1.25rem;letter-spacing:-.02em}
.license-facts{gap:0;margin:0;border-bottom:1px solid #253247}.license-facts>div{padding:15px 20px;border:0;border-right:1px solid #253247;border-radius:0;background:rgba(7,12,19,.28)}.license-facts>div:last-child{border-right:0}.license-facts span{text-transform:uppercase;letter-spacing:.075em;font-size:.63rem}.license-facts strong{font-size:.86rem}
.operation-grid{gap:10px;margin:16px 20px}.operation-card{padding:15px;border-radius:11px;background:#121b28;border-color:#29384d}.operation-card h3{font-size:.92rem}.operation-card p{min-height:38px;margin:.4rem 0 .8rem;font-size:.76rem;line-height:1.48}
.machines-title{margin:20px 20px 9px}.machines-title h3{font-size:.98rem}.table-wrap{margin:0 20px 20px;border-color:#29374b;border-radius:10px;background:#0b111a}.machine-table th,.machine-table td{padding:11px 12px}.machine-table thead,.audit-table thead{background:#111a27}.machine-table tbody tr:nth-child(even),.audit-table tbody tr:nth-child(even){background:rgba(255,255,255,.014)}
th{font-size:.63rem;color:#9dabbc}td{font-size:.78rem}.row-actions{gap:6px}.row-actions button{min-height:31px;padding:6px 8px}
.advanced{margin:0 20px 20px;padding:13px 0 0}.advanced>summary{font-size:.76rem}
#audit .section-title{padding-bottom:15px;border-bottom:1px solid #253247}.audit-toolbar{grid-template-columns:minmax(300px,1fr) 190px auto;margin:15px 0 11px}.audit-table td{line-height:1.45}
.pill,.status,.event-badge{padding:5px 8px;font-size:.62rem;letter-spacing:.06em}.notice{position:relative;border-radius:10px}.eyebrow{font-size:.63rem;letter-spacing:.14em}
.login-card{border-radius:16px;background:linear-gradient(150deg,#121b28,#0b111a);border-color:#2b394d}
@media(max-width:1180px){.dashboard-shell{padding-left:246px;padding-right:22px}.summary{grid-template-columns:repeat(2,1fr)}.toolbar{grid-template-columns:1fr 190px}.filter-result{grid-column:1/-1;text-align:left;padding:0}.operation-grid{grid-template-columns:1fr 1fr}.local-card{grid-column:1/-1}}
@media(max-width:820px){.dashboard-shell{width:94vw;padding:132px 0 64px}.dashboard-shell>.top{left:0;padding:9px 3vw}.dashboard-shell .sidebar{inset:69px 0 auto;width:auto;height:50px;padding:5px 3vw;display:block;overflow:hidden;border-right:0;border-bottom:1px solid #253247}.side-brand,.side-status{display:none}.side-nav{display:flex;gap:4px;margin:0;overflow:auto}.side-nav a,.side-nav a:first-child{flex:0 0 auto;min-height:38px;padding:8px 10px;border:1px solid transparent;box-shadow:none;background:transparent;font-size:.74rem}.toolbar{position:static}.dashboard-shell #overview,.dashboard-shell #licenses,.dashboard-shell #machines,.dashboard-shell #audit{scroll-margin-top:130px}}
@media(max-width:620px){body{font-size:14px}.dashboard-shell>.top .top-actions .button:first-child{display:none}.summary{grid-template-columns:1fr 1fr;gap:8px}.summary article{min-height:104px;padding:15px}.summary strong{font-size:1.75rem}.scope-banner{align-items:flex-start}.scope-banner>.pill{white-space:normal}.toolbar,.audit-toolbar,.operation-grid,.license-facts,.grid-form,.advanced-grid{grid-template-columns:1fr}.license-facts>div{border-right:0;border-bottom:1px solid #253247}.license-facts>div:last-child{border-bottom:0}.operation-grid{margin:12px}.machines-title{margin-left:12px}.table-wrap{margin-left:12px;margin-right:12px}.license-head{padding:16px}.license-pills{justify-content:flex-start}.panel{padding:16px}.license-card{padding:0}.top-actions{gap:5px}.top-actions .button,.top-actions button{padding:7px 8px}}
@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}*,*:before,*:after{transition:none!important}}
/* Minimal production layout: compact, horizontal and information-first. */
.dashboard-shell{width:min(1380px,94vw);margin:auto;padding:76px 0 64px;background:none}.dashboard-shell .sidebar{display:none}.dashboard-shell>.top{left:0;min-height:56px;padding:0 3vw;display:flex;align-items:center;gap:28px}.dashboard-shell>.top>div{flex:0 0 auto}.dashboard-shell>.top h1{margin:0;font-size:.96rem}.dashboard-shell>.top>div>.eyebrow,.dashboard-shell>.top p{display:none}.compact-nav{display:flex;align-self:stretch;gap:22px}.compact-nav a{position:relative;display:flex;align-items:center;color:var(--muted);font-size:.76rem;font-weight:700;text-decoration:none}.compact-nav a:hover{color:var(--text)}.compact-nav a:first-child{color:var(--text)}.compact-nav a:first-child:after{content:"";position:absolute;inset:auto 0 0;height:2px;background:var(--cyan)}.dashboard-shell>.top .top-actions{margin-left:auto;flex-wrap:nowrap}.dashboard-shell>.top .button,.dashboard-shell>.top button{min-height:32px;padding:6px 10px;font-size:.7rem}
.summary{gap:0;margin:0 0 14px;border:1px solid var(--line);border-radius:10px;background:var(--panel);overflow:hidden}.summary article{min-height:74px;padding:12px 17px;border:0;border-right:1px solid var(--line);border-radius:0;background:none;box-shadow:none}.summary article:last-child{border-right:0}.summary article:before{display:none}.summary strong{margin-top:7px;font-size:1.62rem}.summary article>span:last-child{margin-top:5px;font-size:.68rem}.metric-icon{top:11px;right:12px;padding:2px 5px;font-size:.55rem}
.scope-banner{padding:12px 15px;margin-bottom:10px;border-radius:9px}.scope-banner h2{font-size:.9rem}.scope-banner p{margin:.25rem 0 0;font-size:.7rem}.scope-banner>.pill{font-size:.56rem}
.toolbar{position:static;grid-template-columns:minmax(300px,1fr) 190px auto;margin:10px 0;padding:10px 12px;border-radius:9px;box-shadow:none}.toolbar input,.toolbar select{min-height:34px}.filter-result{padding-bottom:8px;font-size:.7rem}
.create-panel{padding:12px 15px;margin:10px 0}.create-panel>summary strong{font-size:.9rem}.create-panel .summary-hint{font-size:.66rem}.grid-form{margin-top:12px}
.section-heading{margin:20px 1px 7px}.section-heading h2{font-size:1.03rem}.generated{font-size:.61rem}
.license-card{margin:7px 0;border-radius:9px;box-shadow:none}.license-head{padding:12px 15px}.license-head h2{margin:.1rem 0;font-size:1rem}.license-head .eyebrow{font-size:.55rem}.license-facts{grid-template-columns:repeat(4,1fr)}.license-facts>div{padding:9px 15px}.license-facts span{font-size:.56rem}.license-facts strong{font-size:.74rem}
.operation-grid{gap:8px;margin:10px 14px}.operation-card{padding:11px}.operation-card h3{margin:.2rem 0;font-size:.78rem}.operation-card p{min-height:0;margin:.25rem 0 .55rem;font-size:.67rem}.operation-card label{font-size:.65rem}.operation-card select,.operation-card button{min-height:32px;padding:6px 8px;font-size:.68rem}
.machines-title{margin:12px 14px 6px}.machines-title h3{font-size:.81rem}.machines-title p{font-size:.65rem}.table-wrap{margin:0 14px 12px}.machine-table th,.machine-table td{padding:8px 9px}.machine-table td{font-size:.68rem}.machine-table th{font-size:.55rem}.row-actions button{min-height:27px;padding:4px 6px;font-size:.61rem}.copy-button{min-height:24px}.advanced{margin:0 14px 12px;padding-top:9px}.advanced>summary{font-size:.67rem}
#audit{padding:15px;margin-top:18px}.section-title h2{font-size:.95rem}.section-title p{font-size:.67rem}.audit-toolbar{margin:10px 0 8px}.audit-table th,.audit-table td{padding:8px 9px;font-size:.67rem}
@media(max-width:820px){.dashboard-shell{width:94vw;padding:112px 0 56px}.dashboard-shell>.top{height:56px;padding:0 3vw;gap:12px}.compact-nav{position:fixed;z-index:21;top:56px;left:0;right:0;height:40px;padding:0 3vw;gap:18px;background:#0b1119;border-bottom:1px solid var(--line)}.dashboard-shell>.top .top-actions .button:not(:first-child){display:none}.summary{grid-template-columns:1fr 1fr}.summary article:nth-child(2){border-right:0}.summary article:nth-child(-n+2){border-bottom:1px solid var(--line)}}
@media(max-width:620px){.dashboard-shell>.top h1{font-size:.86rem}.dashboard-shell>.top .top-actions{margin-left:auto}.summary article{min-height:68px;padding:10px 12px}.summary strong{font-size:1.42rem}.scope-banner{display:block}.scope-banner>.pill{display:inline-block;margin-top:7px}.toolbar{grid-template-columns:1fr}.operation-grid,.license-facts{grid-template-columns:1fr}.operation-grid{margin:8px}.license-facts>div{padding:8px 12px}.license-head{padding:10px 12px}.license-pills{margin-top:6px}.table-wrap{margin-left:8px;margin-right:8px}.machines-title{margin-left:8px}.advanced{margin-left:8px;margin-right:8px}}
.compact-nav button{align-self:stretch;padding:0;border:0;border-radius:0;background:transparent;color:var(--muted);font-size:.76rem;font-weight:700}.compact-nav button:hover{color:var(--text);transform:none;box-shadow:none}.quick-menu{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:10px}.quick-menu button{display:grid;gap:2px;min-height:56px;padding:10px 13px;text-align:left;background:var(--panel);box-shadow:none}.quick-menu strong{font-size:.78rem}.quick-menu span{color:var(--muted);font-size:.64rem}
.app-modal{width:min(1280px,96vw);height:min(860px,94vh);max-width:none;max-height:none;padding:0;border:1px solid var(--line);border-radius:12px;background:#0b1119;color:var(--text);box-shadow:0 30px 90px rgba(0,0,0,.65)}.app-modal::backdrop{background:rgba(2,5,9,.78);backdrop-filter:blur(4px)}.modal-frame{height:100%;display:grid;grid-template-rows:55px 1fr}.modal-head{display:flex;align-items:center;justify-content:space-between;padding:0 16px;border-bottom:1px solid var(--line);background:#101722}.modal-head h2{margin:1px 0;font-size:.98rem}.modal-close{width:32px;min-height:32px;padding:0;border-radius:7px;font-size:1.25rem}.modal-body{overflow:auto;padding:12px}.modal-body>.toolbar{margin-top:0}.modal-body>.section-heading{margin-top:14px}.modal-body #audit{margin:0;padding:4px;border:0;background:transparent;box-shadow:none}.security-list{display:grid}.security-list article{display:grid;grid-template-columns:145px 1fr auto;align-items:center;gap:12px;min-height:48px;padding:8px 10px;border-bottom:1px solid var(--line)}.security-list article div{display:grid}.security-list small,.security-list time{color:var(--muted);font-size:.66rem}.audit-cleanup{margin:12px 14px;padding-top:10px;border-top:1px solid var(--line)}.audit-cleanup>summary{color:var(--muted);font-size:.7rem;cursor:pointer}.cleanup-form{display:grid;grid-template-columns:180px 1fr auto;align-items:end;gap:8px;margin-top:10px}.cleanup-form input,.cleanup-form select{min-height:34px}
body:has(.app-modal[open]){overflow:hidden}
.app-modal .modal-card{height:100%;display:grid;grid-template-rows:auto 1fr auto;overflow:auto;padding:18px}.app-modal .detail-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px;padding:12px 0}.app-modal .detail-grid>div{padding:10px;border:1px solid var(--line);border-radius:8px;background:#101722}.app-modal .detail-grid span{display:block;color:var(--muted);font-size:.64rem;text-transform:uppercase;letter-spacing:.06em}.app-modal .detail-grid strong{display:block;margin-top:4px;overflow-wrap:anywhere;font-size:.8rem}.audit-table td small{display:block;color:var(--muted);margin-top:3px}
.suite-detail-modal{width:min(920px,94vw);height:auto;max-height:88vh}.suite-detail-modal .modal-card{height:auto;max-height:88vh;padding:0;grid-template-rows:52px minmax(0,1fr) 52px;overflow:hidden}.suite-detail-modal .modal-head{padding:0 16px}.suite-detail-modal .detail-grid{grid-template-columns:repeat(3,minmax(0,1fr));gap:0;padding:0;overflow:auto;background:#0b1119}.suite-detail-modal .detail-grid>div{min-height:62px;padding:9px 12px;border:0;border-right:1px solid var(--line);border-bottom:1px solid var(--line);border-radius:0;background:transparent}.suite-detail-modal .detail-grid>div:nth-child(3n){border-right:0}.suite-detail-modal .detail-grid span{font-size:.56rem;line-height:1.2}.suite-detail-modal .detail-grid strong{margin-top:3px;font-size:.7rem;line-height:1.32;word-break:break-word}.suite-detail-modal .activation-actions{margin:0;padding:8px 12px;border-top:1px solid var(--line);justify-content:flex-end;background:#101722}.suite-detail-modal .activation-actions>*{flex:0 0 auto;min-height:34px;padding:6px 12px;font-size:.68rem}
.suite-customers{padding:22px 0 8px;overflow:hidden}.suite-customers>.eyebrow,.suite-customers>h2,.suite-customers>p{margin-left:22px;margin-right:22px}.suite-customer-list{margin:18px 0 0;border:0;border-top:1px solid var(--line);border-radius:0;background:transparent}.suite-customer-table{min-width:900px}.suite-customer-table thead{background:rgba(255,255,255,.018)}.suite-customer-table th{padding:9px 16px;border-bottom:1px solid var(--line);font-size:.58rem}.suite-customer-table td{padding:13px 16px;vertical-align:middle;border-bottom:1px solid rgba(38,50,68,.72);background:transparent}.suite-customer-table tbody tr:hover{background:rgba(65,216,255,.035)}.suite-customer-table td small{display:block;margin-top:2px;color:var(--muted);font-size:.64rem}.suite-customer-table code{font-size:.68rem}.suite-customer-table button{min-height:31px;padding:5px 9px;font-size:.65rem}
.suite-detail-modal{width:min(780px,94vw)}.suite-detail-modal .detail-grid{display:block;padding:8px 22px 12px}.suite-detail-modal .detail-grid>div{display:grid;grid-template-columns:190px minmax(0,1fr);align-items:start;min-height:0;padding:8px 0;border:0;border-bottom:1px solid rgba(38,50,68,.66);background:transparent}.suite-detail-modal .detail-grid>div:last-child{border-bottom:0}.suite-detail-modal .detail-grid span{padding-top:1px;font-size:.58rem}.suite-detail-modal .detail-grid strong{margin:0;font-size:.72rem;font-weight:650}.suite-detail-modal .modal-head{background:#0e1620}.suite-detail-modal .activation-actions{background:#0e1620}
.active-customers{margin-top:18px;padding:0;overflow:hidden}.active-customers>header{display:flex;justify-content:space-between;gap:20px;align-items:flex-start;padding:22px}.active-customers h2,.active-customers p{margin:3px 0}.customer-totals{display:flex;gap:10px}.customer-totals span{padding:9px 13px;border:1px solid var(--line);border-radius:10px;color:var(--muted);font-size:.67rem}.customer-totals strong{color:var(--text);font-size:.9rem}.customer-list{border-top:1px solid var(--line)}.customer-row{display:grid;grid-template-columns:92px minmax(180px,1.25fr) minmax(190px,1fr) 105px minmax(165px,1fr) 105px;gap:14px;align-items:center;padding:14px 22px;border-bottom:1px solid rgba(38,50,68,.68);color:var(--text);text-decoration:none}.customer-row:hover{background:rgba(65,216,255,.035)}.customer-row small{display:block;margin-top:3px;color:var(--muted);font-size:.62rem}.customer-row code{font-size:.65rem;word-break:break-all}.customer-row>b{color:var(--cyan);font-size:.66rem}.presence{display:inline-flex;align-items:center;gap:6px;width:max-content;font-size:.62rem;font-weight:800;letter-spacing:.05em}.presence i{width:8px;height:8px;border-radius:50%}.presence.is-online{color:#58e3a3}.presence.is-online i{background:#58e3a3;box-shadow:0 0 10px rgba(88,227,163,.75)}.presence.is-offline{color:#8793a6}.presence.is-offline i{background:#667185}.customer-summary{margin-bottom:18px}.timeline{margin-top:16px;border-top:1px solid var(--line)}.timeline article{display:grid;grid-template-columns:155px 92px minmax(0,1fr);gap:16px;padding:13px 4px;border-bottom:1px solid var(--line);align-items:start}.timeline time,.timeline small{color:var(--muted);font-size:.66rem}.timeline small{display:block;margin-top:4px}.event-kind{color:var(--cyan);font-size:.6rem;font-weight:800;letter-spacing:.06em}
.sr-only{position:absolute!important;width:1px!important;height:1px!important;padding:0!important;margin:-1px!important;overflow:hidden!important;clip:rect(0,0,0,0)!important;white-space:nowrap!important;border:0!important}
.front-grid{display:grid;grid-template-columns:minmax(0,1.45fr) minmax(320px,.75fr);gap:10px;margin-top:10px}.operations,.command-center{border:1px solid var(--line);border-radius:10px;background:linear-gradient(145deg,#111923,#0d141d);overflow:hidden}.operations>header,.command-center>header{display:flex;align-items:center;justify-content:space-between;height:52px;padding:0 15px;border-bottom:1px solid var(--line)}.operations h2,.command-center h2{margin:1px 0;font-size:.86rem}.live{display:flex;align-items:center;gap:6px;color:var(--green);font-size:.55rem;font-weight:850;letter-spacing:.08em}.live i,.status-dot{width:6px;height:6px;border-radius:50%;background:var(--green);box-shadow:0 0 8px var(--green)}.op-row{display:grid;grid-template-columns:105px 1fr 38px;align-items:center;gap:12px;height:43px;padding:0 15px;border-bottom:1px solid rgba(38,48,61,.72)}.op-row>span{color:var(--muted);font-size:.68rem}.op-row>strong{text-align:right;font-size:.67rem}.bar{height:5px;border-radius:9px;background:#202a37;overflow:hidden}.bar i{display:block;height:100%;border-radius:9px;background:linear-gradient(90deg,var(--cyan),var(--green));box-shadow:0 0 10px rgba(53,213,139,.35)}.operations>footer{height:37px;display:flex;align-items:center;justify-content:space-between;padding:0 15px;color:var(--muted);font-size:.61rem}.operations>footer span{display:flex;align-items:center;gap:7px}.command-center>header{justify-content:flex-start}.command-center>button{width:100%;height:54px;display:grid;grid-template-columns:30px 1fr auto;align-items:center;gap:10px;padding:0 13px;border:0;border-bottom:1px solid var(--line);border-radius:0;background:transparent;text-align:left;box-shadow:none}.command-center>button:last-child{border-bottom:0}.command-center>button:hover{background:#151f2b;transform:none}.command-center>button>span:nth-child(2){display:grid}.command-center small{color:var(--muted);font-size:.6rem}.command-center strong{font-size:.7rem}.command-center b{min-width:22px;text-align:center;color:var(--muted);font-size:.7rem}.command-center b.attention{color:var(--amber)}.command-icon{width:27px;height:27px;display:grid;place-items:center;border:1px solid #304055;border-radius:7px;background:#131e2a;color:var(--cyan);font-size:.55rem;font-weight:900;letter-spacing:.06em}
@media(max-width:620px){.quick-menu{grid-template-columns:1fr}.app-modal{width:100vw;height:100vh;border:0;border-radius:0}.modal-body{padding:8px}.app-modal .detail-grid{grid-template-columns:1fr}.suite-detail-modal{max-height:100vh}.suite-detail-modal .detail-grid{display:block;padding:6px 16px}.suite-detail-modal .detail-grid>div{display:block;padding:9px 0}.suite-detail-modal .detail-grid strong{margin-top:3px}.security-list article{grid-template-columns:1fr}.cleanup-form{grid-template-columns:1fr}.compact-nav{overflow:auto}.compact-nav a,.compact-nav button{flex:0 0 auto}}
@media(max-width:820px){.front-grid{grid-template-columns:1fr}}@media(max-width:620px){.operations>footer time{display:none}.op-row{grid-template-columns:90px 1fr 34px}.front-grid{gap:8px}}
.health-dashboard{width:min(1320px,96vw);margin:auto}.health-hero{padding:19px 21px}.health-summary{margin-bottom:12px}.health-summary .health-online strong{color:var(--green)}.health-summary .health-offline strong{color:var(--amber)}.health-summary .health-range strong{color:var(--cyan)}.health-controls{padding:17px 19px}.health-controls .section-title{margin-bottom:14px}.health-filter{display:grid;grid-template-columns:minmax(280px,1.35fr) 205px 210px auto auto;gap:10px;align-items:end}.health-results{padding:0;overflow:hidden}.health-results>.section-title{padding:18px 20px 15px;border-bottom:1px solid var(--line)}.health-legend{display:flex;gap:20px;flex-wrap:wrap;padding:10px 20px;background:#0c131d;border-bottom:1px solid var(--line);color:var(--muted);font-size:.67rem}.health-legend span{display:flex;align-items:center;gap:7px}.health-legend i{width:7px;height:7px;border-radius:50%}.legend-online{background:var(--green)}.legend-warning{background:var(--amber)}.legend-neutral{background:#718096}.health-results .table-wrap{margin:0;border:0;border-radius:0}.health-table{min-width:980px;table-layout:auto}.health-table th,.health-table td{padding:9px 10px;vertical-align:middle}.health-table th:first-child,.health-table td:first-child{width:27%}.health-game{display:block;min-width:130px}.health-game strong{font-size:.74rem;overflow-wrap:anywhere}.health-result{display:inline-block;padding:4px 7px;border:1px solid #2b3a4e;border-radius:6px;background:#101925;color:#b9eff8;font:600 .61rem ui-monospace,SFMono-Regular,monospace}.range-state{display:inline-flex;align-items:center;gap:6px;white-space:nowrap;font-size:.68rem;font-weight:750}.range-state:before{content:"";width:6px;height:6px;border-radius:50%;background:currentColor}.range-ok{color:var(--green)}.range-pending{color:var(--amber)}.health-actions{display:flex;align-items:center;gap:5px;min-width:155px}.health-actions>.button,.health-test>summary{min-height:29px;padding:5px 7px;font-size:.62rem}.health-test{position:relative}.health-test>summary{display:flex;align-items:center;border:1px solid rgba(65,216,255,.4);border-radius:8px;color:var(--cyan);cursor:pointer;font-weight:780;list-style:none;white-space:nowrap}.health-test>summary::-webkit-details-marker{display:none}.health-test[open]>summary{background:rgba(65,216,255,.1)}.health-test form{position:absolute;z-index:12;right:0;top:36px;width:285px;padding:13px;display:grid;gap:9px;border:1px solid #34465e;border-radius:10px;background:#111a27;box-shadow:0 18px 45px rgba(0,0,0,.55)}.health-test form small{color:var(--muted);font-size:.61rem;line-height:1.4}.health-test form input{min-height:35px}.health-test form button{min-height:34px}.health-results>.top-actions{padding:13px 20px;border-top:1px solid var(--line)}
.health-dashboard{width:min(1320px,96vw);margin:auto}.health-hero{padding:19px 21px}.health-summary{margin-bottom:12px}.health-summary .health-online strong{color:var(--green)}.health-summary .health-offline strong{color:var(--amber)}.health-summary .health-range strong{color:var(--cyan)}.health-controls{padding:17px 19px}.health-controls .section-title{margin-bottom:14px}.health-filter{display:grid;grid-template-columns:minmax(280px,1.35fr) 205px 210px auto auto;gap:10px;align-items:end}.health-results{padding:0;overflow:hidden}.health-results>.section-title{padding:18px 20px 15px;border-bottom:1px solid var(--line)}.health-legend{display:flex;gap:20px;flex-wrap:wrap;padding:10px 20px;background:#0c131d;border-bottom:1px solid var(--line);color:var(--muted);font-size:.67rem}.health-legend span{display:flex;align-items:center;gap:7px}.health-legend i{width:7px;height:7px;border-radius:50%}.legend-online{background:var(--green)}.legend-warning{background:var(--amber)}.legend-neutral{background:#718096}.health-results .table-wrap{margin:0;border:0;border-radius:0}.health-table{min-width:980px;table-layout:auto}.health-table th,.health-table td{padding:9px 10px;vertical-align:middle}.health-table th:first-child,.health-table td:first-child{width:27%}.health-game{display:block;min-width:130px}.health-game strong{font-size:.74rem;overflow-wrap:anywhere}.health-result{display:inline-block;padding:4px 7px;border:1px solid #2b3a4e;border-radius:6px;background:#101925;color:#b9eff8;font:600 .61rem ui-monospace,SFMono-Regular,monospace}.range-state{display:inline-flex;align-items:center;gap:6px;white-space:nowrap;font-size:.68rem;font-weight:750}.range-state:before{content:"";width:6px;height:6px;border-radius:50%;background:currentColor}.range-ok{color:var(--green)}.range-pending{color:var(--amber)}.health-actions{display:flex;align-items:center;gap:5px;min-width:155px}.health-actions>.button,.health-test>summary{min-height:29px;padding:5px 7px;font-size:.62rem}.health-test{position:relative}.health-test>summary{display:flex;align-items:center;border:1px solid rgba(65,216,255,.4);border-radius:8px;color:var(--cyan);cursor:pointer;font-weight:780;list-style:none;white-space:nowrap}.health-test>summary::-webkit-details-marker{display:none}.health-test[open]>summary{background:rgba(65,216,255,.1)}.health-test form{position:absolute;z-index:12;right:0;top:36px;width:285px;padding:13px;display:grid;gap:9px;border:1px solid #34465e;border-radius:10px;background:#111a27;box-shadow:0 18px 45px rgba(0,0,0,.55)}.health-test form small{color:var(--muted);font-size:.61rem;line-height:1.4}.health-test form input{min-height:35px}.health-test form button{min-height:34px}.health-testing{display:inline-flex;align-items:center;gap:7px;padding:6px 8px;border:1px solid rgba(65,216,255,.35);border-radius:8px;color:var(--cyan);font-size:.62rem;font-weight:800;white-space:nowrap}.health-testing i{width:9px;height:9px;border:2px solid rgba(65,216,255,.28);border-top-color:var(--cyan);border-radius:50%;animation:health-spin .75s linear infinite}@keyframes health-spin{to{transform:rotate(360deg)}}tr[data-health-pending]{background:rgba(65,216,255,.045)}.health-results>.top-actions{padding:13px 20px;border-top:1px solid var(--line)}
@media(max-width:1050px){.health-filter{grid-template-columns:1fr 1fr 1fr}.health-filter button,.health-filter>.button{width:100%}}@media(max-width:680px){.health-dashboard{width:94vw}.health-filter{grid-template-columns:1fr}.health-summary{grid-template-columns:1fr 1fr}.health-results>.section-title{align-items:flex-start}.health-test form{position:fixed;inset:auto 3vw 20px;width:94vw}}
.platform-row th{padding:5px 8px;background:#0d1621;border-top:1px solid #2b3a4e;border-bottom:1px solid #253247}.platform-toggle{width:100%;min-height:48px;padding:8px 10px;display:flex;align-items:center;justify-content:space-between;border:0;background:transparent;text-align:left;box-shadow:none}.platform-toggle:hover{background:rgba(65,216,255,.055);transform:none;box-shadow:none}.platform-toggle>span{display:grid;gap:2px}.platform-toggle b{color:var(--text);font-size:.76rem;letter-spacing:.025em}.platform-toggle small{color:var(--muted);font-size:.61rem;font-weight:650}.platform-toggle i{color:var(--cyan);font-style:normal;font-size:1rem;transition:transform .16s}.platform-toggle[aria-expanded=true] i{transform:rotate(180deg)}tr[data-platform-item][hidden]{display:none}
.health-loading-overlay{position:fixed;z-index:100;inset:0;display:grid;place-items:center;background:rgba(4,8,13,.86);backdrop-filter:blur(7px)}.health-loading-overlay[hidden]{display:none}.health-loading-overlay>div{width:min(340px,88vw);padding:30px 24px;display:grid;justify-items:center;gap:9px;border:1px solid rgba(65,216,255,.3);border-radius:16px;background:#101925;box-shadow:0 30px 90px rgba(0,0,0,.65)}.health-loading-overlay i{width:38px;height:38px;border:3px solid rgba(65,216,255,.18);border-top-color:var(--cyan);border-radius:50%;animation:health-spin .7s linear infinite}.health-loading-overlay strong{margin-top:5px;font-size:1rem}.health-loading-overlay span{color:var(--muted);font-size:.72rem}
""";

    private const string CreateLicenseSubmitButton =
        "<button type=submit class=primary data-force-submit data-busy=\"Criando licença...\">Criar e mostrar código único</button>";

    private const string AdminJavascript = """
(() => {
  "use strict";

  for (const trigger of document.querySelectorAll("[data-open-modal]")) {
    trigger.addEventListener("click", () => {
      const dialog = document.getElementById(trigger.dataset.openModal || "");
      if (dialog instanceof HTMLDialogElement && !dialog.open) dialog.showModal();
    });
  }
  for (const trigger of document.querySelectorAll("[data-close-modal]")) {
    trigger.addEventListener("click", () => {
      const dialog = trigger.closest("dialog");
      if (dialog instanceof HTMLDialogElement) dialog.close();
    });
  }
  for (const trigger of document.querySelectorAll("[data-focus-cleanup]")) {
    trigger.addEventListener("click", () => {
      const dialog = trigger.closest("dialog");
      const cleanup = dialog && dialog.querySelector("details.audit-cleanup");
      if (!(cleanup instanceof HTMLDetailsElement)) return;
      cleanup.open = true;
      cleanup.scrollIntoView({ behavior: "smooth", block: "center" });
      window.setTimeout(() => cleanup.querySelector("select")?.focus(), 250);
    });
  }
  for (const dialog of document.querySelectorAll("dialog.app-modal")) {
    dialog.addEventListener("click", event => {
      if (event.target === dialog) dialog.close();
    });
  }

  const normalize = value => (value || "").toLocaleLowerCase("pt-BR").trim();
  const setBusy = button => {
    if (!button || button.disabled) return;
    button.dataset.originalText = button.textContent || "";
    button.disabled = true;
    button.textContent = button.dataset.busy || "Aplicando...";
  };

  for (const button of document.querySelectorAll("button[data-confirm]")) {
    button.addEventListener("click", event => {
      if (!window.confirm(button.dataset.confirm || "Confirma esta operação?")) event.preventDefault();
    });
  }

  for (const form of document.querySelectorAll("form")) {
    form.addEventListener("submit", event => {
      const button = event.submitter;
      if (button && !button.hasAttribute("data-force-submit")) setBusy(button);
    });
  }

  if (document.querySelector("[data-health-pending]"))
  {
    const loading = document.querySelector("[data-health-loading]");
    if (loading) loading.hidden = false;
    window.setTimeout(() => window.location.reload(), 2000);
  }

  for (const form of document.querySelectorAll("[data-health-probe-form]"))
    form.addEventListener("submit", () => {
      const loading = document.querySelector("[data-health-loading]");
      if (loading) loading.hidden = false;
    });

  for (const button of document.querySelectorAll("[data-toggle-platform]")) {
    button.addEventListener("click", () => {
      const platform = button.dataset.togglePlatform || "";
      const opening = button.getAttribute("aria-expanded") !== "true";
      button.setAttribute("aria-expanded", opening ? "true" : "false");
      for (const row of document.querySelectorAll("[data-platform-item]"))
        if (row.dataset.platformItem === platform) row.hidden = !opening;
    });
  }

  for (const button of document.querySelectorAll("button[data-force-submit]")) {
    button.addEventListener("click", event => {
      const form = button.form;
      if (!form || button.disabled) return;
      event.preventDefault();
      if (!form.reportValidity()) return;
      const original = button.textContent;
      const status = form.querySelector(".submit-status");
      button.disabled = true;
      button.textContent = button.dataset.busy || "Enviando...";
      if (status) status.textContent = "";
      window.setTimeout(() => {
        if (!document.contains(button)) return;
        button.disabled = false;
        button.textContent = original;
        if (status) status.textContent = "O servidor não confirmou o envio. Atualize o painel e confira a lista antes de tentar novamente.";
      }, 10000);
      HTMLFormElement.prototype.submit.call(form);
    });
  }

  const copyText = async (text, button) => {
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      const original = button.textContent;
      button.textContent = "Copiado";
      window.setTimeout(() => { if (document.contains(button)) button.textContent = original; }, 1600);
    } catch (_) {
      button.textContent = "Não copiado";
    }
  };
  for (const button of document.querySelectorAll("[data-copy]"))
    button.addEventListener("click", () => copyText(button.dataset.copy || "", button));
  const oneTime = document.querySelector("[data-one-time-url]");
  const clearOneTime = () => {
    if (!oneTime) return;
    const code = oneTime.querySelector("#activation-code");
    if (code) { code.textContent = ""; code.removeAttribute("id"); code.remove(); }
    for (const button of oneTime.querySelectorAll("[data-copy-target]")) { button.removeAttribute("data-copy-target"); button.disabled = true; button.remove(); }
  };
  if (oneTime) {
    history.replaceState(null, "", oneTime.dataset.oneTimeUrl || "/admin/suite/issued");
    window.addEventListener("pagehide", clearOneTime);
    window.addEventListener("pageshow", event => { if (event.persisted) { clearOneTime(); location.replace(oneTime.dataset.oneTimeUrl || "/admin/suite/issued"); } });
    const exit = oneTime.querySelector("[data-one-time-exit]");
    if (exit) exit.addEventListener("click", event => { event.preventDefault(); const target = exit.href; clearOneTime(); location.replace(target); });
  }
  for (const button of document.querySelectorAll("[data-copy-target]"))
    button.addEventListener("click", () => {
      const target = document.querySelector(button.dataset.copyTarget || "");
      copyText(target ? target.textContent || "" : "", button);
    });

  const licenseSearch = document.querySelector("#license-search");
  const licenseStatus = document.querySelector("#license-status-filter");
  const licenseResult = document.querySelector("#license-filter-result");
  const licenseCards = [...document.querySelectorAll("[data-license-card]")];
  const filterLicenses = () => {
    const query = normalize(licenseSearch && licenseSearch.value);
    const status = licenseStatus ? licenseStatus.value : "all";
    let visible = 0;
    for (const card of licenseCards) {
      const matchesText = !query || normalize(card.dataset.search).includes(query);
      const matchesStatus = status === "all" || card.dataset.status === status;
      card.hidden = !(matchesText && matchesStatus);
      if (!card.hidden) visible += 1;
    }
    if (licenseResult) licenseResult.textContent = `${visible} de ${licenseCards.length} licenças`;
  };
  if (licenseSearch) licenseSearch.addEventListener("input", filterLicenses);
  if (licenseStatus) licenseStatus.addEventListener("change", filterLicenses);
  filterLicenses();

  const startSuiteClientsRefresh = () => {
    if (!document.getElementById("suite-clients")) return;
    let requestInFlight = false;
    let stopped = false;
    const refresh = async () => {
      if (stopped || requestInFlight || document.hidden) return;
      const current = document.getElementById("suite-clients");
      if (!current) { stopped = true; return; }
      requestInFlight = true;
      try {
        const response = await fetch("/admin/fragments/suite-clients", {
          method: "GET",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "manual",
          headers: { "Accept": "text/html", "X-Requested-With": "fetch" }
        });
        if (!response.ok || response.type === "opaqueredirect"
            || response.headers.get("X-Turborama-Fragment") !== "suite-clients") return;
        const parsed = new DOMParser().parseFromString(await response.text(), "text/html");
        const replacement = parsed.getElementById("suite-clients");
        const liveCurrent = document.getElementById("suite-clients");
        if (replacement && liveCurrent) liveCurrent.replaceWith(replacement);
      } catch (_) {
        // Mantém o último estado visível; a próxima rodada tenta novamente.
      } finally {
        requestInFlight = false;
      }
    };
    const timer = window.setInterval(refresh, 3000);
    document.addEventListener("visibilitychange", () => {
      if (!document.hidden) refresh();
    });
    window.addEventListener("pagehide", () => {
      stopped = true;
      window.clearInterval(timer);
    }, { once: true });
  };
  startSuiteClientsRefresh();

  const auditSearch = document.querySelector("#audit-search");
  const auditSeverity = document.querySelector("#audit-severity");
  const auditResult = document.querySelector("#audit-filter-result");
  const auditRows = [...document.querySelectorAll("[data-audit-row]")];
  const filterAudit = () => {
    const query = normalize(auditSearch && auditSearch.value);
    const severity = auditSeverity ? auditSeverity.value : "all";
    let visible = 0;
    for (const row of auditRows) {
      const matchesText = !query || normalize(row.dataset.search).includes(query);
      const matchesSeverity = severity === "all" || row.dataset.severity === severity;
      row.hidden = !(matchesText && matchesSeverity);
      if (!row.hidden) visible += 1;
    }
    if (auditResult) auditResult.textContent = `${visible} de ${auditRows.length} eventos`;
  };
  if (auditSearch) auditSearch.addEventListener("input", filterAudit);
  if (auditSeverity) auditSeverity.addEventListener("change", filterAudit);
  filterAudit();
})();
""";

    internal static bool HasForcedCreateLicenseSubmissionForSelfTest()
        => CreateLicenseSubmitButton.Contains("type=submit", StringComparison.Ordinal)
            && CreateLicenseSubmitButton.Contains("data-force-submit", StringComparison.Ordinal)
            && AdminJavascript.Contains("HTMLFormElement.prototype.submit.call(form)", StringComparison.Ordinal)
            && PageStart("self-test").Contains("/admin/assets/admin.js", StringComparison.Ordinal);

    internal static bool HasProfessionalDashboardForSelfTest()
    {
        var device = new AdminDeviceSnapshot(new string('a', 64), "ACTIVE", "SOFTWARE_BOUND_ONLINE",
            "25.0.0.0", 1, 2, long.MaxValue, true, 3, true);
        var license = new AdminLicenseSnapshot("CLI-0018", "TR-000125", "MAINTENANCE", true,
            "SOFTWARE_BOUND_ONLINE", 1, 0, 0, new Dictionary<int, long>(), [device], false, "");
        var page = DashboardPage(new AdminDashboardSnapshot(1, 3, [license],
            [new OnlineAuditEntry(1, "MACHINE_BINDING_MISMATCH", "TR-000125", new string('a', 64), "denied")]),
            "csrf", "", "");
        return page.Contains("Servidor de licença, gabinete autônomo", StringComparison.Ordinal)
            && page.Contains("Preços, Mercado Pago, PDV, QR Code e créditos", StringComparison.Ordinal)
            && page.Contains("/admin/export/audit.csv", StringComparison.Ordinal)
            && page.Contains("data-license-card", StringComparison.Ordinal)
            && page.Contains("value=\"MAINTENANCE\" selected", StringComparison.Ordinal)
            && page.Contains("data-confirm", StringComparison.Ordinal)
            && page.Contains("id=audit-search", StringComparison.Ordinal)
            && !page.Contains("/admin/actions/prices", StringComparison.Ordinal)
            && !page.Contains("/admin/actions/mercadopago", StringComparison.Ordinal);
    }

    internal static bool HasSafeAuditCsvForSelfTest()
    {
        var csv = AuditCsv([new OnlineAuditEntry(1, "EVENT", "TR-000125", new string('a', 64), "valor;\"teste\"")]);
        return csv.StartsWith("Quando;Evento;Licenca;Maquina;Detalhe\r\n", StringComparison.Ordinal)
            && csv.Contains("\"valor;\"\"teste\"\"\"", StringComparison.Ordinal)
            && csv.EndsWith("\r\n", StringComparison.Ordinal);
    }

    internal static bool HasAutomaticSuiteClientRefreshForSelfTest()
        => AdminJavascript.Contains("/admin/fragments/suite-clients", StringComparison.Ordinal)
            && AdminJavascript.Contains("window.setInterval(refresh, 3000)", StringComparison.Ordinal)
            && AdminJavascript.Contains("document.hidden", StringComparison.Ordinal)
            && AdminJavascript.Contains("X-Turborama-Fragment", StringComparison.Ordinal)
            && AdminJavascript.Contains("liveCurrent.replaceWith(replacement)", StringComparison.Ordinal);
}
