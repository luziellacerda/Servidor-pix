using TurboRamaSuiteNotifications;

CompletionProtocolChecks.Run();
DownloadCompletionChecks.Run();

var completed = new DateTimeOffset(2026, 9, 8, 9, 31, 45, TimeSpan.FromHours(-3));
var data = new ExtractionCompletionMessageData("João Silva", "Conteúdo de teste",
    "Emuladores", completed, "TS-0123456789AB");
var sendTime = completed.AddMinutes(2);
var messages = Enumerable.Range(0, 10)
    .Select(variant => ExtractionCompletionMessage.Format(data, sendTime, variant)).ToArray();
Check(messages.Distinct().Count() == 10, "As dez aberturas não são distintas.");
foreach (var message in messages)
{
    Check(message.Contains("LZ GAMES | TURBORAMA SUITE")
        && message.Contains("Equipe LZ Games") && message.Contains("João")
        && message.Contains("Bom dia") && !message.Contains("Silva"), "Marca/nome/saudação.");
    Check(message.Contains("08/09/2026 às 09:31:45")
        && message.Contains("TS-0123456789AB") && message.Contains("arquivos verificados"), "Dados técnicos.");
    Check(message.Length < 1600 && !message.Contains('\r'), "Formato ou tamanho da mensagem.");
}
Check(messages.Select(message => message[message.IndexOf("✅ *DOWNLOAD", StringComparison.Ordinal)..])
    .Distinct().Count() == 1, "A escolha da abertura alterou os dados técnicos.");
foreach (var (hour, minute, expected) in new[]
{
    (0, 0, "Boa noite"), (4, 59, "Boa noite"), (5, 0, "Bom dia"),
    (11, 59, "Bom dia"), (12, 0, "Boa tarde"), (17, 59, "Boa tarde"),
    (18, 0, "Boa noite"), (23, 59, "Boa noite")
})
{
    var local = new DateTimeOffset(2026, 9, 8, hour, minute, 0, TimeSpan.FromHours(-3));
    Check(ExtractionCompletionMessage.Greeting(local.ToUniversalTime()) == expected, "Limite de horário/fuso.");
}
var delayed = ExtractionCompletionMessage.Format(data, completed.AddHours(10), 0);
Check(delayed.Contains("Boa noite") && delayed.Contains("09:31:45"), "Saudação deve usar envio, não conclusão.");
Check(ExtractionCompletionMessage.Format(data, sendTime, 0) == messages[0], "Renderização não é determinística.");
var safe = ExtractionCompletionMessage.Format(data with
{
    CustomerName = "*João*\nSilva", ContentName = "Arquivo\r\n*Aviso*\u202E.txt"
}, sendTime, 0);
Check(!safe.Contains("*João*") && !safe.Contains("*Aviso*") && !safe.Contains('\u202E'), "Injeção de formatação.");
foreach (var invalid in new[]
{
    data with { CustomerName = "   " }, data with { ContentName = "***" },
    data with { Protocol = "bad\nprotocol" }, data with { CompletedAt = sendTime.AddDays(1) }
}) ExpectInvalid(() => ExtractionCompletionMessage.Format(invalid, sendTime, 0));
ExpectInvalid(() => ExtractionCompletionMessage.Format(data, sendTime, -1));
ExpectInvalid(() => ExtractionCompletionMessage.Format(data, sendTime, 10));
for (var index = 0; index < 1000; index++)
    Check(ExtractionCompletionMessage.SelectVariant() is >= 0 and < 10, "Sorteio fora do intervalo.");
var forbidden = new[] { "Phone", "Ip", "Mac", "LicenseId", "DeviceId", "Path" };
Check(!typeof(ExtractionCompletionMessageData).GetProperties().Any(property =>
    forbidden.Contains(property.Name, StringComparer.OrdinalIgnoreCase)), "Campo pessoal indevido no modelo.");
Console.WriteLine("PASS: 10 aberturas, marca, horários UTC-3, dados técnicos, privacidade e sanitização.");
Console.WriteLine(messages[0]);

static void Check(bool result, string message)
{ if (!result) throw new InvalidOperationException(message); }
static void ExpectInvalid(Action action)
{
    try { action(); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException("Entrada inválida aceita.");
}
