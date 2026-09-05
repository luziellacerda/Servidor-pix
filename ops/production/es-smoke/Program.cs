using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Npgsql;
using TurboRamaSuite.Management;
using TurboRamaSuite.Network;
using TurboRamaSuiteOnlineServer;
using static SharedIntegrationChecks;

// This bounded operator check targets the already authorized destination. It
// creates only two random synthetic identities, reserves their exact notification
// event keys as SKIPPED before opening Suite, and removes their rows in finally.
const string onlineKeyId="2d8987dc740a47cba0a8ee7c3e0fbffaff057e2325c46ea2324ee335de69da50";
const string tlsPin="13f9dcbb7a9687c2f88ff73de5621cfab849d0ec02191dcdd1ee8a6275dacba7";
if(args.Length!=1||args[0] is not ("--tls-only" or "--production-smoke"))
    throw new InvalidOperationException("Use --tls-only or --production-smoke on the authorized production host.");
using var tlsHandler=new HttpClientHandler();
tlsHandler.ServerCertificateCustomValidationCallback=(_,cert,_,errors)=>
    cert is not null&&errors==SslPolicyErrors.None&&HexHash(cert.PublicKey.ExportSubjectPublicKeyInfo())==tlsPin;
using var external=new HttpClient(tlsHandler){BaseAddress=new Uri("https://app.lzgames.com.br/"),Timeout=TimeSpan.FromSeconds(10)};
using(var probe=await external.PostAsync("/v1/suite/challenges",new StringContent("{}",Encoding.UTF8,"application/json")))
    Check((int)probe.StatusCode==400,"The approved TLS pin and public Suite route must be available.");
if(args[0]=="--tls-only"){Console.WriteLine("APPROVED CLIENT TLS PIN: OK");return;}
var trace=await external.GetStringAsync("/cdn-cgi/trace");
var observed=trace.Split('\n').Single(x=>x.StartsWith("ip=",StringComparison.Ordinal))[3..].Trim();
Check(IPAddress.TryParse(observed,out var observedIp),"The authorized edge must identify the test connection.");
var expectedIpMask=NetworkInventoryContract.MaskIp(observedIp!);
external.DefaultRequestHeaders.Add("X-Forwarded-For","198.51.100.50");

var spki=await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"approved-online-spki.der"));
Check(HexHash(spki)==onlineKeyId,"Production online authority must match the approved Windows candidate.");
using var online=RSA.Create();online.ImportSubjectPublicKeyInfo(spki,out _);
await using var db=NpgsqlDataSource.Create("Host=/var/run/postgresql;Database=postgres;Username=postgres;Maximum Pool Size=2;Application Name=es-production-smoke");
using var a=new SyntheticClient();using var b=new SyntheticClient();
var owned=new[]{a.License,b.License};
using var origin=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:5190/"),Timeout=TimeSpan.FromSeconds(10)};
using var unixHandler=new SocketsHttpHandler{ConnectCallback=async(_,ct)=>{
    var socket=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);
    try{await socket.ConnectAsync(new UnixDomainSocketEndPoint("/run/turborama-suite-admin/admin.sock"),ct);return new NetworkStream(socket,true);}
    catch{socket.Dispose();throw;}}};
