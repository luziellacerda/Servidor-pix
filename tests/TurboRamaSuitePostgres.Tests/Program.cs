using Npgsql;
using System.Security.Cryptography;
using TurboRamaSuiteOnlineServer;

var connection=Environment.GetEnvironmentVariable("SUITE_TEST_CONNECTION")??throw new InvalidOperationException("SUITE_TEST_CONNECTION missing");
await using var data=NpgsqlDataSource.Create(connection);var store=new PostgresSuiteStore(data);
await ServiceConcurrentIdempotency();
await ConcurrentIdempotency();
await EmissionVersusCompletion();
await OldVerifierDenied();
await Retry("TS-PG-RETRY-40001","40001");
await Retry("TS-PG-RETRY-40P01","40P01");
await HeartbeatWithoutOpen();
Console.WriteLine("SUITE POSTGRES TESTS: OK (service-level PostgreSQL race, emission versus completion single authorization, concurrent identical completion, divergent replay, stale verifier, 40001 and 40P01 retry)");
async Task ServiceConcurrentIdempotency()
{
 const string id="TS-PG-SERVICE-RACE";using var machine=RSA.Create(2048);using var online=RSA.Create(2048);
 var spki=machine.ExportSubjectPublicKeyInfo();var deviceId=Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();var fingerprint=new string('a',64);
 var descriptor=new DeviceDescriptor(1,deviceId,"SOFTWARE_BOUND_ONLINE",Protocol.Algorithm,Convert.ToBase64String(spki),fingerprint,"1.0.0");
 var pepper=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var code="postgres-service-race-code";var verifier=ActivationCodes.Verify(pepper,code);
 await Cleanup(id);
 await using(var setupLicense=data.CreateCommand("INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,activation_expires_at,activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices) VALUES($1,'TURBORAMA_SUITE','ACTIVE',$2,clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,'SOFTWARE_ONLY',1)")){setupLicense.Parameters.AddWithValue(id);setupLicense.Parameters.AddWithValue(verifier);await setupLicense.ExecuteNonQueryAsync();}
 await using(var setupEnrollment=data.CreateCommand("INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint) VALUES($1,$2,'SOFTWARE_BOUND_ONLINE','SOFTWARE_ONLY','rsa-pss-sha256',$3,$4)")){setupEnrollment.Parameters.AddWithValue(id);setupEnrollment.Parameters.AddWithValue(deviceId);setupEnrollment.Parameters.AddWithValue(descriptor.PublicKeySpki);setupEnrollment.Parameters.AddWithValue(fingerprint);await setupEnrollment.ExecuteNonQueryAsync();}
 using var signer=new RsaAssertionSigner(online);var service=new SuiteService(store,signer,TimeProvider.System,pepper);
 var issued=Payload<ActivationChallengeAssertion>(await service.ActivationChallengeAsync(new(1,Protocol.ProductId,id,code,descriptor),default));
 var challenge=new ChallengeResponse(1,issued.ChallengeId,issued.Nonce,issued.ExpiresAtUnixSeconds);var context=Protocol.ActivationContextHash(id,descriptor);var message=Protocol.SigningMessage(challenge,id,deviceId,"","device.activate",context);
 string signature;try{signature=Convert.ToBase64String(machine.SignData(message,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));}finally{CryptographicOperations.ZeroMemory(message);}
 var proof=new ActivationProof(1,Protocol.ProductId,id,issued.ChallengeId,descriptor,signature);var calls=new[]{service.CompleteActivationAsync(proof,default),service.CompleteActivationAsync(proof,default)};await Task.WhenAll(calls);
 if(calls[0].Result.Signature!=calls[1].Result.Signature)throw new InvalidOperationException("service PostgreSQL race diverged");
 await ExpectSuite(()=>service.CompleteActivationAsync(proof with { Signature=Convert.ToBase64String(RandomNumberGenerator.GetBytes(256)) },default),"REPLAY_DENIED");
}
async Task HeartbeatWithoutOpen()
{
 const string id="TS-PG-HEARTBEAT-NO-OPEN";await Cleanup(id);var device=new string('0',64);
 string[] setupSql=["INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_consumed,license_term,identity_policy,maximum_active_devices,enrollment_state) VALUES($1,'TURBORAMA_SUITE','ACTIVE',true,'LIFETIME','SOFTWARE_ONLY',1,'BOUND')","INSERT INTO suite.suite_license_enrollments(license_id,device_id,binding_type,identity_policy,algorithm,public_key_spki,hardware_fingerprint) VALUES($1,$2,'SOFTWARE_BOUND_ONLINE','SOFTWARE_ONLY','rsa-pss-sha256',repeat('A',344),repeat('8',64))","INSERT INTO suite.suite_devices(license_id,device_id,binding_type,public_key_spki,hardware_fingerprint,status,algorithm) VALUES($1,$2,'SOFTWARE_BOUND_ONLINE',repeat('A',344),repeat('8',64),'ACTIVE','rsa-pss-sha256')","INSERT INTO suite.suite_challenges(challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,expires_at,revocation_generation) VALUES(repeat('b',64),'TURBORAMA_SUITE',$1,$2,repeat('c',64),'session.heartbeat',repeat('d',64),'nonce',clock_timestamp()+interval '5 minutes',0)"];foreach(var sql in setupSql){await using var setup=data.CreateCommand(sql);setup.Parameters.AddWithValue(id);if(sql.Contains("$2"))setup.Parameters.AddWithValue(device);await setup.ExecuteNonQueryAsync();}
 var challenge=new ChallengeRecord(new string('b',64),Protocol.ProductId,id,device,new string('c',64),"session.heartbeat",new string('d',64),"nonce",DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),null,null);await ExpectSuite(()=>store.CompleteSessionAsync(challenge,new(id,device,new string('c',64),"ACTIVE",DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds(),DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0),"session.heartbeat",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),default),"SESSION_INVALID");await using var check=data.CreateCommand("SELECT count(*) FROM suite.suite_sessions WHERE license_id=$1");check.Parameters.AddWithValue(id);if((long)(await check.ExecuteScalarAsync()??-1L)!=0)throw new InvalidOperationException("heartbeat inserted a session");
}
async Task Cleanup(string id)
{
 string[] statements=[
  "DELETE FROM suite.suite_activation_completions WHERE challenge_id IN (SELECT challenge_id FROM suite.suite_challenges WHERE license_id=$1)",
  "DELETE FROM suite.suite_sessions WHERE license_id=$1", "DELETE FROM suite.suite_devices WHERE license_id=$1",
  "DELETE FROM suite.suite_license_enrollments WHERE license_id=$1", "DELETE FROM suite.suite_challenges WHERE license_id=$1",
  "DELETE FROM suite.suite_audit_events WHERE license_id=$1", "DELETE FROM suite.suite_transfer_history WHERE license_id=$1",
  "DELETE FROM suite.suite_lifecycle_commands WHERE license_id=$1", "DELETE FROM suite.suite_license_deliveries WHERE license_id=$1",
  "DELETE FROM suite.suite_licenses WHERE license_id=$1"];
 foreach(var sql in statements){await using var cmd=data.CreateCommand(sql);cmd.Parameters.AddWithValue(id);await cmd.ExecuteNonQueryAsync();}
}
static T Payload<T>(SignedAssertionEnvelope envelope) where T:class=>StrictJson.Parse<T>(Convert.FromBase64String(envelope.Payload));
async Task EmissionVersusCompletion()
{
 var f=await Fixture("TS-PG-ISSUE-COMPLETE",'6','7');var locked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 var emission=Task.Run(async()=>{await using var conn=await data.OpenConnectionAsync();await using var tx=await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);await using var read=new NpgsqlCommand("SELECT activation_consumed FROM suite.suite_licenses WHERE license_id=$1 FOR UPDATE",conn,tx);read.Parameters.AddWithValue(f.Device.LicenseId);var consumed=(bool)(await read.ExecuteScalarAsync()??true);locked.TrySetResult();await release.Task;if(consumed){await tx.RollbackAsync();return false;}await using var update=new NpgsqlCommand("UPDATE suite.suite_licenses SET activation_verifier=repeat('8',64),activation_expires_at=clock_timestamp()+interval '15 minutes' WHERE license_id=$1",conn,tx);update.Parameters.AddWithValue(f.Device.LicenseId);await update.ExecuteNonQueryAsync();await tx.CommitAsync();return true;});
 await locked.Task.WaitAsync(TimeSpan.FromSeconds(5));var completion=store.CompleteActivationAsync(f.Challenge,new string('9',64),f.Device,Envelope("completion"),default);release.TrySetResult();
 if(!await emission)throw new InvalidOperationException("synthetic emission did not authorize");await ExpectSuite(async()=>{_ = await completion;},"ACTIVATION_REPLAY");
 await using var check=data.CreateCommand("SELECT activation_consumed,(SELECT count(*) FROM suite.suite_devices WHERE license_id=$1) FROM suite.suite_licenses WHERE license_id=$1");check.Parameters.AddWithValue(f.Device.LicenseId);await using var row=await check.ExecuteReaderAsync();await row.ReadAsync();if(row.GetBoolean(0)||row.GetInt64(1)!=0)throw new InvalidOperationException("emission/completion double authorization");
}



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
 await Cleanup(id);
 await using(var license=data.CreateCommand("INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,activation_expires_at,activation_consumed,license_term,expires_at,identity_policy,maximum_active_devices) VALUES($1,'TURBORAMA_SUITE','ACTIVE',$2,clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,'SOFTWARE_ONLY',1)")){license.Parameters.AddWithValue(id);license.Parameters.AddWithValue(verifier);await license.ExecuteNonQueryAsync();}
 await using(var challenge=data.CreateCommand("INSERT INTO suite.suite_challenges(challenge_id,product_id,license_id,device_id,session_id,action,context_hash,nonce,expires_at,activation_verifier,device_json,activation_generation,revocation_generation) VALUES($1,'TURBORAMA_SUITE',$2,$3,'','device.activate',repeat('9',64),'nonce',clock_timestamp()+interval '5 minutes',$4,'{}',0,0)")){challenge.Parameters.AddWithValue(challengeId);challenge.Parameters.AddWithValue(id);challenge.Parameters.AddWithValue(device);challenge.Parameters.AddWithValue(verifier);await challenge.ExecuteNonQueryAsync();}
 return(new(challengeId,Protocol.ProductId,id,device,"","device.activate",new string('9',64),"nonce",DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),verifier,"{}"),new(id,device,"SOFTWARE_BOUND_ONLINE",new string('A',344),new string('8',64),"ACTIVE"));
}
static SignedAssertionEnvelope Envelope(string signature)=>new(1,"activation-result","rsa-pss-sha256","key",Convert.ToBase64String("{}"u8),signature);
static async Task ExpectSuite(Func<Task> action,string code){try{await action();}catch(SuiteException ex)when(ex.Code==code){return;}throw new InvalidOperationException(code+" was not raised");}
