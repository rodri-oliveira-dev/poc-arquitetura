#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/lib"
if [[ ! -f "$SCRIPTS_LIB_DIR/common.sh" ]]; then
  SCRIPTS_LIB_DIR="$SCRIPT_DIR/../lib"
fi
# shellcheck source=../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"
CERT_DIR="${1:-"$ROOT_DIR/infra/nginx/certs"}"
DAYS="${DAYS:-365}"

mkdir -p "$CERT_DIR"

CERT_PATH="$CERT_DIR/localhost.crt"
KEY_PATH="$CERT_DIR/localhost.key"

if command -v mkcert >/dev/null 2>&1; then
  mkcert -install
  mkcert -cert-file "$CERT_PATH" -key-file "$KEY_PATH" localhost ledger.localhost balance.localhost
elif command -v openssl >/dev/null 2>&1; then
  openssl req -x509 -newkey rsa:2048 -nodes -days "$DAYS" \
    -keyout "$KEY_PATH" \
    -out "$CERT_PATH" \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost"
else
  echo "Instale mkcert ou OpenSSL para gerar os certificados locais do Nginx." >&2
  exit 1
fi

chmod 0644 "$CERT_PATH"
chmod 0600 "$KEY_PATH"

echo "Certificado local gerado em $CERT_PATH"
echo "Chave privada local gerada em $KEY_PATH"
echo "Esses arquivos sao ignorados pelo Git e nao devem ser versionados."
