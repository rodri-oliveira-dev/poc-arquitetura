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
  cat >&2 <<'USAGE'
Uso: bash ./scripts/security/run-owasp-zap-all-apis.sh [opcoes]

Executa OWASP ZAP API Scan diretamente em todas as APIs HTTP da POC, sem gateway.

Opcoes gerais:
  --docker-network NETWORK
  --zap-image IMAGE
  --output-root DIR
  --swagger-path PATH
  --health-timeout N
  --health-interval N
  --use-authentication
  --token TOKEN
  --active-scan
  --fail-on-alerts
  -h, --help

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
USAGE
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
    --docker-network) require_value "$1" "${2:-}"; DOCKER_NETWORK="$2"; shift 2 ;;
    --zap-image) require_value "$1" "${2:-}"; ZAP_IMAGE="$2"; shift 2 ;;
    --output-root) require_value "$1" "${2:-}"; OUTPUT_ROOT="$2"; shift 2 ;;
    --swagger-path) require_value "$1" "${2:-}"; SWAGGER_PATH="$2"; shift 2 ;;
    --health-timeout) require_value "$1" "${2:-}"; HEALTH_TIMEOUT_SECONDS="$2"; shift 2 ;;
    --health-interval) require_value "$1" "${2:-}"; HEALTH_INTERVAL_SECONDS="$2"; shift 2 ;;
    --use-authentication) USE_AUTHENTICATION=true; shift ;;
    --token) require_value "$1" "${2:-}"; TOKEN="$2"; shift 2 ;;
    --active-scan) ACTIVE_SCAN=true; shift ;;
    --fail-on-alerts) FAIL_ON_ALERTS=true; shift ;;
    --ledger-url) require_value "$1" "${2:-}"; LEDGER_URL="$2"; shift 2 ;;
    --balance-url) require_value "$1" "${2:-}"; BALANCE_URL="$2"; shift 2 ;;
    --transfer-url) require_value "$1" "${2:-}"; TRANSFER_URL="$2"; shift 2 ;;
    --payment-url) require_value "$1" "${2:-}"; PAYMENT_URL="$2"; shift 2 ;;
    --audit-url) require_value "$1" "${2:-}"; AUDIT_URL="$2"; shift 2 ;;
    --identity-url) require_value "$1" "${2:-}"; IDENTITY_URL="$2"; shift 2 ;;
    --ledger-zap-url) require_value "$1" "${2:-}"; LEDGER_ZAP_URL="$2"; shift 2 ;;
    --balance-zap-url) require_value "$1" "${2:-}"; BALANCE_ZAP_URL="$2"; shift 2 ;;
    --transfer-zap-url) require_value "$1" "${2:-}"; TRANSFER_ZAP_URL="$2"; shift 2 ;;
    --payment-zap-url) require_value "$1" "${2:-}"; PAYMENT_ZAP_URL="$2"; shift 2 ;;
    --audit-zap-url) require_value "$1" "${2:-}"; AUDIT_ZAP_URL="$2"; shift 2 ;;
    --identity-zap-url) require_value "$1" "${2:-}"; IDENTITY_ZAP_URL="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Opcao invalida: $1" >&2; usage; exit 2 ;;
  esac
done

if [[ -n "$TOKEN" && "$USE_AUTHENTICATION" != true ]]; then
  echo "--token exige --use-authentication." >&2
  exit 2
fi

if ! [[ "$HEALTH_TIMEOUT_SECONDS" =~ ^[1-9][0-9]*$ ]]; then
  echo "--health-timeout deve ser um inteiro positivo." >&2
  exit 2
fi
if ! [[ "$HEALTH_INTERVAL_SECONDS" =~ ^[0-9]+$ ]]; then
  echo "--health-interval deve ser um inteiro nao negativo." >&2
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
SCAN_TYPE="api-baseline"
if [[ "$ACTIVE_SCAN" == true ]]; then
  SCAN_TYPE="api-active"
fi

