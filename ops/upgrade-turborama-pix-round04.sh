#!/usr/bin/env bash
set -euo pipefail

package="/home/lz-servidor/turborama-download/Servidor-pix/outputs/TurboRamaPixOnlineServer-portable-RODADA04-20260807.zip"
expected_sha="49bade5ee22a29d01b254d16141972249f0cee4c6c33eaf9e06624c885f7d63c"
app="/opt/turborama-pix"
state="/var/lib/turborama-pix"
config="/etc/turborama-pix"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="/var/backups/turborama-pix/round04-${stamp}"
prepared="/opt/turborama-pix.round04-new-${stamp}"
rollback="/opt/turborama-pix.rollback-${stamp}"
failed="/opt/turborama-pix.failed-${stamp}"
stage="$(mktemp -d /tmp/turborama-pix-round04.XXXXXX)"

cleanup() {
  rm -rf -- "$stage"
}
trap cleanup EXIT

actual_sha="$(sha256sum "$package" | awk '{print $1}')"
if [[ "$actual_sha" != "$expected_sha" ]]; then
  echo "ERRO: SHA-256 externo divergente." >&2
  exit 1
fi

for target in "$backup" "$prepared" "$rollback" "$failed"; do
  if [[ -e "$target" ]]; then
    echo "ERRO: destino inesperadamente existente: $target" >&2
    exit 1
  fi
done

unzip -q "$package" -d "$stage"
(
  cd "$stage"
  sha256sum --check CHECKSUMS-SHA256.txt
  dotnet TurboRamaPixOnlineServer.dll --self-test
)

install -d -m 0755 -o root -g root "$prepared"
install -m 0644 -o root -g root "$stage"/*.dll "$prepared/"
install -m 0644 -o root -g root "$stage"/*.json "$prepared/"
(
  cd "$prepared"
  sha256sum --check "$stage/CHECKSUMS-SHA256.txt"
  dotnet TurboRamaPixOnlineServer.dll --self-test
)

install -d -m 0700 -o root -g root "$backup"
cp -a "$app" "$backup/application"
cp -a "$state" "$backup/state"
cp -a "$config" "$backup/config"
cp -a /etc/systemd/system/turborama-pix.service "$backup/turborama-pix.service"
{
  printf 'created_utc=%s\n' "$stamp"
  printf 'package_sha256=%s\n' "$actual_sha"
  printf 'service_active_before=%s\n' "$(systemctl is-active turborama-pix)"
  printf 'service_enabled_before=%s\n' "$(systemctl is-enabled turborama-pix)"
  printf 'rollback_application=%s\n' "$rollback"
} > "$backup/metadata.txt"
(
  cd "$backup"
  find . -type f ! -name SHA256SUMS ! -name SHA256SUMS.verify -print0 \
    | sort -z \
    | xargs -0 sha256sum > SHA256SUMS
  sha256sum --check SHA256SUMS | tee SHA256SUMS.verify
)
chmod -R go-rwx "$backup"

systemctl stop turborama-pix
mv "$app" "$rollback"
mv "$prepared" "$app"

rollback_update() {
  systemctl stop turborama-pix || true
  if [[ -d "$app" && ! -e "$failed" ]]; then
    mv "$app" "$failed"
  fi
  if [[ -d "$rollback" && ! -e "$app" ]]; then
    mv "$rollback" "$app"
  fi
  systemctl start turborama-pix
  echo "ROLLBACK_EXECUTADO"
}

if ! systemctl start turborama-pix; then
  rollback_update
  exit 1
fi

healthy=false
for _ in 1 2 3 4 5 6 7 8 9 10; do
  if curl --fail --silent --show-error --max-time 3 \
      http://127.0.0.1:5187/v1/health >/dev/null; then
    healthy=true
    break
  fi
  sleep 1
done

if [[ "$healthy" != true ]]; then
  rollback_update
  exit 1
fi

printf 'ATUALIZACAO_OK\n'
printf 'BACKUP=%s\n' "$backup"
printf 'ROLLBACK=%s\n' "$rollback"
printf 'DLL_SHA256=%s\n' "$(sha256sum "$app/TurboRamaPixOnlineServer.dll" | awk '{print $1}')"
