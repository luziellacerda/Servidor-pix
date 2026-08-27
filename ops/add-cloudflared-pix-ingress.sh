#!/usr/bin/env bash
set -euo pipefail

config_file="/etc/cloudflared/config.yml"
hostname="pix.lzgames.com.br"
service_url="http://127.0.0.1:5187"
backup_stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_dir="/var/backups/turborama-pix/pre-tunnel-${backup_stamp}"
temp_file="$(mktemp /tmp/cloudflared-config.XXXXXX)"
trap 'rm -f -- "$temp_file"' EXIT

if grep -Eq "^[[:space:]]*-[[:space:]]*hostname:[[:space:]]*${hostname//./\\.}[[:space:]]*$" "$config_file"; then
  echo "O hostname ja existe na configuracao; nenhuma alteracao foi feita." >&2
  exit 1
fi

install -d -m 0700 -o root -g root "$backup_dir"
cp -a "$config_file" "$backup_dir/config.yml.before-pix"
cp -a /etc/systemd/system/cloudflared.service "$backup_dir/cloudflared.service"
sha256sum "$backup_dir/config.yml.before-pix" "$backup_dir/cloudflared.service" \
  > "$backup_dir/SHA256SUMS"
chmod -R go-rwx "$backup_dir"

awk -v hostname="$hostname" -v service_url="$service_url" '
  /^[[:space:]]*-[[:space:]]*service:[[:space:]]*http_status:404[[:space:]]*$/ && !inserted {
    print "  - hostname: " hostname
    print "    service: " service_url
    inserted=1
  }
  { print }
  END { if (!inserted) exit 42 }
' "$config_file" > "$temp_file"

chown root:root "$temp_file"
chmod 0644 "$temp_file"
cloudflared tunnel --config "$temp_file" ingress validate
install -m 0644 -o root -g root "$temp_file" "$config_file"
cloudflared tunnel --config "$config_file" ingress validate

printf '%s\n' "$backup_dir"
