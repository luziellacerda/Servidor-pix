#!/bin/bash
# Imprime só a SPKI pública e o keyId. Não imprime a chave privada.
set -euo pipefail
pem="${1:-/etc/turborama-suite/station-assertion.pem}"
der="$(mktemp)"
trap 'rm -f "$der"' EXIT
openssl pkey -in "$pem" -pubout -outform DER -out "$der" >/dev/null
spki="$(openssl base64 -A -in "$der" | tr '+/' '-_' | tr -d '=')"
keyid="$(openssl dgst -sha256 "$der" | awk '{print $2}')"
printf 'keyId=%s\n' "$keyid"
printf 'spkiBase64Url=%s\n' "$spki"
