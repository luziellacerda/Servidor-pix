#!/usr/bin/env python3
"""Cria uma licença Station de teste ou emite outro código se o anterior venceu.

Não imprime pepper, DSN nem chave. Não mexe na unit 5190 nem no admin 5191.
O código dura 15 minutos. O primeiro print é a única cópia.
"""
import base64
import hashlib
import hmac
import os
import secrets
import subprocess
import tempfile

PEPPER = os.environ.get(
    "STATION_ADMIN_PEPPER_FILE",
    "/etc/turborama-suite/station-activation-pepper",
)
DATABASE = os.environ.get("STATION_TEST_DATABASE", "postgres")
SYSTEM = "TURBOBOX_V1"
PURCHASE = "station-teste-20261001"
ITEM = "android-1"
EVENT = "c0ffee20261001aa0000000000000001"
CUSTOMER = "teste-station"
NAME = "Teste Station"
SKU = "STATION_ANDROID_LIFETIME_1_DEVICE"
PRODUCT = "TURBORAMA_STATION_ANDROID"


def psql(sql):
    result = subprocess.run(
        ["sudo", "-u", "postgres", "psql", "-d", DATABASE, "-v", "ON_ERROR_STOP=1",
         "-t", "-A", "-f", "-"],
        input=sql.encode(),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        error = result.stderr.decode("utf-8", "replace")
        raise SystemExit(error[-800:])
    return result.stdout.decode().strip()


def main():
    pepper = base64.b64decode(open(PEPPER, encoding="utf-8").read().strip())
    if len(pepper) < 32:
        raise SystemExit("pepper Station curto")
    existing = psql(
        "SELECT coalesce(license_id,'') || '|' || "
        "coalesce(to_char(activation_expires_at AT TIME ZONE 'UTC', "
        "'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"'),'') "
        "FROM suite.suite_license_deliveries d "
        "JOIN suite.suite_licenses l ON l.license_id=d.license_id "
        f"WHERE d.source_system='{SYSTEM}' AND d.source_purchase_id='{PURCHASE}' "
        f"AND d.source_item_key='{ITEM}' AND d.product_id='{PRODUCT}' "
        "AND l.activation_verifier IS NOT NULL "
        "AND l.activation_expires_at>clock_timestamp();"
    )
    if existing and not existing.startswith("|"):
        license_id, expires = existing.split("|", 1)
        print(f"licenseId={license_id}")
        print("activationCode=JA_ATIVO_NAO_REIMPRIMIVEL")
        print(f"expiresAt={expires}")
        print(f"displayName={NAME}")
        print(f"sourcePurchaseId={PURCHASE}")
        print(f"sourceItemKey={ITEM}")
        print(f"customerRef={CUSTOMER}")
        return

    code = base64.urlsafe_b64encode(secrets.token_bytes(32)).rstrip(b"=").decode()
    verifier = hmac.new(pepper, code.encode(), hashlib.sha256).hexdigest()
    digest = hashlib.sha256("\n".join([
        SYSTEM, EVENT, PURCHASE, ITEM, "1", SKU, "PURCHASE_PAID",
        "9990", "BRL", CUSTOMER, NAME,
    ]).encode()).hexdigest()
    license_id = "STA-" + secrets.token_hex(16).upper()
    request_id = "station-teste-" + secrets.token_hex(8)
    sql = f"""
BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('suite:station-teste-20261001', 0));
INSERT INTO suite.suite_licenses(license_id,product_id,status,
  activation_verifier,activation_expires_at,activation_consumed,
  license_term,expires_at,identity_policy,maximum_active_devices,
  provisioning_origin,enrollment_state,claim_mode)
SELECT '{license_id}','{PRODUCT}','ACTIVE',NULL,NULL,false,
  'LIFETIME',NULL,'SOFTWARE_ONLY',1,'COMMERCE','PENDING_ENROLLMENT','FIRST_CLAIM'
WHERE NOT EXISTS (
  SELECT 1 FROM suite.suite_license_deliveries
  WHERE source_system='{SYSTEM}' AND source_purchase_id='{PURCHASE}'
    AND source_item_key='{ITEM}' AND product_id='{PRODUCT}');
INSERT INTO suite.suite_license_deliveries(source_system,source_purchase_id,
  source_item_key,source_product_sku,product_id,license_id,
  provisioning_state,financial_state,last_source_version)
SELECT '{SYSTEM}','{PURCHASE}','{ITEM}','{SKU}','{PRODUCT}','{license_id}',
  'PROVISIONED','PAID',1
WHERE NOT EXISTS (
  SELECT 1 FROM suite.suite_license_deliveries
  WHERE source_system='{SYSTEM}' AND source_purchase_id='{PURCHASE}'
    AND source_item_key='{ITEM}' AND product_id='{PRODUCT}');
INSERT INTO suite.station_customer_projection(license_id,source_system,
  source_purchase_id,source_item_key,customer_ref,display_name)
SELECT d.license_id,'{SYSTEM}','{PURCHASE}','{ITEM}','{CUSTOMER}','{NAME}'
FROM suite.suite_license_deliveries d
WHERE d.source_system='{SYSTEM}' AND d.source_purchase_id='{PURCHASE}'
  AND d.source_item_key='{ITEM}' AND d.product_id='{PRODUCT}'
  AND NOT EXISTS (
    SELECT 1 FROM suite.station_customer_projection p
    WHERE p.license_id=d.license_id);
INSERT INTO suite.suite_commerce_inbox(source_system,source_event_id,
  source_purchase_id,source_item_key,source_version,source_product_sku,
  event_type,payload_digest,processed_at,outcome,detail_code,result_json)
SELECT '{SYSTEM}','{EVENT}','{PURCHASE}','{ITEM}',1,'{SKU}',
  'PURCHASE_PAID','{digest}',clock_timestamp(),'PROVISIONED','PROVISIONED',
  '{{"sourcePurchaseId":"{PURCHASE}","sourceItemKey":"{ITEM}","outcome":"PROVISIONED"}}'::jsonb
WHERE NOT EXISTS (
  SELECT 1 FROM suite.suite_commerce_inbox
  WHERE source_system='{SYSTEM}' AND source_event_id='{EVENT}');
UPDATE suite.suite_licenses l SET
  activation_verifier='{verifier}',
  activation_expires_at=clock_timestamp()+interval '15 minutes',
  activation_consumed=false,
  activation_generation=activation_generation+1,
  updated_at=clock_timestamp()
FROM suite.suite_license_deliveries d
WHERE d.license_id=l.license_id
  AND d.source_system='{SYSTEM}' AND d.source_purchase_id='{PURCHASE}'
  AND d.source_item_key='{ITEM}' AND d.product_id='{PRODUCT}'
  AND l.status='ACTIVE' AND l.enrollment_state='PENDING_ENROLLMENT'
  AND NOT l.activation_consumed;
INSERT INTO suite.suite_audit_events(event_type,license_id,correlation_id,
  outcome,detail_code,admin_actor,request_id)
SELECT 'STATION_CODE_ISSUED', d.license_id, '{request_id}',
  'SUCCESS','FIRST_CLAIM','station-teste','{request_id}'
FROM suite.suite_license_deliveries d
JOIN suite.suite_licenses l ON l.license_id=d.license_id
WHERE d.source_system='{SYSTEM}' AND d.source_purchase_id='{PURCHASE}'
  AND d.source_item_key='{ITEM}' AND d.product_id='{PRODUCT}'
  AND l.activation_verifier='{verifier}';
SELECT CASE WHEN l.activation_verifier='{verifier}' THEN 'OK' ELSE 'NAO_GRAVADO' END
  || '|' || d.license_id || '|' || to_char(l.activation_expires_at AT TIME ZONE 'UTC',
  'YYYY-MM-DD"T"HH24:MI:SS"Z"')
FROM suite.suite_license_deliveries d
JOIN suite.suite_licenses l ON l.license_id=d.license_id
WHERE d.source_system='{SYSTEM}' AND d.source_purchase_id='{PURCHASE}'
  AND d.source_item_key='{ITEM}' AND d.product_id='{PRODUCT}';
COMMIT;
"""
    fd, path = tempfile.mkstemp(prefix="station-teste-", suffix=".sql")
    try:
        os.write(fd, sql.encode())
        os.close(fd)
        os.chmod(path, 0o600)
        result = subprocess.run(
            ["sudo", "-u", "postgres", "psql", "-d", DATABASE, "-v", "ON_ERROR_STOP=1",
             "-t", "-A", "-f", path],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
    finally:
        try:
            os.remove(path)
        except OSError:
            pass
    if result.returncode != 0:
        raise SystemExit(result.stderr.decode("utf-8", "replace")[-800:])
    lines = [line.strip() for line in result.stdout.decode().splitlines() if line.startswith("OK|") or line.startswith("NAO_GRAVADO|")]
    if not lines or lines[-1].startswith("NAO_GRAVADO"):
        raise SystemExit("licenca nao recebeu o codigo; enrollment pode ja estar concluido")
    _, license_id, expires = lines[-1].split("|", 2)
    print(f"licenseId={license_id}")
    print(f"activationCode={code}")
    print(f"expiresAt={expires}")
    print(f"displayName={NAME}")
    print(f"sourcePurchaseId={PURCHASE}")
    print(f"sourceItemKey={ITEM}")
    print(f"customerRef={CUSTOMER}")


if __name__ == "__main__":
    main()
