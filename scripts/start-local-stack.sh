#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
NO_BUILD="${NO_BUILD:-false}"
if [[ -z "${POSTGRES_PASSWORD:-}" && -f "$ROOT_DIR/.env" ]]; then
  POSTGRES_PASSWORD="$(sed -nE 's/^[[:space:]]*POSTGRES_PASSWORD[[:space:]]*=[[:space:]]*(.*)[[:space:]]*$/\1/p' "$ROOT_DIR/.env" | tail -n 1)"
fi
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-local_dev_password}"

wait_database() {
  local service="$1"
  local user="$2"
  local database="$3"

  for _ in $(seq 1 60); do
    if docker compose -f "$COMPOSE_FILE" exec -T "$service" pg_isready -U "$user" -d "$database" >/dev/null 2>&1; then
      return 0
    fi

    sleep 2
  done

  echo "Banco indisponivel apos timeout: $service" >&2
  return 1
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

compose_up=(docker compose -f "$COMPOSE_FILE" up -d)
if [[ "$NO_BUILD" != "true" ]]; then
  compose_up+=(--build)
fi

"${compose_up[@]}" \
  ledger-db \
  balance-db \
  kafka \
  kafka-init-topics \
  jaeger \
  otel-collector \
  prometheus \
  alertmanager \
  loki \
  alloy \
  grafana \
  auth-api

wait_database ledger-db appuser appdb
wait_database balance-db userBalance dbBalance

run_migration \
  "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=$POSTGRES_PASSWORD" \
  "src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj" \
  "src/LedgerService.Api/LedgerService.Api.csproj" \
  "AppDbContext"

run_migration \
  "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=$POSTGRES_PASSWORD" \
  "src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj" \
  "src/BalanceService.Api/BalanceService.Api.csproj" \
  "BalanceDbContext"

api_up=(docker compose -f "$COMPOSE_FILE" up -d)
if [[ "$NO_BUILD" != "true" ]]; then
  api_up+=(--build)
fi

"${api_up[@]}" ledger-service ledger-worker balance-service balance-worker

echo "OK. Stack local pronta."
