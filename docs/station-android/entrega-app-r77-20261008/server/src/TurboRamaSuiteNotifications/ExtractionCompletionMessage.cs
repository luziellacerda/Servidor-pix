using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TurboRamaSuiteNotifications;

/// <summary>
/// Message data resolved by the SERVER after authenticating a completion event.
/// Customer name comes from the account; content name from the catalog and
/// category label from the fixed mapping of the authenticated category ID.
/// Deliberately carries no phone, IP, MAC, license, device ID or filesystem path.
/// This module formats text only: it does not send or authorize notifications.
/// </summary>
public sealed record ExtractionCompletionMessageData(
    string CustomerName,
    string ContentName,
    string CategoryName,
    DateTimeOffset CompletedAt,
    string Protocol);

public static class ExtractionCompletionMessage
{
    public const int VariantCount = 10;
    public const string TemplateVersion = "lz-games-extraction-completed/v1";
    private static readonly TimeSpan MessageOffset = TimeSpan.FromHours(-3);

    // Select ONCE when accepting the event. Persist this value with the outbox
    // item; a delivery retry must not select another text for the same event.
    public static int SelectVariant() => RandomNumberGenerator.GetInt32(VariantCount);

    public static string Greeting(DateTimeOffset sendTime)
    {
        var hour = sendTime.ToOffset(MessageOffset).Hour;
        return hour is >= 5 and < 12 ? "Bom dia"
            : hour is >= 12 and < 18 ? "Boa tarde" : "Boa noite";
    }

    public static string Format(ExtractionCompletionMessageData data,
        DateTimeOffset sendTime, int variant)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (variant is < 0 or >= VariantCount)
            throw new ArgumentOutOfRangeException(nameof(variant));
        if (data.CompletedAt == default || data.CompletedAt > sendTime.AddMinutes(5))
            throw new ArgumentException("Completion time is invalid.", nameof(data));
        if (data.Protocol is not { Length: >= 8 and <= 32 }
            || data.Protocol.Any(character =>
                !(character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-')))
            throw new ArgumentException("Notification protocol is invalid.", nameof(data));

        var customer = SafeText(data.CustomerName, 60);
        var firstName = customer.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var content = SafeText(data.ContentName, 180);
        var category = SafeText(data.CategoryName, 80);
        var greeting = Greeting(sendTime);
        var introduction = variant switch
        {
            0 => $"{greeting}, {firstName}! Tudo pronto por aqui. Seu conteúdo foi processado com sucesso — aproveite sua próxima experiência!",
            1 => $"Olá, {firstName}! {greeting}! Passando para trazer uma boa notícia: seu download e a descompactação terminaram. Obrigado por estar com a gente!",
            2 => $"{greeting}, {firstName}! Seu conteúdo está pronto. Cuidamos das etapas de processamento e reunimos os detalhes abaixo para você conferir.",
            3 => $"{greeting}, {firstName}! A espera terminou: seus arquivos foram descompactados e verificados com sucesso. Desejamos ótimos momentos de diversão!",
            4 => $"Olá, {firstName}! {greeting}! Concluímos o processamento do seu conteúdo. É um prazer fazer parte dos seus momentos de lazer!",
            5 => $"{greeting}, {firstName}! Temos uma boa atualização para você: tudo concluído e arquivos disponíveis na pasta de destino. Aproveite!",
            6 => $"{greeting}, {firstName}! Seu próximo momento de diversão está mais perto. O conteúdo solicitado foi processado com sucesso!",
            7 => $"Olá, {firstName}! {greeting}! Tudo certo com seus arquivos: download, descompactação e verificação concluídos. Agradecemos pela confiança!",
            8 => $"{greeting}, {firstName}! Seu conteúdo acaba de ficar pronto. Confira os detalhes da conclusão abaixo e aproveite no seu tempo!",
            9 => $"{greeting}, {firstName}! Finalizamos mais uma etapa para você: seus arquivos estão prontos na pasta de destino. Conte com a nossa equipe nessa jornada!",
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        var completed = data.CompletedAt.ToOffset(MessageOffset);
        return $"🎮 *LZ GAMES | TURBORAMA SUITE*\n\n"
            + $"👋 {introduction}\n\n"
            + "✅ *DOWNLOAD E DESCOMPACTAÇÃO CONCLUÍDOS*\n\n"
            + $"📦 *Conteúdo:* {content}\n"
            + $"🗂️ *Categoria:* {category}\n"
            + "🔎 *Integridade:* arquivos verificados\n"
            + "💾 *Gravação final:* concluída no destino configurado\n"
            + $"📅 *Conclusão:* {completed.ToString("dd/MM/yyyy 'às' HH:mm:ss", CultureInfo.InvariantCulture)} (UTC−3)\n"
            + $"🧾 *Protocolo:* {data.Protocol}\n\n"
            + "📂 Seus arquivos estão disponíveis na pasta de destino.\n\n"
            + "🤝 Obrigado por fazer parte da nossa comunidade!\n"
            + "*Equipe LZ Games 🎮*\n"
            + "_TURBORAMA SUITE · Notificação automática_";
    }

    private static string SafeText(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096)
            throw new ArgumentException("Notification text is invalid.");
        var output = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
                || Rune.IsWhiteSpace(rune))
            {
                if (output.Length > 0 && output[^1] != ' ') output.Append(' ');
                continue;
            }
            if (rune.Value is '*' or '_' or '~' or '`') continue;
            if (output.Length + rune.Utf16SequenceLength > maximumLength) break;
            output.Append(rune.ToString());
        }
        var result = output.ToString().Trim();
        return result.Length > 0 ? result
            : throw new ArgumentException("Notification text is empty.");
    }
}
