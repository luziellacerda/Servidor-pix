#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ ${EUID} -ne 0 ]]; then
  echo 'BLOCKED: execute as root.' >&2
  exit 2
fi
if [[ $# -ne 3 ]]; then
  echo 'usage: apply-suite-content-migrations.sh REPOSITORY_DIR DATABASE BACKUP_SHA256_SIDECAR' >&2
  exit 2
fi

repository=$(realpath --canonicalize-existing "$1")
database=$2
backup_evidence=$(realpath --canonicalize-existing "$3")
case "$database" in (*[!A-Za-z0-9_-]*|'') echo 'BLOCKED: invalid database name.' >&2; exit 2;; esac
[[ -s "$backup_evidence" ]] || { echo 'BLOCKED: backup evidence is empty.' >&2; exit 2; }
backup_directory=$(dirname "$backup_evidence")
backup_sidecar=$(basename "$backup_evidence")
(cd "$backup_directory" && sha256sum --check --status "$backup_sidecar") || {
  echo 'BLOCKED: backup checksum evidence is invalid.' >&2
  exit 2
}
[[ -f "$repository/ops/production/prepare-suite-content-roles.sql" ]] || exit 2

manifest="$repository/ops/production/suite-content-migrations.sha256"
[[ -f "$manifest" ]] || { echo 'BLOCKED: migration checksum manifest missing.' >&2; exit 2; }
(cd "$repository" && sha256sum --check --strict --status \
  ops/production/suite-content-migrations.sha256) || {
  echo 'BLOCKED: migration source checksum mismatch.' >&2
  exit 2
}

migrations=(010_suite_content_catalog 011_suite_content_publish_integrity \
  012_suite_content_permissions 013_suite_content_management 014_suite_content_retention)
for migration in "${migrations[@]}"; do
  migration_file="$migration.up.sql"
  [[ -f "$repository/migrations/suite/$migration_file" ]] || {
    echo "BLOCKED: missing migration $migration_file" >&2
    exit 2
  }
done

marker_count=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 \
  --tuples-only --no-align --dbname="$database" --command="SELECT count(*) FROM suite.schema_migrations WHERE version IN ('010_suite_content_catalog','011_suite_content_publish_integrity','012_suite_content_permissions','013_suite_content_management','014_suite_content_retention');")
ledger_exists=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 \
  --tuples-only --no-align --dbname="$database" --command="SELECT CASE WHEN to_regclass('suite.schema_migration_checksums') IS NULL THEN 0 ELSE 1 END;")
baseline_count=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 \
  --tuples-only --no-align --dbname="$database" --command="SELECT count(*) FROM suite.schema_migrations WHERE version IN ('001_suite_foundation','002_lifetime_single_active_device','003_admin_otp_audit','004_suite_commerce_lifecycle','005_suite_commerce_permissions','006_suite_readiness_permissions','007_suite_device_transfer_permissions','008_suite_transfer_runtime_permissions','009_suite_commerce_provision_permissions');")
total_count=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 \
  --tuples-only --no-align --dbname="$database" --command="SELECT count(*) FROM suite.schema_migrations;")
if [[ "$baseline_count" != 9 || "$total_count" != $((9 + marker_count)) ]]; then
  echo 'BLOCKED: expected exact 001-009 baseline plus known content migrations.' >&2
  exit 1
fi
if [[ "$marker_count" != 0 && "$ledger_exists" != 1 ]]; then
  echo 'BLOCKED: content migration marker exists without checksum ledger.' >&2
  exit 1
fi
if [[ "$marker_count" == 0 && "$ledger_exists" != 0 ]]; then
  echo 'BLOCKED: checksum ledger exists before migration 010.' >&2
  exit 1
fi
if [[ "$ledger_exists" == 1 ]]; then
  ledger_count=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 \
    --tuples-only --no-align --dbname="$database" --command="SELECT count(*) FROM suite.schema_migration_checksums;")
  if [[ "$ledger_count" != "$marker_count" ]]; then
    echo 'BLOCKED: migration marker/checksum cardinality mismatch.' >&2
    exit 1
  fi
fi

runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 --dbname="$database" \
  --file="$repository/ops/production/prepare-suite-content-roles.sql"
for migration in "${migrations[@]}"; do
  expected=$(awk -v path="migrations/suite/$migration.up.sql" \
    '$2 == path { print $1 }' "$manifest")
  [[ "$expected" =~ ^[0-9a-f]{64}$ ]] || {
    echo "BLOCKED: checksum missing for $migration" >&2
    exit 2
  }
  applied=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 --tuples-only --no-align \
    --dbname="$database" --command="SELECT count(*) FROM suite.schema_migrations WHERE version='$migration';")
  if [[ "$applied" == 0 ]]; then
    runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 --dbname="$database" \
      --set=migration_sha256="$expected" \
      --file="$repository/migrations/suite/$migration.up.sql"
  elif [[ "$applied" != 1 ]]; then
    echo "BLOCKED: inconsistent migration marker $migration" >&2
    exit 1
  else
    recorded=$(runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 \
      --tuples-only --no-align --dbname="$database" \
      --command="SELECT script_sha256 FROM suite.schema_migration_checksums WHERE version='$migration';")
    if [[ "$recorded" != "$expected" ]]; then
      echo "BLOCKED: migration checksum ledger mismatch for $migration" >&2
      exit 1
    fi
  fi
done

runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 --tuples-only --no-align \
  --dbname="$database" --command="SELECT count(*) FROM suite.schema_migrations WHERE version IN ('010_suite_content_catalog','011_suite_content_publish_integrity','012_suite_content_permissions','013_suite_content_management','014_suite_content_retention');" \
  | grep -qx '5' || { echo 'BLOCKED: migration verification failed.' >&2; exit 1; }
runuser -u postgres -- psql --no-psqlrc --set=ON_ERROR_STOP=1 --tuples-only --no-align \
  --dbname="$database" --command="SELECT count(*) FROM suite.schema_migration_checksums WHERE version IN ('010_suite_content_catalog','011_suite_content_publish_integrity','012_suite_content_permissions','013_suite_content_management','014_suite_content_retention');" \
  | grep -qx '5' || { echo 'BLOCKED: migration checksum ledger incomplete.' >&2; exit 1; }
echo 'OK: Suite content migrations 010-014 applied; no catalog was published.'
