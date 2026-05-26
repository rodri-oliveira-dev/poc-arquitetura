#!/usr/bin/env bash
set -euo pipefail

# Runner reprodutível:
#  a) gera .env.k6.auto a partir do compose.yaml
#  b) obtém TOKEN conforme README
#  c) roda k6 dentro do compose network via docker compose

MODE="${1:-}"
if [[ -z "$MODE" ]]; then
  echo "Uso: ./scripts/run-loadtests.sh <smoke|balance50|resilience>" 1>&2
  exit 2
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
COMPOSE_K6_FILE="${COMPOSE_K6_FILE:-$ROOT_DIR/compose.k6.yaml}"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env.k6.auto}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$ROOT_DIR/artifacts/k6}"

mkdir -p "$ARTIFACTS_DIR"

get_local_env_value() {
  local name="$1"
  local env_file="$ROOT_DIR/.env"

  if [[ ! -f "$env_file" ]]; then
    return 0
  fi

  sed -nE "s/^[[:space:]]*$name[[:space:]]*=[[:space:]]*(.*)[[:space:]]*$/\1/p" "$env_file" |
    tail -n 1 |
    sed -E "s/^['\"]//; s/['\"]$//"
}

get_local_config_value() {
  local name="$1"
  local default_value="$2"
  local value="${!name:-}"

  if [[ -z "$value" ]]; then
    value="$(get_local_env_value "$name")"
  fi

  if [[ -z "$value" ]]; then
    value="$default_value"
  fi

  printf '%s' "$value"
}

print_balance_database_auth_failure() {
  local user="$1"
  local database="$2"
  local host_name

  host_name="$(get_local_config_value BALANCE_DB_HOST balance-db)"

  cat >&2 <<EOF
Falha de autenticacao no banco Balance para o usuario "$user" e database "$database".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs balance-db
  docker compose logs balance-service
  docker compose exec -T balance-db psql -h "$host_name" -U "$user" -d "$database" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie somente o volume local do Balance se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
EOF
}

assert_balance_database_authentication() {
  local user
  local database
  local password
  local host_name

  host_name="$(get_local_config_value BALANCE_DB_HOST balance-db)"
  user="$(get_local_config_value BALANCE_DB_USER userBalance)"
  database="$(get_local_config_value BALANCE_DB_NAME dbBalance)"
  password="$(get_local_config_value BALANCE_DB_PASSWORD local_dev_password)"

  if ! docker compose -f "$COMPOSE_FILE" exec -T \
    -e "PGPASSWORD=$password" \
    balance-db \
    psql -h "$host_name" -U "$user" -d "$database" -v "ON_ERROR_STOP=1" -c "select 1;" >/dev/null 2>&1; then
    print_balance_database_auth_failure "$user" "$database"
    exit 1
  fi
}

wait_compose_service_healthy() {
  local service="$1"
  local timeout_seconds="${2:-240}"
  local deadline=$((SECONDS + timeout_seconds))
  local health=""

  while (( SECONDS < deadline )); do
    if docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" ps "$service" --format json | grep -q '"Health":"healthy"'; then
      return 0
    fi

    health="$(docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" ps "$service" --format json | sed -nE 's/.*"Health":"([^"]*)".*/\1/p' | tail -n 1)"
    if [[ "$health" == "unhealthy" ]]; then
      echo "$service ficou unhealthy durante a preparacao do k6." 1>&2
      exit 1
    fi

    sleep 5
  done

  echo "Timeout aguardando $service ficar healthy. Ultimo health: ${health:-desconhecido}" 1>&2
  exit 1
}

# a) gerar env
COMPOSE_FILE="$COMPOSE_FILE" OUT_FILE="$ENV_FILE" "$ROOT_DIR/scripts/compose-env.sh" >/dev/null

# Aplica o override de carga nas APIs antes de executar o k6. O compose.k6.yaml
# mantem os testes apontando para as APIs HTTP e aumenta apenas limites tecnicos
# que poderiam transformar o cenario de throughput em teste de rate limiting.
docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" up -d --no-build --force-recreate keycloak ledger-service balance-service

