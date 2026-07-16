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

OUTPUT_ROOT="$ROOT_DIR/artifacts/zap"
DOCKER_NETWORK=""
ZAP_IMAGE="ghcr.io/zaproxy/zaproxy:stable"
SWAGGER_PATH="/swagger/v1/swagger.json"
ENV_FILE=""
USE_AUTHENTICATION=false
TOKEN=""
ACTIVE_SCAN=false
FAIL_ON_ALERTS=false
TARGETS=()
PYTHON_BIN=""

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/security/run-owasp-zap-all-apis.sh [opcoes]

Executa OWASP ZAP API Scan diretamente contra todas as APIs informadas,
sem depender de API Gateway.

Opcoes:
  --target "NOME|SLUG|BASE_URL"
                       Adiciona uma API. Pode ser repetido.
  --targets-file PATH  Arquivo com um target por linha no mesmo formato.
  --docker-network N   Rede Docker compartilhada entre o ZAP e as APIs.
  --output-root DIR    Diretorio raiz dos relatorios.
  --swagger-path PATH  Caminho OpenAPI comum aos targets.
  --zap-image IMAGE    Imagem oficial do OWASP ZAP.
  --env-file PATH      Arquivo usado por get-token.sh.
  --use-authentication Obtem/injeta Authorization Bearer.
  --token TOKEN        Token Bearer manual.
  --active-scan        Habilita active scan. O padrao e baseline seguro.
  --fail-on-alerts     Propaga alertas do ZAP como falha.
  -h, --help           Mostra esta ajuda.
EOF
}

require_option_value() {
  local option="$1"
  local value="${2:-}"
  if [[ -z "$value" || "$value" == --* ]]; then
    echo "$option exige um valor." >&2
    return 1
  fi
}

load_targets_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    echo "Arquivo de targets nao encontrado: $path" >&2
    exit 2
  fi

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%%#*}"
    line="$(printf '%s' "$line" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')"
    [[ -z "$line" ]] && continue
    TARGETS+=("$line")
  done < "$path"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      TARGETS+=("$2")
      shift 2
      ;;
    --targets-file)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      load_targets_file "$2"
      shift 2
      ;;
    --docker-network)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      DOCKER_NETWORK="$2"
      shift 2
      ;;
    --output-root)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      OUTPUT_ROOT="$2"
      shift 2
      ;;
    --swagger-path)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      SWAGGER_PATH="$2"
      shift 2
      ;;
    --zap-image)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      ZAP_IMAGE="$2"
      shift 2
      ;;
    --env-file)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      ENV_FILE="$2"
      shift 2
      ;;
    --use-authentication)
      USE_AUTHENTICATION=true
      shift
      ;;
    --token)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      TOKEN="$2"
      USE_AUTHENTICATION=true
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

