#!/usr/bin/env sh
set -eu

script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
cert_dir="${1:-"$script_dir/../infra/nginx/certs"}"
days="${DAYS:-365}"

mkdir -p "$cert_dir"

cert_path="$cert_dir/localhost.crt"
key_path="$cert_dir/localhost.key"

if command -v mkcert >/dev/null 2>&1; then
  mkcert -install
  mkcert -cert-file "$cert_path" -key-file "$key_path" localhost ledger.localhost balance.localhost
elif command -v openssl >/dev/null 2>&1; then
  openssl req -x509 -newkey rsa:2048 -nodes -days "$days" \
    -keyout "$key_path" \
    -out "$cert_path" \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost"
else
  echo "Instale mkcert ou OpenSSL para gerar os certificados locais do Nginx." >&2
  exit 1
fi

chmod 0644 "$cert_path"
chmod 0600 "$key_path"

echo "Certificado local gerado em $cert_path"
echo "Chave privada local gerada em $key_path"
echo "Esses arquivos sao ignorados pelo Git e nao devem ser versionados."
