using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using TurboRamaSuite.Management;

sealed partial class SuiteAdminBff
{
    public Task<ManagedSessions> SessionsAsync(string[] ids,SuiteContentAdminProof proof,CancellationToken ct)=>
        SendManagement<ManagedSessions>("sessions/query",new SessionQuery(ids),proof,SessionManagementPermissions.Read,false,ct);
    public Task<ManagedNetworkReports> NetworkAsync(string[] ids,SuiteContentAdminProof proof,CancellationToken ct)=>
        SendManagement<ManagedNetworkReports>("sessions/network",new SessionQuery(ids),proof,SessionManagementPermissions.NetworkRead,false,ct);
    public Task<RevokeEsSessionResult> RevokeSessionAsync(RevokeEsSessionRequest target,SuiteContentAdminProof proof,CancellationToken ct)=>
        SendManagement<RevokeEsSessionResult>("sessions/revoke",target,proof,SessionManagementPermissions.Revoke,true,ct);

    private async Task<T> SendManagement<T>(string path,object body,SuiteContentAdminProof proof,string claim,bool mutation,CancellationToken ct)
    {
        if(proof.Claim!=claim||proof.Actor is not {Length:>=1 and <=64}||proof.Actor.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '@' or '.' or '_' or '-'))||
            !IsHex(proof.IpDigest,64)||mutation&&proof.StepUpAt is null)throw new HttpRequestException("SUITE_SESSION_PROOF_INVALID");
        using var request=Request(HttpMethod.Post,path,body);
        request.Headers.TryAddWithoutValidation("X-Suite-Admin-Actor",proof.Actor);
        request.Headers.TryAddWithoutValidation("X-Suite-Admin-Claims",claim);
        request.Headers.TryAddWithoutValidation("X-Suite-Client-Ip-Digest",proof.IpDigest);
        if(mutation)
        {
            request.Headers.TryAddWithoutValidation("X-Suite-Csrf-Verified","1");
            request.Headers.TryAddWithoutValidation("X-Suite-Step-Up-At",proof.StepUpAt!.Value.ToString(CultureInfo.InvariantCulture));
        }
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new HttpRequestException("SUITE_SESSION_REQUEST_FAILED",null,response.StatusCode);
        return await ReadBoundedAsync<T>(response.Content,ct);
    }
}

static partial class SuiteAdminPanel
{
    private sealed record SessionTarget(string Actor,string LicenseId,string DeviceId,string SessionId,string RequestId,long ExpiresAt);
    private static IDataProtector SessionProtector(HttpContext context)=>context.RequestServices
        .GetRequiredService<IDataProtectionProvider>().CreateProtector("TurboRama.Suite.ES.SessionTermination/v1");

