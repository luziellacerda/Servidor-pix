<?php
declare(strict_types=1);
require_once __DIR__.'/lib.php';
require_once __DIR__.'/notification-lib.php';

const TB_STATION_ISSUE_URL_DEFAULT = 'http://127.0.0.1:5194';
const TB_STATION_TOKEN_DEFAULT = '/opt/turborama-station-20261001/secrets/station-admin.token';
const TB_STATION_PRODUCT = 'TurboRama Station Android';

function tb_station_issue_url(): string
{
    $url = trim((string)getenv('TURBOBOX_STATION_ISSUE_URL'));
    return $url !== '' ? $url : TB_STATION_ISSUE_URL_DEFAULT;
}

function tb_station_token_file(): string
{
    $path = trim((string)getenv('TURBOBOX_STATION_TOKEN_FILE'));
    return $path !== '' ? $path : TB_STATION_TOKEN_DEFAULT;
}

function tb_station_admin_token(): string
{
    $file = tb_station_token_file();
    if (!is_file($file)) {
        throw new RuntimeException('Canal Station indisponível.');
    }
    $mode = fileperms($file) & 0777;
    if (($mode & 0007) !== 0) {
        throw new RuntimeException('Canal Station indisponível.');
    }
    $token = trim((string)file_get_contents($file));
    if ($token === '') {
        throw new RuntimeException('Canal Station indisponível.');
    }
    return $token;
}

function tb_station_admin_request(string $method, string $path, ?array $body = null): array
{
    $url = rtrim(tb_station_issue_url(), '/').$path;
    $token = tb_station_admin_token();
    $headers = ['Accept: application/json', 'X-Station-Admin-Token: '.$token];
    $ch = curl_init($url);
    $opts = [
        CURLOPT_CUSTOMREQUEST => $method,
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_CONNECTTIMEOUT => 2,
        CURLOPT_TIMEOUT => 10,
        CURLOPT_PROTOCOLS => CURLPROTO_HTTP,
        CURLOPT_REDIR_PROTOCOLS => 0,
    ];
    if ($body !== null) {
        $payload = json_encode($body, JSON_THROW_ON_ERROR);
        $headers[] = 'Content-Type: application/json';
        $opts[CURLOPT_POSTFIELDS] = $payload;
    }
    $opts[CURLOPT_HTTPHEADER] = $headers;
    curl_setopt_array($ch, $opts);
    $raw = curl_exec($ch);
    $code = (int)curl_getinfo($ch, CURLINFO_RESPONSE_CODE);
    curl_close($ch);
    if ($raw === false || $code < 200) {
        throw new RuntimeException('Servidor de senhas Station indisponível.');
    }
    $decoded = json_decode((string)$raw, true, 8);
    if (!is_array($decoded)) {
        throw new RuntimeException('Servidor de senhas Station indisponível.');
    }
    return [$code, $decoded];
}

function tb_station_reason_label(?string $reason): string
{
    return match ($reason) {
        'ALREADY_ACTIVATED' => 'Já ativada neste aparelho',
        'NOT_PAID' => 'Pagamento ou provisão incompletos',
        'SUSPENDED' => 'Licença suspensa',
        'NOT_ACTIVE' => 'Licença inativa',
        'NOT_PENDING' => 'Fora de matrícula pendente',
        default => 'Indisponível',
    };
}

function tb_station_schema(PDO $db): void
{
    $db->exec("CREATE TABLE IF NOT EXISTS station_contacts (
        license_id TEXT PRIMARY KEY,
        user_id INTEGER,
        phone TEXT NOT NULL DEFAULT '',
        display_name TEXT NOT NULL DEFAULT '',
        last_issue_kind TEXT NOT NULL DEFAULT '',
        last_issued_at TEXT,
        activation_notified_at TEXT,
        created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
    )");
}

function tb_station_ttl_label(bool $firstIssue, ?int $ttlMinutes = null): string
{
    if ($firstIssue || $ttlMinutes === 2880) {
        return '48 horas';
    }
    return '30 minutos';
}

function tb_station_is_test_license(array $row): bool
{
    $id = strtoupper(trim((string)($row['licenseId'] ?? $row['license_id'] ?? '')));
    $ref = strtolower(trim((string)($row['customerRef'] ?? $row['customer_ref'] ?? '')));
    $name = strtolower(trim((string)($row['displayName'] ?? $row['display_name'] ?? '')));
    return !empty($row['isTest'])
        || $ref === 'teste-station'
        || $name === 'teste station';
}

