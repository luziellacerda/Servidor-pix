#!/usr/bin/env bash
set -euo pipefail

package="/home/lz-servidor/turborama-download/Servidor-pix/outputs/TurboRamaPixOnlineServer-portable-RODADA05-20260807.zip"
expected_package_sha="d5dfdc6e7057c5fe2dfb1fa4ac3f0e2f963d77b7a9c493c89e7265c186a569d4"
expected_dll_sha="38ad5837d7ce7e17b5a5fc4149a026ca32c711a52226e358278d0ee8d903d327"
app="/opt/turborama-pix"
state="/var/lib/turborama-pix"
config="/etc/turborama-pix"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="/var/backups/turborama-pix/round05-${stamp}"
prepared="/opt/turborama-pix.round05-new-${stamp}"
rollback="/opt/turborama-pix.rollback-round05-${stamp}"
failed="/opt/turborama-pix.failed-round05-${stamp}"
stage="$(mktemp -d /tmp/turborama-pix-round05.XXXXXX)"

cleanup() {
  rm -rf -- "$stage"
}
trap cleanup EXIT

package_sha="$(sha256sum "$package" | awk '{print $1}')"
if [[ "$package_sha" != "$expected_package_sha" ]]; then
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
  [[ "$(sha256sum TurboRamaPixOnlineServer.dll | awk '{print $1}')" == "$expected_dll_sha" ]]
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
cp -a /etc/cloudflared "$backup/cloudflared"
cp -a /etc/systemd/system/cloudflared.service "$backup/cloudflared.service"
{
  printf 'created_utc=%s\n' "$stamp"
  printf 'package_sha256=%s\n' "$package_sha"
  printf 'installed_dll_before=%s\n' "$(sha256sum "$app/TurboRamaPixOnlineServer.dll" | awk '{print $1}')"
  printf 'state_sha256_before=%s\n' "$(sha256sum "$state/state.json" | awk '{print $1}')"
  printf 'env_sha256_before=%s\n' "$(sha256sum "$config/server.env" | awk '{print $1}')"
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
printf 'STATE_SHA256_AFTER=%s\n' "$(sha256sum "$state/state.json" | awk '{print $1}')"