wait_compose_service_healthy keycloak
assert_balance_database_authentication

# b) obter token pelo provider local configurado. Por padrao, Keycloak.
TOKEN=""
for _ in $(seq 1 30); do
  if TOKEN="$(ENV_FILE="$ENV_FILE" "$ROOT_DIR/scripts/get-token.sh" 2>/dev/null)" && [[ -n "$TOKEN" ]]; then
    break
  fi
  sleep 2
done

if [[ -z "$TOKEN" ]]; then
  TOKEN="$(ENV_FILE="$ENV_FILE" "$ROOT_DIR/scripts/get-token.sh")"
fi
if [[ -z "$TOKEN" ]]; then
  echo "Falha ao obter TOKEN. Você pode informar manualmente via env TOKEN=..." 1>&2
  exit 1
fi

warmup_balance() {
  local date_value="${DATE:-$(date +%F)}"
  local merchant_id="${MERCHANT_ID:-tese}"
  local encoded_merchant
  encoded_merchant="$(python3 - "$merchant_id" <<'PY'
import sys
from urllib.parse import quote
print(quote(sys.argv[1], safe=""))
PY
)"
  local url="http://localhost:5228/v1/consolidados/diario/$date_value?merchantId=$encoded_merchant"

  for _ in $(seq 1 30); do
    if curl -fsS -H "Authorization: Bearer $TOKEN" "$url" >/dev/null; then
      return 0
    fi
    sleep 1
  done

  curl -fsS -H "Authorization: Bearer $TOKEN" "$url" >/dev/null
}

warmup_balance

ts="$(date +%Y%m%d-%H%M%S)"

run_k6() {
  local scenarioName="$1"; shift
  local scriptPath="$1"; shift
  local summaryFile="summary-$MODE-$scenarioName-$ts.json"
  local hostSummary="$ARTIFACTS_DIR/$summaryFile"

  docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" --profile k6 run --rm \
    --user "$(id -u):$(id -g)" \
    -e "TOKEN=$TOKEN" \
    "$@" \
    k6 run "$scriptPath" --summary-export "/artifacts/$summaryFile"

  if [[ ! -f "$hostSummary" ]]; then
    echo "Summary k6 nao encontrado: $hostSummary" 1>&2
    exit 1
  fi

  python3 - "$hostSummary" <<'PY'
import json
import sys

path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    summary = json.load(f)

metrics = summary.get("metrics", {})
checks_failed = int(metrics.get("checks", {}).get("fails", 0) or 0)
http_failed_rate = float(metrics.get("http_req_failed", {}).get("value", 0) or 0)
dropped_iterations = int(metrics.get("dropped_iterations", {}).get("count", 0) or 0)

if checks_failed > 0 or http_failed_rate > 0.05 or dropped_iterations > 0:
    print(
        f"k6 falhou: checks_failed={checks_failed}; "
        f"http_req_failed={http_failed_rate}; dropped_iterations={dropped_iterations}",
        file=sys.stderr,
    )
    sys.exit(1)
PY
}

case "$MODE" in
  smoke)
    run_k6 ledger_resilience scenarios/ledger_resilience.js -e VUS=1 -e DURATION=10s
    run_k6 balance_daily_50rps scenarios/balance_daily_50rps.js -e RATE=1 -e DURATION=10s -e PREALLOCATED_VUS=5 -e MAX_VUS=10
    ;;
  balance50)
    run_k6 balance_daily_50rps scenarios/balance_daily_50rps.js -e RATE=50 -e DURATION=1m
    ;;
  resilience)
    run_k6 ledger_resilience scenarios/ledger_resilience.js -e VUS=5 -e DURATION=1m
    ;;
  *)
    echo "Modo inválido: $MODE (use smoke|balance50|resilience)" 1>&2
    exit 2
    ;;
esac

echo "OK. Artifacts em: $ARTIFACTS_DIR" 1>&2

# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).
