#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/../lib"
# shellcheck source=../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"
# shellcheck source=lib/owasp-zap-compose.sh
. "$SCRIPT_DIR/lib/owasp-zap-compose.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"

ACTIVE_SCAN=false
FAIL_ON_ALERTS=false
KEEP_ENVIRONMENT=false
SKIP_BUILD=false
ENV_FILE=""
ENV_FILE_EXPLICIT=false
OUTPUT_ROOT="$ROOT_DIR/artifacts/zap"
TEMP_ENV_FILE=""
REPORT_DIR=""

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/security/run-owasp-zap-local.sh [opcoes]

Orquestra localmente o fluxo OWASP ZAP autenticado das seis APIs usando
Docker Compose, migrations existentes, preflight autenticado e relatorios ZAP.

Opcoes:
  --active-scan       Habilita active scan. O padrao e baseline seguro.
  --fail-on-alerts    Propaga alertas Medium/High do ZAP como falha.
  --keep-environment  Nao derruba containers e volumes ao final.
  --skip-build        Inicia as APIs sem reconstruir imagens.
  --env-file PATH     Usa arquivo informado em vez de criar ambiente efemero.
  --output-root PATH  Raiz dos relatorios.
  -h, --help          Mostra esta ajuda.
EOF
}

log_phase() {
  echo "[owasp-local] $1"
}

require_local_dependencies() {
  local command_name
  for command_name in bash curl docker dotnet sed; do
    require_command "$command_name"
  done
  if ! { command -v python3 >/dev/null 2>&1 && python3 --version >/dev/null 2>&1; } &&
    ! { command -v python >/dev/null 2>&1 && python --version >/dev/null 2>&1; }; then
    echo "Comando obrigatorio nao encontrado: python3 ou python" >&2
    return 1
  fi

  if ! docker version >/dev/null 2>&1; then
    echo "Docker nao esta disponivel." >&2
    return 1
  fi
}

create_ephemeral_env_file() {
  local env_dir
  env_dir="$ROOT_DIR/artifacts/zap-local-env"
  mkdir -p "$env_dir"
  TEMP_ENV_FILE="$(mktemp "$env_dir/.env.owasp-zap-local.XXXXXX")"

  bash "$ROOT_DIR/scripts/local/create-env-local.sh" \
    --force \
    --output "$TEMP_ENV_FILE" >/dev/null

  ENV_FILE="$TEMP_ENV_FILE"
}

# shellcheck disable=SC2317,SC2329
cleanup() {
  local original_exit_code=$?
  local cleanup_exit_code=0
  set +e

  if [[ "$KEEP_ENVIRONMENT" == true ]]; then
    {
      echo "[owasp-local] Ambiente preservado por --keep-environment."
      echo "[owasp-local] Inspecione com: docker compose --env-file \"$ENV_FILE\" -f compose.yaml -f compose.owasp-zap.yaml ps"
      echo "[owasp-local] Encerre com: docker compose --env-file \"$ENV_FILE\" -f compose.yaml -f compose.owasp-zap.yaml down --volumes --remove-orphans"
    } >&2
  elif [[ -n "${ENV_FILE:-}" && -f "$ENV_FILE" ]]; then
    compose_command "$ENV_FILE" down --volumes --remove-orphans
    cleanup_exit_code=$?
    if [[ "$cleanup_exit_code" -ne 0 ]]; then
      echo "[owasp-local] Falha ao encerrar ambiente Compose. Exit code: $cleanup_exit_code" >&2
    fi
  fi

  if [[ "$ENV_FILE_EXPLICIT" != true && -n "${TEMP_ENV_FILE:-}" && -f "$TEMP_ENV_FILE" ]]; then
    rm -f "$TEMP_ENV_FILE"
    cleanup_exit_code=$((cleanup_exit_code == 0 ? $? : cleanup_exit_code))
  fi

  if [[ "$original_exit_code" -ne 0 ]]; then
    exit "$original_exit_code"
  fi

  if [[ "$cleanup_exit_code" -ne 0 ]]; then
    exit "$cleanup_exit_code"
  fi

  exit 0
}

trap cleanup EXIT

