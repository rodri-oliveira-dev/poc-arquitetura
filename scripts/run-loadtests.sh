#!/usr/bin/env bash
set -euo pipefail

# Runner reprodutível:
#  a) gera .env.k6.auto a partir do compose.yaml
#  b) obtém TOKEN conforme README
#  c) roda k6 dentro do compose network via docker compose

MODE="${1:-}"
if [[ -z "$MODE" ]]; then
  echo "Uso: ./scripts/run-loadtests.sh <smoke|balance50|resilience|transfer-smoke|transfer-load|transfer-fullstack-kafka>" 1>&2
  exit 2
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
COMPOSE_KAFKA_FILE="${COMPOSE_KAFKA_FILE:-$ROOT_DIR/compose.kafka.yaml}"
COMPOSE_K6_FILE="${COMPOSE_K6_FILE:-$ROOT_DIR/compose.k6.yaml}"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env.k6.auto}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$ROOT_DIR/artifacts/k6}"

mkdir -p "$ARTIFACTS_DIR"

get_local_env_value() {
  local name="$1"

  for env_file in "$ROOT_DIR/.env.local" "$ROOT_DIR/.env"; do
    if [[ ! -f "$env_file" ]]; then
      continue
    fi

    local value
    value="$(sed -nE "s/^[[:space:]]*$name[[:space:]]*=[[:space:]]*(.*)[[:space:]]*$/\1/p" "$env_file" |
      tail -n 1 |
      sed -E "s/^['\"]//; s/['\"]$//")"
    if [[ -n "$value" ]]; then
      printf '%s' "$value"
      return 0
    fi
  done
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

get_required_local_config_value() {
  local name="$1"
  local value
  value="$(get_local_config_value "$name" "")"

  if [[ -z "$value" ]]; then
    echo "Defina $name no ambiente, em .env.local ou em .env." >&2
    exit 1
  fi

  printf '%s' "$value"
}

get_compose_env_args() {
  for env_file in "$ROOT_DIR/.env.local" "$ROOT_DIR/.env"; do
    if [[ -f "$env_file" ]]; then
      printf '%s\n' "--env-file"
      printf '%s\n' "$env_file"
      return 0
    fi
  done
}

mapfile -t compose_env_args < <(get_compose_env_args)

threshold_value() {
  local prefix="$1"
  local percentile="$2"
  local default_value="$3"
  local specific_name="${prefix}_HTTP_REQ_DURATION_${percentile}_MS"
  local global_name="K6_HTTP_REQ_DURATION_${percentile}_MS"
  local value

  value="$(get_local_config_value "$specific_name" "")"
  if [[ -z "$value" ]]; then
    value="$(get_local_config_value "$global_name" "")"
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

  host_name="$(get_local_config_value BALANCE_DB_HOST postgres-db)"

  cat >&2 <<EOF
Falha de autenticacao no banco Balance para o usuario "$user" e database "$database".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs postgres-db
  docker compose logs balance-service
  docker compose exec -T postgres-db psql -h "$host_name" -U "$user" -d "$database" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie manualmente o volume local do PostgreSQL se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
EOF
}

assert_balance_database_authentication() {
  local user
  local database
  local password
  local host_name

  host_name="$(get_local_config_value BALANCE_DB_HOST postgres-db)"
  user="$(get_local_config_value BALANCE_DB_WRITE_USER "$(get_local_config_value BALANCE_DB_USER balance_write_user)")"
  database="$(get_local_config_value BALANCE_DB_NAME appdb)"
  password="$(get_required_local_config_value BALANCE_DB_WRITE_PASSWORD)"

  if ! docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" exec -T \
    -e "PGPASSWORD=$password" \
    postgres-db \
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
    if docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" ps "$service" --format json | grep -q '"Health":"healthy"'; then
      return 0
    fi

    health="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" ps "$service" --format json | sed -nE 's/.*"Health":"([^"]*)".*/\1/p' | tail -n 1)"
    if [[ "$health" == "unhealthy" ]]; then
      echo "$service ficou unhealthy durante a preparacao do k6." 1>&2
      exit 1
    fi

    sleep 5
  done

  echo "Timeout aguardando $service ficar healthy. Ultimo health: ${health:-desconhecido}" 1>&2
  exit 1
}

wait_http_endpoint() {
  local url="$1"
  local timeout_seconds="${2:-120}"
  local deadline=$((SECONDS + timeout_seconds))

  while (( SECONDS < deadline )); do
    if curl -fsS "$url" >/dev/null; then
      return 0
    fi

    sleep 2
  done

  echo "Timeout aguardando endpoint HTTP: $url" 1>&2
  exit 1
}