    private static void MapSessions(WebApplication app)
    {
        app.MapPost("/admin/clientes/actions/revoke-es-session",async(HttpContext context,IAntiforgery antiforgery,
            OnlineAdminConfiguration admin,SuiteAdminBff bff,CancellationToken ct)=>
        {
            if(!Has(context,SessionManagementPermissions.Revoke))return Results.Forbid();
            string? license=null;
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var form=await context.Request.ReadFormAsync(ct);
                if(form["confirmTarget"]!="1")throw new SecurityException();
                var encoded=form["target"].ToString();if(encoded.Length is <20 or >4096)throw new SecurityException();
                var target=JsonSerializer.Deserialize<SessionTarget>(SessionProtector(context).Unprotect(encoded))??throw new SecurityException();
                var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if(target.Actor!=context.User.Identity?.Name||target.ExpiresAt<now||target.ExpiresAt>now+300||
                    !ValidLicenseId(target.LicenseId)||!IsHex(target.DeviceId,64)||!IsHex(target.SessionId,64))throw new SecurityException();
                license=target.LicenseId;
                RequireStepUp(admin,ContentPassword(form,"adminPassword"));
                await bff.RevokeSessionAsync(new(target.LicenseId,target.DeviceId,"EMULATIONSTATION",target.SessionId,target.SessionId,target.RequestId),
                    ContentProof(context,SessionManagementPermissions.Revoke) with {StepUpAt=now},ct);
                return Results.Redirect("/admin/clientes/"+Uri.EscapeDataString(license)+"?ok=ES_SESSION_REVOKED");
            }
            catch(Exception e)when(e is SecurityException or CryptographicException or JsonException or AntiforgeryValidationException or HttpRequestException or InvalidOperationException or BadHttpRequestException)
            {return Results.Redirect(license is null?"/admin?error=OPERATION_DENIED":"/admin/clientes/"+Uri.EscapeDataString(license)+"?error=SESSION_NOT_CHANGED");}
        }).RequireAuthorization();
    }

    private static async Task<string> SessionSectionAsync(HttpContext context,SuiteAdminBff bff,string license,string csrf,CancellationToken ct)
    {
        if(!Has(context,SessionManagementPermissions.Read))return "<section class=panel><p>Acesso às sessões restrito ao administrador autorizado.</p></section>";
        try
        {
            var sessions=await bff.SessionsAsync([license],ContentProof(context,SessionManagementPermissions.Read),ct);
            ManagedNetworkReports? network=null;
            var networkUnavailable=false;
            if(Has(context,SessionManagementPermissions.NetworkRead))
                try{network=await bff.NetworkAsync([license],ContentProof(context,SessionManagementPermissions.NetworkRead),ct);}
                catch(HttpRequestException){networkUnavailable=true;}
            var html=new StringBuilder("<section class='panel suite-sessions'><span class=eyebrow>CONEXÕES AUTORIZADAS</span><h2>Sessões por aplicação</h2><p class=muted>Vínculo técnico do computador com a licença. O cadastro comercial não representa um login do titular.</p>");
            if(context.Request.Query["ok"]=="ES_SESSION_REVOKED")html.Append("<div class='notice success'>A sessão EmulationStation selecionada foi encerrada no servidor.</div>");
            if(context.Request.Query["error"]=="SESSION_NOT_CHANGED")html.Append("<div class='notice warning'>A operação não foi concluída. Atualize as sessões e confira a confirmação e sua senha.</div>");
            html.Append("<div class=table-wrap><table class=audit-table><thead><tr><th>Aplicação / computador</th><th>Estado</th><th>Último contato</th><th>Validade</th><th>Gerenciar</th></tr></thead><tbody>");
            foreach(var row in sessions.Sessions)
            {
                html.Append("<tr><td><strong>").Append(AppName(row.AppScope)).Append("</strong><small>").Append(E(Masked(row.DeviceId))).Append(" · sessão ").Append(E(Masked(row.SessionId)))
                    .Append("</small></td><td>").Append(SessionState(row.State)).Append("</td><td>").Append(Contact(row.LastContactAtUnixSeconds)).Append("</td><td>").Append(Contact(row.AuthorizedUntilUnixSeconds)).Append("</td><td>");
                if(row.AppScope=="EMULATIONSTATION"&&row.State is "ONLINE" or "NO_RECENT_CONTACT"&&Has(context,SessionManagementPermissions.Revoke))
                {
                    var target=new SessionTarget(context.User.Identity?.Name??"unknown",row.LicenseId,row.DeviceId,row.SessionId,RequestId(),DateTimeOffset.UtcNow.ToUnixTimeSeconds()+300);
                    var encoded=SessionProtector(context).Protect(JsonSerializer.Serialize(target));
                    html.Append("<details><summary>Encerrar sessão EmulationStation</summary><p>Confirme a licença ").Append(E(Masked(row.LicenseId))).Append(", computador ").Append(E(Masked(row.DeviceId))).Append(" e sessão ").Append(E(Masked(row.SessionId)))
                        .Append(".</p><form method=post action=/admin/clientes/actions/revoke-es-session>").Append(Csrf(csrf))
                        .Append("<input type=hidden name=target value='").Append(E(encoded)).Append("'><label><input type=checkbox name=confirmTarget value=1 required>Confirmo esta sessão EmulationStation</label>")
                        .Append("<label>Senha administrativa<input type=password name=adminPassword required autocomplete=current-password></label><button class=danger data-confirm='Encerrar a sessão EmulationStation selecionada?' data-busy='Encerrando...'>Encerrar sessão EmulationStation</button></form></details>");
                }
                else html.Append("—");
                html.Append("</td></tr>");
            }
            if(sessions.Sessions.Length==0)html.Append("<tr><td colspan=5>Nenhuma sessão registrada.</td></tr>");
            html.Append("</tbody></table></div><h3>Informações complementares de rede</h3><p>IP apenas informativo: mudança de rede não bloqueia acesso.</p>");
            if(networkUnavailable)html.Append("<p class=muted>As informações de rede estão temporariamente indisponíveis.</p>");
            else if(network is null)html.Append("<p class=muted>Consulta de rede restrita à permissão específica.</p>");
            else if(network.Reports.Length==0)html.Append("<p class=muted>Não informado por esta versão do programa.</p>");
            else foreach(var report in network.Reports)
            {
                html.Append("<article class=panel><strong>").Append(AppName(report.AppScope)).Append(" · ").Append(E(Masked(report.DeviceId))).Append("</strong><p>IP observado pelo servidor: <code>").Append(E(report.IpMasked)).Append("</code></p><p>Interfaces informadas pelo cliente: ");
                if(report.Interfaces.Length==0)html.Append("nenhuma interface relevante informada");
                foreach(var item in report.Interfaces)html.Append("<code>").Append(E(item.Mac)).Append("</code> ").Append(item.InterfaceType=="WIRELESS"?"Wi-Fi":"Ethernet").Append(item.LocallyAdministered?" (endereço local ou randomizado)":"").Append(item.Virtual?" (virtual)":"").Append("; ");
                html.Append("</p><small>Coleta: ").Append(Contact(report.CollectedAtUnixSeconds)).Append(" · recebimento: ").Append(Contact(report.ReceivedAtUnixSeconds)).Append("</small></article>");
            }
            return html.Append("</section>").ToString();
        }
        catch(HttpRequestException){return "<section class=panel><h2>Sessões por aplicação</h2><p>Consulta temporariamente indisponível. Tente atualizar esta página.</p></section>";}
    }

    internal static async Task<string> ActiveCustomerPanelAsync(HttpContext context,SuiteAdminBff bff,CancellationToken ct)
    {
        if(!Has(context,SessionManagementPermissions.Read))return "<section class=panel id=suite-clients><p>Consulta de sessões restrita ao administrador autorizado.</p></section>";
        var filter=context.Request.Query["suiteSearch"].ToString().Trim();if(filter.Length>64)filter=filter[..64];
        var all=LoadCustomerLicenses().Where(c=>ValidLicenseId(c.LicenseId)&&
            (filter.Length==0||c.Customer.Contains(filter,StringComparison.OrdinalIgnoreCase)||c.Email.Contains(filter,StringComparison.OrdinalIgnoreCase)||c.LicenseId.Contains(filter,StringComparison.OrdinalIgnoreCase)))
            .GroupBy(c=>c.LicenseId,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(c=>c.LicenseId,StringComparer.Ordinal).ToArray();
        var page=int.TryParse(context.Request.Query["suitePage"],out var p)?Math.Clamp(p,1,Math.Max(1,(all.Length+24)/25)):1;
        var customers=all.Skip((page-1)*25).Take(25).ToArray();
        var query=QueryString.Create(new Dictionary<string,string?> { ["suitePage"]=page.ToString(CultureInfo.InvariantCulture),["suiteSearch"]=filter }).ToUriComponent();
        var html=new StringBuilder("<section class='panel active-customers' id=suite-clients data-query='").Append(E(query)).Append("'><header><div><span class=eyebrow>CLIENTES SUITE</span><h2>Conexões por aplicação</h2><p class=muted>Presença autenticada de Suite e EmulationStation. Compras e liberações permanecem no cadastro comercial.</p></div></header>")
            .Append("<form method=get action=/admin><label>Buscar cliente<input name=suiteSearch maxlength=64 value='").Append(E(filter)).Append("'></label><button>Buscar</button></form>");
        if(customers.Length==0)return html.Append("<div class=empty-state>Nenhum cliente encontrado.</div></section>").ToString();
        try
        {
            var data=await bff.SessionsAsync(customers.Select(c=>c.LicenseId).ToArray(),ContentProof(context,SessionManagementPermissions.Read),ct);
            html.Append("<p>").Append(data.Sessions.Count(s=>s.State=="ONLINE")).Append(" sessões online nesta página · ").Append(all.Length).Append(" clientes no filtro</p><div class=customer-list>");
            foreach(var customer in customers)
            {
                var rows=data.Sessions.Where(s=>s.LicenseId==customer.LicenseId).ToArray();
                html.Append("<a class=customer-row href='/admin/clientes/").Append(Uri.EscapeDataString(customer.LicenseId)).Append("'><span><strong>").Append(E(customer.Customer)).Append("</strong><small>").Append(E(customer.Email)).Append("</small></span><code>").Append(E(Masked(customer.LicenseId))).Append("</code><span>");
                foreach(var row in rows)html.Append("<strong>").Append(AppName(row.AppScope)).Append(" · ").Append(SessionState(row.State)).Append("</strong><small>").Append(Contact(row.LastContactAtUnixSeconds)).Append("</small>");
                if(rows.Length==0)html.Append("Sem sessão registrada");
                html.Append("</span><b>Ver histórico →</b></a>");
            }
            html.Append("</div>");
        }
        catch(HttpRequestException){html.Append("<div class=empty-state>Não foi possível consultar as sessões. A próxima atualização tentará novamente.</div>");}
        html.Append("<nav aria-label='Páginas de clientes'>Página ").Append(page).Append(" de ").Append(Math.Max(1,(all.Length+24)/25));
        foreach(var next in new[] {page-1,page+1})if(next>=1&&next<=(all.Length+24)/25)
            html.Append(" <a class='button ghost' href='/admin").Append(E(QueryString.Create(new Dictionary<string,string?> { ["suitePage"]=next.ToString(CultureInfo.InvariantCulture),["suiteSearch"]=filter }).ToUriComponent())).Append("#suite-clients'>").Append(next<page?"Anterior":"Próxima").Append("</a>");
        return html.Append("</nav></section>").ToString();
    }
    private static string AppName(string scope)=>scope=="EMULATIONSTATION"?"EmulationStation":scope=="SUITE"?"TurboRama Suite":"Aplicação não reconhecida";
    private static string SessionState(string state)=>state switch {"ONLINE"=>"Online","EXPIRED"=>"Expirada","REVOKED"=>"Revogada",_=>"Sem contato recente"};
    private static string Masked(string value)=>value.Length<=10?"••••":value[..4]+"…"+value[^4..];
    private static string Contact(long timestamp)=>timestamp<=0?"Sem contato registrado":When(timestamp);
}
