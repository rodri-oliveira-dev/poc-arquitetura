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
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
MESSAGING_PROVIDER="${MESSAGING_PROVIDER:-Kafka}"
if [[ "$MESSAGING_PROVIDER" == "PubSub" ]]; then
  COMPOSE_OVERLAY_FILE="${COMPOSE_OVERLAY_FILE:-$ROOT_DIR/compose.pubsub.yaml}"
elif [[ "$MESSAGING_PROVIDER" == "Kafka" ]]; then
  COMPOSE_OVERLAY_FILE="${COMPOSE_OVERLAY_FILE:-}"
else
  echo "MESSAGING_PROVIDER invalido: $MESSAGING_PROVIDER (use PubSub ou Kafka)" >&2
  exit 2
fi
COMPOSE_OBSERVABILITY_FILE="${COMPOSE_OBSERVABILITY_FILE:-$ROOT_DIR/compose.observability.yaml}"
NO_BUILD="${NO_BUILD:-false}"
OBSERVABILITY="${OBSERVABILITY:-false}"

COMPOSE_ENV_FILE="$(get_compose_env_file)"
POSTGRES_HOST_PORT="$(get_local_config_value POSTGRES_HOST_PORT 15432)"
POSTGRES_DATABASE="appdb"
LEDGER_DB_PASSWORD="$(get_required_local_config_value LEDGER_DB_PASSWORD)"
LEDGER_DB_MIGRATOR_PASSWORD="$(get_required_local_config_value LEDGER_DB_MIGRATOR_PASSWORD)"
BALANCE_DB_READ_PASSWORD="$(get_required_local_config_value BALANCE_DB_READ_PASSWORD)"
BALANCE_DB_WRITE_PASSWORD="$(get_required_local_config_value BALANCE_DB_WRITE_PASSWORD)"
BALANCE_DB_MIGRATOR_PASSWORD="$(get_required_local_config_value BALANCE_DB_MIGRATOR_PASSWORD)"
TRANSFER_DB_PASSWORD="$(get_required_local_config_value TRANSFER_DB_PASSWORD)"
TRANSFER_DB_MIGRATOR_PASSWORD="$(get_required_local_config_value TRANSFER_DB_MIGRATOR_PASSWORD)"
PAYMENT_DB_PASSWORD="$(get_required_local_config_value PAYMENT_DB_PASSWORD)"
PAYMENT_DB_MIGRATOR_PASSWORD="$(get_required_local_config_value PAYMENT_DB_MIGRATOR_PASSWORD)"
IDENTITY_DB_PASSWORD="$(get_required_local_config_value IDENTITY_DB_PASSWORD)"
IDENTITY_DB_MIGRATOR_PASSWORD="$(get_required_local_config_value IDENTITY_DB_MIGRATOR_PASSWORD)"

compose_files=()
if [[ -n "$COMPOSE_ENV_FILE" ]]; then
  compose_files+=(--env-file "$COMPOSE_ENV_FILE")
fi
compose_files+=(-f "$COMPOSE_FILE")
if [[ -n "$COMPOSE_OVERLAY_FILE" ]]; then
  compose_files+=(-f "$COMPOSE_OVERLAY_FILE")
fi
if [[ "$MESSAGING_PROVIDER" == "PubSub" ]]; then
  compose_files+=(--profile legacy-pubsub)
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