assert_local_pubsub_stack() {
  local config
  local running_services
  config="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" config)"

  for expected in \
    "Messaging__Provider: PubSub" \
    "PUBSUB_EMULATOR_HOST: pubsub-emulator:8085"
  do
    if ! grep -Fq "$expected" <<<"$config"; then
      echo "Stack k6 deve usar Pub/Sub emulator local. Configuracao ausente: $expected" >&2
      exit 1
    fi
  done

  running_services="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" ps --status running --services)"
  for service in pubsub-emulator ledger-worker balance-worker; do
    if ! grep -qx "$service" <<<"$running_services"; then
      echo "Servico obrigatorio para k6 local nao esta em execucao: $service. Suba ./scripts/start-local-stack.sh antes do teste." >&2
      exit 1
    fi
  done

  local pubsub_init
  pubsub_init="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" ps -a pubsub-init --format json)"
  if ! grep -q '"State":"exited"' <<<"$pubsub_init" ||
    ! grep -q '"ExitCode":0' <<<"$pubsub_init"; then
    echo "pubsub-init nao concluiu com sucesso. Confira: docker compose logs pubsub-init" >&2
    exit 1
  fi
}

assert_local_transfer_kafka_stack() {
  local config
  local running_services
  config="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_KAFKA_FILE" config)"

  for expected in \
    "Messaging__Provider: Kafka" \
    "TransferService__Worker__Kafka__BootstrapServers: kafka:9092" \
    "TransferService__Worker__Topics__Solicitada: transfer.transferencia.solicitada"
  do
    if ! grep -Fq "$expected" <<<"$config"; then
      echo "Stack full-stack do TransferService deve usar Kafka. Configuracao ausente: $expected" >&2
      exit 1
    fi
  done

  running_services="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_KAFKA_FILE" ps --status running --services)"
  for service in kafka ledger-service transfer-service transfer-worker; do
    if ! grep -qx "$service" <<<"$running_services"; then
      echo "Servico obrigatorio para full-stack Kafka nao esta em execucao: $service. Suba ./scripts/start-local-stack-kafka.sh antes do teste." >&2
      exit 1
    fi
  done

  local kafka_init
  kafka_init="$(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_KAFKA_FILE" ps -a kafka-init-topics --format json)"
  if ! grep -q '"State":"exited"' <<<"$kafka_init" ||
    ! grep -q '"ExitCode":0' <<<"$kafka_init"; then
    echo "kafka-init-topics nao concluiu com sucesso. Confira: docker compose logs kafka-init-topics" >&2
    exit 1
  fi
}

kafka_topic_end_offset() {
  local topic="$1"
  docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_KAFKA_FILE" exec -T kafka \
    /opt/kafka/bin/kafka-run-class.sh \
    kafka.tools.GetOffsetShell \
    --broker-list kafka:9092 \
    --topic "$topic" \
    --time -1 |
    awk -F: '{ sum += $NF } END { print sum + 0 }'
}

capture_transfer_kafka_offsets() {
  local output_file="$1"
  : >"$output_file"
  for topic in \
    transfer.transferencia.solicitada \
    transfer.transferencia.debito-criado \
    transfer.transferencia.credito-criado \
    transfer.transferencia.concluida \
    transfer.transferencia.dlq
  do
    printf '%s=%s\n' "$topic" "$(kafka_topic_end_offset "$topic")" >>"$output_file"
  done
}

offset_value() {
  local file="$1"
  local topic="$2"
  sed -nE "s/^${topic//./\\.}=([0-9]+)$/\1/p" "$file" | tail -n 1
}

assert_transfer_kafka_events_published() {
  local before_file="$1"
  local after_file="$2"

  for topic in \
    transfer.transferencia.solicitada \
    transfer.transferencia.debito-criado \
    transfer.transferencia.credito-criado \
    transfer.transferencia.concluida
  do
    local before
    local after
    before="$(offset_value "$before_file" "$topic")"
    after="$(offset_value "$after_file" "$topic")"
    if (( after <= before )); then
      echo "Kafka nao recebeu evento esperado no topico $topic durante o full-stack smoke." >&2
      exit 1
    fi
  done

  local dlq_topic="transfer.transferencia.dlq"
  local dlq_before
  local dlq_after
  dlq_before="$(offset_value "$before_file" "$dlq_topic")"
  dlq_after="$(offset_value "$after_file" "$dlq_topic")"
  if (( dlq_after != dlq_before )); then
    echo "DLQ Kafka recebeu mensagem no fluxo feliz do TransferService: $dlq_topic antes=$dlq_before depois=$dlq_after" >&2
    exit 1
  fi

  echo "OK. Full-stack Kafka publicou eventos da Saga e manteve DLQ sem crescimento."
}

postgres_count() {
  local service="$1"
  local user="$2"
  local database="$3"
  local sql="$4"
  local password="$5"
  docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" exec -T \
    -e "PGPASSWORD=$password" \
    "$service" \
    psql -h "$service" -U "$user" -d "$database" -t -A -c "$sql" |
    tr -d '[:space:]'
}

