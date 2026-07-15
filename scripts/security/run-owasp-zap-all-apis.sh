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

ZAP_IMAGE="ghcr.io/zaproxy/zaproxy:stable"
OUTPUT_ROOT="$ROOT_DIR/zap-reports"
DOCKER_NETWORK=""
HEALTH_TIMEOUT_SECONDS=180
HEALTH_INTERVAL_SECONDS=5
SWAGGER_PATH="/swagger/v1/swagger.json"
ACTIVE_SCAN=false
FAIL_ON_ALERTS=false
USE_AUTHENTICATION=false
TOKEN=""
CONTAINER_NAME="poc-arquitetura-zap-all-apis"
ZAP_OPTIONS="-config connection.sslAcceptAll=true"

LEDGER_URL="http://localhost:5226"
BALANCE_URL="http://localhost:5228"
TRANSFER_URL="http://localhost:5230"
PAYMENT_URL="http://localhost:5234"
AUDIT_URL="http://localhost:5235"
IDENTITY_URL="http://localhost:5232"

LEDGER_ZAP_URL=""
BALANCE_ZAP_URL=""
TRANSFER_ZAP_URL=""
PAYMENT_ZAP_URL=""
AUDIT_ZAP_URL=""
IDENTITY_ZAP_URL=""

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/security/run-owasp-zap-all-apis.sh [opcoes]

Executa OWASP ZAP API Scan em todas as APIs HTTP da POC, diretamente, sem gateway.

Opcoes gerais:
  --docker-network NETWORK       Rede Docker compartilhada pelas APIs e pelo ZAP.
  --zap-image IMAGE              Imagem oficial do OWASP ZAP.
  --output-root DIR              Diretorio raiz dos relatorios.
  --swagger-path PATH            Caminho OpenAPI comum a todas as APIs.
  --health-timeout N             Timeout por API para /health, em segundos.
  --health-interval N            Intervalo entre tentativas de /health.
  --use-authentication           Injeta Authorization Bearer obtido por get-token.sh.
  --token TOKEN                  Token Bearer manual; exige --use-authentication.
  --active-scan                  Executa active scan. O padrao usa modo seguro (-S).
  --fail-on-alerts               Propaga alertas do ZAP como falha.
  -h, --help                     Mostra esta ajuda.

URLs vistas pelo host:
  --ledger-url URL
  --balance-url URL
  --transfer-url URL
  --payment-url URL
  --audit-url URL
  --identity-url URL

URLs vistas pelo container ZAP:
  --ledger-zap-url URL
  --balance-zap-url URL
  --transfer-zap-url URL
  --payment-zap-url URL
  --audit-zap-url URL
  --identity-zap-url URL
EOF
}

require_value() {
  local option="$1"
  local value="${2:-}"
  if [[ -z "$value" || "$value" == --* ]]; then
    echo "$option exige um valor." >&2
    usage
    exit 2
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --docker-network)
      require_value "$1" "${2:-}"
      DOCKER_NETWORK="$2"
      shift 2
      ;;
    --zap-image)
      require_value "$1" "${2:-}"
      ZAP_IMAGE="$2"
      shift 2
      ;;
    --output-root)
      require_value "$1" "${2:-}"
      OUTPUT_ROOT="$2"
      shift 2
      ;;
    --swagger-path)
      require_value "$1" "${2:-}"
      SWAGGER_PATH="$2"
      shift 2
      ;;
    --health-timeout)
      require_value "$1" "${2:-}"
      HEALTH_TIMEOUT_SECONDS="$2"
      shift 2
      ;;
    --health-interval)
      require_value "$1" "${2:-}"
      HEALTH_INTERVAL_SECONDS="$2"
      shift 2
      ;;
    --use-authentication)
      USE_AUTHENTICATION=true
      shift
      ;;
    --token)
      require_value "$1" "${2:-}"
      TOKEN="$2"
      shift 2
      ;;
    --active-scan)
      ACTIVE_SCAN=true
      shift
      ;;
    --fail-on-alerts)
      FAIL_ON_ALERTS=true
      shift
      ;;
    --ledger-url)
      require_value "$1" "${2:-}"
      LEDGER_URL="$2"
      shift 2
      ;;
    --balance-url)
      require_value "$1" "${2:-}"
      BALANCE_URL="$2"
      shift 2
      ;;
    --transfer-url)
      require_value "$1" "${2:-}"
      TRANSFER_URL="$2"
      shift 2
      ;;
    --payment-url)
      require_value "$1" "${2:-}"
      PAYMENT_URL="$2"
      shift 2
      ;;
    --audit-url)
      require_value "$1" "${2:-}"
      AUDIT_URL="$2"
      shift 2
      ;;
    --identity-url)
      require_value "$1" "${2:-}"
      IDENTITY_URL="$2"
      shift 2
      ;;
    --ledger-zap-url)
      require_value "$1" "${2:-}"
      LEDGER_ZAP_URL="$2"
      shift 2
      ;;
    --balance-zap-url)
      require_value "$1" "${2:-}"
      BALANCE_ZAP_URL="$2"
      shift 2
      ;;
    --transfer-zap-url)
      require_value "$1" "${2:-}"
      TRANSFER_ZAP_URL="$2"
      shift 2
      ;;
    --payment-zap-url)
      require_value "$1" "${2:-}"
      PAYMENT_ZAP_URL="$2"
      shift 2
      ;;
    --audit-zap-url)
      require_value "$1" "${2:-}"
      AUDIT_ZAP_URL="$2"
      shift 2
      ;;
    --identity-zap-url)
      require_value "$1" "${2:-}"
      IDENTITY_ZAP_URL="$2"
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

