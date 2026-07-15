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
LEDGER_ZAP_URL=""
BALANCE_ZAP_URL=""
DOCKER_NETWORK=""
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
  --ledger-url URL     Sobrescreve a URL do LedgerService.Api vista pelo host para /health.
  --balance-url URL    Sobrescreve a URL do BalanceService.Api vista pelo host para /health.
  --ledger-zap-url URL URL do LedgerService.Api vista pelo container ZAP.
  --balance-zap-url URL
                      URL do BalanceService.Api vista pelo container ZAP.
  --docker-network NETWORK
                      Conecta o container temporario do ZAP a rede Docker informada.
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
    --ledger-zap-url)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      LEDGER_ZAP_URL="${2:-}"
      shift 2
      ;;
    --balance-zap-url)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      BALANCE_ZAP_URL="${2:-}"
      shift 2
      ;;
    --docker-network)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      DOCKER_NETWORK="${2:-}"
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
if [[ -z "$LEDGER_ZAP_URL" ]]; then
  LEDGER_ZAP_URL="$LEDGER_URL"
fi
if [[ -z "$BALANCE_ZAP_URL" ]]; then
  BALANCE_ZAP_URL="$BALANCE_URL"
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="$OUTPUT_ROOT/$TIMESTAMP"
SCAN_COMMAND="zap-api-scan.py"
SCAN_TYPE="api-baseline"
if [[ "$ACTIVE_SCAN" == true ]]; then
  SCAN_TYPE="api-active"
fi

SCAN_RESULTS=()
declare -A OPENAPI_DECLARED_SERVERS=()

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

