using System.Globalization;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;

static partial class SuiteAdminPanel
{
    private const string Read = "suite.read";
    private const string Issue = "suite.activation.issue";
    private const string Export = "suite.audit.export";
    private const string ContentRead = "suite.content.read";
    private const string ContentReplace = "suite.content.origin.replace";
    private const string ContentVersion = "suite.content.version.publish";
    private const string ContentCheck = "suite.content.check";

    public static void Map(WebApplication app)
    {
        MapSessions(app);
        app.MapGet("/admin/clientes/{licenseId}",async(string licenseId,HttpContext context,
            IAntiforgery antiforgery,SuiteAdminBff bff,CancellationToken ct)=>
        {
            if(!Has(context,Read)||!ValidLicenseId(licenseId))return Results.Forbid();
            var customer=LoadCustomerLicenses().FirstOrDefault(x=>
                string.Equals(x.LicenseId,licenseId,StringComparison.OrdinalIgnoreCase));
            if(customer is null)return Results.NotFound();
            try
            {
                var csrf=antiforgery.GetAndStoreTokens(context).RequestToken??"";
                return Html(CustomerHistoryPage(customer,await bff.CustomerActivityAsync(licenseId,ct),
                    csrf,context.Request.Query["ok"].ToString(),await SessionSectionAsync(context,bff,licenseId,csrf,ct)));
            }
            catch(HttpRequestException){return Html(Unavailable(),503);}
        }).RequireAuthorization();
        app.MapPost("/admin/clientes/actions/clear-games",async(HttpContext context,IAntiforgery antiforgery,
            OnlineAdminConfiguration admin,SuiteAdminBff bff,CancellationToken ct)=>
        {
            if(!Has(context,Read))return Results.Forbid();
            try
            {
                await antiforgery.ValidateRequestAsync(context);var form=await context.Request.ReadFormAsync(ct);
                var id=Required(form,"licenseId");if(!ValidLicenseId(id)||
                    !AdminPasswordHash.Verify(admin.PasswordHash,Required(form,"adminPassword")))throw new SecurityException();
                await bff.ClearCustomerActivityAsync(id,context.User.Identity?.Name??"unknown",RequestId(),ct);
                return Results.Redirect("/admin/clientes/"+Uri.EscapeDataString(id)+"?ok=HISTORICO_LIMPO");
            }
            catch(Exception ex)when(ex is SecurityException or AntiforgeryValidationException or HttpRequestException)
            {return Results.Redirect("/admin?error=OPERATION_DENIED");}
        }).RequireAuthorization();

        app.MapGet("/admin/suite", async (HttpContext context, IAntiforgery antiforgery,
            SuiteAdminBff bff, CancellationToken ct) =>
        {
            if (!Has(context, Read)) return Results.Forbid();
            try
            {
                var selected = context.Request.Query["licenseId"].ToString().Trim();
                var customers = LoadCustomerLicenses();
                if (selected.Length == 0) return Html(LicenseSearch(customers, await ActiveCustomerPanelAsync(context,bff,ct)));
                if (!ValidLicenseId(selected)) throw new SecurityException();
                return Html(Page(await bff.StatusAsync(selected, ct),
                    antiforgery.GetAndStoreTokens(context).RequestToken ?? "",
                    context.Request.Query["error"].ToString(), customers,
                    await SessionSectionAsync(context,bff,selected,antiforgery.GetAndStoreTokens(context).RequestToken??"",ct)));
            }
            catch (HttpRequestException) { return Html(Unavailable(), 503); }
        }).RequireAuthorization();

        app.MapGet("/admin/suite/issued", () => Results.Redirect("/admin/suite"))
            .RequireAuthorization();

        app.MapPost("/admin/suite/actions/issue-otp", async (HttpContext context,
            IAntiforgery antiforgery, OnlineAdminConfiguration admin, SuiteAdminBff bff,
            SuiteIssueGuard guard, CancellationToken ct) =>
        {
            if (!Has(context, Issue)) return Results.Forbid();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(ct);
                var id = Required(form, "licenseId");
                var device = Required(form, "deviceId");
                if (!Fixed(id, Required(form, "confirmLicenseId")) ||
                    !Fixed(device, Required(form, "confirmDeviceId")) ||
                    !ValidLicenseId(id)) throw new SecurityException();
                var actor = context.User.Identity?.Name ?? "unknown";
                if (!AdminPasswordHash.Verify(admin.PasswordHash, Required(form, "adminPassword")))
                {
                    await AuditDenial(bff, id, device, actor, "STEP_UP_DENIED", ct);
                    return Results.Redirect("/admin/suite?error=OPERATION_DENIED");
                }
                var key = actor + '\0' + id + '\0' +
                    (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                if (!guard.Allow(key))
                {
                    await AuditDenial(bff, id, device, actor, "RATE_LIMIT", ct);
                    return Results.Redirect("/admin/suite?error=RATE_LIMIT");
                }
                var status = await bff.StatusAsync(id, ct);
                if (!status.CanIssue || !Fixed(status.DeviceId, device)) throw new SecurityException();
                var issued = await bff.IssueAsync(id, device, actor, RequestId(), ct);
                context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                return Html(Otp(issued,null,null));
            }
            catch (Exception ex) when (ex is SecurityException or AntiforgeryValidationException or
                HttpRequestException)
            {
                return Results.Redirect("/admin/suite?error=OPERATION_DENIED");
            }
        }).RequireAuthorization();

        app.MapPost("/admin/suite/actions/issue-first-claim",async(HttpContext context,
            IAntiforgery antiforgery,OnlineAdminConfiguration admin,SuiteAdminBff bff,
            SuiteIssueGuard guard,CancellationToken ct)=>
        {
            if(!Has(context,Issue))return Results.Forbid();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form=await context.Request.ReadFormAsync(ct);var id=Required(form,"licenseId");
                if(!ValidLicenseId(id)||!Fixed(id,Required(form,"confirmLicenseId")))throw new SecurityException();
                var actor=context.User.Identity?.Name??"unknown";
                if(!AdminPasswordHash.Verify(admin.PasswordHash,Required(form,"adminPassword")))
                    return Results.Redirect("/admin/suite?licenseId="+Uri.EscapeDataString(id)+"&error=OPERATION_DENIED");
                var key=actor+'\0'+id+'\0'+(context.Connection.RemoteIpAddress?.ToString()??"unknown");
                if(!guard.Allow(key))return Results.Redirect("/admin/suite?licenseId="+Uri.EscapeDataString(id)+"&error=RATE_LIMIT");
                var customer=LoadCustomerLicenses().FirstOrDefault(item=>
                    string.Equals(item.LicenseId,id,StringComparison.OrdinalIgnoreCase));
                if(customer is null)throw new SecurityException();
                var issued=await bff.IssueFirstClaimAsync(id,actor,RequestId(),ct);
                context.Response.Headers.CacheControl="no-store, no-cache, must-revalidate";
                context.Response.Headers.Pragma="no-cache";
                return Html(Otp(new SuiteOtpResult("TURBORAMA_SUITE",issued.LicenseId,"",issued.Otp,issued.ExpiresAt),
                    customer.Customer,customer.Email));
            }
            catch(Exception ex)when(ex is SecurityException or AntiforgeryValidationException or HttpRequestException)
            {return Results.Redirect("/admin/suite?error=OPERATION_DENIED");}
        }).RequireAuthorization();

        app.MapGet("/admin/suite/export/audit.csv", async (HttpContext context,
            SuiteAdminBff bff, CancellationToken ct) =>
        {
            if (!Has(context, Export)) return Results.Forbid();
            try
            {
                return Results.File(await bff.AuditAsync(ct), "text/csv; charset=utf-8",
                    "turborama-suite-otp-audit.csv");
            }
            catch (HttpRequestException) { return Results.StatusCode(503); }
        }).RequireAuthorization();