if [[ -n "$TOKEN" && "$USE_AUTHENTICATION" != true ]]; then
  echo "--token exige --use-authentication." >&2
  exit 2
fi

SWAGGER_PATH="/${SWAGGER_PATH#/}"

LEDGER_ZAP_URL="${LEDGER_ZAP_URL:-$LEDGER_URL}"
BALANCE_ZAP_URL="${BALANCE_ZAP_URL:-$BALANCE_URL}"
TRANSFER_ZAP_URL="${TRANSFER_ZAP_URL:-$TRANSFER_URL}"
PAYMENT_ZAP_URL="${PAYMENT_ZAP_URL:-$PAYMENT_URL}"
AUDIT_ZAP_URL="${AUDIT_ZAP_URL:-$AUDIT_URL}"
IDENTITY_ZAP_URL="${IDENTITY_ZAP_URL:-$IDENTITY_URL}"

API_NAMES=(
  "LedgerService.Api"
  "BalanceService.Api"
  "TransferService.Api"
  "PaymentService.Api"
  "AuditService.Api"
  "IdentityService.Api"
)
API_SLUGS=(
  "ledger-service-api"
  "balance-service-api"
  "transfer-service-api"
  "payment-service-api"
  "audit-service-api"
  "identity-service-api"
)
API_HOST_URLS=(
  "$LEDGER_URL"
  "$BALANCE_URL"
  "$TRANSFER_URL"
  "$PAYMENT_URL"
  "$AUDIT_URL"
  "$IDENTITY_URL"
)
API_ZAP_URLS=(
  "$LEDGER_ZAP_URL"
  "$BALANCE_ZAP_URL"
  "$TRANSFER_ZAP_URL"
  "$PAYMENT_ZAP_URL"
  "$AUDIT_ZAP_URL"
  "$IDENTITY_ZAP_URL"
)

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="$OUTPUT_ROOT/$TIMESTAMP"
SCAN_COMMAND="zap-api-scan.py"
SCAN_TYPE="api-baseline"
if [[ "$ACTIVE_SCAN" == true ]]; then
  SCAN_TYPE="api-active"
fi

declare -A OPENAPI_PATH_COUNTS=()
declare -A OPENAPI_OPERATION_COUNTS=()
SCAN_RESULTS=()
FINAL_EXIT_CODE=0

absolute_path() {
  python3 - "$1" <<'PY'
import os
import sys
print(os.path.abspath(sys.argv[1]))
PY
}

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Comando obrigatorio nao encontrado: $command_name" >&2
    exit 1
  fi
}

