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
                    "default-src 'none'; style-src 'self'; script-src 'self'; img-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
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
        app.MapGet("/admin/assets/admin.js", () => Results.Text(AdminJavascript,
            "text/javascript; charset=utf-8"));

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
        MapAction(app, "/admin/actions/payment-enrollment-code", (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            return Task.FromResult("payment:" + repository.IssuePaymentEnrollmentCode(
                Required(form, "customerId")));
        }, showActivationCode: true);
        MapAction(app, "/admin/actions/device-transfer", (form, repository, admin, _) =>
        {
            RequireStepUp(admin, Required(form, "adminPassword"));
            return Task.FromResult("activation:" + repository.PrepareDeviceTransfer(
                Required(form, "licenseId")));
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
                    return Html(OneTimeCodePage(outcome[11..], "Código de ativação",
                        "Transporte-o diretamente para o único gabinete autorizado."));
                }
                if (showActivationCode && outcome.StartsWith("payment:", StringComparison.Ordinal))
                {
                    return Html(OneTimeCodePage(outcome[8..], "Código bancário",
                        "Use-o no CONFIGURAR-USER-TOKEN-PIX.exe em até 15 minutos."));
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
        var devices = snapshot.Licenses.Sum(license => license.Devices.Count);
        var activeLicenses = snapshot.Licenses.Count(license => license.Status == "ACTIVE");
        var pixEnabled = snapshot.Licenses.Count(license => license.PixEnabled);
        var rejected = snapshot.RejectedMachineAttempts;
        var html = new StringBuilder();
        html.Append(PageStart("Central TurboRama PIX"));
        html.Append("<header class=top><div><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central de licenças</h1><p>Máquinas autorizadas, disponibilidade e segurança em um só lugar.</p></div><nav class=top-actions aria-label=\"Ações do painel\"><a class='button ghost' href=#new-license>Nova licença</a><a class='button ghost' href=/admin/export/audit.csv>Exportar auditoria</a>");
        html.Append(FormStart("/admin/logout", token)).Append("<button class=ghost>Sair</button></form></nav></header>");
        if (ok.Length != 0) html.Append("<div class='notice success' role=status>Alteração aplicada e registrada.</div>");
        if (error.Length != 0) html.Append("<div class='notice danger'>A operação foi recusada. Código: ")
            .Append(E(error)).Append("</div>");
        html.Append("<section class=summary aria-label=\"Resumo operacional\"><article><span class=metric-icon>LIC</span><strong>")
            .Append(activeLicenses).Append("<small>/").Append(snapshot.Licenses.Count)
            .Append("</small></strong><span>Licenças ativas</span></article><article><span class=metric-icon>PC</span><strong>")
            .Append(online).Append("<small>/").Append(devices)
            .Append("</small></strong><span>Máquinas online</span></article><article><span class=metric-icon>PIX</span><strong>")
            .Append(pixEnabled).Append("</strong><span>Licenças com PIX liberado</span></article><article class='")
            .Append(rejected > 0 ? "metric-alert" : "").Append("'><span class=metric-icon>SEG</span><strong>")
            .Append(rejected).Append("</strong><span>Tentativas recusadas</span></article></section>");
        html.Append("<section class='scope-banner'><div><span class=eyebrow>ARQUITETURA SERVIDOR-AUTORITATIVO</span><h2>Licença e cada nova cobrança validadas no servidor</h2><p>Access Token e PDV permanecem somente no servidor LZ Games. O gabinete prova sua identidade, recebe apenas QR e estado do pagamento e continua executando jogos e créditos locais mesmo sem o serviço PIX.</p></div><span class='pill on'>SEGREDOS FORA DO GABINETE</span></section>");
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
            .Append(When(snapshot.GeneratedAtUnixSeconds)).Append("</span></div><div id=license-list>");
        foreach (var license in snapshot.Licenses) AppendLicense(html, license, token);
        if (snapshot.Licenses.Count == 0)
            html.Append("<section class='panel empty-state'><strong>Nenhuma licença cadastrada</strong><p>Use “Nova licença” para preparar a primeira instalação.</p></section>");
        html.Append("</div>");
        AppendAudit(html, snapshot.RecentAudit);
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
        html.Append("<section class=operation-card><span class=eyebrow>PIX</span><h3>Autorização remota</h3><p>Controla no servidor a permissão de criar cada nova cobrança PIX desta licença.</p>")
            .Append(FormStart("/admin/actions/pix", token, "stack")).Append(Hidden("licenseId", license.LicenseId))
            .Append(Hidden("enabled", license.PixEnabled ? "false" : "true"))
            .Append("<button class='").Append(license.PixEnabled ? "danger" : "primary")
            .Append("' data-busy=\"Aplicando...\" data-confirm=\"")
            .Append(license.PixEnabled ? "Confirma o bloqueio de novas cobranças PIX?" : "Confirma a liberação de novas cobranças PIX?")
            .Append("\">").Append(license.PixEnabled ? "Bloquear novas cobranças" : "Liberar novas cobranças")
            .Append("</button></form></section>");
        html.Append("<section class='operation-card local-card'><span class=eyebrow>DADOS BANCÁRIOS</span><h3>Segredos somente no servidor</h3><p>Gere um código de uso único para o configurador portátil enviar o token e o PDV diretamente ao Linux.</p>")
            .Append(FormStart("/admin/actions/payment-enrollment-code", token, "stack"))
            .Append(Hidden("customerId", license.CustomerId))
            .Append(Input("adminPassword", "Confirme sua senha", "", "password"))
            .Append("<button class=primary data-busy=\"Gerando...\">Gerar código bancário (15 min)</button></form></section></div>");
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

    private static void AppendAudit(StringBuilder html, IReadOnlyList<OnlineAuditEntry> audit)
    {
        html.Append("<section class=panel id=audit><div class=section-title><div><span class=eyebrow>SEGURANÇA</span><h2>Eventos recentes</h2><p>Histórico administrativo e recusas criptográficas.</p></div><a class='button ghost' href=/admin/export/audit.csv>Baixar CSV</a></div><div class=audit-toolbar><label>Pesquisar eventos<input id=audit-search type=search placeholder=\"Evento, licença, máquina ou detalhe\" autocomplete=off></label><label>Tipo<select id=audit-severity><option value=all>Todos</option><option value=critical>Críticos</option><option value=warning>Atenção</option><option value=success>Sucesso</option><option value=info>Informativos</option></select></label><div class=filter-result id=audit-filter-result role=status aria-live=polite></div></div><div class=table-wrap><table class=audit-table><thead><tr><th>Quando</th><th>Evento</th><th>Licença / máquina</th><th>Detalhe</th></tr></thead><tbody>");
        if (audit.Count == 0) html.Append("<tr><td colspan=4 class=muted>Nenhum evento registrado.</td></tr>");
        foreach (var item in audit)
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
        html.Append("</tbody></table></div></section>");
    }

    private static string LoginPage(string token, string message, bool enabled)
        => PageStart("Acesso administrativo") + "<section class=login><div class=login-card><div class=login-mark>TR</div><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central de licenças</h1><p>"
            + E(message) + "</p>" + (enabled ? FormStart("/admin/login", token, "stack")
                + Input("username", "Usuário", "") + Input("password", "Senha", "", "password")
                + "<button class=primary data-busy=\"Autenticando...\">Entrar com segurança</button></form>" : "")
                + "<div class=login-security>Protegido por Cloudflare Access + autenticação TurboRama</div></div></section>" + PageEnd();

    private static string OneTimeCodePage(string code, string title, string instruction)
		=> PageStart(title) + "<section class=login><div class=login-card><span class=eyebrow>USO ÚNICO</span><h1>" + E(title) + "</h1><p>Ele não será mostrado novamente. " + E(instruction) + "</p><code class=activation id=activation-code>"
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
    private static string PageStart(string title)
        => "<!doctype html><html lang=pt-BR><head><meta charset=utf-8><meta name=viewport content=\"width=device-width,initial-scale=1\"><title>"
            + E(title) + "</title><link rel=stylesheet href=/admin/assets/admin.css><script defer src=/admin/assets/admin.js></script></head><body><main class=shell>";
    private static string PageEnd() => "</main></body></html>";

    private const string Css = """
:root{color-scheme:dark;--bg:#061016;--panel:#0d1a21;--panel-2:#12232c;--line:#213943;--line-strong:#315563;--text:#f3f8f9;--muted:#94aab2;--cyan:#29c9e8;--green:#35df91;--red:#ff6e7f;--amber:#ffc857;--blue:#69a7ff;--shadow:0 20px 60px rgba(0,0,0,.28)}*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:radial-gradient(circle at 85% -10%,#164352 0,transparent 34%),linear-gradient(180deg,#07141a,#050c11 70%);color:var(--text);font:15px/1.5 Inter,ui-sans-serif,system-ui,-apple-system,Segoe UI,sans-serif}.shell{width:min(1480px,94vw);margin:auto;padding:32px 0 72px}.top,.top-actions,.section-title,.license-head,.license-pills,.row-actions,.id-line,.activation-actions{display:flex;align-items:center;gap:12px}.top,.section-title,.license-head{justify-content:space-between}.top{margin-bottom:24px}.top h1,.section-heading h2,.panel h2,.login h1,.scope-banner h2{margin:.2rem 0}.top p,.section-title p,.muted,.generated,.operation-card p,.scope-banner p{color:var(--muted)}.top-actions{justify-content:flex-end;flex-wrap:wrap}.top-actions form{display:inline-flex}.eyebrow{display:block;font-size:.7rem;font-weight:850;letter-spacing:.16em;color:var(--cyan)}.summary{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px;margin-bottom:18px}.summary article,.panel,.login-card,.scope-banner{background:linear-gradient(145deg,rgba(15,29,36,.98),rgba(8,18,24,.98));border:1px solid var(--line);border-radius:18px;box-shadow:var(--shadow)}.summary article{position:relative;padding:20px;min-height:122px}.summary strong{display:block;margin-top:10px;font-size:2rem;line-height:1;color:var(--green)}.summary strong small{font-size:.95rem;color:var(--muted);font-weight:700}.summary article>span:last-child{display:block;margin-top:10px;color:var(--muted)}.metric-icon{position:absolute;right:16px;top:16px;color:#53727e;font-size:.67rem;font-weight:900;letter-spacing:.12em}.summary .metric-alert strong{color:var(--amber)}.scope-banner{display:flex;align-items:center;justify-content:space-between;gap:28px;padding:22px 24px;margin-bottom:18px;border-color:rgba(41,201,232,.3);background:linear-gradient(120deg,rgba(17,43,52,.98),rgba(8,20,26,.98))}.scope-banner p{max-width:900px;margin:.45rem 0 0}.panel{padding:22px;margin:18px 0}.toolbar,.audit-toolbar{display:grid;grid-template-columns:minmax(280px,1fr) 220px auto;gap:14px;align-items:end}.filter-result{color:var(--muted);font-size:.82rem;text-align:right;padding-bottom:10px}.section-heading{display:flex;align-items:end;justify-content:space-between;margin:30px 2px 10px}.section-heading h2{font-size:1.35rem}.create-panel{scroll-margin-top:18px}.create-panel>summary,.advanced>summary{display:flex;align-items:center;justify-content:space-between;gap:20px;cursor:pointer;list-style:none}.create-panel>summary::-webkit-details-marker,.advanced>summary::-webkit-details-marker{display:none}.create-panel>summary strong{display:block;margin-top:5px;font-size:1.25rem}.summary-hint{color:var(--cyan);font-weight:800}.create-panel[open]>summary{padding-bottom:20px;border-bottom:1px solid var(--line)}.grid-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;align-items:end;margin-top:20px}.grid-form>.submit-status{grid-column:1/-1;color:#ffb4bd;font-weight:700}.license-card{scroll-margin-top:18px}.license-card[hidden],[data-audit-row][hidden]{display:none}.license-head{padding-bottom:18px;border-bottom:1px solid var(--line)}.license-head h2{overflow-wrap:anywhere}.license-pills{justify-content:flex-end;flex-wrap:wrap}.license-facts{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin:16px 0}.license-facts>div{padding:13px 14px;background:rgba(5,15,20,.65);border:1px solid var(--line);border-radius:12px}.license-facts span{display:block;color:var(--muted);font-size:.75rem}.license-facts strong{display:block;margin-top:4px}.operation-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin:18px 0}.operation-card{padding:17px;background:linear-gradient(145deg,rgba(18,35,44,.88),rgba(8,19,25,.88));border:1px solid var(--line);border-radius:14px}.operation-card h3{margin:.35rem 0}.operation-card p{min-height:45px;font-size:.84rem}.local-card{border-color:rgba(41,201,232,.25)}.machines-title{display:flex;align-items:end;justify-content:space-between;margin:24px 0 10px}.machines-title h3,.machines-title p{margin:.2rem 0}.pill,.status,.event-badge{display:inline-block;border-radius:999px;padding:6px 10px;font-weight:850;font-size:.69rem;letter-spacing:.045em;white-space:nowrap}.compact{padding:4px 8px;margin-top:7px}.pill.on,.status.online,.state-active,.event-success{background:rgba(53,223,145,.12);color:var(--green);border:1px solid rgba(53,223,145,.35)}.pill.off,.status.offline,.state-revoked,.event-critical{background:rgba(255,110,127,.11);color:#ff9baa;border:1px solid rgba(255,110,127,.36)}.state-suspended,.state-maintenance,.state-transfer_pending,.event-warning{background:rgba(255,200,87,.1);color:var(--amber);border:1px solid rgba(255,200,87,.35)}.neutral,.event-info{background:rgba(105,167,255,.1);color:#9fc6ff;border:1px solid rgba(105,167,255,.3)}button,.button,select,input{border-radius:10px;border:1px solid var(--line-strong);background:#07151b;color:var(--text);padding:10px 13px;font:inherit}button,.button{cursor:pointer;font-weight:780;text-decoration:none;text-align:center}button:hover,.button:hover{border-color:var(--cyan);transform:translateY(-1px)}button:disabled{opacity:.55;cursor:wait;transform:none}.primary{background:linear-gradient(135deg,#16bfe0,#2ad487);border:0;color:#031116}.danger,.danger-zone button{border-color:rgba(255,110,127,.55);color:#ff9baa}.ghost{background:transparent}.stack{display:grid;gap:11px}.stack button{width:100%}label{display:grid;gap:6px;color:var(--muted);font-size:.8rem;font-weight:750}input:focus,select:focus,button:focus,.button:focus,summary:focus{outline:2px solid var(--cyan);outline-offset:2px}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:13px;background:rgba(5,14,19,.45)}table{width:100%;border-collapse:collapse;min-width:980px}th,td{text-align:left;padding:12px;border-bottom:1px solid var(--line);vertical-align:top}tbody tr:last-child td{border-bottom:0}tbody tr:hover{background:rgba(41,201,232,.035)}th{color:var(--muted);font-size:.69rem;letter-spacing:.09em;text-transform:uppercase}code{color:#b9eff8}.id-line{align-items:flex-start}.copy-button{font-size:.68rem;padding:4px 7px}.row-actions{align-items:flex-start;flex-wrap:wrap}.row-actions form{display:inline-flex}.row-actions button{font-size:.72rem;padding:7px 9px}.advanced{margin-top:16px;padding-top:14px;border-top:1px solid var(--line)}.advanced>summary{color:var(--muted);font-weight:800}.advanced[open]>summary{color:var(--text);margin-bottom:14px}.advanced-grid{display:grid;grid-template-columns:1fr 1fr;gap:14px}.danger-zone{border-color:rgba(255,110,127,.28)}.audit-toolbar{margin:18px 0 12px}.audit-table{min-width:900px}.event-code{font-size:.68rem;color:var(--muted)}.notice{padding:13px 16px;border-radius:11px;margin-bottom:15px;border:1px solid transparent}.success{background:rgba(53,223,145,.11);color:var(--green);border-color:rgba(53,223,145,.25)}.notice.danger{background:rgba(255,110,127,.11);color:#ffb4bd;border-color:rgba(255,110,127,.25)}.empty-state{text-align:center;padding:42px}.empty-state strong{font-size:1.2rem}.login{min-height:82vh;display:grid;place-items:center}.login-card{width:min(470px,92vw);padding:34px}.login-mark{width:52px;height:52px;display:grid;place-items:center;border-radius:15px;margin-bottom:20px;background:linear-gradient(135deg,var(--cyan),var(--green));color:#031116;font-weight:950;letter-spacing:.05em}.login-security{margin-top:22px;padding-top:17px;border-top:1px solid var(--line);color:var(--muted);font-size:.75rem}.activation{display:block;padding:16px;margin:18px 0;background:#061015;border:1px solid var(--line);border-radius:10px;overflow-wrap:anywhere;color:var(--amber);font-size:1rem}.activation-actions{align-items:stretch}.activation-actions>*{flex:1}.compact-text{font-size:.78rem}@media(max-width:1100px){.summary{grid-template-columns:repeat(2,1fr)}.operation-grid{grid-template-columns:1fr 1fr}.local-card{grid-column:1/-1}.license-facts{grid-template-columns:repeat(2,1fr)}}@media(max-width:760px){.shell{width:min(94vw,680px);padding-top:22px}.top,.scope-banner,.section-title,.license-head,.section-heading{align-items:flex-start;flex-direction:column}.top-actions{justify-content:flex-start}.summary,.operation-grid,.license-facts,.grid-form,.toolbar,.audit-toolbar,.advanced-grid{grid-template-columns:1fr}.local-card{grid-column:auto}.filter-result{text-align:left;padding:0}.license-pills{justify-content:flex-start}.scope-banner .pill{white-space:normal}.activation-actions{flex-direction:column}table{min-width:820px}}
""";

    private const string CreateLicenseSubmitButton =
        "<button type=submit class=primary data-force-submit data-busy=\"Criando licença...\">Criar e mostrar código único</button>";

    private const string AdminJavascript = """
(() => {
  "use strict";

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
        return page.Contains("Licença e cada nova cobrança validadas no servidor", StringComparison.Ordinal)
            && page.Contains("Access Token e PDV permanecem somente no servidor", StringComparison.Ordinal)
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
}
