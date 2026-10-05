using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using TurboRamaSuiteOnlineServer;

internal static class StationSessionConcurrencyChecks
{
    private sealed record Client(string License, string Device, RSA Key);
    public static async Task RunAsync(NpgsqlDataSource database, NpgsqlDataSource apiDatabase,
        StationService service)
    {
        await using(var guard=database.CreateCommand("SHOW data_directory"))
            if(!((string?)await guard.ExecuteScalarAsync())!.StartsWith("/tmp/pg_virtualenv.",StringComparison.Ordinal))
                throw new Exception("Session capacity fixture requires an isolated pg_virtualenv");
        var clients=new List<Client>();
        try
        {
            for(int i=0;i<64;i++)
            {
                var key=RSA.Create(2048);
                var spki=StationProtocol.Encode(key.ExportSubjectPublicKeyInfo());
                var client=new Client("STA-CAPACITY-"+Guid.NewGuid().ToString("N").ToUpperInvariant(),
                    StationProtocol.Encode(SHA256.HashData(key.ExportSubjectPublicKeyInfo())),key);
                clients.Add(client);
                // Every interpolated value is generated hex/base64url fixture data.
                await using var seed=database.CreateCommand($"""
                    INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_consumed,
                      license_term,expires_at,identity_policy,maximum_active_devices,provisioning_origin,
                      enrollment_state,claim_mode)
                    VALUES('{client.License}','TURBORAMA_STATION_ANDROID','ACTIVE',true,'LIFETIME',NULL,
                      'SOFTWARE_ONLY',1,'LEGACY_ADMIN','BOUND','FIRST_CLAIM');
                    INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,source_item_key,
                      source_product_sku,product_id,license_id,provisioning_state,financial_state,last_source_version)
                    VALUES('STATION_SESSION_CAPACITY_TEST','{client.License}','synthetic',
                      'STATION_ANDROID_LIFETIME_1_DEVICE','TURBORAMA_STATION_ANDROID','{client.License}',
                      'PROVISIONED','PAID',1);
                    INSERT INTO suite.station_devices(license_id,device_id,public_key_spki,manufacturer,
                      model,android_sdk,client_version,status)
                    VALUES('{client.License}','{client.Device}','{spki}','Synthetic','Session capacity',35,'1','ACTIVE');
                    """);
                await seed.ExecuteNonQueryAsync();
            }
            async Task<StationDeviceEnvelope> Proof(Client client)
            {
                var challenge=Payload(await service.SessionChallengeAsync(new(1,
                    StationProtocol.Prefix+"request-session-challenge/v1",StationProtocol.Product,
                    StationProtocol.Application,client.Device,"1","Synthetic","Session capacity",35,client.License),
                    CancellationToken.None));
                var bytes=JsonSerializer.SerializeToUtf8Bytes(new {
                    schemaVersion=1,domain=StationProtocol.Prefix+"open-session/v1",
                    productId=StationProtocol.Product,applicationId=StationProtocol.Application,
                    deviceId=client.Device,clientVersion="1",deviceManufacturer="Synthetic",
                    deviceModel="Session capacity",androidSdk=35,licenseId=client.License,
                    challengeId=challenge.GetProperty("challengeId").GetString(),nonce=challenge.GetProperty("nonce").GetString()
                });
                return new(StationProtocol.Encode(bytes),StationProtocol.Encode(client.Key.SignData(bytes,
                    HashAlgorithmName.SHA256,RSASignaturePadding.Pss)));
            }
            using var slots=new SemaphoreSlim(16);
            var watch=Stopwatch.StartNew();
            await Task.WhenAll(clients.Select(async client=>{
                await slots.WaitAsync();
                try
                {
                    var result=Payload(await service.OpenSessionAsync(await Proof(client),CancellationToken.None));
                    var identity=await service.Session(result.GetProperty("accessToken").GetString()!,CancellationToken.None);
                    if(identity.LicenseId!=client.License||identity.DeviceId!=client.Device)
                        throw new Exception("Parallel session changed owner");
                }
                finally{slots.Release();}
            }));
            var owner=clients[0];
            var replay=await Proof(owner);
            async Task<bool> Once()
            {
                try{await service.OpenSessionAsync(replay,CancellationToken.None);return true;}
                catch(SuiteException e)when(e.Code=="STATION_CHALLENGE_INVALID"){return false;}
            }
            var outcomes=await Task.WhenAll(Once(),Once());
            if(outcomes.Count(x=>x)!=1)throw new Exception("Concurrent challenge replay was accepted");
            var first=await Proof(owner);var second=await Proof(owner);
            await Task.WhenAll(service.OpenSessionAsync(first,CancellationToken.None),
                service.OpenSessionAsync(second,CancellationToken.None));
            await using(var active=database.CreateCommand("SELECT count(*) FROM suite.station_sessions WHERE license_id=$1 AND status='ACTIVE'"))
            {
                active.Parameters.AddWithValue(owner.License);
                if((long)(await active.ExecuteScalarAsync())! !=1)throw new Exception("Concurrent renewals left multiple active sessions");
            }
            var revoke=await Proof(owner);
            await using(var connection=await database.OpenConnectionAsync())
            await using(var transaction=await connection.BeginTransactionAsync())
            {
                await using var block=new NpgsqlCommand("UPDATE suite.suite_licenses SET status='REVOKED',revocation_generation=revocation_generation+1 WHERE license_id=$1",connection,transaction);
                block.Parameters.AddWithValue(owner.License);await block.ExecuteNonQueryAsync();
                var pending=service.OpenSessionAsync(revoke,CancellationToken.None);
                await Task.Delay(150);
                if(pending.IsCompleted)throw new Exception("Renewal did not wait for the authoritative license lock");
                await transaction.CommitAsync();
                try{await pending;throw new Exception("Session renewal survived committed license revocation");}
                catch(SuiteException e)when(e.Code=="STATION_LICENSE_DENIED"){}
            }
            Console.WriteLine(JsonSerializer.Serialize(new {passed=true,scope="Isolated PostgreSQL with real Station service, RSA proofs and API role",
                independentRenewals=64,parallelWorkers=16,seconds=watch.Elapsed.TotalSeconds,
                concurrentReplayRejected=true,singleActiveSession=true,revocationDuringLockRejected=true}));
        }
        finally{foreach(var client in clients)client.Key.Dispose();}
    }
    private static JsonElement Payload(object envelope)
    {
        var json=JsonSerializer.SerializeToElement(envelope);
        return JsonSerializer.Deserialize<JsonElement>(StationProtocol.Decode(json.GetProperty("payload").GetString()!,4096));
    }
}