ZAP_IMAGE_IDENTITY="<nao consultado>"
ZAP_WORKDIR_PERMISSION_STRATEGY="<nao preparada>"
FINAL_EXIT_CODE=0
SCAN_RESULTS=()
declare -A OPENAPI_PATH_COUNTS=()
declare -A OPENAPI_OPERATION_COUNTS=()
DOCKER_HOST_ARGS=()

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

remove_zap_container() {
  set +e
  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1
  set -e
}

cleanup() {
  local cleanup_exit_code=0

  set +e
  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1

  if [[ -d "$OUTPUT_DIR" ]]; then
    chmod 0755 "$OUTPUT_DIR" >/dev/null 2>&1
    cleanup_exit_code=$?
    if [[ "$cleanup_exit_code" -ne 0 ]]; then
      echo "Aviso: nao foi possivel restaurar permissao 0755 em $OUTPUT_DIR." >&2
    fi
  fi
  set -e
}
trap cleanup EXIT

assert_docker() {
  if ! docker version >/dev/null 2>&1; then
    echo "Docker nao esta disponivel." >&2
    exit 1
  fi
  if ! docker compose version >/dev/null 2>&1; then
    echo "docker compose nao esta disponivel." >&2
    exit 1
  fi
}

ensure_zap_image() {
  if docker image inspect "$ZAP_IMAGE" >/dev/null 2>&1; then
    return 0
  fi

  echo "Imagem ZAP nao encontrada localmente. Baixando $ZAP_IMAGE..." >&2
  docker pull "$ZAP_IMAGE"
}

assert_network() {
  if [[ -z "$DOCKER_NETWORK" ]]; then
    return 0
  fi
  if ! docker network inspect "$DOCKER_NETWORK" >/dev/null 2>&1; then
    echo "Rede Docker informada para o ZAP nao existe: $DOCKER_NETWORK" >&2
    exit 1
  fi
}

detect_zap_image_identity() {
  local output
  local exit_code=0

  set +e
  output="$(docker run --rm --entrypoint sh "$ZAP_IMAGE" -c 'id && printf "UID_GID=%s:%s\n" "$(id -u)" "$(id -g)"' 2>&1)"
  exit_code=$?
  set -e

  if [[ "$exit_code" -eq 0 ]]; then
    ZAP_IMAGE_IDENTITY="$output"
    return 0
  fi

  ZAP_IMAGE_IDENTITY="falha ao consultar usuario da imagem ZAP: $output"
  return 1
}

zap_image_uid() {
  printf '%s\n' "$ZAP_IMAGE_IDENTITY" |
    sed -n 's/^UID_GID=\([0-9][0-9]*\):[0-9][0-9]*$/\1/p' |
    tail -n 1
}

prepare_zap_workdir() {
  local zap_uid=""

  mkdir -p "$OUTPUT_DIR"
  if ! detect_zap_image_identity; then
    echo "Aviso: nao foi possivel detectar o UID/GID da imagem ZAP; usando fallback restrito ao diretorio timestampado." >&2
  fi
  zap_uid="$(zap_image_uid)"

  if [[ -n "$zap_uid" ]] && command -v setfacl >/dev/null 2>&1; then
    if setfacl -m "u:${zap_uid}:rwx" "$OUTPUT_DIR"; then
      ZAP_WORKDIR_PERMISSION_STRATEGY="setfacl u:${zap_uid}:rwx em $OUTPUT_DIR"
      return 0
    fi
  fi

  chmod 0777 "$OUTPUT_DIR"
  ZAP_WORKDIR_PERMISSION_STRATEGY="chmod 0777 somente no diretorio timestampado $OUTPUT_DIR"
}

docker_network_args() {
  if [[ -n "$DOCKER_NETWORK" ]]; then
    printf '%s\n' "--network" "$DOCKER_NETWORK"
  fi
}

docker_host_args() {
  if [[ "${#DOCKER_HOST_ARGS[@]}" -gt 0 ]]; then
    printf '%s\n' "${DOCKER_HOST_ARGS[@]}"
  fi
}