while [[ $# -gt 0 ]]; do
  case "$1" in
    --active-scan)
      ACTIVE_SCAN=true
      shift
      ;;
    --fail-on-alerts)
      FAIL_ON_ALERTS=true
      shift
      ;;
    --keep-environment)
      KEEP_ENVIRONMENT=true
      shift
      ;;
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --env-file)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      ENV_FILE="$2"
      ENV_FILE_EXPLICIT=true
      shift 2
      ;;
    --output-root)
      require_option_value "$1" "${2:-}" || { usage; exit 2; }
      OUTPUT_ROOT="$2"
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

cd "$ROOT_DIR"

log_phase "Preparando ambiente"
require_local_dependencies

if [[ -z "$ENV_FILE" ]]; then
  create_ephemeral_env_file
elif [[ ! -f "$ENV_FILE" ]]; then
  echo "Arquivo de ambiente nao encontrado: $ENV_FILE" >&2
  exit 2
fi

mkdir -p "$OUTPUT_ROOT"
export_env_secret_masks "$ENV_FILE"

compose_command "$ENV_FILE" config --quiet

if [[ "$ACTIVE_SCAN" == true ]]; then
  echo "[owasp-local] Active scan habilitado. Use apenas em ambiente descartavel e autorizado."
fi

log_phase "Iniciando dependências"
compose_command "$ENV_FILE" up -d postgres-db keycloak mailpit keycloak-identity-admin-init
wait_for_postgres "$ENV_FILE"

log_phase "Aplicando migrations"
apply_owasp_zap_migrations

log_phase "Iniciando APIs"
api_up_args=(up -d)
if [[ "$SKIP_BUILD" != true ]]; then
  api_up_args+=(--build)
fi
api_up_args+=(
  ledger-service
  balance-service
  transfer-service
  payment-service
  audit-service
  identity-service
)
compose_command "$ENV_FILE" "${api_up_args[@]}"
wait_for_owasp_zap_apis "$ENV_FILE"

log_phase "Validando autenticação"
token_output=""
set +e
token_output="$(ENV_FILE="$ENV_FILE" bash "$ROOT_DIR/scripts/validation/get-token.sh" 2>/dev/null)"
token_exit_code=$?
set -e
if [[ "$token_exit_code" -ne 0 ]]; then
  echo "[owasp-local] Falha ao obter token de automacao. Exit code: $token_exit_code" >&2
  exit "$token_exit_code"
fi
if [[ -z "$token_output" ]]; then
  echo "[owasp-local] Token de automacao vazio." >&2
  exit 1
fi
if [[ "${GITHUB_ACTIONS:-false}" == "true" ]]; then
  echo "::add-mask::$token_output"
fi

bash "$ROOT_DIR/scripts/security/validate-zap-authentication.sh" \
  --env-file "$ENV_FILE" \
  --token "$token_output"

log_phase "Executando OWASP ZAP"
api_network="$(discover_owasp_zap_network "$ENV_FILE" ledger-service)"
validate_owasp_zap_services_on_network "$ENV_FILE" "$api_network"

zap_args=(
  --output-root "$OUTPUT_ROOT"
  --docker-network "$api_network"
  --env-file "$ENV_FILE"
  --token "$token_output"
  --targets-file "$ROOT_DIR/scripts/security/owasp-zap-ci-targets.txt"
)
if [[ "$ACTIVE_SCAN" == true ]]; then
  zap_args+=(--active-scan)
fi
if [[ "$FAIL_ON_ALERTS" == true ]]; then
  zap_args+=(--fail-on-alerts)
fi

set +e
bash "$ROOT_DIR/scripts/security/run-owasp-zap-all-apis.sh" "${zap_args[@]}"
zap_exit_code=$?
set -e

REPORT_DIR="$(find "$OUTPUT_ROOT" -mindepth 1 -maxdepth 1 -type d -print 2>/dev/null | sort | tail -n 1 || true)"
if [[ -n "$REPORT_DIR" ]]; then
  log_phase "Relatórios disponíveis em $REPORT_DIR"
else
  log_phase "Relatórios não encontrados em $OUTPUT_ROOT"
fi

exit "$zap_exit_code"