cleanup() {
  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  if [[ -d "$OUTPUT_DIR" ]]; then
    chmod 0755 "$OUTPUT_DIR" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

assert_docker() {
  docker version >/dev/null
  docker compose version >/dev/null
}

ensure_zap_image() {
  if ! docker image inspect "$ZAP_IMAGE" >/dev/null 2>&1; then
    echo "Imagem ZAP nao encontrada localmente. Baixando $ZAP_IMAGE..." >&2
    docker pull "$ZAP_IMAGE"
  fi
}

assert_network() {
  if [[ -n "$DOCKER_NETWORK" ]]; then
    docker network inspect "$DOCKER_NETWORK" >/dev/null
  fi
}

health_url() {
  printf '%s/health' "${1%/}"
}

swagger_url() {
  printf '%s%s' "${1%/}" "$SWAGGER_PATH"
}

url_host() {
  python3 - "$1" <<'PY'
import sys
from urllib.parse import urlparse
print(urlparse(sys.argv[1]).hostname or "")
PY
}

zap_base_url() {
  local url="$1"
  python3 - "$url" <<'PY'
import sys
from urllib.parse import urlparse, urlunparse

parsed = urlparse(sys.argv[1])
host = parsed.hostname or ""
if host in {"localhost", "127.0.0.1", "::1"}:
    netloc = "host.docker.internal"
    if parsed.port:
        netloc += f":{parsed.port}"
    parsed = parsed._replace(netloc=netloc)
print(urlunparse(parsed).rstrip("/"))
PY
}

docker_network_args() {
  if [[ -n "$DOCKER_NETWORK" ]]; then
    printf '%s\n' "--network" "$DOCKER_NETWORK"
  fi
}

DOCKER_HOST_ARGS=()

initialize_docker_host_args() {
  local host
  local url
  for url in "${API_ZAP_URLS[@]}"; do
    host="$(url_host "$url")"
    if [[ "$host" == "localhost" || "$host" == "127.0.0.1" || "$host" == "::1" ]]; then
      DOCKER_HOST_ARGS=(--add-host "host.docker.internal:host-gateway")
      return 0
    fi
  done
}

docker_host_args() {
  if [[ "${#DOCKER_HOST_ARGS[@]}" -gt 0 ]]; then
    printf '%s\n' "${DOCKER_HOST_ARGS[@]}"
  fi
}

assert_health() {
  local api_name="$1"
  local base_url="$2"
  local target
  target="$(health_url "$base_url")"

  local attempts=$((HEALTH_TIMEOUT_SECONDS / (HEALTH_INTERVAL_SECONDS > 0 ? HEALTH_INTERVAL_SECONDS : 1)))
  if (( attempts < 1 )); then
    attempts=1
  fi

  local i
  for ((i = 1; i <= attempts; i++)); do
    if curl -fsS "$target" >/dev/null; then
      echo "Health check OK: $api_name ($target)" >&2
      return 0
    fi
    if (( HEALTH_INTERVAL_SECONDS > 0 )); then
      sleep "$HEALTH_INTERVAL_SECONDS"
    fi
  done

  echo "$api_name indisponivel em $target apos ${HEALTH_TIMEOUT_SECONDS}s." >&2
  exit 1
}

get_token() {
  if [[ -n "$TOKEN" ]]; then
    printf '%s' "$TOKEN"
    return 0
  fi

  echo "Obtendo token para o ZAP pelo provider local configurado..." >&2
  "$ROOT_DIR/scripts/validation/get-token.sh"
}

enable_authorization() {
  local access_token="$1"
  if [[ -z "$access_token" ]]; then
    echo "Token vazio para configurar Authorization no ZAP." >&2
    exit 1
  fi

  ZAP_OPTIONS="-config connection.sslAcceptAll=true"
  ZAP_OPTIONS+=" -config replacer.full_list(0).description=authorization-header"
  ZAP_OPTIONS+=" -config replacer.full_list(0).enabled=true"
  ZAP_OPTIONS+=" -config replacer.full_list(0).matchtype=REQ_HEADER"
  ZAP_OPTIONS+=" -config replacer.full_list(0).matchstr=Authorization"
  ZAP_OPTIONS+=" -config replacer.full_list(0).regex=false"
  ZAP_OPTIONS+=" -config replacer.full_list(0).replacement=Bearer $access_token"
}

prepare_output_dir() {
  mkdir -p "$OUTPUT_DIR"
  chmod 0777 "$OUTPUT_DIR"
}

assert_output_writable() {
  local args=(run --rm -v "$OUTPUT_DIR:/zap/wrk:rw")
  local value
  while IFS= read -r value; do args+=("$value"); done < <(docker_network_args)
  docker "${args[@]}" "$ZAP_IMAGE" sh -c \
    'test -d /zap/wrk && test -w /zap/wrk && touch /zap/wrk/.zap-write-test && rm /zap/wrk/.zap-write-test'
}

validate_openapi() {
  local api_name="$1"
  local zap_url="$2"
  local effective_base
  effective_base="$(zap_base_url "$zap_url")"
  local target
  target="$(swagger_url "$effective_base")"

  local args=(run --rm)
  local value
  while IFS= read -r value; do args+=("$value"); done < <(docker_network_args)
  while IFS= read -r value; do args+=("$value"); done < <(docker_host_args)

  local output
  output="$(
    docker "${args[@]}" "$ZAP_IMAGE" python3 - "$target" <<'PY'
import json
import sys
from urllib.request import Request, urlopen

url = sys.argv[1]
request = Request(url, headers={"Accept": "application/json"})
with urlopen(request, timeout=30) as response:
    if response.status < 200 or response.status >= 400:
        raise RuntimeError(f"HTTP {response.status}")
    document = json.load(response)

if not isinstance(document, dict) or not (document.get("openapi") or document.get("swagger")):
    raise RuntimeError("Documento nao e OpenAPI/Swagger valido")

paths = document.get("paths")
if not isinstance(paths, dict) or not paths:
    raise RuntimeError("Documento OpenAPI nao contem paths")

http_methods = {"get", "put", "post", "delete", "options", "head", "patch", "trace"}
operation_count = sum(
    1
    for path_item in paths.values()
    if isinstance(path_item, dict)
    for method in path_item
    if method.lower() in http_methods
)
if operation_count == 0:
    raise RuntimeError("Documento OpenAPI nao contem operacoes HTTP")

print(f"PATHS={len(paths)}")
print(f"OPERATIONS={operation_count}")
PY
  )"

  local path_count="0"
  local operation_count="0"
  local line
  while IFS= read -r line; do
    case "$line" in
      PATHS=*) path_count="${line#PATHS=}" ;;
      OPERATIONS=*) operation_count="${line#OPERATIONS=}" ;;
    esac
  done <<<"$output"

  OPENAPI_PATH_COUNTS["$api_name"]="$path_count"
  OPENAPI_OPERATION_COUNTS["$api_name"]="$operation_count"

  echo "OpenAPI validado: $api_name; paths=$path_count; operacoes=$operation_count; alvo=$target" >&2
}

