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
    }

    public static void UseSecurityHeaders(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/admin"))
            {
                context.Response.Headers.CacheControl = "no-store, max-age=0";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'none'; style-src 'self'; img-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
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
        app.MapGet("/admin/assets/admin.css", () => Results.Text(Css, "text/css; charset=utf-8"));

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
                new Claim(ClaimTypes.Role, "Administrator")
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

        app.MapGet("/admin", (HttpContext context, IAntiforgery antiforgery,
            OnlineStateRepository repository) =>
        {
            var token = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
            return Html(DashboardPage(repository.ReadAdminDashboard(), token,
                context.Request.Query["ok"].ToString(), context.Request.Query["error"].ToString()));
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

    private static string DashboardPage(AdminDashboardSnapshot snapshot, string token, string ok, string error)
    {
        var online = snapshot.Licenses.Sum(license => license.Devices.Count(device => device.Online));
        var rejected = snapshot.RejectedMachineAttempts;
        var html = new StringBuilder();
        html.Append(PageStart("Central TurboRama PIX"));
        html.Append("<header class=top><div><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central de licenças e PIX</h1><p>Controle de máquinas, sessões, preços e recebimento.</p></div>");
        html.Append(FormStart("/admin/logout", token)).Append("<button class=ghost>Sair</button></form></header>");
        if (ok.Length != 0) html.Append("<div class='notice success'>Alteração aplicada e registrada.</div>");
        if (error.Length != 0) html.Append("<div class='notice danger'>A operação foi recusada. Código: ")
            .Append(E(error)).Append("</div>");
        html.Append("<section class=summary><article><strong>").Append(snapshot.Licenses.Count)
            .Append("</strong><span>Licenças</span></article><article><strong>").Append(online)
            .Append("</strong><span>Máquinas online</span></article><article><strong>").Append(rejected)
            .Append("</strong><span>Tentativas recusadas</span></article></section>");
        html.Append("<section class=panel><div class=section-title><div><span class=eyebrow>NOVA INSTALAÇÃO</span><h2>Criar licença</h2></div></div>")
            .Append(FormStart("/admin/actions/create-license", token, "grid-form"))
            .Append(Input("customerId", "Cliente", "CLI-TURBORAMA-TESTE"))
            .Append(Input("licenseId", "Licença", "TR-TURBORAMA-TESTE-001"))
            .Append("<label>Proteção<select name=bindingType><option>SOFTWARE_BOUND_ONLINE</option><option>TPM_BOUND</option></select></label>")
            .Append(Input("maximumDevices", "Máquinas permitidas", "1", "number"))
            .Append(Input("adminPassword", "Confirme sua senha", "", "password"))
            .Append("<button class=primary>Criar e mostrar código único</button></form></section>");
        foreach (var license in snapshot.Licenses) AppendLicense(html, license, token);
        AppendAudit(html, snapshot.RecentAudit);
        html.Append(PageEnd());
        return html.ToString();
    }

    private static void AppendLicense(StringBuilder html, AdminLicenseSnapshot license, string token)
    {
        html.Append("<section class=panel><div class=section-title><div><span class=eyebrow>")
            .Append(E(license.CustomerId)).Append("</span><h2>").Append(E(license.LicenseId))
            .Append("</h2><p>").Append(E(license.BindingType)).Append(" · ")
            .Append(license.Devices.Count).Append('/').Append(license.MaximumDevices).Append(" máquinas</p></div>")
            .Append("<div class='pill ").Append(license.PixEnabled ? "on'>PIX ATIVO" : "off'>PIX BLOQUEADO")
            .Append("</div></div>");
        html.Append("<div class=actions>")
            .Append(FormStart("/admin/actions/pix", token)).Append(Hidden("licenseId", license.LicenseId))
            .Append(Hidden("enabled", license.PixEnabled ? "false" : "true"))
            .Append("<button class='").Append(license.PixEnabled ? "danger" : "primary").Append("'>")
            .Append(license.PixEnabled ? "Bloquear novas cobranças PIX" : "Ativar serviço PIX").Append("</button></form>")
            .Append(FormStart("/admin/actions/license-status", token)).Append(Hidden("licenseId", license.LicenseId))
            .Append("<select name=status><option>ACTIVE</option><option>SUSPENDED</option><option>MAINTENANCE</option><option>TRANSFER_PENDING</option><option>REVOKED</option></select><button>Alterar licença</button></form></div>");
        html.Append("<div class=columns><div><h3>Preços sincronizados</h3><p class=muted>Versão ")
            .Append(license.ConfigurationVersion).Append(". Site e EmulationStation usam esta configuração.</p>")
            .Append(FormStart("/admin/actions/prices", token, "price-grid")).Append(Hidden("licenseId", license.LicenseId));
        foreach (var minutes in new[] { 15, 30, 45, 60, 120 })
        {
            license.PackagePricesCents.TryGetValue(minutes, out var cents);
            html.Append(Input("price" + minutes, minutes + " minutos", cents == 0 ? "" : Reais(cents), "text"));
        }
        html.Append("<button class=primary>Salvar preços no servidor</button></form></div>");
        html.Append("<div><h3>Mercado Pago</h3><p class=muted>")
            .Append(license.MercadoPagoConfigured ? "Credencial protegida · caixa " + E(license.ExternalPosId) : "Ainda não configurado")
            .Append("</p>").Append(FormStart("/admin/actions/mercadopago", token, "stack"))
            .Append(Hidden("customerId", license.CustomerId))
            .Append(Input("externalPosId", "External ID do caixa", license.ExternalPosId))
            .Append(Input("accessToken", "Novo Access Token", "", "password"))
            .Append(Input("adminPassword", "Confirme sua senha", "", "password"))
            .Append("<button>Validar e substituir credencial</button></form></div></div>");
        html.Append("<h3>Máquinas autorizadas</h3><div class=table-wrap><table><thead><tr><th>Máquina</th><th>Proteção</th><th>Versão</th><th>Conexão</th><th>Recusas</th><th>Configuração</th><th>Ações</th></tr></thead><tbody>");
        if (license.Devices.Count == 0) html.Append("<tr><td colspan=7 class=muted>Nenhuma máquina ativada.</td></tr>");
        foreach (var device in license.Devices)
        {
            html.Append("<tr><td><code>").Append(E(Short(device.DeviceId))).Append("</code><br><span class=muted>")
                .Append(E(device.Status)).Append("</span></td><td>").Append(E(device.BindingType))
                .Append("</td><td>").Append(E(device.AgentVersion)).Append("</td><td><span class='status ")
                .Append(device.Online ? "online'>ONLINE" : "offline'>OFFLINE").Append("</span><br><span class=muted>")
                .Append(When(device.LastContactUnixSeconds)).Append("</span></td><td>").Append(device.RejectedAttempts)
                .Append("</td><td>").Append(device.CanManageConfiguration ? "Permitida" : "Somente leitura")
                .Append("</td><td><div class=row-actions>");
            html.Append(FormStart("/admin/actions/device-status", token)).Append(Hidden("licenseId", license.LicenseId))
                .Append(Hidden("deviceId", device.DeviceId)).Append(Hidden("status", device.Status == "ACTIVE" ? "SUSPENDED" : "ACTIVE"))
                .Append("<button>").Append(device.Status == "ACTIVE" ? "Recusar" : "Permitir").Append("</button></form>");
            html.Append(FormStart("/admin/actions/device-configuration", token)).Append(Hidden("licenseId", license.LicenseId))
                .Append(Hidden("deviceId", device.DeviceId)).Append(Hidden("allowed", device.CanManageConfiguration ? "false" : "true"))
                .Append("<button>").Append(device.CanManageConfiguration ? "Bloquear edição" : "Permitir edição").Append("</button></form>");
            html.Append(FormStart("/admin/actions/force-reauth", token)).Append(Hidden("licenseId", license.LicenseId))
                .Append(Hidden("deviceId", device.DeviceId)).Append("<button>Encerrar sessão</button></form></div></td></tr>");
        }
        html.Append("</tbody></table></div>")
            .Append(FormStart("/admin/actions/activation-code", token, "activation-form"))
            .Append(Hidden("licenseId", license.LicenseId))
            .Append(Input("adminPassword", "Senha para gerar novo código", "", "password"))
            .Append("<button>Gerar novo código de ativação</button></form></section>");
    }

    private static void AppendAudit(StringBuilder html, IReadOnlyList<OnlineAuditEntry> audit)
    {
        html.Append("<section class=panel><div class=section-title><div><span class=eyebrow>SEGURANÇA</span><h2>Eventos recentes</h2></div></div><div class=table-wrap><table><thead><tr><th>Quando</th><th>Evento</th><th>Licença</th><th>Máquina</th><th>Detalhe</th></tr></thead><tbody>");
        foreach (var item in audit)
            html.Append("<tr><td>").Append(When(item.AtUnixSeconds)).Append("</td><td>").Append(E(item.Event))
                .Append("</td><td>").Append(E(item.LicenseId)).Append("</td><td><code>")
                .Append(E(Short(item.DeviceId))).Append("</code></td><td>").Append(E(item.Detail)).Append("</td></tr>");
        html.Append("</tbody></table></div></section>");
    }

    private static string LoginPage(string token, string message, bool enabled)
        => PageStart("Acesso administrativo") + "<section class=login><div class=login-card><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central PIX</h1><p>"
            + E(message) + "</p>" + (enabled ? FormStart("/admin/login", token, "stack")
                + Input("username", "Usuário", "") + Input("password", "Senha", "", "password")
                + "<button class=primary>Entrar com segurança</button></form>" : "") + "</div></section>" + PageEnd();

    private static string ActivationCodePage(string code)
        => PageStart("Código de ativação") + "<section class=login><div class=login-card><span class=eyebrow>USO ÚNICO</span><h1>Código de ativação</h1><p>Ele não será mostrado novamente. Transporte-o diretamente para o único gabinete autorizado.</p><code class=activation>"
            + E(code) + "</code><a class='button primary' href=/admin>Voltar ao painel</a></div></section>" + PageEnd();

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
    private static string PageStart(string title)
        => "<!doctype html><html lang=pt-BR><head><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><title>"
            + E(title) + "</title><link rel=stylesheet href=/admin/assets/admin.css></head><body><main class=shell>";
    private static string PageEnd() => "</main></body></html>";

    private const string Css = """
:root{color-scheme:dark;--bg:#071015;--panel:#101d24;--line:#243943;--text:#eef7f8;--muted:#91a9b2;--cyan:#1dcbe8;--green:#39e58c;--red:#ff6577;--amber:#ffcc4a}*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at top right,#13323d 0,#071015 42%);color:var(--text);font:15px/1.45 system-ui,-apple-system,Segoe UI,sans-serif}.shell{width:min(1500px,94vw);margin:auto;padding:34px 0 70px}.top,.section-title,.actions,.inline-form,.row-actions{display:flex;align-items:center;gap:12px}.top,.section-title{justify-content:space-between}.top{margin-bottom:28px}.top h1,.panel h2,.login h1{margin:.2rem 0}.top p,.section-title p,.muted{color:var(--muted)}.eyebrow{font-size:.72rem;font-weight:800;letter-spacing:.15em;color:var(--cyan)}.summary{display:grid;grid-template-columns:repeat(3,1fr);gap:15px;margin-bottom:18px}.summary article,.panel,.login-card{background:linear-gradient(145deg,rgba(16,29,36,.97),rgba(10,20,26,.97));border:1px solid var(--line);border-radius:18px;box-shadow:0 18px 48px rgba(0,0,0,.25)}.summary article{padding:20px}.summary strong{display:block;font-size:2rem;color:var(--green)}.summary span{color:var(--muted)}.panel{padding:24px;margin:18px 0}.pill,.status{display:inline-block;border-radius:999px;padding:7px 12px;font-weight:800;font-size:.75rem;letter-spacing:.05em}.pill.on,.status.online{background:rgba(57,229,140,.12);color:var(--green);border:1px solid rgba(57,229,140,.4)}.pill.off,.status.offline{background:rgba(255,101,119,.12);color:var(--red);border:1px solid rgba(255,101,119,.4)}button,.button,select,input{border-radius:10px;border:1px solid #35505d;background:#0a171d;color:var(--text);padding:10px 13px;font:inherit}button,.button{cursor:pointer;font-weight:750;text-decoration:none}.primary{background:linear-gradient(135deg,#0ea5c6,#1bc98a);border:0;color:#041114}.danger{border-color:rgba(255,101,119,.55);color:#ff9baa}.ghost{background:transparent}.actions{flex-wrap:wrap;margin:18px 0}.grid-form{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;align-items:end}.columns{display:grid;grid-template-columns:1.2fr .8fr;gap:24px;margin:24px 0}.price-grid{display:grid;grid-template-columns:repeat(5,1fr);gap:10px;align-items:end}.price-grid button{grid-column:1/-1}.stack{display:grid;gap:12px}.stack button{width:100%}label{display:grid;gap:6px;color:var(--muted);font-size:.82rem;font-weight:700}input:focus,select:focus,button:focus{outline:2px solid var(--cyan);outline-offset:2px}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:12px}table{width:100%;border-collapse:collapse;min-width:850px}th,td{text-align:left;padding:12px;border-bottom:1px solid var(--line);vertical-align:top}th{color:var(--muted);font-size:.72rem;letter-spacing:.08em}code{color:#b9eff8}.row-actions{align-items:flex-start;flex-wrap:wrap}.row-actions button{font-size:.75rem;padding:7px 9px}.activation-form{margin-top:16px;display:flex;align-items:end;gap:10px}.notice{padding:13px 16px;border-radius:10px;margin-bottom:15px}.success{background:rgba(57,229,140,.12);color:var(--green)}.notice.danger{background:rgba(255,101,119,.12);color:#ffb4bd}.login{min-height:82vh;display:grid;place-items:center}.login-card{width:min(460px,92vw);padding:32px}.activation{display:block;padding:16px;margin:18px 0;background:#061015;border:1px solid var(--line);border-radius:10px;overflow-wrap:anywhere;color:var(--amber)}@media(max-width:900px){.summary,.columns,.grid-form{grid-template-columns:1fr}.price-grid{grid-template-columns:repeat(2,1fr)}.top,.section-title{align-items:flex-start;flex-direction:column}.activation-form{display:grid}}
""";
}