assert_zap_workdir_writable() {
  local docker_args=(run --rm -v "$OUTPUT_DIR:/zap/wrk:rw")
  local arg
  local output
  local exit_code=0

  while IFS= read -r arg; do docker_args+=("$arg"); done < <(docker_network_args)

  set +e
  output="$(docker "${docker_args[@]}" "$ZAP_IMAGE" sh -c 'test -d /zap/wrk && test -w /zap/wrk && temp_file="$(mktemp /zap/wrk/.zap-write-test.XXXXXX)" && rm -f "$temp_file"' 2>&1)"
  exit_code=$?
  set -e

  if [[ "$exit_code" -ne 0 ]]; then
    echo "Falha operacional: /zap/wrk nao esta gravavel pelo usuario da imagem ZAP." >&2
    echo "  Caminho absoluto montado: $OUTPUT_DIR" >&2
    echo "  UID/GID usados pela imagem ZAP: $ZAP_IMAGE_IDENTITY" >&2
    echo "  Estrategia aplicada: $ZAP_WORKDIR_PERMISSION_STRATEGY" >&2
    echo "  Saida completa: $output" >&2
    exit 1
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
  python3 - "$1" <<'PY'
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

initialize_docker_host_args() {
  local url
  local host

  for url in "${API_ZAP_URLS[@]}"; do
    host="$(url_host "$url")"
    if [[ "$host" == "localhost" || "$host" == "127.0.0.1" || "$host" == "::1" ]]; then
      DOCKER_HOST_ARGS=(--add-host "host.docker.internal:host-gateway")
      return 0
    fi
  done
}

assert_health() {
  local api_name="$1"
  local base_url="$2"
  local target
  local interval_divisor="$HEALTH_INTERVAL_SECONDS"
  local attempts
  local attempt

  target="$(health_url "$base_url")"
  if [[ "$interval_divisor" -eq 0 ]]; then
    interval_divisor=1
  fi
  attempts=$((HEALTH_TIMEOUT_SECONDS / interval_divisor))
  if [[ "$attempts" -lt 1 ]]; then
    attempts=1
  fi

  for ((attempt = 1; attempt <= attempts; attempt++)); do
    if curl -fsS "$target" >/dev/null; then
      echo "Health check OK: $api_name ($target)" >&2
      return 0
    fi
    if [[ "$HEALTH_INTERVAL_SECONDS" -gt 0 ]]; then
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

validate_openapi() {
  local api_name="$1"
  local zap_url="$2"
  local effective_base
  local target
  local docker_args=(run --rm)
  local arg
  local output
  local path_count=0
  local operation_count=0
  local line

  effective_base="$(zap_base_url "$zap_url")"
  target="$(swagger_url "$effective_base")"

  while IFS= read -r arg; do docker_args+=("$arg"); done < <(docker_network_args)
  while IFS= read -r arg; do docker_args+=("$arg"); done < <(docker_host_args)

  output="$(docker "${docker_args[@]}" "$ZAP_IMAGE" python3 - "$target" <<'PY'
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

  while IFS= read -r line; do
    case "$line" in
      PATHS=*) path_count="${line#PATHS=}" ;;
      OPERATIONS=*) operation_count="${line#OPERATIONS=}" ;;
    esac
  done <<<"$output"

  if [[ "$path_count" -lt 1 || "$operation_count" -lt 1 ]]; then
    echo "Cobertura OpenAPI invalida para $api_name: paths=$path_count, operacoes=$operation_count." >&2
    exit 1
  fi

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
  local target
  local html="$slug.html"
  local json="$slug.json"
  local markdown="$slug.md"
  local log_file="$slug.log"
  local docker_args=()
  local arg
  local exit_code=0
  local status

  effective_base="$(zap_base_url "$zap_url")"
  target="$(swagger_url "$effective_base")"
  remove_zap_container

  docker_args=(run --name "$CONTAINER_NAME" -v "$OUTPUT_DIR:/zap/wrk:rw")
  while IFS= read -r arg; do docker_args+=("$arg"); done < <(docker_network_args)
  while IFS= read -r arg; do docker_args+=("$arg"); done < <(docker_host_args)

  docker_args+=(
    "$ZAP_IMAGE"
    zap-api-scan.py
    -t "$target"
    -f openapi
    -O "$effective_base"
    -r "$html"
    -J "$json"
    -w "$markdown"
    -z "$ZAP_OPTIONS"
  )
  if [[ "$ACTIVE_SCAN" != true ]]; then
    docker_args+=(-S)
  fi
  if [[ "$FAIL_ON_ALERTS" != true ]]; then
    docker_args+=(-I)
  fi

  echo "Executando ZAP $SCAN_TYPE em $api_name; operacoes=${OPENAPI_OPERATION_COUNTS[$api_name]}; alvo=$target" >&2

  set +e
  docker "${docker_args[@]}" >"$OUTPUT_DIR/$log_file" 2>&1
  exit_code=$?
  set -e
  remove_zap_container

  if [[ "$exit_code" -ge 3 ]]; then
    status="failed-operational"
    FINAL_EXIT_CODE="$exit_code"
  elif [[ "$exit_code" -eq 0 ]]; then
    status="completed"
  else
    status="completed-with-alerts"
    if [[ "$FAIL_ON_ALERTS" == true && "$FINAL_EXIT_CODE" -eq 0 ]]; then
      FINAL_EXIT_CODE="$exit_code"
    fi
  fi

  SCAN_RESULTS+=(
    "$api_name|$slug|$host_url|$target|$effective_base|${OPENAPI_PATH_COUNTS[$api_name]}|${OPENAPI_OPERATION_COUNTS[$api_name]}|$status|$exit_code|$html,$json,$markdown,$log_file"
  )
}