run_scan() {
  local api_name="$1"
  local slug="$2"
  local host_url="$3"
  local zap_url="$4"

  local effective_base
  effective_base="$(zap_base_url "$zap_url")"
  local target
  target="$(swagger_url "$effective_base")"

  local html="$slug.html"
  local json="$slug.json"
  local markdown="$slug.md"
  local log_file="$slug.log"

  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

  local args=(run --name "$CONTAINER_NAME" -v "$OUTPUT_DIR:/zap/wrk:rw")
  local value
  while IFS= read -r value; do args+=("$value"); done < <(docker_network_args)
  while IFS= read -r value; do args+=("$value"); done < <(docker_host_args)

  args+=(
    "$ZAP_IMAGE"
    "$SCAN_COMMAND"
    -t "$target"
    -f openapi
    -O "$effective_base"
    -r "$html"
    -J "$json"
    -w "$markdown"
    -z "$ZAP_OPTIONS"
  )

  if [[ "$ACTIVE_SCAN" != true ]]; then
    args+=(-S)
  fi
  if [[ "$FAIL_ON_ALERTS" != true ]]; then
    args+=(-I)
  fi

  echo "Executando ZAP $SCAN_TYPE em $api_name; operacoes=${OPENAPI_OPERATION_COUNTS[$api_name]}; alvo=$target" >&2

  local exit_code=0
  set +e
  docker "${args[@]}" >"$OUTPUT_DIR/$log_file" 2>&1
  exit_code=$?
  set -e

  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

  local status
  if (( exit_code >= 3 )); then
    status="failed-operational"
  elif (( exit_code == 0 )); then
    status="completed"
  else
    status="completed-with-alerts"
  fi

  SCAN_RESULTS+=(
    "$api_name|$slug|$host_url|$target|$effective_base|${OPENAPI_PATH_COUNTS[$api_name]}|${OPENAPI_OPERATION_COUNTS[$api_name]}|$status|$exit_code|$html,$json,$markdown,$log_file"
  )

  if (( exit_code >= 3 )); then
    FINAL_EXIT_CODE="$exit_code"
  elif [[ "$FAIL_ON_ALERTS" == true && "$exit_code" -ne 0 && "$FINAL_EXIT_CODE" -eq 0 ]]; then
    FINAL_EXIT_CODE="$exit_code"
  fi
}

