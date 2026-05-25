#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

USE_NGINX=false
AUTH_URL=""
LEDGER_URL=""
BALANCE_URL=""
ZAP_IMAGE="ghcr.io/zaproxy/zaproxy:stable"
OUTPUT_ROOT="$ROOT_DIR/zap-reports"
ACTIVE_SCAN=false
FAIL_ON_ALERTS=false
CONTAINER_NAME="poc-arquitetura-zap"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/run-owasp-zap.sh [opcoes]

Opcoes:
  --auth-url URL       Sobrescreve a URL do Auth.Api.
  --ledger-url URL     Sobrescreve a URL do LedgerService.Api.
  --balance-url URL    Sobrescreve a URL do BalanceService.Api.
  --use-nginx          Usa URLs HTTPS via Nginx local.
  --zap-image IMAGE    Imagem oficial do OWASP ZAP.
  --output-root DIR    Diretorio raiz dos relatorios.
  --active-scan        Executa zap-full-scan.py. Pode gerar trafego mais invasivo.
  --fail-on-alerts     Propaga alertas do ZAP como falha do script.
  -h, --help           Mostra esta ajuda.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --auth-url)
      AUTH_URL="${2:-}"
      shift 2
      ;;
    --ledger-url)
      LEDGER_URL="${2:-}"
      shift 2
      ;;
    --balance-url)
      BALANCE_URL="${2:-}"
      shift 2
      ;;
    --use-nginx)
      USE_NGINX=true
      shift
      ;;
    --zap-image)
      ZAP_IMAGE="${2:-}"
      shift 2
      ;;
    --output-root)
      OUTPUT_ROOT="${2:-}"
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

if [[ -z "$AUTH_URL" ]]; then
  if [[ "$USE_NGINX" == true ]]; then AUTH_URL="https://auth.localhost:7443"; else AUTH_URL="http://localhost:5030"; fi
fi
if [[ -z "$LEDGER_URL" ]]; then
  if [[ "$USE_NGINX" == true ]]; then LEDGER_URL="https://ledger.localhost:7443"; else LEDGER_URL="http://localhost:5226"; fi
fi
if [[ -z "$BALANCE_URL" ]]; then
  if [[ "$USE_NGINX" == true ]]; then BALANCE_URL="https://balance.localhost:7443"; else BALANCE_URL="http://localhost:5228"; fi
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="$OUTPUT_ROOT/$TIMESTAMP"
SCAN_COMMAND="zap-baseline.py"
SCAN_TYPE="baseline"
if [[ "$ACTIVE_SCAN" == true ]]; then
  SCAN_COMMAND="zap-full-scan.py"
  SCAN_TYPE="active"
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

health_url() {
  local base_url="$1"
  printf '%s/health' "${base_url%/}"
}

assert_health() {
  local api_name="$1"
  local base_url="$2"
  local url
  local suggestion

  url="$(health_url "$base_url")"
  if ! curl -kfsS --max-time 15 "$url" >/dev/null; then
    if [[ "$USE_NGINX" == true ]]; then
      suggestion="Suba a stack completa com Nginx, por exemplo ./scripts/start-full-stack.sh, e confirme os certificados locais."
    else
      suggestion="Suba a stack local, por exemplo ./scripts/start-local-stack.sh, ou informe --use-nginx para validar via borda local."
    fi

    echo "$api_name indisponivel em $url. $suggestion" >&2
    exit 1
  fi
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

  for url in "$AUTH_URL" "$LEDGER_URL" "$BALANCE_URL"; do
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
  local html="$slug.html"
  local json="$slug.json"
  local markdown="$slug.md"
  local exit_code=0
  local status
  local docker_args=()
  local host_arg

  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  target_url="$(zap_target_url "$url")"

  docker_args=(run --name "$CONTAINER_NAME" -v "$OUTPUT_DIR:/zap/wrk:rw")
  while IFS= read -r host_arg; do
    docker_args+=("$host_arg")
  done < <(docker_host_args)

  docker_args+=(
    "$ZAP_IMAGE"
    "$SCAN_COMMAND"
    -t "$target_url"
    -r "$html"
    -J "$json"
    -w "$markdown"
    -z "-config connection.sslAcceptAll=true"
  )

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

  SCAN_RESULTS+=("$api_name|$url|$target_url|$status|$exit_code|$html,$json,$markdown")

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
    echo "- Container temporario: \`$CONTAINER_NAME\`"
    echo "- Diretorio de saida: \`$OUTPUT_DIR\`"
    echo
    echo "## APIs analisadas"
    echo

    local result api_name url target_url status exit_code files
    for result in "${SCAN_RESULTS[@]}"; do
      IFS='|' read -r api_name url target_url status exit_code files <<<"$result"
      echo "- $api_name: \`$url\`"
      echo "  - Alvo visto pelo container: \`$target_url\`"
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
      echo "- Active scan foi executado por parametro explicito e pode gerar trafego mais invasivo que o baseline scan."
    fi
  } >"$summary_path"
}

require_command docker
require_command curl
require_command python3
assert_docker

assert_health "Auth.Api" "$AUTH_URL"
assert_health "LedgerService.Api" "$LEDGER_URL"
assert_health "BalanceService.Api" "$BALANCE_URL"

ensure_zap_image

mkdir -p "$OUTPUT_DIR"

run_zap_scan "Auth.Api" "auth-api" "$AUTH_URL"
run_zap_scan "LedgerService.Api" "ledger-service-api" "$LEDGER_URL"
run_zap_scan "BalanceService.Api" "balance-service-api" "$BALANCE_URL"

echo "OK. Relatorios OWASP ZAP em: $OUTPUT_DIR" >&2
