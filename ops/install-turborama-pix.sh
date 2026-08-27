#!/usr/bin/env bash
set -euo pipefail

package_file="/home/lz-servidor/turborama-download/Servidor-pix/outputs/TurboRamaPixOnlineServer-portable-52c8847.zip"
package_sha256="71af50e4d837d0e831277e4edd16fdedf1f242acb8f27b338b7ed17a275f994a"
app_dir="/opt/turborama-pix"
state_dir="/var/lib/turborama-pix"
config_dir="/etc/turborama-pix"
env_file="$config_dir/server.env"
unit_file="/etc/systemd/system/turborama-pix.service"
stage_dir="$(mktemp -d /tmp/turborama-pix-install.XXXXXX)"
trap 'rm -rf -- "$stage_dir"' EXIT

if [[ "$(sha256sum "$package_file" | awk '{print $1}')" != "$package_sha256" ]]; then
  echo "SHA-256 do pacote nao confere." >&2
  exit 1
fi

for target in "$app_dir" "$state_dir" "$config_dir" "$unit_file"; do
  if [[ -e "$target" ]]; then
    echo "Destino ja existe; instalacao interrompida com seguranca: $target" >&2
    exit 1
  fi
done

unzip -q "$package_file" -d "$stage_dir"
(
  cd "$stage_dir"
  sha256sum --check CHECKSUMS-SHA256.txt
)

if ! id -u turborama-pix >/dev/null 2>&1; then
  useradd --system --home-dir "$state_dir" --shell /usr/sbin/nologin turborama-pix
fi

install -d -m 0755 -o root -g root "$app_dir"
install -d -m 0700 -o turborama-pix -g turborama-pix "$state_dir"
install -d -m 0700 -o root -g root "$config_dir"
install -m 0644 -o root -g root "$stage_dir"/*.dll "$app_dir/"
install -m 0644 -o root -g root "$stage_dir"/*.json "$app_dir/"

state_key="$(openssl rand -base64 32)"
secret_key="$(openssl rand -base64 32)"
while [[ "$secret_key" == "$state_key" ]]; do
  secret_key="$(openssl rand -base64 32)"
done

umask 077
{
  echo 'ASPNETCORE_ENVIRONMENT=Production'
  echo 'ASPNETCORE_URLS=http://127.0.0.1:5187'
  echo 'TURBORAMA_SERVER_STATE_FILE=/var/lib/turborama-pix/state.json'
  printf 'TURBORAMA_SERVER_STATE_KEY=%s\n' "$state_key"
  printf 'TURBORAMA_SERVER_SECRET_KEY=%s\n' "$secret_key"
  echo 'TURBORAMA_PAYMENT_EXPIRATION_MINUTES=15'
  echo 'TURBORAMA_ALLOW_HTTP_LOOPBACK=true'
} > "$env_file"
chown root:root "$env_file"
chmod 0600 "$env_file"

cat > "$unit_file" <<'UNIT'
[Unit]
Description=TurboRama PIX Online Server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=turborama-pix
Group=turborama-pix
WorkingDirectory=/opt/turborama-pix
EnvironmentFile=/etc/turborama-pix/server.env
ExecStart=/usr/bin/dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll
Restart=on-failure
RestartSec=5s
UMask=0077
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/turborama-pix

[Install]
WantedBy=multi-user.target
UNIT
chown root:root "$unit_file"
chmod 0644 "$unit_file"

systemctl daemon-reload
systemctl enable --now turborama-pix.service
