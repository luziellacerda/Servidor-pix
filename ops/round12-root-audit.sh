#!/usr/bin/env bash
set -euo pipefail
[[ ${EUID:-$(id -u)} -eq 0 ]] || { echo 'ERRO: execute com sudo.' >&2; exit 1; }
env_file=/etc/turborama-pix/server.env
unit_file=/etc/systemd/system/turborama-pix.service
app_dir=/opt/turborama-pix
[[ -r "$env_file" && -r "$unit_file" && -d "$app_dir" ]] || { echo 'ERRO: instalação privada inacessível.' >&2; exit 1; }
state_file=$(sed -n 's/^TURBORAMA_SERVER_STATE_FILE=//p' "$env_file" | tail -n 1)
state_file=${state_file%$'\r'}
state_file=${state_file#\"}; state_file=${state_file%\"}
state_file=${state_file#\'}; state_file=${state_file%\'}
state_file=${state_file:-/var/lib/turborama-pix/state.json}
[[ "$state_file" = /* && "$state_file" != *$'\n'* ]] || { echo 'ERRO: caminho de estado inválido.' >&2; exit 1; }
[[ -r "$state_file" ]] || { echo 'ERRO: estado inacessível.' >&2; exit 1; }
audit_dir=/var/backups/turborama-pix/round12-audit-$(date -u +%Y%m%dT%H%M%SZ)
install -d -m 0700 -o root -g root "$audit_dir"
sha256sum "$state_file" > "$audit_dir/state.sha256"
stat -c 'state_size=%s state_mode=%a state_owner=%U:%G' "$state_file" > "$audit_dir/state.metadata"
payload_file=$(mktemp "$audit_dir/state-payload.XXXXXX")
trap 'rm -f -- "$payload_file"' EXIT
jq -er '.payload // .Payload' "$state_file" | base64 -d > "$payload_file"
jq -e . "$payload_file" >/dev/null
jq -r '(.customers // .Customers // []) as $c | (.licenses // .Licenses // []) as $l | (.payments // .Payments // []) as $p | [($c|length), ($l|length), ([$l[]|(.devices // .Devices // [])[]]|length), ([$l[]|(.devices // .Devices // [])[]|select((.activeSessionId // .ActiveSessionId // "") != "")]|length), ($p|length), ([$c[]|select((.mercadoPago // .MercadoPago) != null)]|length), ([$l[]|select(((.packagePricesCents // .PackagePricesCents // {})|length)>0)]|length)] | @tsv' "$payload_file" > "$audit_dir/counts.tsv"
jq -r '[((.licenses // .Licenses // [])[] | (.devices // .Devices // [])[]) | {status:(.status // .Status // "UNKNOWN"),lastContact:(.lastContactUnixSeconds // .LastContactUnixSeconds // 0)}] | if length==0 then "machine_status=NONE last_contact=0" else (max_by(.lastContact) | "machine_status=" + .status + " last_contact=" + (.lastContact|tostring)) end' "$payload_file" > "$audit_dir/machine-status.txt"
rm -f -- "$payload_file"
systemctl show turborama-pix.service -p ActiveState -p UnitFileState -p User -p Group -p WorkingDirectory -p ExecStart -p NRestarts > "$audit_dir/service.txt"
sha256sum "$app_dir"/* > "$audit_dir/application.sha256"
chmod -R go-rwx "$audit_dir"
printf 'AUDIT_DIR=%s\n' "$audit_dir"
printf 'STATE_SHA256=%s\n' "$(cut -d' ' -f1 "$audit_dir/state.sha256")"
printf 'STATE_METADATA=%s\n' "$(cat "$audit_dir/state.metadata")"
IFS=$'	' read -r customers licenses machines sessions payments credentials prices < "$audit_dir/counts.tsv"
printf 'COUNTS=customers:%s licenses:%s machines:%s sessions:%s payments:%s credentials:%s prices:%s
' "$customers" "$licenses" "$machines" "$sessions" "$payments" "$credentials" "$prices"
printf 'LATEST_MACHINE=%s
' "$(cat "$audit_dir/machine-status.txt")"
printf 'FASE1_PRIVATE_AUDIT=OK\n'