async_flow_counts() {
  local ledger_user
  local ledger_database
  local ledger_password
  local balance_user
  local balance_database
  local balance_password
  local outbox_total
  local outbox_processed
  local balance_processed
  ledger_user="$(get_local_config_value LEDGER_DB_USER ledger_app_user)"
  ledger_database="$(get_local_config_value LEDGER_DB_NAME appdb)"
  ledger_password="$(get_required_local_config_value LEDGER_DB_PASSWORD)"
  balance_user="$(get_local_config_value BALANCE_DB_READ_USER "$(get_local_config_value BALANCE_DB_USER balance_read_user)")"
  balance_database="$(get_local_config_value BALANCE_DB_NAME appdb)"
  balance_password="$(get_required_local_config_value BALANCE_DB_READ_PASSWORD)"
  outbox_total="$(postgres_count postgres-db "$ledger_user" "$ledger_database" "SELECT COUNT(*) FROM outbox_messages WHERE event_type IN ('LedgerEntryCreated.v1', 'LedgerEntryCreated.v2');" "$ledger_password")"
  outbox_processed="$(postgres_count postgres-db "$ledger_user" "$ledger_database" "SELECT COUNT(*) FROM outbox_messages WHERE event_type IN ('LedgerEntryCreated.v1', 'LedgerEntryCreated.v2') AND status = 'Processed';" "$ledger_password")"
  balance_processed="$(postgres_count postgres-db "$balance_user" "$balance_database" "SELECT COUNT(*) FROM processed_events;" "$balance_password")"
  printf '%s %s %s\n' "$outbox_total" "$outbox_processed" "$balance_processed"
}

wait_async_flow_progress() {
  local before_outbox="$1"
  local before_balance="$2"
  local deadline=$((SECONDS + 90))
  local current_outbox
  local current_balance

  while (( SECONDS < deadline )); do
    read -r _ current_outbox current_balance < <(async_flow_counts)
    if (( current_outbox > before_outbox && current_balance > before_balance )); then
      echo "OK. Smoke Pub/Sub publicou Outbox e projetou evento no Balance."
      return 0
    fi
    sleep 2
  done

  echo "Timeout aguardando publish/consume via Pub/Sub emulator apos smoke k6." >&2
  exit 1
}

wait_async_flow_idle() {
  local deadline=$((SECONDS + 120))
  local outbox_total
  local outbox_processed
  local balance_processed

  while (( SECONDS < deadline )); do
    read -r outbox_total outbox_processed balance_processed < <(async_flow_counts)
    if (( outbox_processed >= outbox_total && balance_processed >= outbox_processed )); then
      echo "OK. Fluxo assincrono local sem backlog antes do k6."
      return 0
    fi
    sleep 2
  done

  echo "Timeout aguardando drenagem do fluxo assincrono antes do k6." >&2
  exit 1
}

# a) gerar env
COMPOSE_FILE="$COMPOSE_FILE" OUT_FILE="$ENV_FILE" "$ROOT_DIR/scripts/compose-env.sh" >/dev/null

is_transfer_fullstack_kafka=false
if [[ "$MODE" == "transfer-fullstack-kafka" ]]; then
  is_transfer_fullstack_kafka=true
fi

is_transfer_mode=false
if [[ "$MODE" == "transfer-smoke" || "$MODE" == "transfer-load" || "$MODE" == "transfer-fullstack-kafka" ]]; then
  is_transfer_mode=true
fi

if [[ "$is_transfer_mode" != true ]]; then
  assert_local_pubsub_stack
fi

# Aplica o override de carga nas APIs antes de executar o k6. O compose.k6.yaml
# mantem os testes apontando para as APIs HTTP e aumenta apenas limites tecnicos
# que poderiam transformar o cenario de throughput em teste de rate limiting.
if [[ "$is_transfer_mode" == true ]]; then
  if [[ "$is_transfer_fullstack_kafka" == true ]]; then
    docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_KAFKA_FILE" -f "$COMPOSE_K6_FILE" up -d --no-build --force-recreate kafka kafka-init-topics ledger-service transfer-service transfer-worker
  else
    docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" up -d --no-build --force-recreate transfer-service
  fi
else
  docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" up -d --no-build --force-recreate ledger-service balance-service
fi

wait_compose_service_healthy keycloak
if [[ "$is_transfer_mode" == true ]]; then
  transfer_host_port="$(get_local_config_value TRANSFER_SERVICE_HOST_PORT 5230)"
  wait_http_endpoint "http://localhost:$transfer_host_port/health"
  if [[ "$is_transfer_fullstack_kafka" == true ]]; then
    ledger_host_port="$(get_local_config_value LEDGER_SERVICE_HOST_PORT 5226)"
    wait_http_endpoint "http://localhost:$ledger_host_port/health"
    assert_local_transfer_kafka_stack
  fi
