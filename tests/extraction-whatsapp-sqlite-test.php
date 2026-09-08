<?php
declare(strict_types=1);
define('TURBORAMA_EXTRACTION_WORKER_TEST', true);
require __DIR__.'/../ops/production/turborama-suite-extraction-whatsapp.php';

// Synthetic in-memory identities; no installed library, data or provider is loaded.
function tb_phone_e164(string $phone): ?string {
    return match ($phone) { 'fixture-A' => 'fixture-normalized-A', 'fixture-B' => 'fixture-normalized-B', 'fixture-blocked' => 'fixture-normalized-blocked', default => null };
}
function tb_whatsapp_recipient_blocked(string $phone): bool { return $phone === 'fixture-normalized-blocked'; }
function check_lookup(bool $condition, string $message): void { if (!$condition) throw new RuntimeException($message); }
$db = new PDO('sqlite::memory:', null, null, [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]);
$db->exec("CREATE TABLE users(id INTEGER PRIMARY KEY,name TEXT,phone TEXT,status TEXT);
CREATE TABLE purchases(id INTEGER PRIMARY KEY,user_id INTEGER,status TEXT);
CREATE TABLE payment_orders(id INTEGER PRIMARY KEY,public_id TEXT,purchase_id INTEGER,status TEXT);
INSERT INTO users VALUES(1,'Pessoa A','fixture-A','active'),(2,'Pessoa B','fixture-B','active');
INSERT INTO purchases VALUES(11,1,'paid'),(22,2,'paid');
INSERT INTO payment_orders VALUES(111,'purchase-A',11,'paid'),(222,'purchase-B',22,'paid');");
check_lookup(extraction_customer_for_purchase($db, 'purchase-A') === ['id'=>1,'name'=>'Pessoa A','phone'=>'fixture-normalized-A'], 'Account A mapping');
check_lookup(extraction_customer_for_purchase($db, 'purchase-B') === ['id'=>2,'name'=>'Pessoa B','phone'=>'fixture-normalized-B'], 'Account B mapping');
foreach (['', 'missing', str_repeat('x', 65), "purchase-A' OR 1=1 --"] as $purchase)
    check_lookup(extraction_customer_for_purchase($db, $purchase) === null, 'Untrusted purchase identifier accepted');
foreach ([
    ["UPDATE payment_orders SET status='pending' WHERE id=111", "UPDATE payment_orders SET status='paid' WHERE id=111"],
    ["UPDATE purchases SET status='pending' WHERE id=11", "UPDATE purchases SET status='paid' WHERE id=11"],
    ["UPDATE users SET status='inactive' WHERE id=1", "UPDATE users SET status='active' WHERE id=1"],
    ["UPDATE users SET phone='invalid' WHERE id=1", "UPDATE users SET phone='fixture-A' WHERE id=1"],
    ["UPDATE users SET phone='fixture-blocked' WHERE id=1", "UPDATE users SET phone='fixture-A' WHERE id=1"]
] as [$change, $restore]) {
    $db->exec($change);
    check_lookup(extraction_customer_for_purchase($db, 'purchase-A') === null, 'Ineligible recipient accepted');
    check_lookup(extraction_customer_for_purchase($db, 'purchase-B')['id'] === 2, 'Other account changed');
    $db->exec($restore);
}
$db->exec("INSERT INTO payment_orders VALUES(333,'purchase-A',22,'paid')");
check_lookup(extraction_customer_for_purchase($db, 'purchase-A') === null, 'Ambiguous purchase owner accepted');
echo "PASS: native SQLite purchase-to-owner lookup, two-account isolation, paid/active gates, normalized phone, existing recipient block and ambiguous owner. No network or real recipient.\n";
