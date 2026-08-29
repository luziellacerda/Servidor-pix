<?php
declare(strict_types=1);

require_once '/home/lz-servidor/HOSTINGER SITE DOCUMENTOS/sistema2026.lzgames.com.br/public_html/turbobox/notification-lib.php';

$report = '/run/turborama-suite-content-monitor/link-alerts.json';
$phone = trim((string)getenv('TURBORAMA_CONTENT_ALERT_PHONE'));
if ($phone === '' || !is_file($report)) {
    exit(0);
}
$raw = file_get_contents($report);
$document = is_string($raw) ? json_decode($raw, true, 16, JSON_THROW_ON_ERROR) : null;
$failures = is_array($document) && is_array($document['failures'] ?? null)
    ? $document['failures'] : [];
foreach ($failures as $failure) {
    $game = trim((string)($failure['game'] ?? ''));
    $code = trim((string)($failure['code'] ?? 'LINK_OFFLINE'));
    if ($game === '' || strlen($game) > 512 || !preg_match('/^[A-Z0-9_]{2,64}$/', $code)) {
        continue;
    }
    $safeGame = str_replace(['*', '_', '~', '`'], '', $game);
    $message = "⚠️ *ALERTA TURBORAMA*\n\nO link do jogo *{$safeGame}* não respondeu no teste diário.\nResultado: {$code}\n\nVerifique o jogo no painel administrativo.";
    if (!tb_queue_whatsapp(null, 'suite_link_down', $phone, $message)) {
        fwrite(STDERR, "TURBORAMA CONTENT ALERT: fila recusou telefone\n");
        exit(2);
    }
}
echo 'TURBORAMA CONTENT ALERT: '.count($failures)." falhas processadas\n";
