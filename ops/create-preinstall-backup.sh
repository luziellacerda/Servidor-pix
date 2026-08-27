#!/usr/bin/env bash
set -euo pipefail

backup_stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_dir="/var/backups/turborama-pix/preinstall-${backup_stamp}"

install -d -m 0700 -o root -g root "$backup_dir"
cp -a /etc/cloudflared "$backup_dir/cloudflared"
cp -a /etc/nginx "$backup_dir/nginx"
cp -a /etc/systemd/system/cloudflared.service "$backup_dir/cloudflared.service"

{
  printf 'created_utc=%s\n' "$backup_stamp"
  printf 'host=%s\n' "$(hostname)"
  printf 'cloudflared_active=%s\n' "$(systemctl is-active cloudflared || true)"
  printf 'cloudflared_enabled=%s\n' "$(systemctl is-enabled cloudflared || true)"
  printf 'nginx_active=%s\n' "$(systemctl is-active nginx || true)"
  printf 'nginx_enabled=%s\n' "$(systemctl is-enabled nginx || true)"
} > "$backup_dir/service-state.txt"

ss -lntp > "$backup_dir/listening-ports.txt"
cloudflared tunnel --config /etc/cloudflared/config.yml ingress validate \
  > "$backup_dir/cloudflared-validation.txt" 2>&1
nginx -t > "$backup_dir/nginx-validation.txt" 2>&1

(
  cd "$backup_dir"
  find . -type f ! -name SHA256SUMS -print0 \
    | sort -z \
    | xargs -0 sha256sum > SHA256SUMS
  sha256sum --check SHA256SUMS > SHA256SUMS.verify
)

chmod -R go-rwx "$backup_dir"
printf '%s\n' "$backup_dir"
