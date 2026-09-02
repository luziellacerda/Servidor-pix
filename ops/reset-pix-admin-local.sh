#!/usr/bin/env bash
set -euo pipefail

if [[ ${EUID} -ne 0 ]]; then
  exec sudo -- "$0"
fi

env_file=/etc/turborama-pix/server.env
server_dll=/opt/turborama-pix/TurboRamaPixOnlineServer.dll

echo "Redefinição local do administrador TurboRama PIX"
echo "A senha não será exibida."
read -r -p "Novo usuário [turborama-admin]: " admin_user
admin_user=${admin_user:-turborama-admin}
if [[ ! ${admin_user} =~ ^[A-Za-z0-9._@-]{3,64}$ ]]; then
  echo "Usuário inválido. Use de 3 a 64 letras, números, ponto, arroba, hífen ou sublinhado."
  exit 1
fi

read -r -s -p "Nova senha (mínimo 12 caracteres): " first_password
echo
read -r -s -p "Confirme a nova senha: " second_password
echo
if [[ ${#first_password} -lt 12 ]]; then
  echo "A senha precisa ter pelo menos 12 caracteres."
  exit 1
fi
if [[ ${first_password} != "${second_password}" ]]; then
  echo "As senhas não coincidem."
  exit 1
fi

hash_output=$(printf '%s\n%s\n' "${first_password}" "${second_password}" | /usr/bin/dotnet "${server_dll}" --hash-admin-password)
admin_hash=${hash_output##*$'\n'}
unset first_password second_password hash_output
if [[ -z ${admin_hash} ]]; then
  echo "Não foi possível gerar o hash protegido."
  exit 1
fi

env_tmp=$(mktemp /etc/turborama-pix/server.env.tmp.XXXXXX)
trap '[[ -n ${env_tmp:-} && -e ${env_tmp:-} ]] && rm -f -- "${env_tmp}"' EXIT
found_user=0
found_hash=0
while IFS= read -r line || [[ -n ${line} ]]; do
  case ${line} in
    TURBORAMA_ADMIN_USERNAME=*) printf 'TURBORAMA_ADMIN_USERNAME=%s\n' "${admin_user}"; found_user=1 ;;
    TURBORAMA_ADMIN_PASSWORD_HASH=*) printf 'TURBORAMA_ADMIN_PASSWORD_HASH=%s\n' "${admin_hash}"; found_hash=1 ;;
    *) printf '%s\n' "${line}" ;;
  esac
done < "${env_file}" > "${env_tmp}"
[[ ${found_user} -eq 1 && ${found_hash} -eq 1 ]] || { echo "Variáveis administrativas não encontradas."; exit 1; }
unset admin_hash
chown root:root "${env_tmp}"
chmod 0600 "${env_tmp}"
mv -f -- "${env_tmp}" "${env_file}"
env_tmp=

systemctl restart turborama-pix
for _ in {1..15}; do
  if curl --silent --fail --output /dev/null http://127.0.0.1:5187/v1/health; then
    echo
    echo "Credenciais atualizadas e Servidor PIX saudável."
    echo "Usuário: ${admin_user}"
    echo "Acesse: https://painelpix.lzgames.com.br/admin/login"
    read -r -p "Pressione Enter para fechar..." _
    exit 0
  fi
  sleep 1
done
echo "Credenciais salvas, mas o health check não respondeu a tempo."
read -r -p "Pressione Enter para fechar..." _
exit 1