function tb_station_format_expiry(string $utc): string
{
    $utc = trim($utc);
    if ($utc === '') {
        return '';
    }
    try {
        $dt = new DateTimeImmutable($utc);
        return $dt->setTimezone(new DateTimeZone('America/Maceio'))->format('d/m/Y H:i').' (horário de Maceió)';
    } catch (Throwable) {
        return $utc;
    }
}

function tb_station_support_phone(): string
{
    $raw = trim((string)getenv('TURBOBOX_WHATSAPP_SENDER'));
    $digits = preg_replace('/\D/', '', $raw) ?? '';
    if ($digits === '') {
        $digits = '5582993474007';
    }
    if (str_starts_with($digits, '55')) {
        $local = substr($digits, 2);
    } else {
        $local = $digits;
    }
    if (strlen($local) === 11) {
        return '('.substr($local, 0, 2).') '.substr($local, 2, 5).'-'.substr($local, 7);
    }
    return $local;
}

function tb_station_mask_license(string $licenseId): string
{
    $licenseId = trim($licenseId);
    if (strlen($licenseId) <= 8) {
        return $licenseId;
    }
    return 'STA-••••'.substr($licenseId, -4);
}

function tb_station_contact(PDO $db, string $licenseId): ?array
{
    tb_station_schema($db);
    $q = $db->prepare('SELECT * FROM station_contacts WHERE license_id=?');
    $q->execute([$licenseId]);
    $row = $q->fetch();
    return is_array($row) ? $row : null;
}

function tb_station_save_contact(PDO $db, string $licenseId, string $phone, ?int $userId, string $name, string $kind): void
{
    tb_station_schema($db);
    $e164 = tb_phone_e164($phone) ?? '';
    $db->prepare("INSERT INTO station_contacts(license_id,user_id,phone,display_name,last_issue_kind,last_issued_at)
        VALUES(?,?,?,?,?,CURRENT_TIMESTAMP)
        ON CONFLICT(license_id) DO UPDATE SET
          user_id=excluded.user_id,
          phone=excluded.phone,
          display_name=excluded.display_name,
          last_issue_kind=excluded.last_issue_kind,
          last_issued_at=CURRENT_TIMESTAMP")->execute([
        $licenseId,
        $userId,
        $e164,
        $name,
        $kind,
    ]);
}

function tb_station_guess_customer(PDO $db, array $row): array
{
    $ref = trim((string)($row['customerRef'] ?? ''));
    $name = trim((string)($row['displayName'] ?? ''));
    $purchase = trim((string)($row['sourcePurchaseId'] ?? ''));
    if ($ref !== '') {
        $q = $db->prepare("SELECT id,name,phone FROM users WHERE role='customer' AND status='active' AND (lower(email)=lower(?) OR phone=? OR replace(replace(phone,'+',''),'-','')=?) LIMIT 1");
        $q->execute([$ref, $ref, preg_replace('/\D/', '', $ref)]);
        $found = $q->fetch();
        if (is_array($found)) {
            return $found;
        }
    }
    if ($purchase !== '') {
        $q = $db->prepare("SELECT u.id,u.name,u.phone FROM users u JOIN payment_orders o ON o.user_id=u.id WHERE o.public_id=? LIMIT 1");
        $q->execute([$purchase]);
        $found = $q->fetch();
        if (is_array($found)) {
            return $found;
        }
    }
    if ($name !== '' && strcasecmp($name, 'Teste Station') !== 0) {
        $q = $db->prepare("SELECT id,name,phone FROM users WHERE role='customer' AND status='active' AND name=?");
        $q->execute([$name]);
        $rows = $q->fetchAll();
        if (count($rows) === 1) {
            return $rows[0];
        }
    }
    return ['id' => null, 'name' => $name, 'phone' => ''];
}

function tb_station_resolve_recipient(PDO $db, string $licenseId, string $postedPhone, array $licenseRow): array
{
    $postedPhone = trim($postedPhone);
    $contact = tb_station_contact($db, $licenseId);
    $guess = tb_station_guess_customer($db, $licenseRow);
    $name = trim((string)($licenseRow['displayName'] ?? ''));
    if ($name === '') {
        $name = trim((string)($guess['name'] ?? ''));
    }
    if ($name === '' && is_array($contact)) {
        $name = trim((string)($contact['display_name'] ?? ''));
    }
    $userId = isset($guess['id']) && $guess['id'] !== null && $guess['id'] !== '' ? (int)$guess['id'] : null;
    if ($userId === null && is_array($contact) && $contact['user_id'] !== null && $contact['user_id'] !== '') {
        $userId = (int)$contact['user_id'];
    }
    $phone = $postedPhone;
    if (tb_phone_e164($phone) === null && is_array($contact)) {
        $phone = (string)($contact['phone'] ?? '');
    }
    if (tb_phone_e164($phone) === null) {
        $phone = (string)($guess['phone'] ?? '');
    }
    return [
        'phone' => $phone,
        'userId' => $userId,
        'name' => $name !== '' ? $name : 'Cliente Station',
    ];
}

