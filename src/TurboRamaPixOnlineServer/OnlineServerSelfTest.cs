using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class OnlineServerSelfTest
{
    public static async Task<int> RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "turborama-online-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var integrityKey = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var repository = new OnlineStateRepository(Path.Combine(root, "state.json"), integrityKey);
            var concurrentRepositoryDenied = false;
            try { _ = new OnlineStateRepository(Path.Combine(root, "state.json"), integrityKey); }
            catch (InvalidOperationException) { concurrentRepositoryDenied = true; }
            Require(concurrentRepositoryDenied, "estado aceitou dois processos simultaneos");
            var activation = repository.CreateLicense("CLI-0018", "TR-000125",
                OnlineProtectionProfile.SoftwareBoundOnline, 1);
            repository.SetPackagePrices("TR-000125", new Dictionary<int, long>
            {
                [15] = 750, [30] = 1_500, [45] = 2_250, [60] = 3_000, [120] = 6_000
            });
            var testToken = "APP_USR-" + new string('A', 64);
            repository.SetMercadoPagoConnection("CLI-0018", "TURBORAMATEST01", testToken);
            var decrypted = repository.GetMercadoPagoConnection("CLI-0018");
            Require(decrypted.AccessToken == testToken && decrypted.ExternalPosId == "TURBORAMATEST01",
                "cofre de credencial do servidor");
            Require(!File.ReadAllText(Path.Combine(root, "state.json")).Contains(testToken, StringComparison.Ordinal),
                "Access Token apareceu em texto aberto no estado");
            using (var validator = new MercadoPagoServerGateway(repository, 15,
                new MercadoPagoPosHandler(testToken, "TURBORAMATEST01", returnMatch: true)))
            {
                await validator.ValidateStoredConnectionAsync("CLI-0018", CancellationToken.None);
            }
            var wrongPosDenied = false;
            try
            {
                using var validator = new MercadoPagoServerGateway(repository, 15,
                    new MercadoPagoPosHandler(testToken, "TURBORAMATEST01", returnMatch: false));
                await validator.ValidateStoredConnectionAsync("CLI-0018", CancellationToken.None);
            }
            catch (SecurityException) { wrongPosDenied = true; }
            Require(wrongPosDenied, "credencial aceitou caixa Mercado Pago de outra conta");
            var providerContext = new OnlinePaymentCreateContext(1, new string('c', 64),
                "PIXGATEWAYTEST", 750, "BRL", 15,
                DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(), 900);
            using (var providerGateway = new MercadoPagoServerGateway(repository, 15,
                new MercadoPagoOrderHandler(testToken, "TURBORAMATEST01")))
            {
                var providerOrder = await providerGateway.CreateAsync("CLI-0018", providerContext,
                    "4cf6260f-9f28-4f21-ba1b-fea0ff76174d", CancellationToken.None);
                Require(providerOrder.Status == "pending" && providerOrder.ProviderOrderId == "ORDTEST123"
                    && providerOrder.QrData.StartsWith("000201", StringComparison.Ordinal),
                    "adaptador real do Mercado Pago nao interpretou a order QR");
            }
            var gateway = new FakeGateway();
            var service = new OnlineLicensingService(repository, gateway);
            using var key = RSA.Create(2048);
            var descriptor = Descriptor(key, OnlineProtectionProfile.SoftwareBoundOnline, new string('a', 64));

            var activationChallenge = await service.CreateActivationChallengeAsync(
                new OnlineActivationChallengeRequest(1, "TR-000125", activation, descriptor), CancellationToken.None);
            var secondActivationChallenge = await service.CreateActivationChallengeAsync(
                new OnlineActivationChallengeRequest(1, "TR-000125", activation, descriptor), CancellationToken.None);
            var activationHash = OnlineLicenseProtocol.ActivationContextHash("TR-000125", descriptor);
            var activationProof = new OnlineActivationProof(1, "TR-000125", activationChallenge.ChallengeId,
                descriptor, Sign(key, activationChallenge, "TR-000125", "", "device.activate", activationHash));
            var activated = await service.CompleteActivationAsync(activationProof, CancellationToken.None);
            Require(activated.Status == "ACTIVE", "ativacao");
            var reusedActivationDenied = false;
            try
            {
                await service.CompleteActivationAsync(new OnlineActivationProof(1, "TR-000125",
                    secondActivationChallenge.ChallengeId, descriptor,
                    Sign(key, secondActivationChallenge, "TR-000125", "", "device.activate", activationHash)),
                    CancellationToken.None);
            }
            catch (OnlineServerException ex) { reusedActivationDenied = ex.InternalReason == "ACTIVATION_INVALID"; }
            Require(reusedActivationDenied, "codigo de ativacao aceitou segundo desafio");

            var sessionOne = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            await OpenSession(service, key, descriptor, sessionOne);

            var copiedDiskSession = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var bindingMismatchDenied = false;
            try { await OpenSession(service, key, descriptor, copiedDiskSession, new string('b', 64)); }
            catch (OnlineServerException ex) { bindingMismatchDenied = ex.InternalReason == "MACHINE_BINDING_MISMATCH"; }
            Require(bindingMismatchDenied, "mudanca de fingerprint nao foi recusada");

            var sessionTwo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var duplicateDenied = false;
            try { await OpenSession(service, key, descriptor, sessionTwo); }
            catch (OnlineServerException ex) { duplicateDenied = ex.InternalReason == "DUPLICATE_DEVICE"; }
            Require(duplicateDenied, "clone concorrente nao foi recusado");
            Require(repository.ListDevices("TR-000125").Single().RejectedAttempts == 2,
                "tentativa de clone nao foi registrada");

            var payment = new OnlinePaymentCreateContext(1, sessionOne, "PIXSELFTEST", 750, "BRL", 15,
                DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds(), 900);
            var paymentHash = OnlineLicenseProtocol.ContextHash(payment);
            var challenge = await service.CreateOperationChallengeAsync(new OnlineChallengeRequest(1,
                "TR-000125", descriptor.DeviceId, sessionOne, "payment.create", paymentHash), CancellationToken.None);
            var proof = new OnlineOperationProof(1, "TR-000125", descriptor.DeviceId, sessionOne,
                "payment.create", paymentHash, challenge.ChallengeId,
                Sign(key, challenge, "TR-000125", sessionOne, "payment.create", paymentHash));
            var order = await service.CreateOrderAsync(new OnlinePaymentCreateProof(proof, payment), CancellationToken.None);
            Require(order.Status == "pending" && gateway.CreateCount == 1, "criacao PIX");

            var idempotentChallenge = await service.CreateOperationChallengeAsync(new OnlineChallengeRequest(1,
                "TR-000125", descriptor.DeviceId, sessionOne, "payment.create", paymentHash), CancellationToken.None);
            var idempotentProof = proof with
            {
                ChallengeId = idempotentChallenge.ChallengeId,
                Signature = Sign(key, idempotentChallenge, "TR-000125", sessionOne, "payment.create", paymentHash)
            };
            var idempotentOrder = await service.CreateOrderAsync(
                new OnlinePaymentCreateProof(idempotentProof, payment), CancellationToken.None);
            Require(idempotentOrder.ProviderOrderId == order.ProviderOrderId && gateway.CreateCount == 1,
                "idempotencia PIX");

            var replayDenied = false;
            try { await service.CreateOrderAsync(new OnlinePaymentCreateProof(proof, payment), CancellationToken.None); }
            catch (OnlineServerException ex) { replayDenied = ex.InternalReason == "CHALLENGE_INVALID"; }
            Require(replayDenied, "replay do nonce nao foi recusado");

            var tampered = payment with { AmountCents = 7500 };
            var tamperDenied = false;
            var tamperChallenge = await service.CreateOperationChallengeAsync(new OnlineChallengeRequest(1,
                "TR-000125", descriptor.DeviceId, sessionOne, "payment.create", paymentHash), CancellationToken.None);
            var tamperProof = proof with
            {
                ChallengeId = tamperChallenge.ChallengeId,
                Signature = Sign(key, tamperChallenge, "TR-000125", sessionOne, "payment.create", paymentHash)
            };
            try { await service.CreateOrderAsync(new OnlinePaymentCreateProof(tamperProof, tampered), CancellationToken.None); }
            catch (OnlineServerException ex) { tamperDenied = ex.InternalReason == "CHALLENGE_CONTEXT_MISMATCH"; }
            Require(tamperDenied, "alteracao de valor nao foi recusada");

            var wrongPrice = payment with { ExternalReference = "PIXWRONGPRICE", AmountCents = 751 };
            var wrongPriceHash = OnlineLicenseProtocol.ContextHash(wrongPrice);
            var wrongPriceChallenge = await service.CreateOperationChallengeAsync(new OnlineChallengeRequest(1,
                "TR-000125", descriptor.DeviceId, sessionOne, "payment.create", wrongPriceHash), CancellationToken.None);
            var wrongPriceProof = proof with
            {
                ContextHash = wrongPriceHash,
                ChallengeId = wrongPriceChallenge.ChallengeId,
                Signature = Sign(key, wrongPriceChallenge, "TR-000125", sessionOne,
                    "payment.create", wrongPriceHash)
            };
            var wrongPriceDenied = false;
            try { await service.CreateOrderAsync(new OnlinePaymentCreateProof(wrongPriceProof, wrongPrice), CancellationToken.None); }
            catch (OnlineServerException ex) { wrongPriceDenied = ex.InternalReason == "PRICE_MISMATCH"; }
            Require(wrongPriceDenied && gateway.CreateCount == 1, "servidor aceitou preco fora da tabela");

            repository.ForceReauthentication("TR-000125", descriptor.DeviceId);
            var forcedReauthDenied = false;
            try
            {
                await service.CreateOperationChallengeAsync(new OnlineChallengeRequest(1, "TR-000125",
                    descriptor.DeviceId, sessionOne, "payment.read", paymentHash), CancellationToken.None);
            }
            catch (OnlineServerException ex) { forcedReauthDenied = ex.InternalReason == "SESSION_EXPIRED"; }
            Require(forcedReauthDenied, "FORCE_REAUTH nao encerrou a sessao");

            Console.WriteLine("SELF-TEST SERVIDOR ONLINE: OK (ativacao de uso unico, prova RSA-PSS, sessao exclusiva, clone registrado, original preservada, tabela de precos, validacao segura do caixa Mercado Pago, cobranca, idempotencia, anti-replay e reautenticacao remota).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SELF-TEST SERVIDOR ONLINE: FALHOU - " + ex.Message);
            return 20;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(integrityKey);
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task OpenSession(OnlineLicensingService service, RSA key,
        OnlineDeviceDescriptor descriptor, string sessionId, string? hardwareFingerprint = null)
    {
        var context = new OnlineSessionContext(1, sessionId, hardwareFingerprint ?? descriptor.HardwareFingerprint,
            descriptor.AgentVersion);
        var hash = OnlineLicenseProtocol.ContextHash(context);
        var challenge = await service.CreateOperationChallengeAsync(new OnlineChallengeRequest(1,
            "TR-000125", descriptor.DeviceId, sessionId, "session.open", hash), CancellationToken.None);
        var proof = new OnlineOperationProof(1, "TR-000125", descriptor.DeviceId, sessionId,
            "session.open", hash, challenge.ChallengeId,
            Sign(key, challenge, "TR-000125", sessionId, "session.open", hash));
        await service.CompleteSessionAsync(new OnlineSessionProof(proof, context), CancellationToken.None);
    }

    private static OnlineDeviceDescriptor Descriptor(RSA key, OnlineProtectionProfile profile, string hardware)
    {
        var spki = key.ExportSubjectPublicKeyInfo();
        try
        {
            return new OnlineDeviceDescriptor(1, OnlineLicenseProtocol.DeviceIdFromSpki(spki),
                OnlineProtectionProfileCodec.Format(profile), OnlineLicenseProtocol.SigningAlgorithm,
                Convert.ToBase64String(spki), hardware, "25.0.0.0");
        }
        finally { CryptographicOperations.ZeroMemory(spki); }
    }

    private static string Sign(RSA key, OnlineChallengeResponse challenge, string licenseId,
        string sessionId, string action, string contextHash)
    {
        var message = OnlineLicenseProtocol.BuildSigningMessage(challenge, licenseId,
            OnlineLicenseProtocol.DeviceIdFromSpki(key.ExportSubjectPublicKeyInfo()), sessionId, action, contextHash);
        var signature = key.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        try { return Convert.ToBase64String(signature); }
        finally { CryptographicOperations.ZeroMemory(message); CryptographicOperations.ZeroMemory(signature); }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private sealed class FakeGateway : IPixPaymentGateway
    {
        public bool IsReady => true;
        public int CreateCount { get; private set; }
        public Task<OnlineOrderResponse> CreateAsync(string customerId, OnlinePaymentCreateContext context,
            string idempotencyKey, CancellationToken token)
        {
            Require(Guid.TryParseExact(idempotencyKey, "D", out _), "chave de idempotencia do provedor");
            CreateCount++;
            return Task.FromResult(new OnlineOrderResponse(1, "turborama-online", context.ExternalReference,
                context.AmountCents, "BRL", "ORDER-" + context.ExternalReference,
                "00020126580014br.gov.bcb.pix-selftest-turborama", "pending"));
        }
        public Task<OnlineOrderResponse> ReadAsync(string customerId, OnlinePaymentReadContext context, CancellationToken token)
            => Task.FromResult(new OnlineOrderResponse(1, "turborama-online", context.ExternalReference,
                context.AmountCents, "BRL", context.ProviderOrderId, "", "approved"));
    }

    private sealed class MercadoPagoPosHandler : HttpMessageHandler
    {
        private readonly string _expectedToken;
        private readonly string _expectedExternalPosId;
        private readonly bool _returnMatch;

        public MercadoPagoPosHandler(string expectedToken, string expectedExternalPosId, bool returnMatch)
        {
            _expectedToken = expectedToken;
            _expectedExternalPosId = expectedExternalPosId;
            _returnMatch = returnMatch;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Require(request.Method == HttpMethod.Get, "validacao Mercado Pago usou metodo incorreto");
            Require(request.RequestUri?.AbsolutePath == "/pos", "validacao Mercado Pago usou endpoint incorreto");
            Require(request.RequestUri?.Query == "?external_id=" + _expectedExternalPosId,
                "validacao Mercado Pago alterou ExternalPosId");
            Require(request.Headers.Authorization?.Scheme == "Bearer"
                && request.Headers.Authorization.Parameter == _expectedToken,
                "validacao Mercado Pago nao enviou credencial protegida");
            var returnedId = _returnMatch ? _expectedExternalPosId : "OUTROCAIXA01";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"paging\":{\"total\":1},\"results\":[{\"id\":1,\"external_id\":\""
                    + returnedId + "\"}]}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class MercadoPagoOrderHandler : HttpMessageHandler
    {
        private readonly string _expectedToken;
        private readonly string _expectedExternalPosId;

        public MercadoPagoOrderHandler(string expectedToken, string expectedExternalPosId)
        {
            _expectedToken = expectedToken;
            _expectedExternalPosId = expectedExternalPosId;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Require(request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/v1/orders",
                "adaptador Mercado Pago usou endpoint incorreto");
            Require(request.Headers.Authorization?.Scheme == "Bearer"
                && request.Headers.Authorization.Parameter == _expectedToken,
                "adaptador Mercado Pago nao enviou credencial protegida");
            Require(request.Headers.TryGetValues("X-Idempotency-Key", out var idempotencyValues)
                && idempotencyValues.Single() == "4cf6260f-9f28-4f21-ba1b-fea0ff76174d",
                "adaptador Mercado Pago alterou a idempotencia");
            using var requestJson = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            var root = requestJson.RootElement;
            Require(root.GetProperty("type").GetString() == "qr"
                && root.GetProperty("total_amount").GetString() == "7.50"
                && root.GetProperty("external_reference").GetString() == "PIXGATEWAYTEST",
                "adaptador Mercado Pago alterou a cobranca");
            var qr = root.GetProperty("config").GetProperty("qr");
            Require(qr.GetProperty("mode").GetString() == "dynamic"
                && qr.GetProperty("external_pos_id").GetString() == _expectedExternalPosId,
                "adaptador Mercado Pago alterou o caixa ou modo QR");
            Require(root.GetProperty("transactions").GetProperty("payments")[0]
                .GetProperty("amount").GetString() == "7.50",
                "adaptador Mercado Pago alterou o valor da transacao");

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"ORDTEST123\",\"external_reference\":\"PIXGATEWAYTEST\","
                    + "\"total_amount\":\"7.50\",\"currency\":\"BRL\",\"status\":\"created\","
                    + "\"type_response\":{\"qr_data\":\"0002010102124365TURBORAMA-SELFTEST-QR-DYNAMIC\"}}",
                    Encoding.UTF8, "application/json")
            };
        }
    }
}
