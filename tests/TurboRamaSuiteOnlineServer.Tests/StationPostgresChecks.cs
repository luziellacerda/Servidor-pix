using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using TurboRamaSuiteOnlineServer;

internal static class StationPostgresChecks
{
    public static async Task RunAsync(string connection)
    {
        await using var database = NpgsqlDataSource.Create(connection);
        await using var apiDatabase = NpgsqlDataSource.Create(
            Environment.GetEnvironmentVariable("STATION_TEST_PG_API") ?? connection);
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var licenseId = "STA-TEST-" + suffix;
        var purchaseId = "TEST-" + suffix;
        var activationCode = StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
        var pepper = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var verifier = ActivationCodes.Verify(pepper, activationCode);
        await using (var create = database.CreateCommand("""
            INSERT INTO suite.suite_licenses(license_id,product_id,status,activation_verifier,
              activation_expires_at,activation_consumed,license_term,expires_at,
              identity_policy,maximum_active_devices,provisioning_origin,
              enrollment_state,claim_mode)
            VALUES($1,'TURBORAMA_STATION_ANDROID','ACTIVE',$2,
              clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,
              'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM')
            """))
        {
            create.Parameters.AddWithValue(licenseId);
            create.Parameters.AddWithValue(verifier);
            await create.ExecuteNonQueryAsync();
        }
        await using (var delivery = database.CreateCommand("""
            INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,
              source_item_key,source_product_sku,product_id,license_id,
              provisioning_state,financial_state,last_source_version)
            VALUES('STATION_TEST',$1,'station',
              'STATION_ANDROID_LIFETIME_1_DEVICE','TURBORAMA_STATION_ANDROID',$2,
              'PROVISIONED','PAID',1)
            """))
        {
            delivery.Parameters.AddWithValue(purchaseId);
            delivery.Parameters.AddWithValue(licenseId);
            await delivery.ExecuteNonQueryAsync();
        }

        using var device = RSA.Create(2048);
        using var signingKey = RSA.Create(2048);
        using var signer = new StationResponseSigner(signingKey.ExportRSAPrivateKeyPem());
        var service = new StationService(new PostgresStationStore(apiDatabase), signer,
            pepper);
        var deviceId = StationProtocol.Encode(SHA256.HashData(
            device.ExportSubjectPublicKeyInfo()));
        var spki = StationProtocol.Encode(device.ExportSubjectPublicKeyInfo());
        var activation = new StationActivationChallengeRequest(1,
            StationProtocol.Prefix + "request-activation-challenge/v1",
            StationProtocol.Product, StationProtocol.Application, deviceId,
            "1", "Synthetic", "Fixture", 35, activationCode, spki);
        var challenge = Payload(await service.ActivationChallengeAsync(activation,
            CancellationToken.None));
        var challengeId = challenge.GetProperty("challengeId").GetString()!;
        var nonce = challenge.GetProperty("nonce").GetString()!;
        var proof = Signed(device, new
        {
            schemaVersion = 1, domain = StationProtocol.Prefix + "activate/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            deviceId, clientVersion = "1", deviceManufacturer = "Synthetic",
            deviceModel = "Fixture", androidSdk = 35, activationCode,
            devicePublicKey = spki, challengeId, nonce
        });
        var activated = Payload(await service.CompleteActivationAsync(proof,
            CancellationToken.None));
        if (activated.GetProperty("licenseId").GetString() != licenseId)
            throw new Exception("Station activation returned another license.");
        try
        {
            await service.CompleteActivationAsync(proof, CancellationToken.None);
            throw new Exception("Station activation replay accepted.");
        }
        catch (SuiteException exception) when (exception.Code ==
            "STATION_CHALLENGE_INVALID") { }

        var sessionChallenge = Payload(await service.SessionChallengeAsync(
            new StationSessionChallengeRequest(1,
                StationProtocol.Prefix + "request-session-challenge/v1",
                StationProtocol.Product, StationProtocol.Application,
                deviceId, "1", "Synthetic", "Fixture", 35, licenseId),
            CancellationToken.None));
        var sessionChallengeId = sessionChallenge.GetProperty("challengeId").GetString()!;
        var sessionNonce = sessionChallenge.GetProperty("nonce").GetString()!;
        var open = Signed(device, new
        {
            schemaVersion = 1, domain = StationProtocol.Prefix + "open-session/v1",
            productId = StationProtocol.Product, applicationId = StationProtocol.Application,
            deviceId, clientVersion = "1", deviceManufacturer = "Synthetic",
            deviceModel = "Fixture", androidSdk = 35, licenseId,
            challengeId = sessionChallengeId, nonce = sessionNonce
        });
        var session = Payload(await service.OpenSessionAsync(open,
            CancellationToken.None));
        var accessToken = session.GetProperty("accessToken").GetString()!;
        if ((await service.Session(accessToken, CancellationToken.None)).LicenseId != licenseId)
            throw new Exception("Station session authorization failed.");
        try
        {
            await service.ProfileAsync(accessToken, CancellationToken.None);
            throw new Exception("Station missing buyer projection accepted.");
        }
        catch (SuiteException exception) when (exception.Code ==
            "STATION_PROFILE_NOT_READY") { }
        await using (var projection = database.CreateCommand("""
            INSERT INTO suite.station_customer_projection(license_id,source_system,
              source_purchase_id,source_item_key,customer_ref,display_name)
            VALUES($1,'STATION_TEST',$2,'station','buyer-synthetic','Pessoa Teste')
            """))
        {
            projection.Parameters.AddWithValue(licenseId);
            projection.Parameters.AddWithValue(purchaseId);
            await projection.ExecuteNonQueryAsync();
        }
        var profile = Payload(await service.ProfileAsync(accessToken,
            CancellationToken.None));
        if (profile.GetProperty("displayName").GetString() != "Pessoa Teste")
            throw new Exception("Station buyer profile mismatch.");
        await using (var revoke = database.CreateCommand("""
            UPDATE suite.suite_licenses SET revocation_generation=revocation_generation+1
            WHERE license_id=$1
            """))
        {
            revoke.Parameters.AddWithValue(licenseId);
            await revoke.ExecuteNonQueryAsync();
        }
        try
        {
            await service.Session(accessToken, CancellationToken.None);
            throw new Exception("Station revoked session accepted.");
        }
        catch (SuiteException exception) when (exception.Code ==
            "STATION_SESSION_INVALID") { }
        await ConcurrentActivationAsync(database, service, pepper);
        Console.WriteLine("STATION POSTGRES TESTS: OK (activation, concurrent limit, replay, session, buyer profile, revocation)");
    }

