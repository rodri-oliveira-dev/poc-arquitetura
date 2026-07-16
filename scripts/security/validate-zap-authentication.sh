#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/../lib"
# shellcheck source=../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"

TOKEN=""
ENV_FILE=""
BASE_URL_PREFIX=""
EMPTY_UUID="00000000-0000-0000-0000-000000000000"
AUTHORIZED_TEST_MERCHANT_ID="m1"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/security/validate-zap-authentication.sh [opcoes]

Valida localmente se o JWT e aceito por endpoints protegidos das APIs antes
de entregar o token ao OWASP ZAP.

Opcoes:
  --token TOKEN             Token Bearer a validar. O valor nunca e impresso.
  --env-file PATH           Arquivo usado por scripts/validation/get-token.sh quando --token nao e informado.
  --base-url-prefix URL     Sobrescreve esquema/host das URLs padrao mantendo as portas atuais.
                            Exemplo: --base-url-prefix http://host.docker.internal
  -h, --help                Mostra esta ajuda.

Variaveis de ambiente para sobrescrever URLs completas:
  LEDGER_API_URL, BALANCE_API_URL, TRANSFER_API_URL,
  PAYMENT_API_URL, AUDIT_API_URL, IDENTITY_API_URL
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --token)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      TOKEN="$2"
      shift 2
      ;;
    --env-file)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      ENV_FILE="$2"
      shift 2
      ;;
    --base-url-prefix)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      BASE_URL_PREFIX="${2%/}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Opcao invalida: $1" >&2
      usage
      exit 2
      ;;
  esac
done

require_command curl

default_base_url() {
  local port="$1"

  if [[ -n "$BASE_URL_PREFIX" ]]; then
    printf '%s:%s' "$BASE_URL_PREFIX" "$port"
    return 0
  fi

  printf 'http://localhost:%s' "$port"
}

obtain_token() {
  local token_env_file="$ENV_FILE"
  local token_output=""
  local token_exit_code=0

  if [[ -n "$TOKEN" ]]; then
    printf '%s' "$TOKEN"
    return 0
  fi

  if [[ -z "$token_env_file" ]]; then
    token_env_file="$ROOT_DIR/.env.local"
  fi

  set +e
  token_output="$(ENV_FILE="$token_env_file" bash "$ROOT_DIR/scripts/validation/get-token.sh" 2>/dev/null)"
  token_exit_code=$?
  set -e

  if [[ "$token_exit_code" -ne 0 ]]; then
    echo "[auth-preflight] Falha ao obter token pelo script scripts/validation/get-token.sh. Exit code: $token_exit_code." >&2
    exit "$token_exit_code"
  fi

  printf '%s' "$token_output"
}

mask_token_for_github_actions() {
  local token="$1"

  if [[ "${GITHUB_ACTIONS:-false}" == "true" ]]; then
    echo "::add-mask::$token"
  fi
}

accepted_status() {
  local status="$1"

  if [[ "$status" =~ ^2[0-9][0-9]$ ]]; then
    return 0
  fi

  case "$status" in
    400|404|409|422)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

failure_message() {
  local status="$1"

  case "$status" in
    000)
      printf 'falha de conectividade.'
      ;;
    401)
      printf 'token rejeitado.'
      ;;
    403)
      printf 'autorizacao insuficiente.'
      ;;
    5??)
      printf 'falha operacional da API.'
      ;;
    *)
      printf 'status nao aceito.'
      ;;
  esac
}

validate_api() {
  local api_name="$1"
  local method="$2"
  local base_url="$3"
  local path="$4"
  local body="${5:-}"
  local url="${base_url%/}$path"
  local -a curl_args=(
    -sS
    -o /dev/null
    -w '%{http_code}'
    --connect-timeout 5
    --max-time 20
    -H "Accept: application/json"
    -H "Authorization: Bearer $ACCESS_TOKEN"
    -X "$method"
  )
  local status
  local curl_exit_code=0

  if [[ -n "$body" ]]; then
    curl_args+=(
      -H "Content-Type: application/json"
      --data-binary "$body"
    )
  fi

  set +e
  status="$(curl "${curl_args[@]}" "$url" 2>/dev/null)"
  curl_exit_code=$?
  set -e

  if [[ "$curl_exit_code" -ne 0 || -z "$status" ]]; then
    status="000"
  fi

  if accepted_status "$status"; then
    echo "[auth-preflight] $api_name: $method $path -> HTTP $status, autenticacao aceita."
    return 0
  fi

  echo "[auth-preflight] $api_name: $method $path -> HTTP $status, $(failure_message "$status")" >&2
  return 1
}

ACCESS_TOKEN="$(obtain_token)"
if [[ -z "$ACCESS_TOKEN" ]]; then
  echo "[auth-preflight] Token vazio para validar autenticacao." >&2
  exit 1
fi

mask_token_for_github_actions "$ACCESS_TOKEN"

LEDGER_BASE_URL="${LEDGER_API_URL:-$(default_base_url 5226)}"
BALANCE_BASE_URL="${BALANCE_API_URL:-$(default_base_url 5228)}"
TRANSFER_BASE_URL="${TRANSFER_API_URL:-$(default_base_url 5230)}"
IDENTITY_BASE_URL="${IDENTITY_API_URL:-$(default_base_url 5232)}"
PAYMENT_BASE_URL="${PAYMENT_API_URL:-$(default_base_url 5234)}"
AUDIT_BASE_URL="${AUDIT_API_URL:-$(default_base_url 5235)}"

failures=0

validate_api "LedgerService.Api" "GET" "$LEDGER_BASE_URL" "/api/v1/lancamentos/estornos/$EMPTY_UUID" || failures=$((failures + 1))
validate_api "BalanceService.Api" "GET" "$BALANCE_BASE_URL" "/api/v1/consolidados/periodo?from=2024-01-01&to=2024-01-01&merchantId=$AUTHORIZED_TEST_MERCHANT_ID" || failures=$((failures + 1))
validate_api "TransferService.Api" "GET" "$TRANSFER_BASE_URL" "/api/v1/transferencias/$EMPTY_UUID" || failures=$((failures + 1))
validate_api "PaymentService.Api" "GET" "$PAYMENT_BASE_URL" "/api/v1/payments/$EMPTY_UUID" || failures=$((failures + 1))
validate_api "AuditService.Api" "GET" "$AUDIT_BASE_URL" "/api/v1/audit-records/$EMPTY_UUID" || failures=$((failures + 1))
validate_api "IdentityService.Api" "POST" "$IDENTITY_BASE_URL" "/api/v1/users" "{}" || failures=$((failures + 1))

if [[ "$failures" -ne 0 ]]; then
  echo "[auth-preflight] $failures API(s) falharam na validacao de autenticacao." >&2
  exit 1
fi

echo "[auth-preflight] Todas as APIs aceitaram o token."
