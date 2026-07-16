#!/usr/bin/env bash

compose_args() {
  local env_file="$1"

  printf '%s\n' \
    --env-file "$env_file" \
    -f compose.yaml \
    -f compose.owasp-zap.yaml
}

compose_command() {
  local env_file="$1"
  shift

  local -a args
  mapfile -t args < <(compose_args "$env_file")
  docker compose "${args[@]}" "$@"
}

read_env_or_default() {
  local env_file="$1"
  local key="$2"
  local default_value="$3"
  local value

  value="$(read_env_value "$env_file" "$key")"
  if [[ -z "$value" ]]; then
    value="$default_value"
  fi

  printf '%s' "$value"
}

export_env_secret_masks() {
  local env_file="$1"
  local key
  local value
  local -a keys=(
    POSTGRES_HOST_PORT
    LEDGER_DB_MIGRATOR_PASSWORD
    BALANCE_DB_MIGRATOR_PASSWORD
    TRANSFER_DB_MIGRATOR_PASSWORD
    PAYMENT_DB_MIGRATOR_PASSWORD
    AUDIT_DB_MIGRATOR_PASSWORD
    IDENTITY_DB_MIGRATOR_PASSWORD
    KEYCLOAK_CLIENT_SECRET
  )

  for key in "${keys[@]}"; do
    value="$(read_env_value "$env_file" "$key")"
    if [[ -z "$value" ]]; then
      echo "Variavel obrigatoria nao encontrada em $env_file: $key" >&2
      return 1
    fi

    if [[ "${GITHUB_ACTIONS:-false}" == "true" && "$key" != "POSTGRES_HOST_PORT" ]]; then
      echo "::add-mask::$value"
    fi

    export "$key=$value"
  done
}

wait_for_postgres() {
  local env_file="$1"
  local attempt

  for attempt in {1..60}; do
    if compose_command "$env_file" exec -T postgres-db \
      pg_isready -U postgres_admin -d appdb >/dev/null 2>&1; then
      return 0
    fi

    sleep 2
  done

  echo "PostgreSQL indisponivel apos timeout." >&2
  compose_command "$env_file" logs --no-color postgres-db || true
  return 1
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

apply_owasp_zap_migrations() {
  local postgres_port="${POSTGRES_HOST_PORT:-15432}"
  local postgres_connection_prefix="Host=127.0.0.1;Port=$postgres_port;Database=appdb"

  run_migration \
    "$postgres_connection_prefix;Username=ledger_migrator_user;Password=$LEDGER_DB_MIGRATOR_PASSWORD" \
    src/ledger/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj \
    src/ledger/LedgerService.Api/LedgerService.Api.csproj \
    AppDbContext

  run_migration \
    "$postgres_connection_prefix;Username=balance_migrator_user;Password=$BALANCE_DB_MIGRATOR_PASSWORD" \
    src/balance/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj \
    src/balance/BalanceService.Api/BalanceService.Api.csproj \
    BalanceDbContext

  run_migration \
    "$postgres_connection_prefix;Username=transfer_migrator_user;Password=$TRANSFER_DB_MIGRATOR_PASSWORD" \
    src/transfer/TransferService.Infrastructure/TransferService.Infrastructure.csproj \
    src/transfer/TransferService.Api/TransferService.Api.csproj \
    TransferServiceDbContext \
    TRANSFER_SERVICE_CONNECTION_STRING

  run_migration \
    "$postgres_connection_prefix;Username=payment_migrator_user;Password=$PAYMENT_DB_MIGRATOR_PASSWORD" \
    src/payment/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj \
    src/payment/PaymentService.Api/PaymentService.Api.csproj \
    PaymentDbContext \
    PAYMENT_SERVICE_CONNECTION_STRING

  run_migration \
    "$postgres_connection_prefix;Username=audit_migrator_user;Password=$AUDIT_DB_MIGRATOR_PASSWORD" \
    src/audit/AuditService.Infrastructure/AuditService.Infrastructure.csproj \
    src/audit/AuditService.Infrastructure/AuditService.Infrastructure.csproj \
    AuditDbContext \
    AUDIT_SERVICE_CONNECTION_STRING

  run_migration \
    "$postgres_connection_prefix;Username=identity_migrator_user;Password=$IDENTITY_DB_MIGRATOR_PASSWORD" \
    src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj \
    src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj \
    IdentityDbContext \
    IDENTITY_SERVICE_CONNECTION_STRING
}

wait_for_owasp_zap_apis() {
  local env_file="$1"
  local ledger_port balance_port transfer_port identity_port payment_port audit_port
  local target name url attempt
  local -a targets

  ledger_port="$(read_env_or_default "$env_file" LEDGER_SERVICE_HOST_PORT 5226)"
  balance_port="$(read_env_or_default "$env_file" BALANCE_SERVICE_HOST_PORT 5228)"
  transfer_port="$(read_env_or_default "$env_file" TRANSFER_SERVICE_HOST_PORT 5230)"
  identity_port="$(read_env_or_default "$env_file" IDENTITY_SERVICE_HOST_PORT 5232)"
  payment_port="$(read_env_or_default "$env_file" PAYMENT_SERVICE_HOST_PORT 5234)"
  audit_port="$(read_env_or_default "$env_file" AUDIT_SERVICE_HOST_PORT 5235)"

  targets=(
    "LedgerService.Api|http://localhost:$ledger_port/health"
    "BalanceService.Api|http://localhost:$balance_port/health"
    "TransferService.Api|http://localhost:$transfer_port/health"
    "IdentityService.Api|http://localhost:$identity_port/health"
    "PaymentService.Api|http://localhost:$payment_port/health"
    "AuditService.Api|http://localhost:$audit_port/health"
  )

  for target in "${targets[@]}"; do
    IFS='|' read -r name url <<<"$target"
    for attempt in {1..90}; do
      if curl -fsS "$url" >/dev/null; then
        echo "$name disponivel em $url"
        break
      fi

      if [[ "$attempt" -eq 90 ]]; then
        echo "API indisponivel: $name ($url)" >&2
        compose_command "$env_file" ps || true
        compose_command "$env_file" logs --no-color \
          ledger-service \
          balance-service \
          transfer-service \
          payment-service \
          audit-service \
          identity-service || true
        return 1
      fi

      sleep 2
    done
  done
}

discover_owasp_zap_network() {
  local env_file="$1"
  local service="${2:-ledger-service}"
  local container_id
  local api_network

  container_id="$(compose_command "$env_file" ps -q "$service")"
  if [[ -z "$container_id" ]]; then
    echo "Nao foi possivel descobrir o container $service." >&2
    return 1
  fi

  api_network="$(
    docker inspect "$container_id" \
      --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' |
      awk '/(^|_)poc-net$/ { print; exit }'
  )"

  if [[ -z "$api_network" ]]; then
    echo "Rede poc-net nao encontrada no container $service." >&2
    return 1
  fi

  printf '%s' "$api_network"
}

validate_owasp_zap_services_on_network() {
  local env_file="$1"
  local api_network="$2"
  local service container_id
  local -a api_services=(
    ledger-service
    balance-service
    transfer-service
    payment-service
    audit-service
    identity-service
  )

  for service in "${api_services[@]}"; do
    container_id="$(compose_command "$env_file" ps -q "$service")"
    if [[ -z "$container_id" ]]; then
      echo "Container da API nao encontrado: $service" >&2
      return 1
    fi

    if ! docker inspect "$container_id" \
      --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' |
      grep -Fx "$api_network" >/dev/null; then
      echo "$service nao esta conectado a rede Docker $api_network." >&2
      return 1
    fi
  done
}