assert_database_authentication() {
  local user="$1"
  local password="$2"

  if ! docker compose "${compose_files[@]}" exec -T \
    -e "PGPASSWORD=$password" \
    postgres-db \
    psql -h postgres-db -U "$user" -d "$POSTGRES_DATABASE" -v "ON_ERROR_STOP=1" -c "select 1;" >/dev/null 2>&1; then
    cat >&2 <<EOF
Falha de autenticacao no PostgreSQL local para o usuario "$user" e database "$POSTGRES_DATABASE".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs postgres-db
  docker compose exec -T postgres-db psql -h postgres-db -U "$user" -d "$POSTGRES_DATABASE" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie somente o volume local do PostgreSQL se os dados forem descartaveis.
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
  local connection_string_env="${5:-ConnectionStrings__DefaultConnection}"

  env \
    ConnectionStrings__DefaultConnection="$connection_string" \
    "$connection_string_env=$connection_string" \
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
  postgres-db \
  keycloak

if [[ "$MESSAGING_PROVIDER" == "PubSub" ]]; then
  "${compose_up[@]}" pubsub-emulator pubsub-init
else
  "${compose_up[@]}" kafka kafka-init-topics
fi

if [[ "$OBSERVABILITY" == "true" ]]; then
  "${compose_up[@]}" jaeger otel-collector prometheus alertmanager loki alloy grafana
fi

wait_database postgres-db postgres_admin "$POSTGRES_DATABASE"
assert_database_authentication ledger_app_user "$LEDGER_DB_PASSWORD"
assert_database_authentication ledger_migrator_user "$LEDGER_DB_MIGRATOR_PASSWORD"
assert_database_authentication balance_read_user "$BALANCE_DB_READ_PASSWORD"
assert_database_authentication balance_write_user "$BALANCE_DB_WRITE_PASSWORD"
assert_database_authentication balance_migrator_user "$BALANCE_DB_MIGRATOR_PASSWORD"
assert_database_authentication transfer_app_user "$TRANSFER_DB_PASSWORD"
assert_database_authentication transfer_migrator_user "$TRANSFER_DB_MIGRATOR_PASSWORD"
assert_database_authentication payment_app_user "$PAYMENT_DB_PASSWORD"
assert_database_authentication payment_migrator_user "$PAYMENT_DB_MIGRATOR_PASSWORD"
assert_database_authentication identity_app_user "$IDENTITY_DB_PASSWORD"
assert_database_authentication identity_migrator_user "$IDENTITY_DB_MIGRATOR_PASSWORD"

run_migration \
  "Host=127.0.0.1;Port=$POSTGRES_HOST_PORT;Database=$POSTGRES_DATABASE;Username=ledger_migrator_user;Password=$LEDGER_DB_MIGRATOR_PASSWORD" \
  "src/ledger/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj" \
  "src/ledger/LedgerService.Api/LedgerService.Api.csproj" \
  "AppDbContext"

run_migration \
  "Host=127.0.0.1;Port=$POSTGRES_HOST_PORT;Database=$POSTGRES_DATABASE;Username=balance_migrator_user;Password=$BALANCE_DB_MIGRATOR_PASSWORD" \
  "src/balance/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj" \
  "src/balance/BalanceService.Api/BalanceService.Api.csproj" \
  "BalanceDbContext"

run_migration \
  "Host=127.0.0.1;Port=$POSTGRES_HOST_PORT;Database=$POSTGRES_DATABASE;Username=transfer_migrator_user;Password=$TRANSFER_DB_MIGRATOR_PASSWORD" \
  "src/transfer/TransferService.Infrastructure/TransferService.Infrastructure.csproj" \
  "src/transfer/TransferService.Api/TransferService.Api.csproj" \
  "TransferServiceDbContext" \
  "TRANSFER_SERVICE_CONNECTION_STRING"

run_migration \
  "Host=127.0.0.1;Port=$POSTGRES_HOST_PORT;Database=$POSTGRES_DATABASE;Username=payment_migrator_user;Password=$PAYMENT_DB_MIGRATOR_PASSWORD" \
  "src/payment/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj" \
  "src/payment/PaymentService.Api/PaymentService.Api.csproj" \
  "PaymentDbContext" \
  "PAYMENT_SERVICE_CONNECTION_STRING"

run_migration \
  "Host=127.0.0.1;Port=$POSTGRES_HOST_PORT;Database=$POSTGRES_DATABASE;Username=identity_migrator_user;Password=$IDENTITY_DB_MIGRATOR_PASSWORD" \
  "src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj" \
  "src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj" \
  "IdentityDbContext" \
  "IDENTITY_SERVICE_CONNECTION_STRING"

api_up=(docker compose "${compose_files[@]}" up -d)
if [[ "$OBSERVABILITY" == "true" ]]; then
  api_up=(docker compose "${compose_files[@]}" -f "$COMPOSE_OBSERVABILITY_FILE" --profile observability up -d)
fi

if [[ "$NO_BUILD" != "true" ]]; then
  api_up+=(--build)
fi

if [[ "$MESSAGING_PROVIDER" == "Kafka" ]]; then
  "${api_up[@]}" ledger-service ledger-worker balance-service balance-worker transfer-service transfer-worker payment-service payment-worker identity-service mailpit
else
  "${api_up[@]}" ledger-service ledger-worker balance-service balance-worker transfer-service payment-service payment-worker identity-service mailpit
fi

echo "OK. Stack local pronta."
