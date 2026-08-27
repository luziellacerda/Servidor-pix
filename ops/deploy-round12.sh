#!/usr/bin/env bash
set -euo pipefail
[[ ${EUID:-$(id -u)} -eq 0 ]] || { echo 'ERRO: execute com sudo.' >&2; exit 1; }

package=/home/lz-servidor/turborama-download/Servidor-pix/outputs/TurboRamaPixOnlineServer-portable-RODADA12-20260814.zip
package_sha=ff0c82974ad721b6f7007ce1c56ce2a1402ff78b4a75a34edbdc19d7cfd63c81
dll_sha=4c5a51d8b547fec90cb276737bcc44be476434bda9efcdfbcaa342cc86efa43f
app=/opt/turborama-pix
env_file=/etc/turborama-pix/server.env
unit=/etc/systemd/system/turborama-pix.service
stamp=$(date -u +%Y%m%dT%H%M%SZ)
backup=/var/backups/turborama-pix/round12-$stamp
prepared=/opt/turborama-pix.round12-new-$stamp
rollback=/opt/turborama-pix.round12-rollback-$stamp
failed=/opt/turborama-pix.round12-failed-$stamp
stage=$(mktemp -d /tmp/turborama-r12-deploy.XXXXXX)
cleanup(){ rm -rf -- "$stage"; }
trap cleanup EXIT

[[ -f "$package" && -d "$app" && -r "$env_file" && -r "$unit" ]] || { echo 'ERRO: instalação esperada ausente.' >&2; exit 1; }
[[ $(stat -c %s "$package") = 104376 ]] || { echo 'ERRO: tamanho do ZIP divergente.' >&2; exit 1; }
[[ $(sha256sum "$package" | cut -d' ' -f1) = "$package_sha" ]] || { echo 'ERRO: hash do ZIP divergente.' >&2; exit 1; }
for target in "$backup" "$prepared" "$rollback" "$failed"; do [[ ! -e "$target" ]] || { echo "ERRO: destino existente: $target" >&2; exit 1; }; done

mkdir "$stage/app"
chmod 0755 "$stage" "$stage/app"
unzip -q "$package" -d "$stage/app"
chmod 0644 "$stage/app"/*
[[ $(find "$stage/app" -maxdepth 1 -type f | wc -l) = 5 ]] || { echo 'ERRO: pacote não contém cinco arquivos.' >&2; exit 1; }
! find "$stage/app" -maxdepth 1 -type f | grep -Eiq '\.(cs|ps1|pdb|pfx|key)$' || { echo 'ERRO: tipo proibido no pacote.' >&2; exit 1; }
(cd "$stage/app" && sha256sum --check CHECKSUMS-SHA256.txt)
[[ $(stat -c %s "$stage/app/TurboRamaPixOnlineServer.dll") = 309760 ]] || { echo 'ERRO: tamanho da DLL divergente.' >&2; exit 1; }
[[ $(sha256sum "$stage/app/TurboRamaPixOnlineServer.dll" | cut -d' ' -f1) = "$dll_sha" ]] || { echo 'ERRO: hash da DLL divergente.' >&2; exit 1; }
runuser -u turborama-pix -- dotnet "$stage/app/TurboRamaPixOnlineServer.dll" --self-test | grep -q '^SELF-TEST SERVIDOR ONLINE: OK'

state=$(sed -n 's/^TURBORAMA_SERVER_STATE_FILE=//p' "$env_file" | tail -n 1)
state=${state%$'\r'}; state=${state#\"}; state=${state%\"}; state=${state#\'}; state=${state%\'}
state=${state:-/var/lib/turborama-pix/state.json}
[[ "$state" = /* && -r "$state" ]] || { echo 'ERRO: estado privado inválido ou inacessível.' >&2; exit 1; }
state_before=$(sha256sum "$state" | cut -d' ' -f1)

install -d -m 0700 -o root -g root "$backup"
cp -a "$app" "$backup/application"
install -d -m 0700 -o root -g root "$backup/state"
cp -a "$state" "$backup/state/"
cp -a "$unit" "$backup/turborama-pix.service"
systemctl show turborama-pix -p ActiveState -p UnitFileState -p User -p Group -p WorkingDirectory -p ExecStart -p NRestarts > "$backup/service-before.txt"
printf 'created_utc=%s\npackage_sha256=%s\ndll_sha256=%s\nstate_sha256=%s\n' "$stamp" "$package_sha" "$dll_sha" "$state_before" > "$backup/metadata.txt"
(cd "$backup" && find . -type f ! -name SHA256SUMS ! -name SHA256SUMS.verify -print0 | sort -z | xargs -0 sha256sum > SHA256SUMS && sha256sum --check SHA256SUMS > SHA256SUMS.verify)
chmod -R go-rwx "$backup"

install -d -m 0755 -o root -g root "$prepared"
for file in TurboRamaPixOnlineServer.dll TurboRamaPixOnlineServer.deps.json TurboRamaPixOnlineServer.runtimeconfig.json TurboRamaPixOnlineServer.staticwebassets.endpoints.json; do install -m 0644 -o root -g root "$stage/app/$file" "$prepared/$file"; done
runuser -u turborama-pix -- dotnet "$prepared/TurboRamaPixOnlineServer.dll" --self-test | grep -q '^SELF-TEST SERVIDOR ONLINE: OK'

rollback_now(){
  set +e
  systemctl stop turborama-pix
  [[ -d "$app" && ! -e "$failed" ]] && mv "$app" "$failed"
  [[ -d "$rollback" && ! -e "$app" ]] && mv "$rollback" "$app"
  current_state=$(sha256sum "$state" 2>/dev/null | cut -d' ' -f1)
  if [[ "$current_state" != "$state_before" ]]; then cp -a "$backup/state/$(basename "$state")" "$state"; fi
  systemctl start turborama-pix
  echo 'ROLLBACK=EXECUTADO_COM_SUCESSO'
  exit 1
}
trap rollback_now ERR
systemctl stop turborama-pix
while systemctl is-active --quiet turborama-pix; do sleep 0.2; done
mv "$app" "$rollback"
mv "$prepared" "$app"
systemctl start turborama-pix
for _ in $(seq 1 30); do systemctl is-active --quiet turborama-pix && curl -fsS http://127.0.0.1:5187/v1/health >/dev/null && break; sleep 1; done
systemctl is-active --quiet turborama-pix
curl -fsS http://127.0.0.1:5187/v1/health >/dev/null
[[ $(systemctl show turborama-pix -p NRestarts --value) = 0 ]]
[[ $(sha256sum "$app/TurboRamaPixOnlineServer.dll" | cut -d' ' -f1) = "$dll_sha" ]]
[[ $(sha256sum "$state" | cut -d' ' -f1) = "$state_before" ]]
trap - ERR
printf 'BACKUP_DIR=%s\nROLLBACK_APP=%s\nSTATE_SHA256=%s\nDLL_SHA256=%s\nDEPLOY_ROUND12=OK\n' "$backup" "$rollback" "$state_before" "$dll_sha"