        MapContent(app);
    }

    private static void MapContent(WebApplication app)
    {
        app.MapGet("/admin/suite/content", async (HttpContext context, IAntiforgery antiforgery,
            SuiteAdminBff bff, SuiteContentAdminGuard guard, CancellationToken ct) =>
        {
            if (!Has(context, ContentRead)) return Results.Forbid();
            if (!ContentPageQuery.TryParse(context.Request.Query, out var query))
                return ContentRedirect(error: "CONSULTA_INVALIDA");
            var proof = ContentProof(context, ContentRead);
            if (!guard.Allow(proof.Actor, proof.IpDigest, query!.ManageItem ?? "all", "read", 120, 60))
                return Html(ContentUnavailable("LIMITE_ATINGIDO"), 429);
            try
            {
                var page = await bff.ContentItemsAsync(proof, query.Cursor, 50,
                    query.Availability, query.ResultCode, query.JobState, query.ItemPrefix,
                    query.Name, ct);
                var audit = await bff.ContentAuditAsync(proof, query.AuditCursor, 20,
                    query.ManageItem, ct);
                var token = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
                return Html(ContentPage(page, audit, query, token,
                    context.Request.Query["ok"].ToString(),
                    context.Request.Query["error"].ToString(),
                    Has(context, ContentReplace), Has(context, ContentVersion),
                    Has(context, ContentCheck)));
            }
            catch (HttpRequestException) { return Html(ContentUnavailable("SERVICO_INDISPONIVEL"), 503); }
        }).RequireAuthorization();

        app.MapGet("/admin/suite/content/health", async (HttpContext context,
            IAntiforgery antiforgery, SuiteAdminBff bff, SuiteContentAdminGuard guard,
            CancellationToken ct) =>
        {
            if (!Has(context, ContentRead)) return Results.Forbid();
            if (!ContentPageQuery.TryParse(context.Request.Query, out var query))
                return Results.Redirect("/admin/suite/content/health?error=CONSULTA_INVALIDA");
            var proof = ContentProof(context, ContentRead);
            if (!guard.Allow(proof.Actor, proof.IpDigest, "link-health", "read", 120, 60))
                return Html(ContentUnavailable("LIMITE_ATINGIDO"), 429);
            try
            {
                var page = await bff.ContentItemsAsync(proof, query!.Cursor, 902,
                    query.Availability, query.ResultCode, null, query.ItemPrefix,
                    query.Name, ct);
                var token = antiforgery.GetAndStoreTokens(context).RequestToken ?? "";
                return Html(ContentHealthPage(page, query, token,
                    context.Request.Query["ok"].ToString(),
                    context.Request.Query["error"].ToString(), Has(context, ContentCheck),
                    context.Request.Query["testing"].ToString(),
                    context.Request.Query["started"].ToString()));
            }
            catch (HttpRequestException)
            {
                return Html(ContentUnavailable("SERVICO_INDISPONIVEL"), 503);
            }
        }).RequireAuthorization();

        app.MapGet("/admin/suite/content/jobs/{candidateId}", async (HttpContext context,
            string candidateId, SuiteAdminBff bff, CancellationToken ct) =>
        {
            if (!Has(context, ContentRead)) return Results.Forbid();
            try
            {
                return Html(ContentJobPage(await bff.ContentJobAsync(
                    ContentProof(context, ContentRead), candidateId, ct)));
            }
            catch (HttpRequestException) { return Html(ContentUnavailable("TRABALHO_INDISPONIVEL"), 404); }
        }).RequireAuthorization();

        app.MapPost("/admin/suite/content/actions/check", async (HttpContext context,
            IAntiforgery antiforgery, OnlineAdminConfiguration admin, SuiteAdminBff bff,
            SuiteContentAdminGuard guard, CancellationToken ct) =>
        {
            if (!Has(context, ContentCheck)) return Results.Forbid();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(ct);
                var itemId = ContentItemId(form);
                var proof = ContentProof(context, ContentCheck);
                if (!guard.Allow(proof.Actor, proof.IpDigest, itemId, "check", 6, 60))
                    return ContentRedirect(error: "LIMITE_ATINGIDO", manage: itemId);
                RequireItemConfirmation(form, itemId);
                RequireStepUp(admin, ContentPassword(form, "adminPassword"));
                proof = proof with { StepUpAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                var checkStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _ = await bff.CheckContentAsync(proof, itemId, RequestId(), ct);
                await File.WriteAllTextAsync("/var/lib/turborama-pix/content-monitor.trigger",
                    $"{itemId} {checkStarted}\n", ct);
                if (form["returnTo"].ToString() == "health")
                    return Results.Redirect("/admin/suite/content/health" + QueryString.Create(
                        new Dictionary<string, string?>
                        {
                            ["ok"] = "VERIFICACAO_ENFILEIRADA",
                            ["testing"] = itemId,
                            ["started"] = checkStarted.ToString(CultureInfo.InvariantCulture)
                        }).ToUriComponent());
                return ContentRedirect(ok: "VERIFICACAO_ENFILEIRADA", manage: itemId);
            }
            catch (Exception ex) when (ContentPostFailure(ex))
            {
                return ContentRedirect(error: "OPERACAO_RECUSADA");
            }
        }).RequireAuthorization();

        app.MapPost("/admin/suite/content/actions/replace", async (HttpContext context,
            IAntiforgery antiforgery, OnlineAdminConfiguration admin, SuiteAdminBff bff,
            SuiteContentAdminGuard guard, CancellationToken ct) =>
        {
            if (!Has(context, ContentReplace)) return Results.Forbid();
            string candidateUrl = "";
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(ct);
                var itemId = ContentItemId(form);
                var proof = ContentProof(context, ContentReplace);
                if (!guard.Allow(proof.Actor, proof.IpDigest, itemId, "replace", 3, 60))
                    return ContentRedirect(error: "LIMITE_ATINGIDO", manage: itemId);
                RequireItemConfirmation(form, itemId);
                RequireStepUp(admin, ContentPassword(form, "adminPassword"));
                candidateUrl = CandidateUrl(form);
                proof = proof with { StepUpAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                _ = await bff.ReplaceContentOriginAsync(proof, itemId, candidateUrl,
                    RequestId(), ct);
                candidateUrl = "";
                return ContentRedirect(ok: "SUBSTITUICAO_ENFILEIRADA", manage: itemId);
            }
            catch (Exception ex) when (ContentPostFailure(ex))
            {
                candidateUrl = "";
                return ContentRedirect(error: "OPERACAO_RECUSADA");
            }
            finally { candidateUrl = ""; }
        }).RequireAuthorization();

        app.MapPost("/admin/suite/content/actions/version", async (HttpContext context,
            IAntiforgery antiforgery, OnlineAdminConfiguration admin, SuiteAdminBff bff,
            SuiteContentAdminGuard guard, CancellationToken ct) =>
        {
            if (!Has(context, ContentVersion)) return Results.Forbid();
            string candidateUrl = "";
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form = await context.Request.ReadFormAsync(ct);
                var itemId = ContentItemId(form);
                var proof = ContentProof(context, ContentVersion);
                if (!guard.Allow(proof.Actor, proof.IpDigest, itemId, "version", 2, 900))
                    return ContentRedirect(error: "LIMITE_ATINGIDO", manage: itemId);
                RequireItemConfirmation(form, itemId);
                var currentVersion = PositiveInt(form, "artifactVersion");
                if (PositiveInt(form, "confirmArtifactVersion") != currentVersion)
                    throw new SecurityException();
                var changeReason = Required(form, "changeReason");
                if (changeReason is not ("VENDOR_RELEASE" or "SECURITY_UPDATE" or
                        "CONTENT_CORRECTION" or "PLATFORM_UPDATE"))
                    throw new SecurityException();
                RequireStepUp(admin, ContentPassword(form, "adminPassword"));
                var firstStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                RequireStepUp(admin, ContentPassword(form, "versionAdminPassword"));
                candidateUrl = CandidateUrl(form);
                proof = proof with
                {
                    StepUpAt = firstStep,
                    VersionStepUpAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    VersionConfirmation = itemId + ":" + currentVersion.ToString(
                        CultureInfo.InvariantCulture)
                };
                _ = await bff.PublishContentVersionAsync(proof, itemId, candidateUrl,
                    RequestId(), changeReason, currentVersion, ct);
                candidateUrl = "";
                return ContentRedirect(ok: "NOVA_VERSAO_ENFILEIRADA", manage: itemId);
            }
            catch (Exception ex) when (ContentPostFailure(ex))
            {
                candidateUrl = "";
                return ContentRedirect(error: "OPERACAO_RECUSADA");
            }
            finally { candidateUrl = ""; }
        }).RequireAuthorization();
    }

    private static bool Has(HttpContext context, string permission) =>
        context.User.HasClaim("permission", permission);

    private static bool ValidLicenseId(string value) => value.Length is >= 8 and <= 64 &&
        value.StartsWith("TS-", StringComparison.Ordinal) &&
        value.Skip(3).All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static SuiteContentAdminProof ContentProof(HttpContext context, string claim) =>
        new(context.User.Identity?.Name ?? "unknown", ClientIpDigest(context), claim);

    private static string ClientIpDigest(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        var effective = remote?.ToString() ?? "unknown";
        if (remote is not null && IPAddress.IsLoopback(remote) &&
            IPAddress.TryParse(context.Request.Headers["CF-Connecting-IP"].ToString(), out var cloudflareIp))
            effective = cloudflareIp.ToString();
        var bytes = Encoding.UTF8.GetBytes("TurboRamaSuiteAdminClientIp/v1\0" + effective);
        try { return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static string RequestId() => Convert.ToHexString(
        RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static async Task AuditDenial(SuiteAdminBff bff, string id, string device,
        string actor, string code, CancellationToken ct)
    {
        try { _ = await bff.DenyAsync(id, device, actor, RequestId(), code, ct); }
        catch (HttpRequestException) { }
    }

    private static string ExpectedLicense() =>
        (Environment.GetEnvironmentVariable("TURBORAMA_SUITE_LICENSE_ID") ?? "").Trim();

    private static string Required(IFormCollection form, string name)
    {
        var value = form[name].ToString();
        if (value.Length is < 1 or > 1024 || value.Any(char.IsControl)) throw new SecurityException();
        return value;
    }

    private static string ContentPassword(IFormCollection form, string name)
    {
        var value = form[name].ToString();
        if (value.Length is < 1 or > 256 || value.Any(character => character == '\0'))
            throw new SecurityException();
        return value;
    }

    private static string CandidateUrl(IFormCollection form)
    {
        var value = form["candidateUrl"].ToString();
        if (value.Length is < 1 or > 4096 || Encoding.UTF8.GetByteCount(value) > 4096 ||
            value.Any(character => character is '\0' or '\r' or '\n'))
            throw new SecurityException();
        return value;
    }

    private static string ContentItemId(IFormCollection form)
    {
        var value = form["itemId"].ToString();
        if (!IsHex(value, 32)) throw new SecurityException();
        return value;
    }

    private static void RequireItemConfirmation(IFormCollection form, string itemId)
    {
        if (!Fixed(itemId, form["confirmItemId"].ToString())) throw new SecurityException();
    }

    private static int PositiveInt(IFormCollection form, string name) =>
        int.TryParse(form[name].ToString(), NumberStyles.None, CultureInfo.InvariantCulture,
            out var value) && value > 0 ? value : throw new SecurityException();

    private static void RequireStepUp(OnlineAdminConfiguration admin, string password)
    {
        if (!AdminPasswordHash.Verify(admin.PasswordHash, password)) throw new SecurityException();
    }

    private static bool Fixed(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a);
        var y = Encoding.UTF8.GetBytes(b);
        try { return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y); }
        finally { CryptographicOperations.ZeroMemory(x); CryptographicOperations.ZeroMemory(y); }
    }

    private static bool IsHex(string? value, int length) => value?.Length == length &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool ContentPostFailure(Exception exception) => exception is SecurityException or
        AntiforgeryValidationException or HttpRequestException or InvalidOperationException or
        BadHttpRequestException;

    private static IResult ContentRedirect(string? ok = null, string? error = null,
        string? manage = null)
        => Results.Redirect(ContentRedirectUri(ok, error, manage));

    private static string ContentRedirectUri(string? ok = null, string? error = null,
        string? manage = null)
    {
        var query = new Dictionary<string, string?>();
        if (ok is "VERIFICACAO_ENFILEIRADA" or "SUBSTITUICAO_ENFILEIRADA" or
            "NOVA_VERSAO_ENFILEIRADA") query["ok"] = ok;
        if (error is "CONSULTA_INVALIDA" or "LIMITE_ATINGIDO" or "OPERACAO_RECUSADA")
            query["error"] = error;
        if (IsHex(manage, 32)) query["manage"] = manage;
        return "/admin/suite/content" + QueryString.Create(query).ToUriComponent();
    }

    private static IResult Html(string html, int code = 200) =>
        Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, code);
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string Shell(string title, string body) =>
        "<!doctype html><html lang=pt-BR><head><meta charset=utf-8>" +
        "<meta name=viewport content='width=device-width,initial-scale=1'><title>" + E(title) +
        "</title><link rel=stylesheet href=/admin/assets/admin.css>" +
        "<script defer src=/admin/assets/admin.js?v=r5-915></script></head><body><main class=shell>" +
        "<header class=top><div><span class=eyebrow>LZ GAMES / TURBORAMA</span>" +
        "<h1>Central de licenças</h1></div><nav class=top-actions>" +
        "<a class='button ghost' href=/admin>PIX</a>" +
        "<a class='button ghost' href=/admin/suite>SUITE</a>" +
        "<a class='button ghost' href=/admin/suite/content/health>Saúde dos links</a>" +
        "<a class='button primary' href=/admin/suite/content>Conteúdo</a></nav></header>" +
        body + "</main></body></html>";

    private static string Page(SuiteAdminStatus status, string token, string error,
        IReadOnlyList<SuiteCustomerLicense> customers, string sessions)
    {
        var otp = status.OtpState switch
        {
            "VALID" => "VÁLIDO ATÉ " + Utc(status.OtpExpiresAt),
            "EXPIRED" => "EXPIRADO",
            "CONSUMED" => "CONSUMIDO",
            _ => "NÃO EMITIDO"
        };
        var html = new StringBuilder("<section class=panel><form method=get action=/admin/suite class=grid-form><label>Localizar outra licença<input name=licenseId required autocomplete=off placeholder='TS-...'></label><button class=ghost>Carregar licença</button></form></section><section class=scope-banner><div><span class=eyebrow>")
            .Append(E(status.ProductId)).Append("</span><h2>Emissor de ativação</h2>")
            .Append("<p>Canal isolado por socket Unix. O código aparece uma única vez.</p></div>")
            .Append("<span class='pill on'>").Append(E(status.Status)).Append("</span></section>");
        if (error.Length > 0)
            html.Append("<div class='notice danger'>Operação recusada: ").Append(E(error)).Append("</div>");
        html.Append("<section class=panel><div class=license-facts><div><span>Produto</span><strong>")
            .Append(E(status.ProductId)).Append("</strong></div><div><span>Licença</span><strong>")
            .Append(E(status.LicenseId)).Append("</strong></div><div><span>Termo</span><strong>")
            .Append(E(status.LicenseTerm)).Append(" · licença vitalícia</strong></div>")
            .Append("<div><span>OTP</span><strong>").Append(E(otp)).Append("</strong></div>")
            .Append("<div><span>Política</span><strong>").Append(E(status.IdentityPolicy))
            .Append("</strong></div><div><span>Binding</span><strong>").Append(E(status.BindingType))
            .Append("</strong></div><div><span>Algoritmo</span><strong>").Append(E(status.Algorithm))
            .Append("</strong></div><div><span>DeviceId esperado</span><strong>").Append(E(status.DeviceId))
            .Append("</strong></div><div><span>Dispositivos ativos</span><strong>")
            .Append(status.ActiveDevices).Append("</strong></div><div><span>Última sessão Suite registrada</span><strong>")
            .Append(E(status.SessionId is null ? "Nenhuma" : Masked(status.SessionId))).Append("</strong></div></div>");
        var commercial=customers.FirstOrDefault(item=>string.Equals(item.LicenseId,status.LicenseId,StringComparison.OrdinalIgnoreCase));
        var canFirstClaim=commercial is not null&&commercial.FinancialState=="PAID"&&commercial.DeliveryState=="PROVISIONED"&&
            commercial.EnrollmentState=="PENDING_ENROLLMENT"&&!commercial.ActivationConsumed&&commercial.ActiveDevices==0&&commercial.OtpState!="VALID";
        if (status.CanIssue)
            html.Append("<form method=post action=/admin/suite/actions/issue-otp class=grid-form>")
                .Append(Csrf(token)).Append("<input type=hidden name=licenseId value='")
                .Append(E(status.LicenseId)).Append("'><input type=hidden name=deviceId value='")
                .Append(E(status.DeviceId)).Append("'><label>Confirme a licença")
                .Append("<input name=confirmLicenseId required autocomplete=off></label>")
                .Append("<label>Confirme a máquina<input name=confirmDeviceId required autocomplete=off></label>")
                .Append("<label>Confirme sua senha<input type=password name=adminPassword required autocomplete=new-password></label>")
                .Append("<button class=primary>Emitir OTP de 15 minutos</button></form>");
        else if(canFirstClaim)
            html.Append("<form method=post action=/admin/suite/actions/issue-first-claim class=grid-form>")
                .Append(Csrf(token)).Append("<input type=hidden name=licenseId value='").Append(E(status.LicenseId))
                .Append("'><label>Confirme a licença<input name=confirmLicenseId required autocomplete=off></label>")
                .Append("<label>Confirme sua senha<input type=password name=adminPassword required autocomplete=new-password></label>")
                .Append("<button class=primary>Gerar OTP para primeira ativação</button></form>")
                .Append("<p class=muted>A licença será vinculada somente ao computador que usar este OTP no programa.</p>");
        else
            html.Append("<div class='notice danger'>Emissão indisponível no estado atual. ")
                .Append("A validação também é repetida atomicamente no servidor.</div>");
        html.Append("<p class=muted>A emissão é recusada se houver máquina ativa, OTP válido ou divergência de identidade.</p>")
            .Append("<a class='button ghost' href=/admin/suite/export/audit.csv>Exportar auditoria SUITE</a></section>")
            .Append("<section class=panel><span class=eyebrow>AUDITORIA SUITE</span><h2>Eventos recentes</h2>")
            .Append("<div class=table-wrap><table class=audit-table><thead><tr><th>Quando (UTC)</th><th>Evento</th>")
            .Append("<th>Resultado</th><th>Detalhe</th><th>Ator</th><th>Expiração OTP</th></tr></thead><tbody>");
        foreach (var item in status.RecentEvents)
            html.Append("<tr><td>").Append(E(item.OccurredAt)).Append("</td><td>")
                .Append(E(Friendly(item.EventType))).Append("</td><td>").Append(E(Friendly(item.Outcome)))
                .Append("</td><td>").Append(E(Friendly(item.DetailCode))).Append("</td><td>")
                .Append(E(item.Actor)).Append("</td><td>").Append(E(item.OtpExpiresAt ?? ""))
                .Append("</td></tr>");
        if (status.RecentEvents.Count == 0)
            html.Append("<tr><td colspan=6>Nenhum evento de OTP.</td></tr>");
        html.Append("</tbody></table></div></section>").Append(sessions).Append(CustomerLicenseList(customers));
        return Shell("TurboRama SUITE", html.ToString());
    }

    private static string LicenseSearch(IReadOnlyList<SuiteCustomerLicense> customers, string sessions) => Shell("TurboRama SUITE",
        "<section class=scope-banner><div><span class=eyebrow>TURBORAMA_SUITE</span>" +
        "<h2>Emissor de ativação</h2><p>Localize a licença que deseja administrar.</p></div>" +
        "<span class='pill on'>ONLINE</span></section><section class=panel>" +
        "<form method=get action=/admin/suite class=grid-form><label>Licença do cliente" +
        "<input name=licenseId required autocomplete=off placeholder='TS-...'></label>" +
        "<button class=primary>Carregar licença</button></form>" +
        "<p class=muted>O estado de emissão será verificado somente após selecionar uma licença.</p>" +
        "</section>" + sessions + CustomerLicenseList(customers));

    private static IReadOnlyList<SuiteCustomerLicense> LoadCustomerLicenses()
    {
        var path = Environment.GetEnvironmentVariable("TURBORAMA_SUITE_CUSTOMERS_FILE") ?? "/var/lib/turborama-pix/suite-customers.json";
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<List<SuiteCustomerLicense>>(stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 8 }) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        { return []; }
    }

    private static string CustomerHistoryPage(SuiteCustomerLicense customer,SuiteCustomerActivity activity,string token,string ok,string sessions)
    {
        var html=new StringBuilder("<section class=scope-banner><div><span class=eyebrow>HISTÓRICO DO CLIENTE</span><h2>")
            .Append(E(customer.Customer)).Append("</h2><p>").Append(E(customer.Email)).Append(" · ").Append(E(customer.LicenseId))
            .Append("</p></div><span class='presence ").Append(activity.Online?"is-online":"is-offline").Append("'><i></i>")
            .Append(activity.Online?"SUITE ONLINE":"SUITE SEM CONTATO").Append("</span></section><section class='summary customer-summary'><article><small>JOGOS BAIXADOS</small><strong>")
            .Append(activity.UniqueDownloads).Append("</strong></article><article><small>TENTATIVAS</small><strong>").Append(activity.DownloadAttempts)
            .Append("</strong></article><article><small>PROGRAMA</small><strong>").Append(E(activity.AgentVersion.Length==0?"—":activity.AgentVersion))
            .Append("</strong></article><article><small>MÁQUINA</small><strong>").Append(E(activity.DeviceStatus))
            .Append("</strong></article></section>");
        html.Append(sessions);
        if(ok=="HISTORICO_LIMPO")html.Append("<div class='notice success'>Registros de jogos removidos com segurança.</div>");
        html.Append("<section class=panel><div class=section-title><div><span class=eyebrow>IDENTIDADE DA PLACA-MÃE</span><h2>Computador vinculado</h2></div></div>");
        if(activity.Motherboard is null)html.Append("<div class=empty-state>Não informado por esta versão do programa.</div>");
        else
        {
            var m=activity.Motherboard;html.Append("<div class='detail-grid motherboard-detail-grid'>")
              .Append(Detail("Placa",m.BaseboardManufacturer+" "+m.BaseboardProduct+" "+m.BaseboardVersion))
              .Append(Detail("Serial",m.BaseboardSerialMasked)).Append(Detail("Sistema",m.SystemManufacturer+" "+m.SystemModel))
              .Append(Detail("UUID",m.SystemUuidMasked)).Append(Detail("BIOS",m.BiosManufacturer+" "+m.BiosVersion))
              .Append(Detail("Windows",m.OsName+" "+m.OsVersion+" · "+m.Architecture)).Append(Detail("Programa",m.ClientVersion))
              .Append(Detail("Coletado",When(m.CollectedAtUnixSeconds))).Append(Detail("Recebido",When(m.ReceivedAtUnixSeconds)))
              .Append(Detail("Comparação",Friendly(m.ComparisonStatus)+" · "+Friendly(m.ComparisonConfidence))).Append("</div>");
            if(m.PendingReview)html.Append("<div class='notice warning'>Possível troca de placa-mãe aguardando revisão. A licença não foi alterada automaticamente.</div>");
        }
        html.Append("</section>");
        html.Append("<section class=panel><div class=section-title><div><span class=eyebrow>LINHA DO TEMPO</span><h2>Atividade registrada</h2></div><a class='button ghost' href=/admin#suite-clients>Voltar aos clientes</a></div><div class=timeline>");
        foreach(var item in activity.Events)html.Append("<article><time>").Append(When(item.AtUnixSeconds)).Append("</time><span class=event-kind>")
            .Append(E(Friendly(item.Kind))).Append("</span><div><strong>").Append(E(Friendly(item.Title))).Append("</strong><small>").Append(E(FriendlyDetail(item.Detail))).Append("</small></div></article>");
        if(activity.Events.Count==0)html.Append("<div class=empty-state>Nenhuma atividade registrada.</div>");
        html.Append("</div><details class='audit-cleanup game-cleanup'><summary>Limpar registros de jogos</summary><p class=cleanup-note>Remove somente downloads finalizados deste cliente. Licença, computador, sessões, compra e segurança permanecem intactos.</p>")
            .Append("<form method=post action=/admin/clientes/actions/clear-games class=cleanup-form>")
            .Append("<input type=hidden name=__RequestVerificationToken value='").Append(E(token)).Append("'>")
            .Append("<input type=hidden name=licenseId value='").Append(E(customer.LicenseId)).Append("'>")
            .Append("<label>Confirme sua senha<input type=password name=adminPassword required autocomplete=current-password></label>")
            .Append("<button class=danger data-confirm='Confirma a exclusão permanente dos registros de jogos deste cliente?' data-busy='Limpando...'>Limpar registros de jogos</button></form></details></section>");
        html.Append("<style>.motherboard-detail-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;margin-top:14px}.motherboard-detail-grid>div{min-width:0;padding:12px 14px;border:1px solid #213943;border-radius:10px;background:#0b151b}.motherboard-detail-grid span{display:block;margin-bottom:5px;color:#94aab2;font-size:.68rem;font-weight:700;letter-spacing:.08em;text-transform:uppercase}.motherboard-detail-grid strong{display:block;color:#f3f8f9;font-size:.9rem;line-height:1.35;overflow-wrap:anywhere}@media(max-width:620px){.motherboard-detail-grid{grid-template-columns:1fr}}</style>");
        return Shell("Histórico · "+customer.Customer,html.ToString());
    }

    private static string CustomerLicenseList(IReadOnlyList<SuiteCustomerLicense> items)
    {
        var html = new StringBuilder("<section class='panel suite-customers'><span class=eyebrow>COMPRAS SUITE</span><h2>Clientes e liberações</h2><p class=muted>Compras pagas e licenças provisionadas. Clique para ver os dados internos.</p><div class='table-wrap suite-customer-list'><table class='audit-table suite-customer-table'><thead><tr><th>ID do cliente</th><th>Cliente</th><th>Licença gerada</th><th>Estado</th><th>Pedido</th><th>Detalhes</th></tr></thead><tbody>");
        var modals = new StringBuilder();
        foreach (var item in items)
        {
            var modal = "suite-purchase-" + item.PurchaseId.ToString(CultureInfo.InvariantCulture);
            var hasLicense = item.LicenseId.StartsWith("TS-", StringComparison.OrdinalIgnoreCase);
            var state = item.FinancialState == "SUSPENDED" || item.DeliveryState == "SUSPENDED" ? "BLOQUEADA" :
                item.DeliveryState == "DEAD_LETTER" || item.OutboxStatus == "DEAD_LETTER" ? "ERRO DE PROVISIONAMENTO" :
                !hasLicense || item.DeliveryState == "PENDING" ? "AGUARDANDO LIBERAÇÃO" :
                item.ActiveDevices > 0 ? "ATIVADA" : item.OtpState == "VALID" ? "OTP VÁLIDO" : "PRONTA PARA ATIVAR";
            var licenseLabel = hasLicense ? item.LicenseId : "Aguardando geração";
            html.Append("<tr><td><strong>#").Append(item.CustomerId).Append("</strong></td><td><strong>").Append(E(item.Customer)).Append("</strong><small>").Append(E(item.Email)).Append("</small></td><td><code>").Append(E(licenseLabel)).Append("</code></td><td><span class='pill on'>").Append(E(state)).Append("</span></td><td>").Append(E(item.OrderRef)).Append("</td><td><button type=button class=ghost data-open-modal='").Append(modal).Append("'>Ver dados</button></td></tr>");
            modals.Append("<dialog class='app-modal suite-detail-modal' id='").Append(modal).Append("'><div class=modal-card><div class=modal-head><div><span class=eyebrow>LICENÇA SUITE</span><h2>").Append(E(item.Customer)).Append("</h2></div><button type=button class=modal-close data-close-modal aria-label=Fechar>×</button></div><div class=detail-grid>").Append(Detail("ID do cliente","#"+item.CustomerId.ToString(CultureInfo.InvariantCulture)))
                .Append(Detail("Licença",licenseLabel)).Append(Detail("Pedido",item.OrderRef)).Append(Detail("E-mail",item.Email)).Append(Detail("WhatsApp",item.Phone)).Append(Detail("Compra",item.PurchaseId.ToString(CultureInfo.InvariantCulture))).Append(Detail("Entrega",item.DeliveryState)).Append(Detail("Financeiro",item.FinancialState)).Append(Detail("Fila",item.OutboxStatus)).Append(Detail("Erro da fila",string.IsNullOrWhiteSpace(item.OutboxError)?"Nenhum":item.OutboxError)).Append(Detail("Matrícula",item.EnrollmentState)).Append(Detail("OTP",item.OtpState)).Append(Detail("Máquina vinculada",string.IsNullOrWhiteSpace(item.DeviceId)?"Nenhuma máquina vinculada":item.DeviceId)).Append(Detail("Estado da máquina",item.DeviceStatus)).Append(Detail("Fingerprint",string.IsNullOrWhiteSpace(item.HardwareFingerprint)?"Não disponível":item.HardwareFingerprint)).Append(Detail("Vínculo",string.IsNullOrWhiteSpace(item.BindingType)?"Não disponível":item.BindingType)).Append(Detail("Algoritmo",string.IsNullOrWhiteSpace(item.DeviceAlgorithm)?"Não disponível":item.DeviceAlgorithm)).Append(Detail("Máquina cadastrada em",string.IsNullOrWhiteSpace(item.DeviceCreatedAt)?"Ainda não cadastrada":item.DeviceCreatedAt)).Append(Detail("Última atividade da máquina",string.IsNullOrWhiteSpace(item.DeviceUpdatedAt)?"Sem atividade":item.DeviceUpdatedAt)).Append(Detail("Dispositivos ativos",item.ActiveDevices.ToString(CultureInfo.InvariantCulture))).Append(Detail("Sessão",item.SessionId??"Nenhuma")).Append(Detail("Catálogo",item.Entitlement)).Append(Detail("Atualização",item.UpdatedAt)).Append("</div><div class=activation-actions>");
            if (hasLicense) modals.Append("<a class='button primary' href='/admin/suite?licenseId=").Append(Uri.EscapeDataString(item.LicenseId)).Append("'>Abrir licença</a>");
            modals.Append("<button type=button class=ghost data-close-modal>Fechar</button></div></div></dialog>");
        }
        if (items.Count == 0) html.Append("<tr><td colspan=6>Nenhuma compra SUITE provisionada.</td></tr>");
        return html.Append("</tbody></table></div></section>").Append(modals).ToString();
    }

    private static string Detail(string label,string value) => "<div><span>"+E(label)+"</span><strong>"+E(value)+"</strong></div>";

    private sealed record SuiteCustomerLicense(string LicenseId,int CustomerId,string Customer,string Email,string Phone,string OrderRef,int PurchaseId,string DeliveryState,string FinancialState,string CreatedAt,string UpdatedAt,string Status,string EnrollmentState,bool ActivationConsumed,string OtpState,long ActiveDevices,string? SessionId,string Entitlement,string OutboxStatus,string OutboxError,string DeviceId,string HardwareFingerprint,string BindingType,string DeviceAlgorithm,string DeviceStatus,string DeviceCreatedAt,string DeviceUpdatedAt);

    private static string ContentPage(SuiteContentItemPage page, SuiteContentAuditPage audit,
        ContentPageQuery query, string token, string ok, string error, bool canReplace,
        bool canVersion, bool canCheck)
    {
        var online = page.Items.Count(item => item.Availability == "ONLINE");
        var maintenance = page.Items.Count - online;
        var jobs = page.Items.Count(item => item.JobState is "STAGED" or "VALIDATING" or "VERIFIED");
        var html = new StringBuilder("<section class=scope-banner><div><span class=eyebrow>CATÁLOGO PROTEGIDO</span>")
            .Append("<h2>Conteúdo da SUITE</h2><p>Links permanecem cifrados no servidor. ")
            .Append("A publicação direta só ocorre após política de origem e metadados seguros aprovados.</p></div>")
            .Append("<span class='pill on'>PRODUÇÃO</span></section>");
        if (AllowedOk(ok) is { } okText)
            html.Append("<div class='notice success'>").Append(E(okText)).Append("</div>");
        if (AllowedError(error) is { } errorText)
            html.Append("<div class='notice danger'>").Append(E(errorText)).Append("</div>");
        html.Append("<section class=summary><article><span class=metric-icon>PG</span><small>ITENS NESTA PÁGINA</small><strong>")
            .Append(page.Items.Count).Append("</strong></article><article><span class=metric-icon>ON</span>")
            .Append("<small>ONLINE</small><strong>").Append(online)
            .Append("</strong></article><article><span class=metric-icon>MT</span><small>EM MANUTENÇÃO</small><strong>")
            .Append(maintenance).Append("</strong></article><article><span class=metric-icon>JB</span>")
            .Append("<small>VALIDAÇÕES ATIVAS</small><strong>").Append(jobs).Append("</strong></article></section>")
            .Append("<section class=panel><form method=get action=/admin/suite/content class=toolbar autocomplete=off>")
            .Append("<label>Nome do jogo<input name=name maxlength=100 value='").Append(E(query.Name))
            .Append("' placeholder='Buscar pelo título'></label><label>Status<select name=availability>")
            .Append(Option("", "Todos", query.Availability)).Append(Option("ONLINE", "Online", query.Availability))
            .Append(Option("EM_MANUTENCAO", "Em manutenção", query.Availability))
            .Append("</select></label><button class=primary>Filtrar</button></form>")
            .Append("<details class=advanced><summary>Filtros técnicos</summary><form method=get action=/admin/suite/content class=grid-form>")
            .Append("<label>Prefixo do itemId<input name=item maxlength=32 value='").Append(E(query.ItemPrefix))
            .Append("'></label><label>Código do último resultado<input name=resultCode maxlength=64 value='")
            .Append(E(query.ResultCode)).Append("'></label><label>Estado do job<input name=jobState maxlength=16 value='")
            .Append(E(query.JobState)).Append("'></label><button>Aplicar filtros técnicos</button></form></details></section>")
            .Append("<section class=panel><div class=section-title><div><span class=eyebrow>ITENS</span>")
            .Append("<h2>Disponibilidade e validação</h2></div><a class='button ghost' href=/admin/suite/content>Início</a></div>")
            .Append("<div class=table-wrap><table class=audit-table><thead><tr><th>Jogo</th><th>ItemId</th>")
            .Append("<th>Estado</th><th>Versão</th><th>Último check</th><th>Resultado</th><th>Job</th><th>Ação</th>")
            .Append("</tr></thead><tbody>");
        foreach (var item in page.Items)
        {
            html.Append("<tr><td><strong>").Append(E(item.DisplayName)).Append("</strong></td><td><code>")
                .Append(E(item.ItemId)).Append("</code></td><td><span class='status ")
                .Append(item.Availability == "ONLINE" ? "online" : "state-maintenance")
                .Append("'>").Append(E(item.Availability)).Append("</span></td><td>")
                .Append(item.ArtifactVersion?.ToString(CultureInfo.InvariantCulture) ?? "—")
                .Append("</td><td>").Append(E(item.LastCheckedAt ?? "—")).Append("</td><td>")
                .Append(E(item.LastResultCode)).Append("</td><td>");
            if (item.CandidateId is not null)
                html.Append("<a href=/admin/suite/content/jobs/").Append(E(item.CandidateId)).Append(">")
                    .Append(E(item.JobState ?? "—")).Append("</a>");
            else html.Append("—");
            html.Append("</td><td><a class='button ghost' href='").Append(E(ContentListUrl(query,
                manage: item.ItemId))).Append("'>Gerenciar</a></td></tr>");
            if (query.ManageItem == item.ItemId)
                html.Append("<tr><td colspan=8>").Append(ContentActions(item, token,
                    canReplace, canVersion, canCheck)).Append("</td></tr>");
        }
        if (page.Items.Count == 0)
            html.Append("<tr><td colspan=8>Nenhum item corresponde aos filtros.</td></tr>");
        html.Append("</tbody></table></div><div class=top-actions><a class='button ghost' href=/admin/suite/content>Primeira página</a>");
        if (page.NextCursor is not null)
            html.Append("<a class='button primary' href='").Append(E(ContentListUrl(query,
                cursor: page.NextCursor, clearManage: true))).Append("'>Próxima página</a>");
        html.Append("</div></section><section class=panel><div class=section-title><div><span class=eyebrow>AUDITORIA DE CONTEÚDO</span>")
            .Append("<h2>Eventos sanitizados</h2></div></div><div class=table-wrap><table class=audit-table>")
            .Append("<thead><tr><th>Quando</th><th>Evento</th><th>Resultado</th><th>Detalhe</th><th>Jogo/item</th><th>Ator</th></tr></thead><tbody>");
        foreach (var entry in audit.Events)
            html.Append("<tr><td>").Append(E(entry.OccurredAt)).Append("</td><td>")
                .Append(E(Friendly(entry.EventType))).Append("</td><td>").Append(E(Friendly(entry.Outcome)))
                .Append("</td><td>").Append(E(Friendly(entry.DetailCode))).Append("</td><td><code>")
                .Append(E(entry.ItemId ?? "—")).Append("</code></td><td>").Append(E(entry.Actor))
                .Append("</td></tr>");
        if (audit.Events.Count == 0)
            html.Append("<tr><td colspan=6>Nenhum evento para este filtro.</td></tr>");
        html.Append("</tbody></table></div>");
        if (audit.NextCursor is not null)
            html.Append("<a class='button ghost' href='").Append(E(ContentListUrl(query,
                auditCursor: audit.NextCursor))).Append("'>Auditoria anterior</a>");
        html.Append("</section>");
        return Shell("Conteúdo da SUITE", html.ToString());
    }

    private static string ContentHealthPage(SuiteContentItemPage page,
        ContentPageQuery query, string token, string ok, string error, bool canCheck,
        string testingItem, string testingStarted)
    {
        var categories = VisualCategories.Value;
        var categorizedItems = page.Items.Select(item => (Item: item,
            Category: categories.TryGetValue(item.ItemId, out var category)
                ? category : VisualCategory.Other)).ToArray();
        var visibleItems = categorizedItems
            .Where(entry => query.Platform is null || entry.Category.Id == query.Platform)
            .OrderBy(entry => entry.Category.Order).ThenBy(entry => entry.Item.DisplayName,
                StringComparer.CurrentCultureIgnoreCase).ToArray();
        var validTestingItem = IsHex(testingItem, 32) ? testingItem : "";
        var validTestingStarted = long.TryParse(testingStarted, NumberStyles.None,
            CultureInfo.InvariantCulture, out var startedUnix) &&
            startedUnix <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() &&
            startedUnix >= DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds()
            ? DateTimeOffset.FromUnixTimeSeconds(startedUnix) : (DateTimeOffset?)null;
        var online = visibleItems.Count(entry => entry.Item.Availability == "ONLINE");
        var offline = visibleItems.Length - online;
        var checkedItems = visibleItems.Count(entry => entry.Item.LastCheckedAt is not null);
        var rangeOk = visibleItems.Count(entry => entry.Item.LastResultCode == "CHECK_OK");
        var html = new StringBuilder("<div class=health-dashboard><section class='scope-banner health-hero'><div><span class=eyebrow>MONITOR DE ORIGENS</span>")
            .Append("<h2>Saúde dos links</h2><p>Diagnóstico separado do gerenciamento. ")
            .Append("Nenhuma URL é exibida e nenhum jogo completo é transferido pelo servidor.</p></div>")
            .Append("<span class='pill on'>MONITOR ATIVO</span></section>")
            .Append("<div class=health-loading-overlay hidden data-health-loading role=status aria-live=polite><div><i></i><strong>Verificando link</strong><span>Lendo uma amostra mínima de dados…</span></div></div>");
        if (error.Length > 0)
            html.Append("<div class='notice danger'>Consulta inválida ou serviço indisponível.</div>");
        if (AllowedOk(ok) is { } success)
            html.Append("<div class='notice success'>").Append(E(success))
                .Append(" Atualize os dados em alguns instantes para ver o resultado.</div>");
        html.Append("<section class='summary health-summary'><article><span class=metric-icon>PG</span><small>ITENS EXIBIDOS</small><strong>")
            .Append(visibleItems.Length).Append("</strong><span>Seleção atual</span></article><article class=health-online><span class=metric-icon>ON</span><small>LINKS ONLINE</small><strong>")
            .Append(online).Append("</strong><span>Disponíveis agora</span></article><article class=health-offline><span class=metric-icon>OF</span><small>PRECISAM DE ATENÇÃO</small><strong>")
            .Append(offline).Append("</strong><span>Offline ou manutenção</span></article><article class=health-range><span class=metric-icon>RG</span><small>RANGE VALIDADO</small><strong>")
            .Append(rangeOk).Append("/").Append(checkedItems).Append("</strong></article></section>")
            .Append("<section class='panel health-controls'><div class=section-title><div><span class=eyebrow>LOCALIZAR</span><h2>Filtros de diagnóstico</h2></div><a class='button ghost' href=/admin/suite/content/health>Limpar filtros</a></div>")
            .Append("<form method=get action=/admin/suite/content/health class=health-filter autocomplete=off>")
            .Append("<label>Jogo<input name=name maxlength=100 value='").Append(E(query.Name))
            .Append("' placeholder='Buscar pelo título'></label><label>Plataforma<select name=platform>")
            .Append(Option("", "Todas as plataformas", query.Platform));
        foreach (var category in categorizedItems.Select(entry => entry.Category).DistinctBy(value => value.Id)
                     .OrderBy(value => value.Order))
            html.Append(Option(category.Id, category.Name, query.Platform));
        html.Append("</select></label><label>Estado<select name=availability>")
            .Append(Option("", "Todos", query.Availability)).Append(Option("ONLINE", "Online", query.Availability))
            .Append(Option("EM_MANUTENCAO", "Offline / manutenção", query.Availability))
            .Append("</select></label><label>Resultado<input name=resultCode maxlength=64 value='")
            .Append(E(query.ResultCode)).Append("' placeholder='Ex.: CHECK_OK'></label><button class=primary>Aplicar filtros</button>")
            .Append("<a class='button ghost' href=/admin/suite/content/health>Atualizar dados</a></form></section>")
            .Append("<section class='panel health-results'><div class=section-title><div><span class=eyebrow>VERIFICAÇÃO SEGURA</span>")
            .Append("<h2>Disponibilidade das origens</h2><p class=muted>O monitor testa conexão e retomada sem revelar o endereço permanente.</p></div><a class='button ghost' href=/admin/suite/content>Gerenciar conteúdo</a></div>")
            .Append("<div class=health-legend><span><i class=legend-online></i>Online</span><span><i class=legend-warning></i>Offline ou manutenção</span><span><i class=legend-neutral></i>Ainda não verificado</span></div>")
            .Append("<div class=table-wrap><table class='audit-table health-table'><thead><tr><th>Jogo</th><th>Estado</th>")
            .Append("<th>Última verificação</th><th>Resultado</th><th>Retomada Range</th><th>Versão</th><th>Ação</th>")
            .Append("</tr></thead><tbody>");
        string? currentCategory = null;
        var currentPlatformOpen = false;
        foreach (var entry in visibleItems)
        {
            var item = entry.Item;
            if (currentCategory != entry.Category.Id)
            {
                currentCategory = entry.Category.Id;
                currentPlatformOpen = query.Platform is not null || query.Name is not null ||
                    visibleItems.Any(value => value.Category.Id == currentCategory &&
                        value.Item.ItemId == validTestingItem);
                var platformItems = visibleItems.Where(value => value.Category.Id == currentCategory)
                    .ToArray();
                html.Append("<tr class=platform-row><th colspan=7><button type=button class=platform-toggle data-toggle-platform='")
                    .Append(E(currentCategory)).Append("' aria-expanded='")
                    .Append(currentPlatformOpen ? "true" : "false").Append("'><span><b>")
                    .Append(E(entry.Category.Name)).Append("</b><small>")
                    .Append(platformItems.Length).Append(" jogos · ")
                    .Append(platformItems.Count(value => value.Item.Availability == "ONLINE"))
                    .Append(" online</small></span><i aria-hidden=true>⌄</i></button></th></tr>");
            }
            var isOnline = item.Availability == "ONLINE";
            var checkedAfterRequest = validTestingStarted is not null &&
                DateTimeOffset.TryParse(item.LastCheckedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var checkedAt) &&
                checkedAt >= validTestingStarted.Value;
            var isTesting = item.ItemId == validTestingItem &&
                validTestingStarted is not null && !checkedAfterRequest;
            var range = item.LastCheckedAt is null ? "Ainda não verificado" :
                item.LastResultCode == "CHECK_OK" ? "Compatível" : "Não confirmado";
            html.Append("<tr data-platform-item='").Append(E(entry.Category.Id)).Append("'")
                .Append(currentPlatformOpen ? "" : " hidden").Append(isTesting ? " data-health-pending" : "")
                .Append("><td><div class=health-game><strong>").Append(E(item.DisplayName))
                .Append("</strong></div></td><td><span class='status ")
                .Append(isOnline ? "online" : "state-maintenance").Append("'>")
                .Append(isOnline ? "ONLINE" : "OFF / MANUTENÇÃO").Append("</span></td><td>")
                .Append(E(item.LastCheckedAt ?? "Nunca")).Append("</td><td><span class=health-result>")
                .Append(isTesting ? "TESTANDO" : item.LastResultCode == "CHECK_OK"
                    ? "OK — DADOS RECEBIDOS" : E(item.LastResultCode)).Append("</span></td><td><span class='range-state ")
                .Append(item.LastResultCode == "CHECK_OK" ? "range-ok" : "range-pending").Append("'>").Append(E(range)).Append("</span>")
                .Append("</td><td>").Append(item.ArtifactVersion?.ToString(CultureInfo.InvariantCulture) ?? "—")
                .Append("</td><td><div class=health-actions>");
            if (isTesting)
                html.Append("<span class=health-testing><i></i>Testando…</span>");
            else if (canCheck)
                html.Append("<details class=health-test><summary>Testar agora</summary>")
                    .Append("<form method=post action=/admin/suite/content/actions/check autocomplete=off data-health-probe-form>")
                    .Append(Csrf(token)).Append(ItemFields(item.ItemId))
                    .Append("<input type=hidden name=confirmItemId value='").Append(E(item.ItemId))
                    .Append("'><input type=hidden name=returnTo value=health>")
                    .Append("<label>Senha administrativa<input type=password name=adminPassword maxlength=256 required autocomplete=new-password></label>")
                    .Append("<small>Teste rápido: lê somente uma pequena amostra de dados.</small>")
                    .Append("<button class=primary data-busy='Testando...'>Executar teste</button></form></details>");
            html.Append("<a class='button ghost' href='/admin/suite/content?manage=")
                .Append(E(item.ItemId)).Append("'>Gerenciar</a></div></td></tr>");
        }
        if (visibleItems.Length == 0)
            html.Append("<tr><td colspan=7>Nenhum link corresponde aos filtros.</td></tr>");
        html.Append("</tbody></table></div><div class=top-actions><a class='button ghost' href=/admin/suite/content/health>Primeira página</a>");
        if (page.NextCursor is not null)
            html.Append("<a class='button primary' href='").Append(E(ContentHealthUrl(query,
                page.NextCursor))).Append("'>Próxima página</a>");
        html.Append("</div></section></div>");
        return Shell("Saúde dos links TurboRama", html.ToString());
    }

    private static string ContentActions(SuiteContentItem item, string token, bool canReplace,
        bool canVersion, bool canCheck)
    {
        var html = new StringBuilder("<div class=operation-grid>");
        if (canCheck)
            html.Append("<form method=post action=/admin/suite/content/actions/check class=operation-card autocomplete=off>")
                .Append("<span class=eyebrow>VERIFICAÇÃO</span><h3>Checar origem atual</h3>")
                .Append("<p>Agenda uma validação sem publicar nem alterar o link.</p>")
                .Append(Csrf(token)).Append(ItemFields(item.ItemId))
                .Append("<label>Digite o itemId<input name=confirmItemId required autocomplete=off></label>")
                .Append("<label>Senha administrativa<input type=password name=adminPassword maxlength=256 required autocomplete=new-password></label>")
                .Append("<button class=primary>Agendar verificação</button></form>");
        if (canReplace)
            html.Append("<form method=post action=/admin/suite/content/actions/replace class=operation-card autocomplete=off>")
                .Append("<span class=eyebrow>SUBSTITUIÇÃO</span><h3>Trocar espelho da mesma versão</h3>")
                .Append("<p>Valida a origem, extensão, política e versão sem armazenar tamanho ou SHA-256.</p>")
                .Append(Csrf(token)).Append(ItemFields(item.ItemId))
                .Append("<label>Nova URL candidata<input type=url name=candidateUrl maxlength=4096 required autocomplete=off></label>")
                .Append("<label>Digite o itemId<input name=confirmItemId required autocomplete=off></label>")
                .Append("<label>Senha administrativa<input type=password name=adminPassword maxlength=256 required autocomplete=new-password></label>")
                .Append("<button class=primary>Validar substituição</button></form>");
        if (canVersion && item.ArtifactVersion is not null)
            html.Append("<form method=post action=/admin/suite/content/actions/version class='operation-card danger-zone' autocomplete=off>")
                .Append("<span class=eyebrow>NOVA VERSÃO</span><h3>Publicar artefato diferente</h3>")
                .Append("<p>Operação reforçada: exige versão atual, itemId e duas confirmações de senha.</p>")
                .Append(Csrf(token)).Append(ItemFields(item.ItemId))
                .Append("<input type=hidden name=artifactVersion value=")
                .Append(item.ArtifactVersion.Value.ToString(CultureInfo.InvariantCulture)).Append(">")
                .Append("<label>Nova URL candidata<input type=url name=candidateUrl maxlength=4096 required autocomplete=off></label>")
                .Append("<label>Motivo<select name=changeReason required>")
                .Append("<option value=VENDOR_RELEASE>Nova versão do fornecedor</option>")
                .Append("<option value=SECURITY_UPDATE>Atualização de segurança</option>")
                .Append("<option value=CONTENT_CORRECTION>Correção de conteúdo</option>")
                .Append("<option value=PLATFORM_UPDATE>Atualização de plataforma</option></select></label>")
                .Append("<label>Digite o itemId<input name=confirmItemId required autocomplete=off></label>")
                .Append("<label>Digite a versão atual (").Append(item.ArtifactVersion.Value)
                .Append(")<input type=number min=1 name=confirmArtifactVersion required autocomplete=off></label>")
                .Append("<label>Primeira confirmação de senha<input type=password name=adminPassword maxlength=256 required autocomplete=new-password></label>")
                .Append("<label>Segunda confirmação de senha<input type=password name=versionAdminPassword maxlength=256 required autocomplete=new-password></label>")
                .Append("<button class=danger>Validar nova versão</button></form>");
        html.Append("</div>");
        return html.ToString();
    }

    private static string Csrf(string token) =>
        "<input type=hidden name=__RequestVerificationToken value='" + E(token) + "'>";
    private static string ItemFields(string itemId) =>
        "<input type=hidden name=itemId value='" + E(itemId) + "'>";
    private static string Option(string value, string label, string? selected) =>
        "<option value='" + E(value) + "'" + (value == selected ? " selected" : "") + ">" +
        E(label) + "</option>";

    private static string ContentListUrl(ContentPageQuery query, string? cursor = null,
        string? auditCursor = null, string? manage = null, bool clearManage = false)
    {
        var values = new Dictionary<string, string?>
        {
            ["cursor"] = cursor ?? query.Cursor,
            ["auditCursor"] = auditCursor ?? query.AuditCursor,
            ["availability"] = query.Availability,
            ["resultCode"] = query.ResultCode,
            ["platform"] = query.Platform,
            ["jobState"] = query.JobState,
            ["item"] = query.ItemPrefix,
            ["name"] = query.Name,
            ["manage"] = clearManage ? null : manage ?? query.ManageItem
        };
        return "/admin/suite/content" + QueryString.Create(values.Where(pair =>
            !string.IsNullOrEmpty(pair.Value))).ToUriComponent();
    }

    private static string ContentHealthUrl(ContentPageQuery query, string? cursor = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["cursor"] = cursor ?? query.Cursor,
            ["availability"] = query.Availability,
            ["resultCode"] = query.ResultCode,
            ["platform"] = query.Platform,
            ["item"] = query.ItemPrefix,
            ["name"] = query.Name
        };
        return "/admin/suite/content/health" + QueryString.Create(values.Where(pair =>
            !string.IsNullOrEmpty(pair.Value))).ToUriComponent();
    }

    private static string? AllowedOk(string value) => value switch
    {
        "VERIFICACAO_ENFILEIRADA" => "Teste de 1 byte agendado. O worker lê uma amostra mínima e fecha a conexão.",
        "SUBSTITUICAO_ENFILEIRADA" => "URL candidata recebida e cifrada. A publicação depende da validação integral.",
        "NOVA_VERSAO_ENFILEIRADA" => "Nova versão recebida e cifrada. Nada foi publicado antes da validação.",
        _ => null
    };

    private static string? AllowedError(string value) => value switch
    {
        "CONSULTA_INVALIDA" => "Filtros inválidos. A consulta foi recusada.",
        "LIMITE_ATINGIDO" => "Limite temporário atingido. Aguarde antes de repetir.",
        "OPERACAO_RECUSADA" => "Operação recusada. Confirme os dados e o estado atual.",
        _ => null
    };

    private static string ContentJobPage(SuiteContentJob job)
    {
        var body = new StringBuilder("<section class=scope-banner><div><span class=eyebrow>TRABALHO DE VALIDAÇÃO</span>")
            .Append("<h2>").Append(E(job.State)).Append("</h2><p>Detalhes sanitizados; a origem não é exibida.</p></div>")
            .Append("<a class='button ghost' href=/admin/suite/content>Voltar</a></section><section class=panel>")
            .Append("<div class=license-facts><div><span>Jogo/item</span><strong>").Append(E(job.ItemId))
            .Append("</strong></div><div><span>Intenção</span><strong>").Append(E(job.ChangeIntent))
            .Append("</strong></div><div><span>Resultado</span><strong>").Append(E(job.ResultCode))
            .Append("</strong></div><div><span>Atualização</span><strong>").Append(E(job.UpdatedAt))
            .Append("</strong></div></div></section>");
        return Shell("Validação de conteúdo", body.ToString());
    }

    private static string ContentUnavailable(string code) => Shell("Conteúdo indisponível",
        "<section class='panel empty-state'><h1>Administração de conteúdo indisponível</h1><p>" +
        E(code) + "</p><a class='button ghost' href=/admin/suite>Voltar à SUITE</a></section>");

    private static string Otp(SuiteOtpResult result,string? customer,string? email) => Shell("OTP SUITE",
        "<section class=login data-one-time-url=/admin/suite/issued><div class=login-card>" +
        "<span class=eyebrow>USO ÚNICO / " + E(result.ProductId) + "</span><h1>OTP de ativação</h1>" +
        (string.IsNullOrWhiteSpace(customer)?"":"<div class=detail-grid>"+Detail("Gerado para",customer)+Detail("E-mail",email??"")+Detail("Licença",result.LicenseId)+"</div>")+
        "<p>Não será mostrado novamente. Expira em " + E(Utc(result.ExpiresAt)) + ".</p>" +
        "<code class=activation id=activation-code>" + E(result.Otp) + "</code>" +
        "<div class=activation-actions><button type=button class=ghost data-copy-target='#activation-code'>" +
        "Copiar código</button><a class='button primary' data-one-time-exit href=/admin/suite>" +
        "Ocultar e voltar</a></div></div></section>");

    private static string Utc(DateTime? value) => value is null ? "" : value.Value.ToUniversalTime()
        .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    private static string When(long value)=>value<=0?"Sem registro":DateTimeOffset.FromUnixTimeSeconds(value)
        .ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss",CultureInfo.GetCultureInfo("pt-BR"));
    private static string Friendly(string value)=>value switch
    {
        "SESSAO"=>"Sessão","SEGURANCA"=>"Segurança","DOWNLOAD"=>"Download",
        "ACTIVE"=>"Ativa","SUCCESS"=>"Concluído","FAILED"=>"Falhou","DENIED"=>"Recusado",
        "COMPLETED"=>"Concluído","EXPIRED"=>"Expirado","REVOKED"=>"Revogado",
        "SUITE_OTP_ISSUED"=>"Código de ativação emitido",
        "SUITE_OTP_DENIED"=>"Emissão do código recusada",
        "SUITE_DEVICE_TRANSFER_AUTHORIZED"=>"Troca de computador autorizada",
        "SUITE_DEVICE_TRANSFER_COMPLETED"=>"Troca de computador concluída",
        "SUITE_COMMERCE_PROVISIONED"=>"Licença criada após a compra",
        "SUITE_COMMERCE_SUSPENDED"=>"Licença suspensa pelo pagamento",
        "SUITE_DOWNLOAD_HISTORY_CLEARED"=>"Histórico de jogos limpo",
        "SUITE_SESSION_OPEN"=>"Programa conectado",
        "SUITE_SESSION_HEARTBEAT"=>"Conexão do programa renovada",
        "FIRST_CLAIM_OTP_ISSUED"=>"Código de primeira ativação emitido",
        "OTP_ISSUED"=>"Código de ativação emitido",
        "OLD_DEVICE_REVOKED"=>"Computador anterior desvinculado",
        "NEW_DEVICE_BOUND"=>"Novo computador vinculado",
        "PURCHASE_PAID"=>"Compra paga","PURCHASE_SUSPENDED"=>"Compra suspensa",
        "PROVISIONED"=>"Licença criada","PAID"=>"Pago","SUSPENDED"=>"Suspenso",
        "CHECK_OK"=>"Link funcionando","CHECK_QUEUED"=>"Verificação agendada",
        "CONTENT_CHECK_REQUESTED"=>"Verificação de link solicitada",
        "CONTENT_CANDIDATE_PUBLISHED"=>"Nova origem publicada",
        "CONTENT_CANDIDATE_REJECTED"=>"Nova origem recusada",
        "CONTENT_CANDIDATE_VERIFIED"=>"Nova origem verificada",
        _=>Humanize(value)
    };
    private static string FriendlyDetail(string value)
    {
        var parts=value.Split(" · ",StringSplitOptions.None);
        return string.Join(" · ",parts.Select(Friendly));
    }
    private static string Humanize(string value)
    {
        if(value.Length==0)return "—";
        var text=value.Replace("SUITE_","").Replace("CONTENT_","").Replace('_',' ').ToLowerInvariant();
        return char.ToUpperInvariant(text[0])+text[1..];
    }
    private static string Unavailable() => Shell("SUITE indisponível",
        "<section class='panel empty-state'><h1>Emissor SUITE indisponível</h1>" +
        "<p>Nenhuma emissão foi realizada.</p><a class='button ghost' href=/admin>Voltar ao PIX</a></section>");

    internal static bool DefaultsDenySuitePermissionsForTest()
    {
        var context = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity())
        };
        return !Has(context, Read) && !Has(context, Issue) && !Has(context, Export) &&
            !Has(context, ContentRead) && !Has(context, ContentReplace) &&
            !Has(context, ContentVersion) && !Has(context, ContentCheck);
    }

    internal static bool HasCspSafeOneTimePageForTest()
    {
        var page = Otp(new("TURBORAMA_SUITE", "TS-TEST", "device", "secret", DateTime.UtcNow),null,null);
        return !page.Contains("<script>", StringComparison.OrdinalIgnoreCase) &&
            !page.Contains("onclick", StringComparison.OrdinalIgnoreCase) &&
            page.Contains("data-copy-target", StringComparison.Ordinal) &&
            page.Contains("data-one-time-url", StringComparison.Ordinal) &&
            page.Contains("src=/admin/assets/admin.js", StringComparison.Ordinal);
    }

    internal static bool HasSafeContentHtmlForTest()
    {
        const string marker = "https://sensitive.example/private.zip";
        var item = new SuiteContentItem(new string('a', 32), "Jogo <Teste> & Seguro", "ONLINE",
            7, "2026-08-29T00:00:00Z", "CHECK_OK", null, null, null);
        var page = ContentPage(new([item], null), new([], null),
            new ContentPageQuery(null, null, null, null, null, null, item.ItemId, null, null),
            "csrf-test", "", "", true, true, true);
        var postForms = new[] { "/actions/check", "/actions/replace", "/actions/version" };
        return !page.Contains(marker, StringComparison.Ordinal) &&
            !page.Contains("value='https://", StringComparison.OrdinalIgnoreCase) &&
            page.Contains("Jogo &lt;Teste&gt; &amp; Seguro", StringComparison.Ordinal) &&
            postForms.All(action => page.Contains(action, StringComparison.Ordinal)) &&
            page.Split("name=__RequestVerificationToken", StringSplitOptions.None).Length - 1 >= 3 &&
            page.Contains("name=adminPassword", StringComparison.Ordinal) &&
            page.Contains("name=versionAdminPassword", StringComparison.Ordinal) &&
            page.Contains("autocomplete=off", StringComparison.Ordinal) &&
            !page.Contains("<script>", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool UsesContentPrgForTest()
    {
        var allowed = new[] { "VERIFICACAO_ENFILEIRADA", "SUBSTITUICAO_ENFILEIRADA",
            "NOVA_VERSAO_ENFILEIRADA" };
        const string secret = "https://sensitive.example/private.zip";
        var rejected = ContentRedirectUri(secret, secret, secret);
        return allowed.All(value => AllowedOk(value) is not null) &&
            AllowedOk(secret) is null && rejected == "/admin/suite/content" &&
            !rejected.Contains(secret, StringComparison.Ordinal);
    }

    private static readonly Lazy<IReadOnlyDictionary<string, VisualCategory>> VisualCategories =
        new(LoadVisualCategories, LazyThreadSafetyMode.ExecutionAndPublication);

    private static IReadOnlyDictionary<string, VisualCategory> LoadVisualCategories()
    {
        const string path = "/opt/turborama-suite-content-publisher/catalog.visual.json";
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 16 });
            var names = document.RootElement.GetProperty("categories").EnumerateArray()
                .ToDictionary(value => value.GetProperty("id").GetString()!, value => new VisualCategory(
                    value.GetProperty("id").GetString()!, value.GetProperty("displayName").GetString()!,
                    value.GetProperty("order").GetInt32()), StringComparer.Ordinal);
            return document.RootElement.GetProperty("items").EnumerateArray().ToDictionary(
                value => value.GetProperty("id").GetString()!,
                value => names.GetValueOrDefault(value.GetProperty("categoryId").GetString()!,
                    VisualCategory.Other), StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new Dictionary<string, VisualCategory>(StringComparer.Ordinal);
        }
    }

    private sealed record VisualCategory(string Id, string Name, int Order)
    {
        public static readonly VisualCategory Other = new("other", "Outros", int.MaxValue);
    }
}

