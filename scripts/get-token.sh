#!/usr/bin/env bash
set -euo pipefail

# Output: imprime SOMENTE o token em stdout.
# Provider padrao: Keycloak via client_credentials.
# Fallback legado: TOKEN_PROVIDER=auth-api usa Auth.Api em POST /auth/login.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env.k6.auto}"

read_env_value() {
  local file="$1"
  local key="$2"

  if [[ ! -f "$file" ]]; then
    return 0
  fi

  sed -nE "s/^[[:space:]]*$key[[:space:]]*=[[:space:]]*(.*)[[:space:]]*$/\1/p" "$file" |
    tail -n 1 |
    sed -E "s/^[[:space:]]+//; s/[[:space:]]+$//; s/^['\"]//; s/['\"]$//"
}

config_value() {
  local key="$1"
  local default_value="${2:-}"
  local value="${!key:-}"

  if [[ -z "$value" ]]; then
    value="$(read_env_value "$ROOT_DIR/.env" "$key")"
  fi
  if [[ -z "$value" ]]; then
    value="$(read_env_value "$ENV_FILE" "$key")"
  fi
  if [[ -z "$value" ]]; then
    value="$default_value"
  fi

  printf '%s' "$value"
}

combine_url() {
  local base_url="$1"
  local path="$2"

  if [[ -z "$base_url" ]]; then
    printf '%s' "$path"
    return 0
  fi
  if [[ -z "$path" ]]; then
    printf '%s' "$base_url"
    return 0
  fi
  if [[ "$path" == http://* || "$path" == https://* ]]; then
    printf '%s' "$path"
    return 0
  fi

  printf '%s/%s' "${base_url%/}" "${path#/}"
}

fail() {
  echo "$1" 1>&2
  exit 1
}

extract_json_string() {
  local json="$1"
  local field="$2"

  echo "$json" | sed -nE "s/.*\"$field\"[[:space:]]*:[[:space:]]*\"([^\"]*)\".*/\1/p"
}

extract_token() {
  local resp="$1"
  local token

  token="$(extract_json_string "$resp" access_token)"
  if [[ -z "$token" ]]; then
    token="$(extract_json_string "$resp" accessToken)"
  fi
  if [[ -z "$token" ]]; then
    fail "Token nao encontrado no response. Campos esperados: accessToken|access_token"
  fi

  printf '%s' "$token"
}

request_json_or_fail() {
  local provider_name="$1"
  local url="$2"
  shift 2

  local resp
  local status
  local body

  if ! resp="$(curl -sS -w $'\n%{http_code}' "$@" "$url" 2>/dev/null)"; then
    fail "Falha ao obter token $provider_name em '$url'"
  fi

  status="$(printf '%s' "$resp" | tail -n 1)"
  body="$(printf '%s' "$resp" | sed '$d')"

  if [[ ! "$status" =~ ^2 ]]; then
    local error
    local description
    error="$(extract_json_string "$body" error)"
    description="$(extract_json_string "$body" error_description)"
    if [[ -n "$error" && -n "$description" ]]; then
      fail "Falha ao obter token $provider_name em '$url': HTTP $status - $error - $description"
    fi
    if [[ -n "$error" ]]; then
      fail "Falha ao obter token $provider_name em '$url': HTTP $status - $error"
    fi
    fail "Falha ao obter token $provider_name em '$url': HTTP $status"
  fi

  printf '%s' "$body"
}

request_keycloak_token() {
  local keycloak_base_url
  local keycloak_host_port
  local realm
  local token_url
  local client_id
  local client_secret
  local scope
  local url
  local args
  local resp

  keycloak_base_url="$(config_value KEYCLOAK_BASE_URL)"
  if [[ -z "$keycloak_base_url" ]]; then
    keycloak_host_port="$(config_value KEYCLOAK_HOST_PORT 8081)"
    keycloak_base_url="http://localhost:$keycloak_host_port"
  fi

  realm="$(config_value KEYCLOAK_REALM poc)"
  token_url="$(config_value KEYCLOAK_TOKEN_URL "/realms/$realm/protocol/openid-connect/token")"
  client_id="$(config_value KEYCLOAK_CLIENT_ID poc-automation)"
  client_secret="$(config_value KEYCLOAK_CLIENT_SECRET local_dev_client_secret)"
  scope="$(config_value KEYCLOAK_SCOPE)"
  url="$(combine_url "$keycloak_base_url" "$token_url")"

  [[ -n "$client_id" ]] || fail "KEYCLOAK_CLIENT_ID nao informado"
  [[ -n "$client_secret" ]] || fail "KEYCLOAK_CLIENT_SECRET nao informado"

  args=(
    -X POST
    -H "Content-Type: application/x-www-form-urlencoded"
    --data-urlencode "grant_type=client_credentials"
    --data-urlencode "client_id=$client_id"
    --data-urlencode "client_secret=$client_secret"
  )
  if [[ -n "$scope" ]]; then
    args+=(--data-urlencode "scope=$scope")
  fi

  resp="$(request_json_or_fail "Keycloak" "$url" "${args[@]}")"
  extract_token "$resp"
}

request_auth_api_token() {
  local auth_base_url
  local use_envfile_auth_base_url
  local token_url
  local auth_poc_username
  local auth_poc_password
  local auth_poc_scope
  local username
  local password
  local scope
  local url
  local resp

  auth_base_url="${AUTH_BASE_URL:-}"
  use_envfile_auth_base_url="$(config_value USE_ENVFILE_AUTH_BASE_URL false)"
  if [[ -z "$auth_base_url" && "${use_envfile_auth_base_url,,}" == "true" ]]; then
    auth_base_url="$(read_env_value "$ENV_FILE" AUTH_BASE_URL)"
  fi

  token_url="$(config_value TOKEN_URL /auth/login)"
  auth_poc_username="$(config_value AUTH_POC_USERNAME)"
  auth_poc_password="$(config_value AUTH_POC_PASSWORD)"
  auth_poc_scope="$(config_value AUTH_POC_SCOPE)"
  username="$(config_value USERNAME)"
  password="$(config_value PASSWORD)"
  scope="$(config_value SCOPE)"

  # USERNAME pode existir no ambiente do SO. O par legado USERNAME/PASSWORD so
  # vale como override quando ambos estao preenchidos.
  if [[ -n "$username" && -z "$password" ]]; then
    username=""
  fi
  if [[ -n "$password" && -z "$username" ]]; then
    password=""
  fi

  username="${username:-${auth_poc_username:-local_user}}"
  password="${password:-${auth_poc_password:-local_password}}"
  scope="${scope:-${auth_poc_scope:-ledger.write balance.read}}"
  auth_base_url="${auth_base_url:-http://localhost:5030}"
  url="$(combine_url "$auth_base_url" "$token_url")"

  resp="$(
    request_json_or_fail "Auth.Api" "$url" \
      -X POST \
      -H "Content-Type: application/json" \
      -d "{\"username\":\"$username\",\"password\":\"$password\",\"scope\":\"$scope\"}"
  )"
  extract_token "$resp"
}

if [[ -n "${TOKEN:-}" ]]; then
  printf '%s' "$TOKEN"
  exit 0
fi

provider="$(config_value TOKEN_PROVIDER keycloak)"
provider="${provider,,}"

case "$provider" in
  keycloak)
    request_keycloak_token
    ;;
  auth-api)
    request_auth_api_token
    ;;
  *)
    fail "TOKEN_PROVIDER invalido: '$provider'. Valores aceitos: keycloak|auth-api"
    ;;
esac
