#!/usr/bin/env bash
set -euo pipefail

# Obtém TOKEN chamando o Auth.Api em POST /auth/login
# Output: imprime SOMENTE o token em stdout.
# Overrides via env:
#   AUTH_BASE_URL, TOKEN_URL, AUTH_POC_USERNAME, AUTH_POC_PASSWORD, AUTH_POC_SCOPE
#   USERNAME, PASSWORD e SCOPE continuam aceitos por compatibilidade.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

read_local_env() {
  local key="$1"
  local file="$ROOT_DIR/.env"
  if [ ! -f "$file" ]; then
    return 0
  fi

  sed -nE "s/^[[:space:]]*$key[[:space:]]*=[[:space:]]*(.*)[[:space:]]*$/\\1/p" "$file" | tail -n 1
}

AUTH_BASE_URL="${AUTH_BASE_URL:-http://localhost:5030}"
TOKEN_URL="${TOKEN_URL:-/auth/login}"
LOCAL_AUTH_POC_USERNAME="$(read_local_env AUTH_POC_USERNAME)"
LOCAL_AUTH_POC_PASSWORD="$(read_local_env AUTH_POC_PASSWORD)"
LOCAL_AUTH_POC_SCOPE="$(read_local_env AUTH_POC_SCOPE)"
USERNAME="${AUTH_POC_USERNAME:-${USERNAME:-${LOCAL_AUTH_POC_USERNAME:-local_user}}}"
PASSWORD="${AUTH_POC_PASSWORD:-${PASSWORD:-${LOCAL_AUTH_POC_PASSWORD:-local_password}}}"
SCOPE="${AUTH_POC_SCOPE:-${SCOPE:-${LOCAL_AUTH_POC_SCOPE:-ledger.write balance.read}}}"

URL="${AUTH_BASE_URL%/}/${TOKEN_URL#/}"

resp="$(
  curl -sS -X POST "$URL" \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$USERNAME\",\"password\":\"$PASSWORD\",\"scope\":\"$SCOPE\"}" \
    2>/dev/null
)" || {
  echo "Falha ao obter token em '$URL'" 1>&2
  exit 1
}

# Extrai access_token (contrato atual) ou accessToken (contrato antigo) sem jq
token="$(echo "$resp" | sed -nE 's/.*"access_token"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p')"
if [ -z "$token" ]; then
  token="$(echo "$resp" | sed -nE 's/.*"accessToken"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p')"
fi

if [ -z "$token" ]; then
  echo "Token nao encontrado no response. Campos esperados: accessToken|access_token" 1>&2
  exit 1
fi

echo -n "$token"