sealed record ContentPageQuery(string? Cursor, string? AuditCursor, string? Availability,
    string? ResultCode, string? JobState, string? ItemPrefix, string? ManageItem, string? Name,
    string? Platform)
{
    public static bool TryParse(IQueryCollection values, out ContentPageQuery? query)
    {
        query = null;
        static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        static bool Cursor(string? value) => value is null || value.Length <= 128 &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
        static bool Code(string? value, int maximum) => value is null || value.Length <= maximum &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_');
        static bool HexPrefix(string? value) => value is null || value.Length <= 32 &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
        static bool Hex(string? value, int length) => value is null || value.Length == length &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        var cursor = Empty(values["cursor"].ToString());
        var auditCursor = Empty(values["auditCursor"].ToString());
        var availability = Empty(values["availability"].ToString());
        var result = Empty(values["resultCode"].ToString());
        var job = Empty(values["jobState"].ToString());
        var item = Empty(values["item"].ToString());
        var manage = Empty(values["manage"].ToString());
        var name = Empty(values["name"].ToString());
        var platform = Empty(values["platform"].ToString());
        if (!Cursor(cursor) || !Cursor(auditCursor) ||
            availability is not (null or "ONLINE" or "EM_MANUTENCAO") ||
            !Code(result, 64) || !Code(job, 16) || !HexPrefix(item) || !Hex(manage, 32) ||
            name is { Length: > 100 } || name?.Any(char.IsControl) == true ||
            platform is { Length: > 64 } || platform?.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) == true)
            return false;
        query = new ContentPageQuery(cursor, auditCursor, availability, result, job, item,
            manage, name, platform);
        return true;
    }
}