else
  assert_balance_database_authentication
fi

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
  local url="http://localhost:5228/api/v1/consolidados/diario/$date_value?merchantId=$encoded_merchant"

  for _ in $(seq 1 30); do
    if curl -fsS -H "Authorization: Bearer $TOKEN" "$url" >/dev/null; then
      return 0
    fi
    sleep 1
  done

  curl -fsS -H "Authorization: Bearer $TOKEN" "$url" >/dev/null
}

if [[ "$is_transfer_mode" != true ]]; then
  warmup_balance
fi

ts="$(date +%Y%m%d-%H%M%S)"

run_k6() {
  local scenarioName="$1"; shift
  local scriptPath="$1"; shift
  local summaryFile="summary-$MODE-$scenarioName-$ts.json"
  local hostSummary="$ARTIFACTS_DIR/$summaryFile"

  docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" --profile k6 run --rm \
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
    read -r _ before_outbox before_balance < <(async_flow_counts)
    run_k6 ledger_resilience scenarios/ledger_resilience.js -e VUS=1 -e DURATION=10s -e LEDGER_HTTP_REQ_DURATION_P95_MS="$(threshold_value LEDGER P95 3000)" -e LEDGER_HTTP_REQ_DURATION_P99_MS="$(threshold_value LEDGER P99 6000)"
    wait_async_flow_progress "$before_outbox" "$before_balance"
    run_k6 balance_daily_50rps scenarios/balance_daily_50rps.js -e RATE=1 -e DURATION=10s -e PREALLOCATED_VUS=5 -e MAX_VUS=10 -e BALANCE_HTTP_REQ_DURATION_P95_MS="$(threshold_value BALANCE P95 3000)" -e BALANCE_HTTP_REQ_DURATION_P99_MS="$(threshold_value BALANCE P99 6000)"
    ;;
  balance50)
    wait_async_flow_idle
    run_k6 balance_daily_50rps scenarios/balance_daily_50rps.js -e RATE=50 -e DURATION=1m -e BALANCE_HTTP_REQ_DURATION_P95_MS="$(threshold_value BALANCE P95 1000)" -e BALANCE_HTTP_REQ_DURATION_P99_MS="$(threshold_value BALANCE P99 2500)"
    ;;
  resilience)
    run_k6 ledger_resilience scenarios/ledger_resilience.js -e VUS=5 -e DURATION=1m -e LEDGER_HTTP_REQ_DURATION_P95_MS="$(threshold_value LEDGER P95 2000)" -e LEDGER_HTTP_REQ_DURATION_P99_MS="$(threshold_value LEDGER P99 5000)"
    ;;
  transfer-smoke)
    run_k6 transfer_smoke scenarios/transfer_smoke.js -e DURATION=30s -e TRANSFER_HTTP_REQ_DURATION_P95_MS="$(threshold_value TRANSFER P95 500)" -e TRANSFER_HTTP_REQ_DURATION_P99_MS="$(threshold_value TRANSFER P99 1000)"
    ;;
  transfer-load)
    run_k6 transfer_load scenarios/transfer_load.js -e VUS=10 -e TRANSFER_HTTP_REQ_DURATION_P95_MS="$(threshold_value TRANSFER P95 1000)" -e TRANSFER_HTTP_REQ_DURATION_P99_MS="$(threshold_value TRANSFER P99 2000)"
    ;;
  transfer-fullstack-kafka)
    before_offsets="$(mktemp)"
    after_offsets="$(mktemp)"
    trap 'rm -f "$before_offsets" "$after_offsets"' EXIT
    capture_transfer_kafka_offsets "$before_offsets"
    run_k6 transfer_fullstack_kafka scenarios/transfer_fullstack_kafka.js -e VUS=1 -e ITERATIONS=1 -e DURATION=90s -e TRANSFER_FINAL_STATUS_TIMEOUT_SECONDS=60 -e TRANSFER_HTTP_REQ_DURATION_P95_MS="$(threshold_value TRANSFER P95 1000)" -e TRANSFER_HTTP_REQ_DURATION_P99_MS="$(threshold_value TRANSFER P99 2000)"
    capture_transfer_kafka_offsets "$after_offsets"
    assert_transfer_kafka_events_published "$before_offsets" "$after_offsets"
    ;;
  *)
    echo "Modo invalido: $MODE (use smoke|balance50|resilience|transfer-smoke|transfer-load|transfer-fullstack-kafka)" 1>&2
    exit 2
    ;;
esac

echo "OK. Artifacts em: $ARTIFACTS_DIR" 1>&2

# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).