    private static async Task ConcurrentActivationAsync(NpgsqlDataSource database,
        StationService service, string pepper)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var licenseId = "STA-RACE-" + suffix;
        var purchaseId = "RACE-" + suffix;
        var code = StationProtocol.Encode(RandomNumberGenerator.GetBytes(32));
        var verifier = ActivationCodes.Verify(pepper, code);
        await using (var create = database.CreateCommand("""
            INSERT INTO suite.suite_licenses(license_id,product_id,status,
              activation_verifier,activation_expires_at,activation_consumed,
              license_term,expires_at,identity_policy,maximum_active_devices,
              provisioning_origin,enrollment_state,claim_mode)
            VALUES($1,'TURBORAMA_STATION_ANDROID','ACTIVE',$2,
              clock_timestamp()+interval '15 minutes',false,'LIFETIME',NULL,
              'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM')
            """))
        {
            create.Parameters.AddWithValue(licenseId);
            create.Parameters.AddWithValue(verifier);
            await create.ExecuteNonQueryAsync();
        }
        await using (var delivery = database.CreateCommand("""
            INSERT INTO suite.suite_license_deliveries(source_system,
              source_purchase_id,source_item_key,source_product_sku,
              product_id,license_id,provisioning_state,financial_state,
              last_source_version)
            VALUES('STATION_TEST',$1,'race',
              'STATION_ANDROID_LIFETIME_1_DEVICE',
              'TURBORAMA_STATION_ANDROID',$2,'PROVISIONED','PAID',1)
            """))
        {
            delivery.Parameters.AddWithValue(purchaseId);
            delivery.Parameters.AddWithValue(licenseId);
            await delivery.ExecuteNonQueryAsync();
        }
        using var first = RSA.Create(2048);
        using var second = RSA.Create(2048);
        async Task<StationDeviceEnvelope> ChallengeAndProof(RSA key)
        {
            var spki = StationProtocol.Encode(key.ExportSubjectPublicKeyInfo());
            var deviceId = StationProtocol.Encode(SHA256.HashData(
                key.ExportSubjectPublicKeyInfo()));
            var challenge = Payload(await service.ActivationChallengeAsync(
                new StationActivationChallengeRequest(1,
                    StationProtocol.Prefix + "request-activation-challenge/v1",
                    StationProtocol.Product, StationProtocol.Application,
                    deviceId, "1", "Synthetic", "Race", 35, code, spki),
                CancellationToken.None));
            return Signed(key, new
            {
                schemaVersion = 1, domain = StationProtocol.Prefix + "activate/v1",
                productId = StationProtocol.Product,
                applicationId = StationProtocol.Application,
                deviceId, clientVersion = "1", deviceManufacturer = "Synthetic",
                deviceModel = "Race", androidSdk = 35,
                activationCode = code, devicePublicKey = spki,
                challengeId = challenge.GetProperty("challengeId").GetString(),
                nonce = challenge.GetProperty("nonce").GetString()
            });
        }
        var firstProof = await ChallengeAndProof(first);
        var secondProof = await ChallengeAndProof(second);
        async Task<bool> Attempt(StationDeviceEnvelope proof)
        {
            try
            {
                await service.CompleteActivationAsync(proof, CancellationToken.None);
                return true;
            }
            catch (SuiteException exception) when (exception.Code is
                "STATION_ACTIVATION_REPLAY" or "STATION_DEVICE_ALREADY_BOUND" or
                "STATION_CHALLENGE_INVALID" or "STATION_TRANSACTION_CONFLICT")
            { return false; }
        }
        var outcome = await Task.WhenAll(Attempt(firstProof), Attempt(secondProof));
        if (outcome.Count(value => value) != 1)
            throw new Exception("Concurrent Station activation did not preserve one device.");
        await using var count = database.CreateCommand("""
            SELECT count(*) FROM suite.station_devices
            WHERE license_id=$1 AND status='ACTIVE'
            """);
        count.Parameters.AddWithValue(licenseId);
        if ((long)(await count.ExecuteScalarAsync() ?? 0L) != 1L)
            throw new Exception("Concurrent Station activation bound multiple devices.");
    }

    private static JsonElement Payload(object envelope)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(envelope));
        var bytes = StationProtocol.Decode(json.RootElement.GetProperty("payload").GetString()!,
            4096);
        using var payload = JsonDocument.Parse(bytes);
        return payload.RootElement.Clone();
    }

    private static StationDeviceEnvelope Signed(RSA key, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return new(StationProtocol.Encode(bytes), StationProtocol.Encode(key.SignData(bytes,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
    }
}