function tb_station_issue_payload(array $d): array
{
    return [
        'name' => (string)($d['name'] ?? ''),
        'product' => TB_STATION_PRODUCT,
        'license' => (string)($d['license'] ?? ''),
        'code' => (string)($d['code'] ?? ''),
        'ttl' => (string)($d['ttl'] ?? ''),
        'expires' => (string)($d['expires'] ?? ''),
        'order' => (string)($d['order'] ?? ''),
        'value' => (string)($d['value'] ?? ''),
        'sku' => (string)($d['sku'] ?? 'STATION_ANDROID_LIFETIME_1_DEVICE'),
        'support' => tb_station_support_phone(),
    ];
}

function tb_station_queue_issue_whatsapp(PDO $db, array $d): bool
{
    if (tb_station_is_test_license($d) || tb_station_is_test_license(['licenseId' => (string)($d['license'] ?? ''), 'customerRef' => (string)($d['customerRef'] ?? '')])) {
        return false;
    }
    $phone = (string)($d['phone'] ?? '');
    if (!tb_phone_e164($phone)) {
        return false;
    }
    $first = !empty($d['firstIssue']);
    $type = $first ? 'station_issued_first' : 'station_issued_reissue';
    $payload = tb_station_issue_payload($d);
    $message = tb_wa_message($type, $payload);
    $userId = isset($d['userId']) && $d['userId'] !== null && $d['userId'] !== '' ? (int)$d['userId'] : null;
    $queued = tb_queue_whatsapp($userId, $type, $phone, $message);
    if ($queued) {
        tb_station_save_contact(
            $db,
            (string)($d['license'] ?? ''),
            $phone,
            $userId,
            (string)($d['name'] ?? ''),
            $first ? 'first' : 'reissue'
        );
    }
    return $queued;
}

function tb_station_queue_activated_whatsapp(PDO $db, array $contact, array $licenseRow): bool
{
    if (tb_station_is_test_license($licenseRow) || tb_station_is_test_license($contact)) {
        return false;
    }
    $phone = (string)($contact['phone'] ?? '');
    if (!tb_phone_e164($phone)) {
        return false;
    }
    $licenseId = (string)($licenseRow['licenseId'] ?? $contact['license_id'] ?? '');
    $name = trim((string)($contact['display_name'] ?? ''));
    if ($name === '') {
        $name = trim((string)($licenseRow['displayName'] ?? 'Cliente Station'));
    }
    $userId = isset($contact['user_id']) && $contact['user_id'] !== null && $contact['user_id'] !== ''
        ? (int)$contact['user_id'] : null;
    $message = tb_wa_message('station_activated', [
        'name' => $name,
        'product' => TB_STATION_PRODUCT,
        'license' => $licenseId,
        'license_masked' => tb_station_mask_license($licenseId),
        'order' => (string)($licenseRow['sourcePurchaseId'] ?? ''),
        'support' => tb_station_support_phone(),
    ]);
    if (!tb_queue_whatsapp($userId, 'station_activated', $phone, $message)) {
        return false;
    }
    $db->prepare("UPDATE station_contacts SET activation_notified_at=CURRENT_TIMESTAMP WHERE license_id=? AND activation_notified_at IS NULL")
        ->execute([$licenseId]);
    return true;
}

function tb_station_poll_activations(): void
{
    static $last = 0;
    if (time() - $last < 20) {
        return;
    }
    $last = time();
    try {
        [$status, $body] = tb_station_admin_request('GET', '/licenses');
    } catch (Throwable) {
        return;
    }
    if ($status !== 200) {
        return;
    }
    $licenses = is_array($body['licenses'] ?? null) ? $body['licenses'] : [];
    $db = tb_db();
    tb_station_schema($db);
    foreach ($licenses as $row) {
        if (!is_array($row)) {
            continue;
        }
        $licenseId = (string)($row['licenseId'] ?? '');
        if ($licenseId === '') {
            continue;
        }
        $consumed = !empty($row['activationConsumed']);
        $enrollment = (string)($row['enrollmentState'] ?? '');
        if (!$consumed && !in_array($enrollment, ['BOUND', 'ENROLLED'], true)) {
            continue;
        }
        $contact = tb_station_contact($db, $licenseId);
        if (!is_array($contact) || (string)($contact['phone'] ?? '') === '') {
            continue;
        }
        if (!empty($contact['activation_notified_at'])) {
            continue;
        }
        tb_station_queue_activated_whatsapp($db, $contact, $row);
    }
}