write_summary() {
  local summary="$OUTPUT_DIR/summary.md"
  {
    echo "# OWASP ZAP - todas as APIs"
    echo
    echo "- Data/hora: $(date '+%Y-%m-%d %H:%M:%S %z')"
    echo "- Imagem ZAP: \`$ZAP_IMAGE\`"
    echo "- Tipo de scan: \`$SCAN_TYPE\`"
    echo "- Autenticacao Bearer: \`$([[ "$USE_AUTHENTICATION" == true ]] && echo habilitada || echo desabilitada)\`"
    echo "- Gateway: \`nao utilizado\`"
    echo "- Rede Docker: \`${DOCKER_NETWORK:-<padrao do Docker>}\`"
    echo "- Caminho OpenAPI: \`$SWAGGER_PATH\`"
    echo
    echo "## APIs analisadas"
    echo

    local result
    local api_name slug host_url target effective_base path_count operation_count status exit_code files
    local total_operations=0
    for result in "${SCAN_RESULTS[@]}"; do
      IFS='|' read -r api_name slug host_url target effective_base path_count operation_count status exit_code files <<<"$result"
      total_operations=$((total_operations + operation_count))
      echo "- $api_name"
      echo "  - URL do host: \`$host_url\`"
      echo "  - OpenAPI visto pelo ZAP: \`$target\`"
      echo "  - Servidor efetivo: \`$effective_base\`"
      echo "  - Paths declarados: \`$path_count\`"
      echo "  - Operacoes HTTP declaradas: \`$operation_count\`"
      echo "  - Status: \`$status\`"
      echo "  - Exit code ZAP: \`$exit_code\`"
      echo "  - Arquivos: $files"
    done

    echo
    echo "## Cobertura de contrato"
    echo
    echo "- APIs esperadas: \`${#API_NAMES[@]}\`"
    echo "- APIs analisadas: \`${#SCAN_RESULTS[@]}\`"
    echo "- Total de operacoes HTTP declaradas nos contratos: \`$total_operations\`"
    echo "- Criterio: cada documento OpenAPI deve conter ao menos um path e uma operacao HTTP."
    echo
    echo "## Observacoes"
    echo
    echo "- O scan importa cada contrato OpenAPI diretamente; nenhum gateway foi criado ou utilizado."
    echo "- O modo padrao e seguro (\`-S\`) e nao executa active scan."
    echo "- O resultado complementa, mas nao substitui pentest e threat modeling."
  } >"$summary"
}

main() {
  require_command docker
  require_command curl
  require_command python3
  assert_docker
  assert_network
  ensure_zap_image
  initialize_docker_host_args

  OUTPUT_DIR="$(absolute_path "$OUTPUT_DIR")"
  prepare_output_dir
  assert_output_writable

  if [[ "$USE_AUTHENTICATION" == true ]]; then
    enable_authorization "$(get_token)"
  fi

  local index
  for index in "${!API_NAMES[@]}"; do
    assert_health "${API_NAMES[$index]}" "${API_HOST_URLS[$index]}"
  done

  for index in "${!API_NAMES[@]}"; do
    validate_openapi "${API_NAMES[$index]}" "${API_ZAP_URLS[$index]}"
  done

  for index in "${!API_NAMES[@]}"; do
    run_scan \
      "${API_NAMES[$index]}" \
      "${API_SLUGS[$index]}" \
      "${API_HOST_URLS[$index]}" \
      "${API_ZAP_URLS[$index]}"
  done

  write_summary

  if [[ "${#SCAN_RESULTS[@]}" -ne "${#API_NAMES[@]}" ]]; then
    echo "Cobertura incompleta: esperado ${#API_NAMES[@]} APIs, analisadas ${#SCAN_RESULTS[@]}." >&2
    exit 1
  fi

  if [[ "$FINAL_EXIT_CODE" -ne 0 ]]; then
    echo "OWASP ZAP concluiu com exit code final $FINAL_EXIT_CODE." >&2
    exit "$FINAL_EXIT_CODE"
  fi

  echo "OK. Relatorios OWASP ZAP em: $OUTPUT_DIR" >&2
}

main
