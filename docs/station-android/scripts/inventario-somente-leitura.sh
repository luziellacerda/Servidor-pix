#!/usr/bin/env bash
set -euo pipefail

printf 'Data: '
date -Is

printf '\nVolumes de trabalho\n'
df -h / /mnt/DADOS

printf '\nServicos principais e executaveis efetivos\n'
services=(
  turborama-pix.service
  turborama-suite-api.service
  turborama-suite-admin.service
  turborama-suite-content-gateway.service
)
for service in "${services[@]}"; do
  systemctl show "$service" -p Id -p ActiveState -p MainPID -p WorkingDirectory -p DropInPaths -p ExecStart --no-pager
  command_line=$(systemctl show "$service" -p ExecStart --value --no-pager)
  binary=$(printf '%s\n' "$command_line" | rg -o '/opt/[^ ;]+\.dll' | head -n 1 || true)
  if [[ -n "$binary" && -f "$binary" ]]; then
    sha256sum -- "$binary"
  else
    printf 'DLL efetiva não resolvida automaticamente; confira o ExecStart.\n'
  fi
done

printf '\nDependencias e monitor de conteudo\n'
for service in nginx.service cloudflared.service postgresql@16-main.service redis-server.service php8.3-fpm.service turbobox-php-fpm.service turborama-suite-content-monitor.service; do
  systemctl show "$service" -p Id -p ActiveState -p Result --no-pager
done

printf '\nVerificacoes HTTP locais sem imprimir respostas\n'
for url in http://127.0.0.1:5187/v1/health http://127.0.0.1:5190/health http://127.0.0.1:5191/health; do
  curl -sS --connect-timeout 2 --max-time 3 -o /dev/null -w '%{url_effective}: HTTP %{http_code}\n' "$url" || true
done

printf '\nCluster PostgreSQL, sem conectar ao banco\n'
pg_lsclusters

printf '\nWorktrees locais, sem mostrar conteudo de arquivos\n'
repos=(
  /home/lz-servidor/worktrees/servidor-pix-content-prod
  /home/lz-servidor/worktrees/servidor-pix-all-downloads-20260908
  /home/lz-servidor/worktrees/turborama-suite-producao-real-r2
)
for repo in "${repos[@]}"; do
  if git -C "$repo" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    printf '%s\n' "$repo"
    git -C "$repo" status --short --branch
    git -C "$repo" log -1 --format='%H %cs %s'
  fi
done

printf '\nFim do inventario de leitura. Nenhum arquivo de ambiente, segredo ou dado de cliente foi consultado.\n'
