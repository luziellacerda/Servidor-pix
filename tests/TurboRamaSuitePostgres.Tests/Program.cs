using Npgsql;
using TurboRamaSuiteOnlineServer;

var connection=Environment.GetEnvironmentVariable("SUITE_TEST_CONNECTION")??throw new InvalidOperationException("SUITE_TEST_CONNECTION missing");
await using var data=NpgsqlDataSource.Create(connection);var store=new PostgresSuiteStore(data);
await ConcurrentIdempotency();
await OldVerifierDenied();
await Retry("TS-PG-RETRY-40001","40001");
await Retry("TS-PG-RETRY-40P01","40P01");
Console.WriteLine("SUITE POSTGRES TESTS: OK (concurrent identical completion, divergent replay, stale verifier, 40001 and 40P01 retry)");

async Task ConcurrentIdempotency()
{
 var f=await Fixture("TS-PG-CONCURRENT",'a','b');var result=Envelope("same");
 var calls=new[]{store.CompleteActivationAsync(f.Challenge,new string('4',64),f.Device,result,default),store.CompleteActivationAsync(f.Challenge,new string('4',64),f.Device,result,default)};
 await Task.WhenAll(calls);if(calls.Any(x=>x.Result.Signature!="same"))throw new InvalidOperationException("concurrent idempotency failed");
 await ExpectSuite(()=>store.CompleteActivationAsync(f.Challenge,new string('5',64),f.Device,Envelope("different"),default),"REPLAY_DENIED");
}
async Task OldVerifierDenied()
{
 var f=await Fixture("TS-PG-STALE",'c','d');await using var cmd=data.CreateCommand("UPDATE suite.suite_licenses SET activation_verifier=repeat('e',64) WHERE license_id=$1");cmd.Parameters.AddWithValue(f.Device.LicenseId);await cmd.ExecuteNonQueryAsync();
 await ExpectSuite(()=>store.CompleteActivationAsync(f.Challenge,new string('6',64),f.Device,Envelope("stale"),default),"ACTIVATION_REPLAY");
}
async Task Retry(string id,string sqlState)
{
 var f=await Fixture(id,sqlState=="40001"?'f':'1',sqlState=="40001"?'2':'3');
 await using(var setup=data.CreateCommand($"DROP SEQUENCE IF EXISTS suite.retry_{sqlState}; CREATE SEQUENCE suite.retry_{sqlState}; CREATE OR REPLACE FUNCTION suite.raise_{sqlState}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.license_id='{id}' AND nextval('suite.retry_{sqlState}')<=2 THEN RAISE EXCEPTION 'synthetic' USING ERRCODE='{sqlState}'; END IF; RETURN NEW; END $$; DROP TRIGGER IF EXISTS retry_{sqlState} ON suite.suite_devices; CREATE TRIGGER retry_{sqlState} BEFORE INSERT ON suite.suite_devices FOR EACH ROW EXECUTE FUNCTION suite.raise_{sqlState}();"))await setup.ExecuteNonQueryAsync();
 var value=await store.CompleteActivationAsync(f.Challenge,sqlState=="40001"?new string('7',64):new string('8',64),f.Device,Envelope(sqlState),default);if(value.Signature!=sqlState)throw new InvalidOperationException(sqlState+" retry failed");
 await using var drop=data.CreateCommand($"DROP TRIGGER retry_{sqlState} ON suite.suite_devices; DROP FUNCTION suite.raise_{sqlState}(); DROP SEQUENCE suite.retry_{sqlState};");await drop.ExecuteNonQueryAsync();
}
async Task<(ChallengeRecord Challenge,DeviceRecord Device)> Fixture(string id,char deviceChar,char verifierChar)
{
 var device=new string(deviceChar,64);var verifier=new string(verifierChar,64);var challengeId=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
 await using(var cleanup=data.CreateCommand("DELETE FROM suite.suite_licenses WHERE license_id=$1")){cleanup.Parameters.AddWithValue(id);await cleanup.ExecuteNonQueryAsync();}
 await using(var license=data.CreateCommand("INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,activation_expires_at,activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices) VALUES($1,'TURBORAMA_SUITE','ACTIVE',$2,clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,'SOFTWARE_ONLY',1)")){license.Parameters.AddWithValue(id);license.Parameters.AddWithValue(verifier);await license.ExecuteNonQueryAsync();}
 await using(var challenge=data.CreateCommand("INSERT INTO suite.suite_challenges(challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,expires_at,activation_verifier,device_json) VALUES($1,'TURBORAMA_SUITE',$2,$3,'','device.activate',repeat('9',64),'nonce',clock_timestamp()+interval '5 minutes',$4,'{}')")){challenge.Parameters.AddWithValue(challengeId);challenge.Parameters.AddWithValue(id);challenge.Parameters.AddWithValue(device);challenge.Parameters.AddWithValue(verifier);await challenge.ExecuteNonQueryAsync();}
 return(new(challengeId,Protocol.ProductId,id,device,"","device.activate",new string('9',64),"nonce",DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),verifier,"{}"),new(id,device,"SOFTWARE_BOUND_ONLINE",new string('A',344),new string('8',64),"ACTIVE"));
}
static SignedAssertionEnvelope Envelope(string signature)=>new(1,"activation-result","rsa-pss-sha256","key",Convert.ToBase64String("{}"u8),signature);
static async Task ExpectSuite(Func<Task> action,string code){try{await action();}catch(SuiteException ex)when(ex.Code==code){return;}throw new InvalidOperationException(code+" was not raised");}
