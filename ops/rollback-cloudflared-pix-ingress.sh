#!/usr/bin/env bash
set -euo pipefail

backup_file="/var/backups/turborama-pix/pre-tunnel-20260807T111242Z/config.yml.before-pix"
config_file="/etc/cloudflared/config.yml"

test -f "$backup_file"
cloudflared tunnel --config "$backup_file" ingress validate
install -m 0644 -o root -g root "$backup_file" "$config_file"
cloudflared tunnel --config "$config_file" ingress validate
systemctl restart cloudflared
systemctl is-active --quiet cloudflared

echo "Rollback do ingress concluido; a rota DNS deve ser removida separadamente se necessario."
