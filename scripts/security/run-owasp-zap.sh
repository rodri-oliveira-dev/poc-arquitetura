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

USE_NGINX=false
LEDGER_URL=""
BALANCE_URL=""
ZAP_IMAGE="ghcr.io/zaproxy/zaproxy:stable"
OUTPUT_ROOT="$ROOT_DIR/zap-reports"
START_STACK=false
NO_BUILD=false
HEALTH_TIMEOUT_SECONDS=90
HEALTH_INTERVAL_SECONDS=3
SWAGGER_PATH="/swagger/v1/swagger.json"
ACTIVE_SCAN=false
FAIL_ON_ALERTS=false
USE_AUTHENTICATION=false
TOKEN=""
CONTAINER_NAME="poc-arquitetura-zap"
ZAP_OPTIONS="-config connection.sslAcceptAll=true"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/security/run-owasp-zap.sh [opcoes]

Opcoes:
  --ledger-url URL     Sobrescreve a URL do LedgerService.Api.
  --balance-url URL    Sobrescreve a URL do BalanceService.Api.
  --use-nginx          Usa URLs HTTPS via Nginx local.
  --zap-image IMAGE    Imagem oficial do OWASP ZAP.
  --output-root DIR    Diretorio raiz dos relatorios.
  --start-stack        Sobe a stack local direta antes do scan.
  --no-build           Ao usar --start-stack, nao rebuilda imagens.
  --health-timeout N   Tempo maximo para aguardar /health em segundos.
  --health-interval N  Intervalo entre tentativas de /health em segundos.
  --swagger-path PATH  Caminho do documento OpenAPI/Swagger em cada API.
  --use-authentication Injeta Authorization Bearer obtido por scripts/validation/get-token.sh.
  --token TOKEN        Token Bearer manual para usar com --use-authentication.
  --active-scan        Executa zap-api-scan.py sem modo seguro. Pode gerar trafego mais invasivo.
  --fail-on-alerts     Propaga alertas do ZAP como falha do script.
  -h, --help           Mostra esta ajuda.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --ledger-url)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      LEDGER_URL="${2:-}"
      shift 2
      ;;
    --balance-url)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      BALANCE_URL="${2:-}"
      shift 2
      ;;
    --use-nginx)
      USE_NGINX=true
      shift
      ;;
    --zap-image)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      ZAP_IMAGE="${2:-}"
      shift 2
      ;;
    --output-root)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      OUTPUT_ROOT="${2:-}"
      shift 2
      ;;
    --start-stack)
      START_STACK=true
      shift
      ;;
    --no-build)
      NO_BUILD=true
      shift
      ;;
    --health-timeout)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      HEALTH_TIMEOUT_SECONDS="${2:-}"
      shift 2
      ;;
    --health-interval)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      HEALTH_INTERVAL_SECONDS="${2:-}"
      shift 2
      ;;
    --swagger-path)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      SWAGGER_PATH="${2:-}"
      shift 2
      ;;
    --use-authentication)
      USE_AUTHENTICATION=true
      shift
      ;;
    --token)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      TOKEN="${2:-}"
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

if [[ -z "$LEDGER_URL" ]]; then
  if [[ "$USE_NGINX" == true ]]; then LEDGER_URL="https://ledger.localhost:7443"; else LEDGER_URL="http://localhost:5226"; fi
fi
if [[ -z "$BALANCE_URL" ]]; then
  if [[ "$USE_NGINX" == true ]]; then BALANCE_URL="https://balance.localhost:7443"; else BALANCE_URL="http://localhost:5228"; fi
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="$OUTPUT_ROOT/$TIMESTAMP"
SCAN_COMMAND="zap-api-scan.py"
SCAN_TYPE="api-baseline"
if [[ "$ACTIVE_SCAN" == true ]]; then
  SCAN_TYPE="api-active"
fi

SCAN_RESULTS=()

cleanup() {
  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  if [[ -d "$OUTPUT_DIR" ]]; then
    write_summary
  fi
}

trap cleanup EXIT

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Comando obrigatorio nao encontrado: $command_name" >&2
    exit 1
  fi
}