write_summary() {
  local summary="$OUTPUT_DIR/summary.md"
  local auth_status="desabilitada"
  local total_operations=0
  local result
  local api_name slug host_url target effective_base path_count operation_count status exit_code files

  if [[ "$USE_AUTHENTICATION" == true ]]; then
    auth_status="habilitada"
  fi

  {
    echo "# OWASP ZAP - todas as APIs"
    echo
    echo "- Data/hora: $(date '+%Y-%m-%d %H:%M:%S %z')"
    echo "- Imagem ZAP: \`$ZAP_IMAGE\`"
    echo "- Tipo de scan: \`$SCAN_TYPE\`"
    echo "- Autenticacao Bearer: \`$auth_status\`"
    echo "- Gateway: \`nao utilizado\`"
    echo "- Rede Docker: \`${DOCKER_NETWORK:-<padrao do Docker>}\`"
    echo "- Caminho OpenAPI: \`$SWAGGER_PATH\`"
    echo "- Usuario da imagem ZAP: \`$(printf '%s' "$ZAP_IMAGE_IDENTITY" | tr '\n' ' ')\`"
    echo "- Preparacao do diretorio: \`$ZAP_WORKDIR_PERMISSION_STRATEGY\`"
    echo
    echo "## APIs analisadas"
    echo

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
    echo "- Total de operacoes HTTP declaradas: \`$total_operations\`"
    echo "- Cada contrato deve conter ao menos um path e uma operacao HTTP."
    echo
    echo "## Observacoes"
    echo
    echo "- Cada API foi analisada diretamente pelo proprio OpenAPI; nenhum gateway foi criado ou utilizado."
    echo "- O modo padrao e seguro (\`-S\`) e nao executa active scan."
    echo "- O resultado complementa, mas nao substitui pentest e threat modeling."
  } >"$summary"
}

main() {
  local index

  require_command docker
  require_command curl
  require_command python3
  assert_docker
  assert_network
  ensure_zap_image
  initialize_docker_host_args

  OUTPUT_DIR="$(absolute_path "$OUTPUT_DIR")"
  prepare_zap_workdir
  assert_zap_workdir_writable

  if [[ "$USE_AUTHENTICATION" == true ]]; then
    enable_authorization "$(get_token)"
  fi

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
