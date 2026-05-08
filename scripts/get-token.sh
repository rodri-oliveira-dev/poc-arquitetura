#!/usr/bin/env bash
set -euo pipefail

# Obtém TOKEN chamando o Auth.Api em POST /auth/login
# Output: imprime SOMENTE o token em stdout.
# Overrides via env:
#   AUTH_BASE_URL, TOKEN_URL, USERNAME, PASSWORD, SCOPE

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

AUTH_BASE_URL="${AUTH_BASE_URL:-http://localhost:5030}"
TOKEN_URL="${TOKEN_URL:-/auth/login}"
USERNAME="${USERNAME:-poc-usuario}"
PASSWORD="${PASSWORD:-Poc#123}"
SCOPE="${SCOPE:-ledger.write balance.read}"

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
