#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
MESSAGING_PROVIDER="${MESSAGING_PROVIDER:-PubSub}"
if [[ "$MESSAGING_PROVIDER" == "PubSub" ]]; then
  COMPOSE_OVERLAY_FILE="${COMPOSE_OVERLAY_FILE:-}"
elif [[ "$MESSAGING_PROVIDER" == "Kafka" ]]; then
  COMPOSE_OVERLAY_FILE="${COMPOSE_OVERLAY_FILE:-$ROOT_DIR/compose.kafka.yaml}"
else
  echo "MESSAGING_PROVIDER invalido: $MESSAGING_PROVIDER (use PubSub ou Kafka)" >&2
  exit 2
fi
COMPOSE_OBSERVABILITY_FILE="${COMPOSE_OBSERVABILITY_FILE:-$ROOT_DIR/compose.observability.yaml}"
NO_BUILD="${NO_BUILD:-false}"
OBSERVABILITY="${OBSERVABILITY:-false}"

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

if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
  POSTGRES_PASSWORD="$(get_local_env_value POSTGRES_PASSWORD)"
fi
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-local_dev_password}"
BALANCE_DB_NAME="$(get_local_config_value BALANCE_DB_NAME dbBalance)"
BALANCE_DB_HOST="$(get_local_config_value BALANCE_DB_HOST balance-db)"
BALANCE_DB_USER="$(get_local_config_value BALANCE_DB_USER userBalance)"
BALANCE_DB_PASSWORD="$(get_local_config_value BALANCE_DB_PASSWORD local_dev_password)"
BALANCE_DB_HOST_PORT="$(get_local_config_value BALANCE_DB_HOST_PORT 15433)"

compose_files=(-f "$COMPOSE_FILE")
if [[ -n "$COMPOSE_OVERLAY_FILE" ]]; then
  compose_files+=(-f "$COMPOSE_OVERLAY_FILE")
fi
if [[ "$MESSAGING_PROVIDER" == "Kafka" ]]; then
  compose_files+=(--profile legacy-kafka)
fi

wait_database() {
  local service="$1"
  local user="$2"
  local database="$3"

  for _ in $(seq 1 60); do
    if docker compose "${compose_files[@]}" exec -T "$service" pg_isready -U "$user" -d "$database" >/dev/null 2>&1; then
      return 0
    fi

    sleep 2
  done

  echo "Banco indisponivel apos timeout: $service" >&2
  return 1
}

assert_balance_database_authentication() {
  if ! docker compose "${compose_files[@]}" exec -T \
    -e "PGPASSWORD=$BALANCE_DB_PASSWORD" \
    balance-db \
    psql -h "$BALANCE_DB_HOST" -U "$BALANCE_DB_USER" -d "$BALANCE_DB_NAME" -v "ON_ERROR_STOP=1" -c "select 1;" >/dev/null 2>&1; then
    cat >&2 <<EOF
Falha de autenticacao no banco Balance para o usuario "$BALANCE_DB_USER" e database "$BALANCE_DB_NAME".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs balance-db
  docker compose logs balance-service
  docker compose exec -T balance-db psql -h "$BALANCE_DB_HOST" -U "$BALANCE_DB_USER" -d "$BALANCE_DB_NAME" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie somente o volume local do Balance se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
EOF
    return 1
  fi
}

run_migration() {
  local connection_string="$1"
  local project="$2"
  local startup_project="$3"
  local db_context="$4"

  ConnectionStrings__DefaultConnection="$connection_string" \
    dotnet tool run dotnet-ef -- database update \
      -p "$project" \
      -s "$startup_project" \
      -c "$db_context" \
      -- --environment Development
}

cd "$ROOT_DIR"

dotnet tool restore

compose_up=(docker compose "${compose_files[@]}" up -d)
if [[ "$OBSERVABILITY" == "true" ]]; then
  export OTEL_ENABLED="${OTEL_ENABLED:-true}"
  compose_up=(docker compose "${compose_files[@]}" -f "$COMPOSE_OBSERVABILITY_FILE" --profile observability up -d)
fi

if [[ "$NO_BUILD" != "true" ]]; then
  compose_up+=(--build)
fi

"${compose_up[@]}" \
  ledger-db \
  balance-db \
  keycloak

if [[ "$MESSAGING_PROVIDER" == "PubSub" ]]; then
  "${compose_up[@]}" pubsub-emulator pubsub-init
else
  "${compose_up[@]}" kafka kafka-init-topics
fi

if [[ "$OBSERVABILITY" == "true" ]]; then
  "${compose_up[@]}" jaeger otel-collector prometheus alertmanager loki alloy grafana
fi

wait_database ledger-db appuser appdb
wait_database balance-db "$BALANCE_DB_USER" "$BALANCE_DB_NAME"
assert_balance_database_authentication

run_migration \
  "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=$POSTGRES_PASSWORD" \
  "src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj" \
  "src/LedgerService.Api/LedgerService.Api.csproj" \
  "AppDbContext"

run_migration \
  "Host=127.0.0.1;Port=$BALANCE_DB_HOST_PORT;Database=$BALANCE_DB_NAME;Username=$BALANCE_DB_USER;Password=$BALANCE_DB_PASSWORD" \
  "src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj" \
  "src/BalanceService.Api/BalanceService.Api.csproj" \
  "BalanceDbContext"

api_up=(docker compose "${compose_files[@]}" up -d)
if [[ "$OBSERVABILITY" == "true" ]]; then
  api_up=(docker compose "${compose_files[@]}" -f "$COMPOSE_OBSERVABILITY_FILE" --profile observability up -d)
fi

if [[ "$NO_BUILD" != "true" ]]; then
  api_up+=(--build)
fi

"${api_up[@]}" ledger-service ledger-worker balance-service balance-worker

echo "OK. Stack local pronta."