assert_docker() {
  if ! docker version >/dev/null 2>&1; then
    echo "Docker nao esta disponivel. Instale/inicie um runtime com Docker-compatible API." >&2
    exit 1
  fi

  if ! docker compose version >/dev/null 2>&1; then
    echo "docker compose nao esta disponivel. Atualize a CLI Docker ou habilite o plugin compose." >&2
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

get_zap_token() {
  if [[ -n "$TOKEN" ]]; then
    printf '%s' "$TOKEN"
    return 0
  fi

  echo "Obtendo token para o ZAP pelo provider local configurado..." >&2
  "$ROOT_DIR/scripts/validation/get-token.sh"
}

enable_zap_authorization_header() {
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

start_local_stack_for_zap() {
  if [[ "$USE_NGINX" == true ]]; then
    echo "--start-stack sobe apenas a stack local direta. Para Nginx, execute ./scripts/local/start-full-stack.sh antes e rode este script com --use-nginx." >&2
    exit 1
  fi

  echo "Iniciando stack local antes do scan ZAP..." >&2
  if [[ "$NO_BUILD" == true ]]; then
    NO_BUILD=true "$ROOT_DIR/scripts/local/start-stack.sh"
  else
    "$ROOT_DIR/scripts/local/start-stack.sh"
  fi
}

health_url() {
  local base_url="$1"
  printf '%s/health' "${base_url%/}"
}

swagger_url() {
  local base_url="$1"
  local path="$SWAGGER_PATH"

  if [[ -z "$path" ]]; then
    path="/swagger/v1/swagger.json"
  fi
  if [[ "$path" != /* ]]; then
    path="/$path"
  fi

  printf '%s%s' "${base_url%/}" "$path"
}

assert_health() {
  local api_name="$1"
  local base_url="$2"
  local url
  local suggestion
  local deadline
  local last_error=""

  url="$(health_url "$base_url")"
  deadline=$((SECONDS + HEALTH_TIMEOUT_SECONDS))

  while (( SECONDS < deadline )); do
    if last_error="$(curl -kfsS --max-time 15 "$url" 2>&1 >/dev/null)"; then
      return 0
    fi

    if (( HEALTH_INTERVAL_SECONDS > 0 )); then
      sleep "$HEALTH_INTERVAL_SECONDS"
    fi
  done

  if [[ "$USE_NGINX" == true ]]; then
    suggestion="Suba a stack completa com Nginx, por exemplo ./scripts/local/start-full-stack.sh, e confirme os certificados locais."
  else
    suggestion="Suba a stack local, por exemplo ./scripts/local/start-stack.sh, ou execute este script com --start-stack."
  fi

  echo "$api_name indisponivel em $url apos ${HEALTH_TIMEOUT_SECONDS}s. $suggestion Ultimo erro: $last_error" >&2
  exit 1
}

url_host() {
  python3 - "$1" <<'PY'
import sys
from urllib.parse import urlparse
print(urlparse(sys.argv[1]).hostname or "")
PY
}

zap_target_url() {
  python3 - "$1" <<'PY'
import sys
from urllib.parse import urlparse, urlunparse

url = sys.argv[1].rstrip("/")
parsed = urlparse(url)
host = parsed.hostname
if host in ("localhost", "127.0.0.1", "::1"):
    netloc = "host.docker.internal"
    if parsed.port:
        netloc = f"{netloc}:{parsed.port}"
    if parsed.username:
        auth = parsed.username
        if parsed.password:
            auth = f"{auth}:{parsed.password}"
        netloc = f"{auth}@{netloc}"
    parsed = parsed._replace(netloc=netloc)
print(urlunparse(parsed).rstrip("/"))
PY
}

docker_host_args() {
  local hosts=("host.docker.internal")
  local host

  local urls=("$LEDGER_URL" "$BALANCE_URL")

  for url in "${urls[@]}"; do
    host="$(url_host "$url")"
    if [[ "$host" == "localhost" || "$host" == *.localhost ]]; then
      hosts+=("$host")
    fi
  done

  printf '%s\n' "${hosts[@]}" | sort -u | while read -r host; do
    [[ -z "$host" ]] && continue
    printf '%s\n' "--add-host" "${host}:host-gateway"
  done
}

run_zap_scan() {
  local api_name="$1"
  local slug="$2"
  local url="$3"
  local target_url
  local openapi_url
  local html="$slug.html"
  local json="$slug.json"
  local markdown="$slug.md"
  local exit_code=0
  local status
  local docker_args=()
  local host_arg

  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  openapi_url="$(swagger_url "$url")"
  target_url="$(zap_target_url "$openapi_url")"

  docker_args=(run --name "$CONTAINER_NAME" -v "$OUTPUT_DIR:/zap/wrk:rw")
  while IFS= read -r host_arg; do
    docker_args+=("$host_arg")
  done < <(docker_host_args)

  docker_args+=(
    "$ZAP_IMAGE"
    "$SCAN_COMMAND"
    -t "$target_url"
    -f openapi
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

  echo "Executando ZAP $SCAN_TYPE em $api_name: $url" >&2
  set +e
  docker "${docker_args[@]}"
  exit_code=$?
  set -e

  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true

  if [[ "$FAIL_ON_ALERTS" == true && "$exit_code" -ne 0 ]]; then
    status="failed-alerts-or-error"
  elif [[ "$exit_code" -ge 3 ]]; then
    status="failed-operational"
  elif [[ "$exit_code" -eq 0 ]]; then
    status="completed"
  else
    status="completed-with-alerts"
  fi

  SCAN_RESULTS+=("$api_name|$url|$openapi_url|$target_url|$status|$exit_code|$html,$json,$markdown")

  if [[ "$exit_code" -ge 3 ]]; then
    echo "Falha operacional no ZAP para $api_name. Exit code: $exit_code" >&2
    exit 1
  fi

  if [[ "$FAIL_ON_ALERTS" == true && "$exit_code" -ne 0 ]]; then
    echo "ZAP encontrou alertas para $api_name e --fail-on-alerts esta ativo. Exit code: $exit_code" >&2
    exit 1
  fi
}

write_summary() {
  local summary_path="$OUTPUT_DIR/summary.md"
  {
    echo "# OWASP ZAP local scan"
    echo
    echo "- Data/hora: $(date '+%Y-%m-%d %H:%M:%S %z')"
    echo "- Imagem ZAP: \`$ZAP_IMAGE\`"
    echo "- Tipo de scan: \`$SCAN_TYPE\`"
    echo "- Definicao OpenAPI: \`$SWAGGER_PATH\`"
    if [[ "$USE_AUTHENTICATION" == true ]]; then
      echo "- Autenticacao Bearer: \`habilitada\`"
    else
      echo "- Autenticacao Bearer: \`desabilitada\`"
    fi
    echo "- Container temporario: \`$CONTAINER_NAME\`"
    echo "- Diretorio de saida: \`$OUTPUT_DIR\`"
    echo
    echo "## APIs analisadas"
    echo

    local result api_name url openapi_url target_url status exit_code files
    for result in "${SCAN_RESULTS[@]}"; do
      IFS='|' read -r api_name url openapi_url target_url status exit_code files <<<"$result"
      echo "- $api_name: \`$url\`"
      echo "  - Swagger/OpenAPI: \`$openapi_url\`"
      echo "  - OpenAPI visto pelo container: \`$target_url\`"
      echo "  - Status: \`$status\`"
      echo "  - Exit code ZAP: \`$exit_code\`"
      echo "  - Arquivos: $files"
    done

    echo
    echo "## Observacoes"
    echo
    echo "- Resultado de DAST local para apoio de desenvolvimento; nao substitui pentest, threat modeling ou validacao de seguranca em ambiente representativo."
    echo "- Relatorios gerados em \`zap-reports/<timestamp>/\` nao devem ser versionados."
    echo "- Por padrao, alertas do ZAP nao tornam o script falho; use \`--fail-on-alerts\` quando quiser propagar alertas como falha."
    if [[ "$ACTIVE_SCAN" == true ]]; then
      echo "- API active scan foi executado por parametro explicito e pode gerar trafego mais invasivo que o baseline seguro."
    else
      echo "- API scan foi executado em modo seguro (\`-S\`), importando OpenAPI sem active scan por padrao."
    fi
    if [[ "$USE_AUTHENTICATION" == true ]]; then
      echo "- Authorization Bearer foi injetado via ZAP Replacer usando token obtido por \`scripts/validation/get-token.sh\` ou por \`--token\`."
    fi
  } >"$summary_path"
}

require_command docker
require_command curl
require_command python3
assert_docker

if [[ "$START_STACK" == true ]]; then
  start_local_stack_for_zap
fi

assert_health "LedgerService.Api" "$LEDGER_URL"
assert_health "BalanceService.Api" "$BALANCE_URL"

if [[ "$USE_AUTHENTICATION" == true ]]; then
  enable_zap_authorization_header "$(get_zap_token)"
fi

ensure_zap_image

mkdir -p "$OUTPUT_DIR"

run_zap_scan "LedgerService.Api" "ledger-service-api" "$LEDGER_URL"
run_zap_scan "BalanceService.Api" "balance-service-api" "$BALANCE_URL"

echo "OK. Relatorios OWASP ZAP em: $OUTPUT_DIR" >&2