if [[ ${#TARGETS[@]} -eq 0 ]]; then
  echo "Nenhuma API informada. Use --target ou --targets-file." >&2
  exit 2
fi

if [[ -z "$DOCKER_NETWORK" ]]; then
  echo "--docker-network e obrigatorio para o scan CI multi-API." >&2
  exit 2
fi

for command_name in docker sed; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Comando obrigatorio nao encontrado: $command_name" >&2
    exit 1
  fi
done

if command -v python3 >/dev/null 2>&1 && python3 --version >/dev/null 2>&1; then
  PYTHON_BIN="python3"
elif command -v python >/dev/null 2>&1 && python --version >/dev/null 2>&1; then
  PYTHON_BIN="python"
else
  echo "Comando obrigatorio nao encontrado: python3 ou python" >&2
  exit 1
fi

if ! docker version >/dev/null 2>&1; then
  echo "Docker nao esta disponivel." >&2
  exit 1
fi

if ! docker network inspect "$DOCKER_NETWORK" >/dev/null 2>&1; then
  echo "Rede Docker informada nao existe: $DOCKER_NETWORK" >&2
  exit 1
fi

if ! docker image inspect "$ZAP_IMAGE" >/dev/null 2>&1; then
  docker pull "$ZAP_IMAGE"
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
OUTPUT_DIR="$("$PYTHON_BIN" - "$OUTPUT_ROOT/$TIMESTAMP" <<'PY'
import os
import sys
print(os.path.abspath(sys.argv[1]))
PY
)"
mkdir -p "$OUTPUT_DIR"
chmod 0777 "$OUTPUT_DIR"

cleanup() {
  local target name slug base_url container_name
  for target in "${TARGETS[@]}"; do
    IFS='|' read -r name slug base_url <<<"$target"
    [[ -z "${slug:-}" ]] && continue
    container_name="poc-arquitetura-zap-${slug//[^a-zA-Z0-9_.-]/-}"
    docker rm -f "$container_name" >/dev/null 2>&1 || true
  done
  chmod 0755 "$OUTPUT_DIR" >/dev/null 2>&1 || true
}
trap cleanup EXIT

ZAP_OPTIONS="-config connection.sslAcceptAll=true"
if [[ "$USE_AUTHENTICATION" == true ]]; then
  if [[ -z "$TOKEN" ]]; then
    token_env_file="$ENV_FILE"
    if [[ -z "$token_env_file" ]]; then
      token_env_file="$ROOT_DIR/.env.local"
    fi

    token_output=""
    token_exit_code=0
    set +e
    token_output="$(ENV_FILE="$token_env_file" bash "$ROOT_DIR/scripts/validation/get-token.sh" 2>&1)"
    token_exit_code=$?
    set -e

    if [[ "$token_exit_code" -ne 0 ]]; then
      echo "Falha ao obter token para o scan autenticado. Exit code: $token_exit_code" >&2
      echo "$token_output" >&2
      {
        echo "# OWASP ZAP multi-API scan"
        echo
        echo "- Etapa que falhou: obtencao do token de autenticacao."
        echo "- Exit code: \`$token_exit_code\`"
        echo "- Artifact gerado antes do scan por falha operacional."
      } > "$OUTPUT_DIR/summary.md"
      exit "$token_exit_code"
    fi

    TOKEN="$token_output"
  fi

  if [[ -z "$TOKEN" ]]; then
    echo "Token vazio para o scan autenticado." >&2
    {
      echo "# OWASP ZAP multi-API scan"
      echo
      echo "- Etapa que falhou: obtencao do token de autenticacao."
      echo "- Exit code: \`1\`"
      echo "- Artifact gerado antes do scan por token vazio."
    } > "$OUTPUT_DIR/summary.md"
    exit 1
  fi

  if [[ "${GITHUB_ACTIONS:-false}" == "true" ]]; then
    echo "::add-mask::$TOKEN"
  fi
fi

SCAN_TYPE="api-baseline"
if [[ "$ACTIVE_SCAN" == true ]]; then
  SCAN_TYPE="api-active"
fi
if [[ "$USE_AUTHENTICATION" == true ]]; then
  SCAN_TYPE+="-authenticated"
fi

SCAN_RESULTS=()
FINAL_EXIT_CODE=0

validate_target() {
  local name="$1"
  local slug="$2"
  local base_url="$3"
  local openapi_url="${base_url%/}${SWAGGER_PATH}"
  local manifest="$OUTPUT_DIR/${slug}-openapi-operations.txt"
  local validation_output
  local validation_exit_code=0

  set +e
  validation_output="$(
    docker run --rm -i \
      --network "$DOCKER_NETWORK" \
      "$ZAP_IMAGE" \
      python3 - "$openapi_url" <<'PY' 2>&1
import json
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

url = sys.argv[1]
request = Request(url, headers={"Accept": "application/json"})
try:
    with urlopen(request, timeout=30) as response:
        status = response.getcode()
        body = response.read(4 * 1024 * 1024)
except HTTPError as error:
    detail = error.read(4096).decode("utf-8", errors="replace")
    print(f"HTTP_STATUS={error.code}")
    print(f"HTTP {error.code}: {detail[:500]}", file=sys.stderr)
    sys.exit(10)
except URLError as error:
    print("HTTP_STATUS=<indisponivel>")
    print(f"Erro de conectividade: {error.reason}", file=sys.stderr)
    sys.exit(11)
except Exception as error:
    print("HTTP_STATUS=<indisponivel>")
    print(f"Erro ao acessar OpenAPI: {error}", file=sys.stderr)
    sys.exit(12)

print(f"HTTP_STATUS={status}")
if status < 200 or status >= 400:
    print(f"HTTP {status}", file=sys.stderr)
    sys.exit(13)

try:
    document = json.loads(body.decode("utf-8"))
except Exception as error:
    print(f"Resposta nao e JSON valido: {error}", file=sys.stderr)
    sys.exit(14)

if not isinstance(document.get("openapi"), str) and not isinstance(document.get("swagger"), str):
    print("Documento JSON nao contem campo 'openapi' ou 'swagger'.", file=sys.stderr)
    sys.exit(15)

paths = document.get("paths")
if not isinstance(paths, dict) or not paths:
    print("Documento OpenAPI nao contem paths validos.", file=sys.stderr)
    sys.exit(16)

http_methods = {"get", "put", "post", "delete", "options", "head", "patch", "trace"}
operations = []
for path, path_item in paths.items():
    if not isinstance(path_item, dict):
        continue
    for method in path_item:
        normalized = method.lower()
        if normalized in http_methods:
            operations.append(f"{normalized.upper()} {path}")

operations.sort()
if not operations:
    print("Documento OpenAPI nao contem operacoes HTTP.", file=sys.stderr)
    sys.exit(17)

print(f"OPERATION_COUNT={len(operations)}")
for operation in operations:
    print(operation)
PY
  )"
  validation_exit_code=$?
  set -e

  printf '%s\n' "$validation_output" > "$manifest"

  if [[ "$validation_exit_code" -ne 0 ]]; then
    echo "Falha ao validar OpenAPI de $name em $openapi_url." >&2
    echo "Status HTTP: $(printf '%s\n' "$validation_output" | sed -n 's/^HTTP_STATUS=//p' | tail -n 1)" >&2
    echo "Rede Docker utilizada: $DOCKER_NETWORK" >&2
    echo "Exit code da validacao: $validation_exit_code" >&2
    echo "Saida da validacao:" >&2
    echo "$validation_output" >&2
    return "$validation_exit_code"
  fi

  local operation_count
  operation_count="$(printf '%s\n' "$validation_output" | sed -n 's/^OPERATION_COUNT=//p' | tail -n 1)"
  if [[ -z "$operation_count" || ! "$operation_count" =~ ^[0-9]+$ || "$operation_count" -eq 0 ]]; then
    echo "Contagem de operacoes invalida para $name: ${operation_count:-<vazia>}" >&2
    echo "URL consultada: $openapi_url" >&2
    echo "Status HTTP: $(printf '%s\n' "$validation_output" | sed -n 's/^HTTP_STATUS=//p' | tail -n 1)" >&2
    echo "Rede Docker utilizada: $DOCKER_NETWORK" >&2
    echo "Exit code da validacao: 17" >&2
    echo "Saida da validacao:" >&2
    echo "$validation_output" >&2
    return 17
  fi

  printf '%s' "$operation_count"
}

run_target() {
  local target="$1"
  local name slug base_url
  IFS='|' read -r name slug base_url <<<"$target"

  if [[ -z "$name" || -z "$slug" || -z "$base_url" ]]; then
    echo "Target invalido: $target. Formato esperado: NOME|SLUG|BASE_URL" >&2
    SCAN_RESULTS+=("${name:-<invalido>}|${slug:-invalid}|${base_url:-<ausente>}|0|failed-target|3|")
    FINAL_EXIT_CODE=3
    return 0
  fi

  if [[ ! "$slug" =~ ^[a-z0-9][a-z0-9-]*$ ]]; then
    echo "Slug invalido para $name: $slug" >&2
    SCAN_RESULTS+=("$name|$slug|$base_url|0|failed-target|3|")
    FINAL_EXIT_CODE=3
    return 0
  fi

  if [[ "$base_url" != http://* && "$base_url" != https://* ]]; then
    echo "Base URL invalida para $name: $base_url" >&2
    SCAN_RESULTS+=("$name|$slug|$base_url|0|failed-target|3|")
    FINAL_EXIT_CODE=3
    return 0
  fi

  local operation_count
  local validation_exit_code=0
  set +e
  operation_count="$(validate_target "$name" "$slug" "$base_url")"
  validation_exit_code=$?
  set -e

  if [[ "$validation_exit_code" -ne 0 ]]; then
    SCAN_RESULTS+=("$name|$slug|$base_url|0|failed-openapi|3|${slug}-openapi-operations.txt")
    FINAL_EXIT_CODE=3
    return 0
  fi

  local openapi_url="${base_url%/}${SWAGGER_PATH}"
  local container_name="poc-arquitetura-zap-${slug}"
  local html="${slug}.html"
  local json="${slug}.json"
  local markdown="${slug}.md"
  local log_file="${slug}.log"
  local exit_code=0
  local status

  docker rm -f "$container_name" >/dev/null 2>&1 || true

  local -a args=(
    run
    --name "$container_name"
    --network "$DOCKER_NETWORK"
    -v "$OUTPUT_DIR:/zap/wrk:rw"
  )

  if [[ "$USE_AUTHENTICATION" == true ]]; then
    args+=(
      --env "ZAP_AUTH_HEADER=Authorization"
      --env "ZAP_AUTH_HEADER_VALUE=Bearer $TOKEN"
    )
  fi

  args+=(
    "$ZAP_IMAGE"
    zap-api-scan.py
    -t "$openapi_url"
    -f openapi
    -O "$base_url"
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

  echo "Executando ZAP em $name: $operation_count operacoes OpenAPI declaradas." >&2
  set +e
  docker "${args[@]}" >"$OUTPUT_DIR/$log_file" 2>&1
  exit_code=$?
  set -e

  docker rm -f "$container_name" >/dev/null 2>&1 || true

  if [[ "$exit_code" -ge 3 ]]; then
    status="failed-operational"
    FINAL_EXIT_CODE="$exit_code"
  elif [[ "$FAIL_ON_ALERTS" == true && "$exit_code" -ne 0 ]]; then
    status="failed-alerts"
    if [[ "$FINAL_EXIT_CODE" -eq 0 ]]; then
      FINAL_EXIT_CODE="$exit_code"
    fi
  elif [[ "$exit_code" -eq 0 ]]; then
    status="completed"
  else
    status="completed-with-alerts"
  fi

  SCAN_RESULTS+=("$name|$slug|$base_url|$operation_count|$status|$exit_code|$html,$json,$markdown,$log_file,${slug}-openapi-operations.txt")
}

for target in "${TARGETS[@]}"; do
  run_target "$target"
done

SUMMARY_PATH="$OUTPUT_DIR/summary.md"
{
  echo "# OWASP ZAP multi-API scan"
  echo
  echo "- Data/hora: $(date '+%Y-%m-%d %H:%M:%S %z')"
  echo "- Imagem ZAP: \`$ZAP_IMAGE\`"
  echo "- Tipo de scan: \`$SCAN_TYPE\`"
  echo "- Gateway: \`nao utilizado\`"
  echo "- Rede Docker: \`$DOCKER_NETWORK\`"
  echo "- Autenticacao Bearer: \`$([[ "$USE_AUTHENTICATION" == true ]] && echo habilitada || echo desabilitada)\`"
  echo "- Contrato OpenAPI: \`$SWAGGER_PATH\`"
  echo
  echo "## APIs analisadas"
  echo

  for result in "${SCAN_RESULTS[@]}"; do
    IFS='|' read -r name slug base_url operation_count status exit_code files <<<"$result"
    echo "- $name"
    echo "  - URL base direta: \`$base_url\`"
    echo "  - OpenAPI: \`${base_url%/}${SWAGGER_PATH}\`"
    echo "  - Operacoes HTTP declaradas: \`$operation_count\`"
    echo "  - Status: \`$status\`"
    echo "  - Exit code ZAP: \`$exit_code\`"
    echo "  - Arquivos: $files"
  done

  echo
  echo "## Criterio de cobertura"
  echo
  echo "- Cada API e importada diretamente pelo respectivo documento OpenAPI."
  echo "- A execucao falha operacionalmente quando um contrato nao pode ser lido, nao possui paths ou nao declara operacoes HTTP."
  echo "- O baseline usa todas as operacoes descritas no OpenAPI; endpoints nao documentados permanecem fora do alcance do API Scan."
  echo "- Active scan permanece desabilitado por padrao para evitar mutacoes agressivas no ambiente."
} > "$SUMMARY_PATH"

if [[ "$FINAL_EXIT_CODE" -ne 0 ]]; then
  echo "OWASP ZAP multi-API concluiu com falha. Exit code final: $FINAL_EXIT_CODE" >&2
  exit "$FINAL_EXIT_CODE"
fi

echo "OK. Relatorios OWASP ZAP multi-API em: $OUTPUT_DIR" >&2