using var admin=new HttpClient(unixHandler){BaseAddress=new Uri("http://localhost/"),Timeout=TimeSpan.FromSeconds(10)};
var adminToken=(await File.ReadAllTextAsync("/etc/turborama-suite-bff/token")).Trim();
var checks=new List<string>{"approved-tls-pin","approved-online-authority"};
var suiteA=SyntheticClient.Hex();var esA=SyntheticClient.Hex();var suiteB=SyntheticClient.Hex();var esB=SyntheticClient.Hex();
var succeeded=false;
try
{
    foreach(var (who,session) in new[]{(a,suiteA),(b,suiteB)})
    {
        await who.Seed(db);
        // These identities have no commerce/delivery/customer recipient. Reserve
        // only their stable event keys, so a real session.open cannot queue work.
        await Sql("INSERT INTO suite.suite_connection_notification_outbox(event_id,event_key,event_type,license_id,device_id,connected_at,status,last_error_code,completed_at) VALUES($1,$2,'device.connected',$3,$4,clock_timestamp(),'SKIPPED','ES_DEPLOYMENT_SMOKE',clock_timestamp())",Guid.NewGuid(),"session.open:"+who.License+":"+session,who.License,who.Device);
    }
    await Exchange(origin,online,a,suiteA,"SUITE");await Exchange(origin,online,a,esA,"SHARED");
    await Exchange(external,online,b,suiteB,"SUITE");await Exchange(external,online,b,esB,"SHARED");
    await Task.WhenAll(Exchange(external,online,a,suiteA,"SUITE",true),Exchange(external,online,a,esA,"SHARED",true),
        Exchange(origin,online,b,suiteB,"SUITE",true),Exchange(origin,online,b,esB,"SHARED",true));
    checks.Add("original-suite-and-shared-es-origin-public-coexistence");
    var oldEsA=esA;
    var oldHeartbeat=await Proof(external,online,a,oldEsA,"SHARED",true);
    var replacement=SyntheticClient.Hex();
    var pending=await Proof(external,online,a,replacement,"SHARED");
    var reopened=Verify(await Submit(external,pending,"SHARED"),EmulationStationAssertionSigner.OpenKind);
    Check(reopened.Status=="ACTIVE"&&reopened.LicenseId==a.License&&reopened.DeviceId==a.Device&&
        reopened.SessionId==replacement&&reopened.AuthorizedUntilUnixSeconds>reopened.ServerTimeUnixSeconds&&
        reopened.HeartbeatAfterSeconds==5,"A fresh proven ES open must replace the previous session exactly as Suite does.");
    using(var replay=await Send(external,"/v1/suite/sessions",pending,"SHARED"))
        Check((int)replay.StatusCode==409&&(await replay.Content.ReadFromJsonAsync<ErrorResponse>())?.Code=="CHALLENGE_INVALID","Proof replay must be rejected.");
    using(var refused=await Send(external,"/v1/suite/sessions",oldHeartbeat,"SHARED"))
        Check((int)refused.StatusCode==409&&(await refused.Content.ReadFromJsonAsync<ErrorResponse>())?.Code=="SESSION_INVALID","A heartbeat issued before replacement must not renew the old ES session.");
    var freshOldHeartbeat=await Proof(external,online,a,oldEsA,"SHARED",true);
    using(var refused=await Send(external,"/v1/suite/sessions",freshOldHeartbeat,"SHARED"))
        Check((int)refused.StatusCode==409&&(await refused.Content.ReadFromJsonAsync<ErrorResponse>())?.Code=="SESSION_INVALID","A fresh proof must not renew the replaced ES session.");
    esA=replacement;
    await Exchange(external,online,a,esA,"SHARED",true);
    var downgrade=await Proof(external,online,a,SyntheticClient.Hex(),"SHARED");
    using(var wrong=await Send(external,EmulationStationService.SessionRoute,downgrade,"DEDICATED"))Check((int)wrong.StatusCode==409,"A shared proof cannot downgrade to legacy replacement.");
    checks.Add("signed-reopen-stale-heartbeat-replay-and-downgrade-denial");
    await Network(a,esA);await Network(b,esB);checks.Add("network-challenge-result-signatures-and-replay");
    await Task.WhenAll(Exchange(external,online,a,suiteA,"SUITE",true),Exchange(external,online,a,esA,"SHARED",true),
        Exchange(origin,online,b,suiteB,"SUITE",true),Exchange(origin,online,b,esB,"SHARED",true));
    var query=new SessionQuery(owned);
    using(var response=await Admin("/sessions/query",query,SessionManagementPermissions.Read))
    {
        response.EnsureSuccessStatusCode();var result=(await response.Content.ReadFromJsonAsync<ManagedSessions>())!;
        Check(result.Sessions.Length==4&&result.Sessions.All(x=>x.State=="ONLINE"&&x.LastContactAtUnixSeconds>0),"Admin must see both applications for both synthetic identities.");
    }
    using(var response=await Admin("/sessions/network",query,SessionManagementPermissions.NetworkRead))
    {
        response.EnsureSuccessStatusCode();var result=(await response.Content.ReadFromJsonAsync<ManagedNetworkReports>())!;
        Check(result.Reports.Length==2&&result.Reports.All(x=>x.IpMasked==expectedIpMask&&x.Interfaces.All(i=>i.Mac.Contains('*'))),"Admin must mask the observed edge IP and ignore supplied forwarded IP.");
    }
    var pendingHeartbeat=await Proof(external,online,a,esA,"SHARED",true);
    var revoke=new RevokeEsSessionRequest(a.License,a.Device,"EMULATIONSTATION",esA,esA,Guid.NewGuid().ToString("N"));
    using(var denied=await Admin("/sessions/revoke",revoke,SessionManagementPermissions.Revoke))Check((int)denied.StatusCode==403,"Revoke without post controls must be denied.");
    // The native privileged operator check exercises the internal BFF contract;
    // it does not claim a browser password/CSRF interaction was performed.
    foreach(var expected in new[]{"REVOKED","ALREADY_REVOKED"})
    {
        using var response=await Admin("/sessions/revoke",revoke,SessionManagementPermissions.Revoke,true);
        response.EnsureSuccessStatusCode();Check((await response.Content.ReadFromJsonAsync<RevokeEsSessionResult>())?.Code==expected,"Exact revocation must be idempotent.");
    }
    using(var refused=await Send(external,"/v1/suite/sessions",pendingHeartbeat,"SHARED"))
        Check((int)refused.StatusCode==409&&(await refused.Content.ReadFromJsonAsync<ErrorResponse>())?.Code=="CHALLENGE_INVALID","Revocation must invalidate the pending ES heartbeat proof.");
    // A challenge proves no session authorization. Submit a fresh signed machine
    // proof and verify that the revoked session cannot obtain an ACTIVE grant.
    var revokedHeartbeat=await Proof(external,online,a,esA,"SHARED",true);
    using(var refused=await Send(external,"/v1/suite/sessions",revokedHeartbeat,"SHARED"))
        Check((int)refused.StatusCode==409&&(await refused.Content.ReadFromJsonAsync<ErrorResponse>())?.Code=="SESSION_INVALID","The revoked ES session cannot renew with a fresh signed proof.");
    await Exchange(external,online,a,suiteA,"SUITE",true);await Exchange(external,online,b,suiteB,"SUITE",true);await Exchange(external,online,b,esB,"SHARED",true);
    Check((long)(await Scalar("SELECT count(*) FROM suite.suite_audit_events WHERE license_id=$1 AND request_id=$2 AND event_type='SUITE_ES_SESSION_REVOKED'",a.License,revoke.RequestId))! ==1,"One exact revocation must produce one audit event.");
    checks.Add("admin-rows-mask-post-controls-exact-revoke-idempotency-and-isolation");
    var legacy=SyntheticClient.Hex();await Exchange(external,online,a,legacy,"DEDICATED");await Exchange(external,online,a,legacy,"DEDICATED",true);
    await Exchange(external,online,a,suiteA,"SUITE",true);checks.Add("dedicated-legacy-es-and-original-suite");
    foreach(var license in owned)
    {
        Check((long)(await Scalar("SELECT count(*) FROM suite.suite_connection_notification_outbox WHERE license_id=$1 AND status='SKIPPED' AND last_error_code='ES_DEPLOYMENT_SMOKE' AND attempts=0",license))! ==1,"Each synthetic notification skip marker must remain untouched.");
        Check((long)(await Scalar("SELECT count(*) FROM suite.suite_connection_notification_outbox WHERE license_id=$1 AND NOT(status='SKIPPED' AND last_error_code IS NOT DISTINCT FROM 'ES_DEPLOYMENT_SMOKE' AND attempts=0)",license))! ==0,"Synthetic checks must not enqueue customer notifications.");
    }
    checks.Add("zero-synthetic-customer-notifications-with-explicit-skip-markers");succeeded=true;
}
finally
{
    var notificationCount=(long)(await Scalar("SELECT count(*) FROM suite.suite_connection_notification_outbox WHERE license_id=ANY($1) AND NOT(status='SKIPPED' AND last_error_code IS NOT DISTINCT FROM 'ES_DEPLOYMENT_SMOKE' AND attempts=0)",(object)owned))!;
    await using var connection=await db.OpenConnectionAsync();await using var tx=await connection.BeginTransactionAsync();
    // Keep security audit events; their license columns intentionally have no FK.
    foreach(var table in new[]{"suite_es_session_revocations","suite_network_inventory","suite_network_challenges","suite_es_challenges","suite_es_sessions","suite_challenges","suite_sessions","suite_connection_notification_outbox","suite_device_presence","suite_devices","suite_license_enrollments","suite_licenses"})
    {
        await using var command=new NpgsqlCommand($"DELETE FROM suite.{table} WHERE license_id=ANY($1)",connection,tx);
        command.Parameters.AddWithValue(owned);await command.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
    Check((long)(await Scalar("SELECT count(*) FROM suite.suite_licenses WHERE license_id=ANY($1)",(object)owned))! ==0,"All synthetic identities must be removed.");
    Console.WriteLine(JsonSerializer.Serialize(new{status=succeeded?"passed":"failed",timeUtc=DateTimeOffset.UtcNow,checks,syntheticIdentitiesRemoved=2,customerNotifications=notificationCount,syntheticNotificationSkipMarkersRemoved=2,securityAuditRetained=true,windowsCngHomologated=false}));
}

static string HexHash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
async Task Sql(string text,params object[] values){await using var c=db.CreateCommand(text);foreach(var value in values)c.Parameters.AddWithValue(value);await c.ExecuteNonQueryAsync();}
async Task<object?> Scalar(string text,params object[] values){await using var c=db.CreateCommand(text);foreach(var value in values)c.Parameters.AddWithValue(value);return await c.ExecuteScalarAsync();}
SessionAssertion Verify(SignedAssertionEnvelope envelope,string kind)
{
    var result=StrictJson.Parse<SessionAssertion>(Convert.FromBase64String(envelope.Payload));
    Check(envelope.Kind==kind&&result.Kind==kind&&envelope.KeyId==onlineKeyId,"Session envelope identity must match.");
    Signature(envelope,Protocol.CanonicalAssertion(result),Protocol.AssertionDomain(result));return result;
}
void Signature(SignedAssertionEnvelope envelope,byte[] canonical,string domain)
{
    Check(Convert.FromBase64String(envelope.Payload).AsSpan().SequenceEqual(canonical)&&envelope.KeyId==onlineKeyId,"Signed payload must be canonical and use the approved authority.");
    Check(online.VerifyData(Encoding.ASCII.GetBytes(domain).Concat(canonical).ToArray(),Convert.FromBase64String(envelope.Signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pss),"Production assertion signature must verify.");
}
async Task Network(SyntheticClient who,string session)
{
    var context=new NetworkInventoryContext(1,Protocol.ProductId,who.License,who.Device,session,"EMULATIONSTATION",NetworkInventoryContract.Action,who.Fingerprint,"ES-production-smoke-1.1.0",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),[new("02:11:22:33:44:55","WIRELESS",true,false)]);
    var hash=NetworkInventoryContract.Hash(context);
    using var issued=await Send(external,NetworkInventoryContract.ChallengeRoute,new NetworkChallengeRequest(1,context.ProductId,who.License,who.Device,session,context.AppScope,context.Action,hash),"NETWORK");
    issued.EnsureSuccessStatusCode();var envelope=(await issued.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;
    var challenge=StrictJson.Parse<NetworkAssertion>(Convert.FromBase64String(envelope.Payload));
    Check(envelope.Kind==NetworkInventoryContract.ChallengeKind&&challenge.ContextHash==hash,"Network challenge must bind the report.");
    Signature(envelope,NetworkInventoryContract.Canonical(challenge),NetworkInventoryContract.ChallengeDomain);
    var bytes=NetworkInventoryContract.SigningMessage(new(1,context.ProductId,who.License,who.Device,session,context.AppScope,context.Action,hash,challenge.ChallengeId,challenge.Nonce,challenge.ExpiresAtUnixSeconds));
    var proof=new NetworkInventoryProof(context,challenge.ChallengeId,Convert.ToBase64String(who.Key.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));
    using var accepted=await Send(external,NetworkInventoryContract.InventoryRoute,proof,"NETWORK");accepted.EnsureSuccessStatusCode();
    var resultEnvelope=(await accepted.Content.ReadFromJsonAsync<SignedAssertionEnvelope>())!;var result=StrictJson.Parse<NetworkAssertion>(Convert.FromBase64String(resultEnvelope.Payload));
    Check(resultEnvelope.Kind==NetworkInventoryContract.ResultKind,"Network receipt must have its own kind.");Signature(resultEnvelope,NetworkInventoryContract.Canonical(result),NetworkInventoryContract.ResultDomain);
    using var replay=await Send(external,NetworkInventoryContract.InventoryRoute,proof,"NETWORK");Check((int)replay.StatusCode==409,"Network proof cannot replay.");
}
Task<HttpResponseMessage> Admin(string path,object body,string claim,bool post=false)
{
    var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
    request.Headers.Add("X-Suite-Admin-Token",adminToken);request.Headers.Add("X-Suite-Admin-Actor","es-deployment-smoke");
    request.Headers.Add("X-Suite-Admin-Claims",claim);request.Headers.Add("X-Suite-Client-Ip-Digest",HexHash(Encoding.ASCII.GetBytes("authorized-local-deployment-smoke")));
    if(post){request.Headers.Add("X-Suite-Csrf-Verified","1");request.Headers.Add("X-Suite-Step-Up-At",DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());}
    return admin.SendAsync(request);
}