assert_docker_network() {
  if [[ -z "$DOCKER_NETWORK" ]]; then
    return 0
  fi

  if ! docker network inspect "$DOCKER_NETWORK" >/dev/null 2>&1; then
    echo "Rede Docker informada para o ZAP nao existe: $DOCKER_NETWORK" >&2
    exit 1
  fi
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

normalize_api_base_url() {
  python3 - "$1" "$SWAGGER_PATH" <<'PY'
import sys
from urllib.parse import urlparse, urlunparse

url = sys.argv[1].rstrip("/")
swagger_path = sys.argv[2] or "/swagger/v1/swagger.json"
if not swagger_path.startswith("/"):
    swagger_path = "/" + swagger_path

parsed = urlparse(url)
path = parsed.path.rstrip("/")
if path.endswith(swagger_path.rstrip("/")):
    path = path[: -len(swagger_path.rstrip("/"))] or ""
    parsed = parsed._replace(path=path.rstrip("/"), params="", query="", fragment="")

print(urlunparse(parsed).rstrip("/"))
PY
}

zap_accessible_base_url() {
  local host_base_url="$1"
  local container_base_url="$2"
  local base_url

  base_url="$(normalize_api_base_url "$container_base_url")"
  if [[ "$base_url" == "$(normalize_api_base_url "$host_base_url")" ]]; then
    zap_target_url "$base_url"
  else
    zap_target_url "$base_url"
  fi
}

docker_host_args() {
  local hosts=("host.docker.internal")
  local host

  local urls=("$LEDGER_URL" "$BALANCE_URL" "$LEDGER_ZAP_URL" "$BALANCE_ZAP_URL")

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

container_openapi_url() {
  local container_base_url="$1"
  local host_base_url="$2"
  local accessible_base_url

  accessible_base_url="$(zap_accessible_base_url "$host_base_url" "$container_base_url")"
  swagger_url "$accessible_base_url"
}

docker_common_run_args() {
  if [[ -n "$DOCKER_NETWORK" ]]; then
    printf '%s\n' "--network" "$DOCKER_NETWORK"
  fi
}

assert_openapi_from_container() {
  local api_name="$1"
  local host_base_url="$2"
  local container_base_url="$3"
  local host_openapi_url
  local target_url
  local effective_server_url
  local declared_servers="<ausente>"
  local docker_args=()
  local arg
  local output
  local exit_code=0

  host_openapi_url="$(swagger_url "$host_base_url")"
  target_url="$(container_openapi_url "$container_base_url" "$host_base_url")"
  effective_server_url="$(zap_accessible_base_url "$host_base_url" "$container_base_url")"

  docker_args=(run --rm)
  while IFS= read -r arg; do
    docker_args+=("$arg")
  done < <(docker_common_run_args)
  while IFS= read -r arg; do
    docker_args+=("$arg")
  done < <(docker_host_args)

  echo "Validando OpenAPI do $api_name antes do scan." >&2
  set +e
  output="$(
    docker "${docker_args[@]}" "$ZAP_IMAGE" python3 - "$target_url" <<'PY' 2>&1
import json
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

url = sys.argv[1]
request = Request(url, headers={"Accept": "application/json"})

try:
    with urlopen(request, timeout=20) as response:
        status = response.getcode()
        body = response.read(1024 * 1024)
except HTTPError as error:
    body = error.read(4096).decode("utf-8", errors="replace")
    print(f"HTTP {error.code}: {body[:500]}", file=sys.stderr)
    sys.exit(10)
except URLError as error:
    print(f"Erro de conectividade: {error.reason}", file=sys.stderr)
    sys.exit(11)
except Exception as error:
    print(f"Erro ao acessar OpenAPI: {error}", file=sys.stderr)
    sys.exit(12)

if status < 200 or status >= 400:
    print(f"HTTP {status}", file=sys.stderr)
    sys.exit(13)

try:
    document = json.loads(body.decode("utf-8"))
except Exception as error:
    print(f"Resposta nao e JSON valido: {error}", file=sys.stderr)
    sys.exit(14)

if not isinstance(document, dict) or not (document.get("openapi") or document.get("swagger")):
    print("Documento JSON nao contem campo 'openapi' ou 'swagger'.", file=sys.stderr)
    sys.exit(15)

paths = document.get("paths")
if not isinstance(paths, dict) or not paths:
    print("Documento OpenAPI nao contem paths validos.", file=sys.stderr)
    sys.exit(16)

servers = document.get("servers")
if servers is None:
    server_urls = []
elif isinstance(servers, list):
    server_urls = [
        server.get("url")
        for server in servers
        if isinstance(server, dict) and isinstance(server.get("url"), str) and server.get("url")
    ]
else:
    print("Campo 'servers' existe, mas nao e um array.", file=sys.stderr)
    sys.exit(17)

print("DECLARED_SERVERS=" + json.dumps(server_urls, ensure_ascii=False))
PY
  )"
  exit_code=$?
  set -e

  if [[ "$exit_code" -ne 0 ]]; then
    {
      echo "Falha ao validar OpenAPI do $api_name a partir do container ZAP."
      echo "  URL vista pelo host: $host_openapi_url"
      echo "  URL vista pelo container: $target_url"
      echo "  Rede Docker utilizada: ${DOCKER_NETWORK:-<padrao do Docker>}"
      echo "  Erro: $output"
    } >&2
    exit 1
  fi

  while IFS= read -r line; do
    if [[ "$line" == DECLARED_SERVERS=* ]]; then
      declared_servers="${line#DECLARED_SERVERS=}"
    fi
  done <<<"$output"

  if [[ "$declared_servers" == "[]" ]]; then
    declared_servers="<ausente>"
  fi

  OPENAPI_DECLARED_SERVERS["$api_name"]="$declared_servers"

  {
    echo "OpenAPI validado para $api_name."
    echo "  URL do documento OpenAPI: $target_url"
    echo "  Servidor declarado no documento: $declared_servers"
    echo "  Servidor efetivo para o ZAP (-O): $effective_server_url"
    echo "  Rede Docker utilizada: ${DOCKER_NETWORK:-<padrao do Docker>}"
  } >&2

  if [[ "$declared_servers" != "<ausente>" && "$declared_servers" != *"$effective_server_url"* ]]; then
    {
      echo "Servidor declarado no OpenAPI difere do servidor efetivo acessivel pelo container ZAP."
      echo "  Divergencia aceita porque o scan usara override explicito com -O."
    } >&2
  fi
}

run_zap_scan() {
  local api_name="$1"
  local slug="$2"
  local host_url="$3"
  local container_url="$4"
  local target_url
  local openapi_url
  local effective_server_url
  local declared_servers
  local html="$slug.html"
  local json="$slug.json"
  local markdown="$slug.md"
  local log_file="$slug.log"
  local exit_code=0
  local status
  local docker_args=()
  local host_arg

  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  openapi_url="$(swagger_url "$host_url")"
  target_url="$(container_openapi_url "$container_url" "$host_url")"
  effective_server_url="$(zap_accessible_base_url "$host_url" "$container_url")"
  declared_servers="${OPENAPI_DECLARED_SERVERS[$api_name]:-<nao validado>}"

  docker_args=(run --name "$CONTAINER_NAME" -v "$OUTPUT_DIR:/zap/wrk:rw")
  while IFS= read -r host_arg; do
    docker_args+=("$host_arg")
  done < <(docker_common_run_args)
  while IFS= read -r host_arg; do
    docker_args+=("$host_arg")
  done < <(docker_host_args)

  docker_args+=(
    "$ZAP_IMAGE"
    "$SCAN_COMMAND"
    -t "$target_url"
    -f openapi
    -O "$effective_server_url"
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

  {
    echo "Executando ZAP $SCAN_TYPE em $api_name."
    echo "  URL do documento OpenAPI: $target_url"
    echo "  Servidor declarado no documento: $declared_servers"
    echo "  Servidor efetivo para o ZAP (-O): $effective_server_url"
    echo "  Rede Docker utilizada: ${DOCKER_NETWORK:-<padrao do Docker>}"
    echo "  Log bruto: $OUTPUT_DIR/$log_file"
  } >&2
  set +e
  docker "${docker_args[@]}" >"$OUTPUT_DIR/$log_file" 2>&1
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

  SCAN_RESULTS+=("$api_name|$host_url|$openapi_url|$target_url|$effective_server_url|$declared_servers|${DOCKER_NETWORK:-<padrao do Docker>}|$status|$exit_code|$html,$json,$markdown,$log_file")

  if [[ "$exit_code" -ge 3 ]]; then
    {
      echo "Falha operacional no ZAP para $api_name. Exit code: $exit_code"
      echo "  URL vista pelo host: $openapi_url"
      echo "  URL vista pelo container: $target_url"
      echo "  Servidor efetivo para o ZAP (-O): $effective_server_url"
      echo "  Rede Docker utilizada: ${DOCKER_NETWORK:-<padrao do Docker>}"
    } >&2
    return 0
  fi

  if [[ "$FAIL_ON_ALERTS" == true && "$exit_code" -ne 0 ]]; then
    echo "ZAP encontrou alertas para $api_name e --fail-on-alerts esta ativo. Exit code: $exit_code" >&2
  fi

  return 0
}

assert_zap_workdir_writable() {
  local docker_args=()
  local arg
  local output
  local exit_code=0

  docker_args=(run --rm -v "$OUTPUT_DIR:/zap/wrk:rw")
  while IFS= read -r arg; do
    docker_args+=("$arg")
  done < <(docker_common_run_args)

  set +e
  output="$(
    docker "${docker_args[@]}" "$ZAP_IMAGE" sh -c 'test -d /zap/wrk && test -w /zap/wrk && tmp="$(mktemp /zap/wrk/.zap-write-test.XXXXXX)" && rm -f "$tmp"' 2>&1
  )"
  exit_code=$?
  set -e

  if [[ "$exit_code" -ne 0 ]]; then
    {
      echo "Falha operacional: /zap/wrk nao esta gravavel pelo usuario da imagem ZAP."
      echo "  Diretorio montado: $OUTPUT_DIR"
      echo "  Imagem ZAP: $ZAP_IMAGE"
      echo "  Rede Docker utilizada: ${DOCKER_NETWORK:-<padrao do Docker>}"
      echo "  Erro: $output"
    } >&2
    exit 1
  fi
}

final_exit_code() {
  local result
  local api_name url openapi_url target_url effective_server_url declared_servers network status exit_code files
  local final_code=0

  for result in "${SCAN_RESULTS[@]}"; do
    IFS='|' read -r api_name url openapi_url target_url effective_server_url declared_servers network status exit_code files <<<"$result"
    if [[ "$exit_code" -ge 3 ]]; then
      final_code="$exit_code"
    elif [[ "$FAIL_ON_ALERTS" == true && "$exit_code" -ne 0 && "$final_code" -eq 0 ]]; then
      final_code="$exit_code"
    fi
  done

  printf '%s' "$final_code"
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
    echo "- Rede Docker do ZAP: \`${DOCKER_NETWORK:-<padrao do Docker>}\`"
    echo "- Diretorio de saida: \`$OUTPUT_DIR\`"
    echo
    echo "## APIs analisadas"
    echo

    local result api_name url openapi_url target_url effective_server_url declared_servers network status exit_code files
    for result in "${SCAN_RESULTS[@]}"; do
      IFS='|' read -r api_name url openapi_url target_url effective_server_url declared_servers network status exit_code files <<<"$result"
      echo "- $api_name: \`$url\`"
      echo "  - Swagger/OpenAPI: \`$openapi_url\`"
      echo "  - OpenAPI visto pelo container: \`$target_url\`"
      echo "  - Servidor declarado no OpenAPI: \`$declared_servers\`"
      echo "  - Servidor efetivo para o ZAP (-O): \`$effective_server_url\`"
      echo "  - Rede Docker: \`$network\`"
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
assert_docker_network

mkdir -p "$OUTPUT_DIR"
assert_zap_workdir_writable

assert_openapi_from_container "LedgerService.Api" "$LEDGER_URL" "$LEDGER_ZAP_URL"
assert_openapi_from_container "BalanceService.Api" "$BALANCE_URL" "$BALANCE_ZAP_URL"

run_zap_scan "LedgerService.Api" "ledger-service-api" "$LEDGER_URL" "$LEDGER_ZAP_URL"
run_zap_scan "BalanceService.Api" "balance-service-api" "$BALANCE_URL" "$BALANCE_ZAP_URL"

FINAL_EXIT_CODE="$(final_exit_code)"
if [[ "$FINAL_EXIT_CODE" -ne 0 ]]; then
  echo "OWASP ZAP concluiu com falha reportada apos executar todos os alvos. Exit code final: $FINAL_EXIT_CODE" >&2
  exit "$FINAL_EXIT_CODE"
fi

echo "OK. Relatorios OWASP ZAP em: $OUTPUT_DIR" >&2
