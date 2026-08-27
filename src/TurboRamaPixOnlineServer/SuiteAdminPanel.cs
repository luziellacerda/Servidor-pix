using System.Globalization;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;

static class SuiteAdminPanel
{
    const string Read="suite.read",Issue="suite.activation.issue",Export="suite.audit.export";
    public static void Map(WebApplication app)
    {
        app.MapGet("/admin/suite",async(HttpContext context,IAntiforgery antiforgery,SuiteAdminBff bff,CancellationToken ct)=>
        {
            if(!Has(context,Read))return Results.Forbid();
            try{return Html(Page(await bff.StatusAsync(ExpectedLicense(),ct),antiforgery.GetAndStoreTokens(context).RequestToken??"",context.Request.Query["error"].ToString()));}
            catch(HttpRequestException){return Html(Unavailable(),503);}
        }).RequireAuthorization();
        app.MapGet("/admin/suite/issued",()=>Results.Redirect("/admin/suite")).RequireAuthorization();
        app.MapPost("/admin/suite/actions/issue-otp",async(HttpContext context,IAntiforgery antiforgery,
            OnlineAdminConfiguration admin,SuiteAdminBff bff,SuiteIssueGuard guard,CancellationToken ct)=>
        {
            if(!Has(context,Issue))return Results.Forbid();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form=await context.Request.ReadFormAsync(ct);
                var id=Required(form,"licenseId");var device=Required(form,"deviceId");
                if(!Fixed(id,Required(form,"confirmLicenseId"))||!Fixed(device,Required(form,"confirmDeviceId"))||!Fixed(id,ExpectedLicense()))throw new SecurityException();
                var actor=context.User.Identity?.Name??"unknown";
                if(!AdminPasswordHash.Verify(admin.PasswordHash,Required(form,"adminPassword")))
                {await AuditDenial(bff,id,device,actor,"STEP_UP_DENIED",ct);return Results.Redirect("/admin/suite?error=OPERATION_DENIED");}
                var key=actor+'\0'+id+'\0'+(context.Connection.RemoteIpAddress?.ToString()??"unknown");
                if(!guard.Allow(key))
                {await AuditDenial(bff,id,device,actor,"RATE_LIMIT",ct);return Results.Redirect("/admin/suite?error=RATE_LIMIT");}
                var status=await bff.StatusAsync(id,ct);
                if(!status.CanIssue||!Fixed(status.DeviceId,device))throw new SecurityException();
                var issued=await bff.IssueAsync(id,device,actor,RequestId(),ct);
                context.Response.Headers.CacheControl="no-store, no-cache, must-revalidate";
                context.Response.Headers.Pragma="no-cache";
                return Html(Otp(issued));
            }
            catch(Exception ex)when(ex is SecurityException or AntiforgeryValidationException or HttpRequestException)
            {return Results.Redirect("/admin/suite?error=OPERATION_DENIED");}
        }).RequireAuthorization();
        app.MapGet("/admin/suite/export/audit.csv",async(HttpContext context,SuiteAdminBff bff,CancellationToken ct)=>
        {
            if(!Has(context,Export))return Results.Forbid();
            try{return Results.File(await bff.AuditAsync(ct),"text/csv; charset=utf-8","turborama-suite-otp-audit.csv");}
            catch(HttpRequestException){return Results.StatusCode(503);}
        }).RequireAuthorization();
    }

    static bool Has(HttpContext context,string permission)=>context.User.HasClaim("permission",permission);
    static string RequestId()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    static async Task AuditDenial(SuiteAdminBff bff,string id,string device,string actor,string code,CancellationToken ct){try{_ = await bff.DenyAsync(id,device,actor,RequestId(),code,ct);}catch(HttpRequestException){}}
    static string ExpectedLicense()=>(Environment.GetEnvironmentVariable("TURBORAMA_SUITE_LICENSE_ID")??"").Trim();
    static string Required(IFormCollection form,string name){var value=form[name].ToString();if(value.Length is <1 or >1024||value.Any(char.IsControl))throw new SecurityException();return value;}
    static bool Fixed(string a,string b){var x=Encoding.UTF8.GetBytes(a);var y=Encoding.UTF8.GetBytes(b);try{return x.Length==y.Length&&CryptographicOperations.FixedTimeEquals(x,y);}finally{CryptographicOperations.ZeroMemory(x);CryptographicOperations.ZeroMemory(y);}}
    static IResult Html(string html,int code=200)=>Results.Content(html,"text/html; charset=utf-8",Encoding.UTF8,code);
    static string E(string? value)=>WebUtility.HtmlEncode(value??"");
    static string Shell(string title,string body)=>"<!doctype html><html lang=pt-BR><head><meta charset=utf-8><meta name=viewport content='width=device-width,initial-scale=1'><title>"+E(title)+"</title><link rel=stylesheet href=/admin/assets/admin.css><script defer src=/admin/assets/admin.js></script></head><body><main class=shell><header class=top><div><span class=eyebrow>LZ GAMES / TURBORAMA</span><h1>Central de licenças</h1></div><nav class=top-actions><a class='button ghost' href=/admin>PIX</a><a class='button primary' href=/admin/suite>SUITE</a></nav></header>"+body+"</main></body></html>";
    static string Page(SuiteAdminStatus status,string token,string error)
    {
        var otp=status.OtpState switch{"VALID"=>"VÁLIDO ATÉ "+Utc(status.OtpExpiresAt),"EXPIRED"=>"EXPIRADO","CONSUMED"=>"CONSUMIDO",_=>"NÃO EMITIDO"};
        var html=new StringBuilder("<section class=scope-banner><div><span class=eyebrow>").Append(E(status.ProductId)).Append("</span><h2>Emissor de ativação</h2><p>Canal isolado por socket Unix. O código aparece uma única vez.</p></div><span class='pill on'>").Append(E(status.Status)).Append("</span></section>");
        if(error.Length>0)html.Append("<div class='notice danger'>Operação recusada: ").Append(E(error)).Append("</div>");
        html.Append("<section class=panel><div class=license-facts><div><span>Produto</span><strong>").Append(E(status.ProductId)).Append("</strong></div><div><span>Licença</span><strong>").Append(E(status.LicenseId)).Append("</strong></div><div><span>Termo</span><strong>").Append(E(status.LicenseTerm)).Append(" · licença vitalícia</strong></div><div><span>OTP</span><strong>").Append(E(otp)).Append("</strong></div><div><span>Política</span><strong>").Append(E(status.IdentityPolicy)).Append("</strong></div><div><span>Binding</span><strong>").Append(E(status.BindingType)).Append("</strong></div><div><span>Algoritmo</span><strong>").Append(E(status.Algorithm)).Append("</strong></div><div><span>DeviceId esperado</span><strong>").Append(E(status.DeviceId)).Append("</strong></div><div><span>Dispositivos ativos</span><strong>").Append(status.ActiveDevices).Append("</strong></div><div><span>Sessão ativa</span><strong>").Append(E(status.SessionId??"Nenhuma")).Append("</strong></div></div>");
        if(status.CanIssue)
            html.Append("<form method=post action=/admin/suite/actions/issue-otp class=grid-form><input type=hidden name=__RequestVerificationToken value='").Append(E(token)).Append("'><input type=hidden name=licenseId value='").Append(E(status.LicenseId)).Append("'><input type=hidden name=deviceId value='").Append(E(status.DeviceId)).Append("'><label>Confirme a licença<input name=confirmLicenseId required autocomplete=off></label><label>Confirme a máquina<input name=confirmDeviceId required autocomplete=off></label><label>Confirme sua senha<input type=password name=adminPassword required autocomplete=new-password></label><button class=primary>Emitir OTP de 15 minutos</button></form>");
        else html.Append("<div class='notice danger'>Emissão indisponível no estado atual. A validação também é repetida atomicamente no servidor.</div>");
        html.Append("<p class=muted>A emissão é recusada se houver máquina ativa, OTP válido ou divergência de identidade.</p><a class='button ghost' href=/admin/suite/export/audit.csv>Exportar auditoria SUITE</a></section><section class=panel><span class=eyebrow>AUDITORIA SUITE</span><h2>Eventos recentes</h2><div class=table-wrap><table class=audit-table><thead><tr><th>Quando (UTC)</th><th>Evento</th><th>Resultado</th><th>Detalhe</th><th>Ator</th><th>Expiração OTP</th></tr></thead><tbody>");
        foreach(var item in status.RecentEvents)html.Append("<tr><td>").Append(E(item.OccurredAt)).Append("</td><td>").Append(E(item.EventType)).Append("</td><td>").Append(E(item.Outcome)).Append("</td><td>").Append(E(item.DetailCode)).Append("</td><td>").Append(E(item.Actor)).Append("</td><td>").Append(E(item.OtpExpiresAt??"")).Append("</td></tr>");
        if(status.RecentEvents.Count==0)html.Append("<tr><td colspan=6>Nenhum evento de OTP.</td></tr>");
        html.Append("</tbody></table></div></section>");
        return Shell("TurboRama SUITE",html.ToString());
    }
    static string Otp(SuiteOtpResult result)=>Shell("OTP SUITE","<section class=login data-one-time-url=/admin/suite/issued><div class=login-card><span class=eyebrow>USO ÚNICO / "+E(result.ProductId)+"</span><h1>OTP de ativação</h1><p>Não será mostrado novamente. Expira em "+E(Utc(result.ExpiresAt))+".</p><code class=activation id=activation-code>"+E(result.Otp)+"</code><div class=activation-actions><button type=button class=ghost data-copy-target='#activation-code'>Copiar código</button><a class='button primary' href=/admin/suite>Ocultar e voltar</a></div></div></section>");
    static string Utc(DateTime? value)=>value is null?"":value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'",CultureInfo.InvariantCulture);
    static string Unavailable()=>Shell("SUITE indisponível","<section class='panel empty-state'><h1>Emissor SUITE indisponível</h1><p>Nenhuma emissão foi realizada.</p><a class='button ghost' href=/admin>Voltar ao PIX</a></section>");

    internal static bool DefaultsDenySuitePermissionsForTest()
    {var context=new DefaultHttpContext{User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())};return !Has(context,Read)&&!Has(context,Issue)&&!Has(context,Export);}

    internal static bool HasCspSafeOneTimePageForTest()
    {var page=Otp(new("TURBORAMA_SUITE","TS-TEST","device","secret",DateTime.UtcNow));return !page.Contains("<script>",StringComparison.OrdinalIgnoreCase)&&!page.Contains("onclick",StringComparison.OrdinalIgnoreCase)&&page.Contains("data-copy-target",StringComparison.Ordinal)&&page.Contains("data-one-time-url",StringComparison.Ordinal)&&page.Contains("src=/admin/assets/admin.js",StringComparison.Ordinal);}
}
