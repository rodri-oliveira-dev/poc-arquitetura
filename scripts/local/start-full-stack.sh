#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/compose.yaml"
COMPOSE_OBSERVABILITY_FILE="$ROOT_DIR/compose.observability.yaml"
COMPOSE_NGINX_FILE="$ROOT_DIR/compose.nginx.yaml"
CERT_FILE="$ROOT_DIR/infra/nginx/certs/localhost.crt"
KEY_FILE="$ROOT_DIR/infra/nginx/certs/localhost.key"
NO_BUILD="${NO_BUILD:-false}"
SKIP_HEALTH_CHECKS="${SKIP_HEALTH_CHECKS:-false}"
CLEANUP="${CLEANUP:-false}"
PROJECT_NAME="${COMPOSE_PROJECT_NAME:-poc-arquitetura}"
PROJECT_NETWORK_SERVICE="poc-net"
OVERLAY_SERVICES=(nginx-edge ledger-service-1 ledger-service-2)

get_compose_env_args() {
  for env_file in "$ROOT_DIR/.env.local" "$ROOT_DIR/.env"; do
    if [[ -f "$env_file" ]]; then
      printf '%s\n' "--env-file"
      printf '%s\n' "$env_file"
      return 0
    fi
  done
}

get_local_config_value() {
  local key="$1"
  local default_value="$2"
  local current_value="${!key:-}"
  if [[ -n "$current_value" ]]; then
    printf '%s\n' "$current_value"
    return 0
  fi

  local env_file
  for env_file in "$ROOT_DIR/.env.local" "$ROOT_DIR/.env"; do
    if [[ -f "$env_file" ]]; then
      local value
      value="$(awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$env_file")"
      if [[ -n "$value" ]]; then
        printf '%s\n' "$value"
        return 0
      fi
    fi
  done

  printf '%s\n' "$default_value"
}

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/local/start-full-stack.sh [--no-build] [--skip-health-checks] [--cleanup]

Sobe a stack local completa com observabilidade e Nginx.
Nao remove volumes, nao executa testes, k6 nem scanners.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-build)
      NO_BUILD=true
      shift
      ;;
    --skip-health-checks)
      SKIP_HEALTH_CHECKS=true
      shift
      ;;
    --cleanup)
      CLEANUP=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Parametro invalido: $1" >&2
      usage
      exit 2
      ;;
  esac
done

assert_command_available() {
  local command_name="$1"
  local install_hint="$2"

  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "$command_name nao encontrado. $install_hint" >&2
    exit 1
  fi
}

assert_nginx_certificates() {
  if [[ -f "$CERT_FILE" && -f "$KEY_FILE" ]]; then
    return 0
  fi

  cat >&2 <<'EOF'
Certificados locais do Nginx nao encontrados.

Arquivos esperados:
  infra/nginx/certs/localhost.crt
  infra/nginx/certs/localhost.key

Gere os certificados conforme docs/development/local-development.md ou infra/nginx/README.md.
O script nao gera certificados automaticamente.
EOF
  exit 1
}

docker_rows() {
  docker "$@" | sed '/^[[:space:]]*$/d'
}

compose_service_containers() {
  local service="$1"
  docker ps -a \
    --filter "label=com.docker.compose.project=$PROJECT_NAME" \
    --filter "label=com.docker.compose.service=$service" \
    --format "{{.Names}}|{{.Status}}"
}

port_is_open() {
  local port="$1"

  if command -v nc >/dev/null 2>&1; then
    nc -z 127.0.0.1 "$port" >/dev/null 2>&1
    return $?
  fi

  if command -v python3 >/dev/null 2>&1; then
    python3 - "$port" <<'PY'
import socket
import sys

port = int(sys.argv[1])
with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.settimeout(0.25)
    sys.exit(0 if s.connect_ex(("127.0.0.1", port)) == 0 else 1)
PY
    return $?
  fi

  return 1
}

port_owner_container() {
  local port="$1"
  docker ps --format "{{.Names}}|{{.Label \"com.docker.compose.project\"}}|{{.Ports}}" |
    awk -F'|' -v port="$port" '$3 ~ "(^|,|[[:space:]])(0\\.0\\.0\\.0|\\[::\\]|127\\.0\\.0\\.1):" port "->" { print $1 "|" $2; exit }'
}

assert_no_external_port_conflicts() {
  local conflicts=()
  local item
  local name
  local port
  local owner

  local required_ports=(
    "PostgreSQL:$(get_local_config_value POSTGRES_HOST_PORT 15432)"
    "Kafka:$(get_local_config_value KAFKA_HOST_PORT 19092)"
    "Pub/Sub emulator:$(get_local_config_value PUBSUB_EMULATOR_HOST_PORT 8085)"
    "Mailpit SMTP:$(get_local_config_value MAILPIT_SMTP_HOST_PORT 1025)"
    "Mailpit UI:$(get_local_config_value MAILPIT_UI_HOST_PORT 8025)"
    "Keycloak:$(get_local_config_value KEYCLOAK_HOST_PORT 8081)"
    "LedgerService.Api:$(get_local_config_value LEDGER_SERVICE_HOST_PORT 5226)"
    "BalanceService.Api:$(get_local_config_value BALANCE_SERVICE_HOST_PORT 5228)"
    "TransferService.Api:$(get_local_config_value TRANSFER_SERVICE_HOST_PORT 5230)"
    "PaymentService.Api:$(get_local_config_value PAYMENT_SERVICE_HOST_PORT 5234)"
    "AuditService.Api:$(get_local_config_value AUDIT_SERVICE_HOST_PORT 5235)"
    "IdentityService.Api:$(get_local_config_value IDENTITY_SERVICE_HOST_PORT 5232)"
    "Portal Nginx HTTPS:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)"
    "Grafana:$(get_local_config_value GRAFANA_HOST_PORT 3000)"
    "Jaeger UI:$(get_local_config_value JAEGER_UI_HOST_PORT 16686)"
    "Prometheus:$(get_local_config_value PROMETHEUS_HOST_PORT 9090)"
    "Alertmanager:$(get_local_config_value ALERTMANAGER_HOST_PORT 9093)"
    "Loki:$(get_local_config_value LOKI_HOST_PORT 3100)"
    "Grafana Alloy:$(get_local_config_value ALLOY_HOST_PORT 12345)"
    "Jaeger OTLP gRPC:$(get_local_config_value JAEGER_OTLP_GRPC_HOST_PORT 4317)"
    "Jaeger OTLP HTTP:$(get_local_config_value JAEGER_OTLP_HTTP_HOST_PORT 4318)"
  )

  for item in "${required_ports[@]}"; do
    name="${item%:*}"
    port="${item##*:}"
    if ! port_is_open "$port"; then
      continue
    fi

    owner="$(port_owner_container "$port" || true)"
    owner_name="${owner%%|*}"
    owner_project="${owner#*|}"
    if [[ "$owner" == *"|"* && "$owner_project" == "$PROJECT_NAME" ]]; then
      continue
    fi

    if [[ -z "$owner_name" ]]; then
      owner="processo local fora do Docker ou container sem publicacao detectavel"
    else
      owner="container Docker '$owner_name'"
    fi
    conflicts+=("  - $name usa a porta $port, ocupada por $owner")
  done

  if [[ "${#conflicts[@]}" -gt 0 ]]; then
    printf 'Ha portas necessarias para a stack completa em uso por recursos externos ao projeto:\n' >&2
    printf '%s\n' "${conflicts[@]}" >&2
    cat >&2 <<'EOF'

Libere essas portas manualmente e execute o script novamente.
A limpeza automatica do script atua somente em containers/redes deste projeto e nao para processos externos.
EOF
    exit 1
  fi
}

run_non_destructive_project_cleanup() {
  echo "Executando limpeza nao destrutiva da stack local..."
  docker compose \
    -f "$COMPOSE_FILE" \
    -f "$COMPOSE_OBSERVABILITY_FILE" \
    -f "$COMPOSE_NGINX_FILE" \
    --profile observability \
    --profile direct-ledger \
    down \
    --remove-orphans
}

confirm_project_cleanup() {
  local reason="$1"
  if [[ "$CLEANUP" == "true" ]]; then
    return 0
  fi

  printf '\n%s\n' "$reason" >&2
  cat >&2 <<'EOF'
A limpeza proposta usa 'docker compose down --remove-orphans' sem '-v'.
Ela para/remove containers e redes locais do projeto, mas preserva volumes, bancos, imagens e certificados.
EOF
  read -r -p "Pode liberar esses recursos antes de subir a stack completa? [s/N] " answer
  [[ "$answer" =~ ^([sS]|[sS][iI][mM]|[yY]|[yY][eE][sS])$ ]]
}

assert_startup_resources_available() {
  assert_no_external_port_conflicts

  local overlay_details=()
  local row
  local service
  for service in "${OVERLAY_SERVICES[@]}"; do
    while IFS= read -r row; do
      [[ -n "$row" ]] && overlay_details+=("  - $row")
    done < <(compose_service_containers "$service")
  done

  if [[ "${#overlay_details[@]}" -gt 0 ]]; then
    local reason
    reason="$(
      cat <<EOF
Foram encontrados containers do overlay Nginx ja existentes antes da subida da stack base.
Esses containers podem aparecer como orfaos quando o script chama start-local-stack.sh e, em estados parciais, podem prender uma network antiga.
$(printf '%s\n' "${overlay_details[@]}")
EOF
    )"
    if confirm_project_cleanup "$reason"; then
      run_non_destructive_project_cleanup
      return
    fi

    echo "Subida interrompida. Libere os recursos listados ou execute novamente com --cleanup para aplicar a limpeza nao destrutiva." >&2
    exit 1
  fi

  local project_networks
  project_networks="$(
    docker network ls \
      --filter "label=com.docker.compose.project=$PROJECT_NAME" \
      --filter "label=com.docker.compose.network=$PROJECT_NETWORK_SERVICE" \
      --format "{{.Name}}"
  )"
  if [[ -n "$project_networks" ]]; then
    local network_containers
    local network_name
    while IFS= read -r network_name; do
      [[ -z "$network_name" ]] && continue
      network_containers="$(docker network inspect "$network_name" --format "{{json .Containers}}" 2>/dev/null || true)"
      if [[ -n "$network_containers" && "$network_containers" != "null" && "$network_containers" != "{}" ]]; then
        local reason="A rede local do projeto Compose '$PROJECT_NAME' ja existe com containers conectados. Isso pode indicar stack anterior ou estado parcial."
        if confirm_project_cleanup "$reason"; then
          run_non_destructive_project_cleanup
          return
        fi

        echo "Subida interrompida. Libere os recursos da rede do projeto '$PROJECT_NAME' ou execute novamente com --cleanup." >&2
        exit 1
      fi
    done <<< "$project_networks"
  fi
}

http_check() {
  local name="$1"
  local url="$2"
  local insecure="${3:-false}"
  local curl_args=(-fsS --max-time 10)

  if [[ "$insecure" == "true" ]]; then
    curl_args=(-k "${curl_args[@]}")
  fi

  echo "Validando $name: $url"
  curl "${curl_args[@]}" "$url" >/dev/null
}

assert_command_available docker "Instale/configure Docker CLI com suporte a 'docker compose'."
assert_command_available dotnet "Instale o .NET SDK definido em global.json."
assert_command_available curl "Instale curl para as validacoes HTTP leves."
docker compose version >/dev/null
assert_nginx_certificates
assert_startup_resources_available

cd "$ROOT_DIR"

export OTEL_ENABLED="${OTEL_ENABLED:-true}"
export OBSERVABILITY=true
if [[ "$NO_BUILD" == "true" ]]; then
  NO_BUILD=true "$ROOT_DIR/scripts/local/start-stack.sh"
else
  "$ROOT_DIR/scripts/local/start-stack.sh"
fi

mapfile -t compose_env_args < <(get_compose_env_args)

nginx_up=(docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_OBSERVABILITY_FILE" -f "$COMPOSE_NGINX_FILE" --profile observability up -d)
if [[ "$NO_BUILD" != "true" ]]; then
  nginx_up+=(--build)
fi
nginx_up+=(ledger-service-1 ledger-service-2 nginx-edge)
"${nginx_up[@]}"

docker compose "${compose_env_args[@]}" -f "$COMPOSE_FILE" -f "$COMPOSE_OBSERVABILITY_FILE" -f "$COMPOSE_NGINX_FILE" --profile observability ps

if [[ "$SKIP_HEALTH_CHECKS" != "true" ]]; then
  http_check "LedgerService.Api direta" "http://localhost:$(get_local_config_value LEDGER_SERVICE_HOST_PORT 5226)/health"
  http_check "BalanceService.Api direta" "http://localhost:$(get_local_config_value BALANCE_SERVICE_HOST_PORT 5228)/health"
  http_check "Portal Nginx" "https://localhost:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)/" true
  http_check "Ledger via Nginx" "https://ledger.localhost:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)/health" true
  http_check "Balance via Nginx" "https://balance.localhost:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)/health" true
  http_check "Grafana" "http://localhost:$(get_local_config_value GRAFANA_HOST_PORT 3000)/api/health"
  http_check "Jaeger" "http://localhost:$(get_local_config_value JAEGER_UI_HOST_PORT 16686)/"
  http_check "Prometheus" "http://localhost:$(get_local_config_value PROMETHEUS_HOST_PORT 9090)/-/ready"
  http_check "Alertmanager" "http://localhost:$(get_local_config_value ALERTMANAGER_HOST_PORT 9093)/-/ready"
fi

cat <<EOF

OK. Stack completa local pronta.

URLs uteis:
  LedgerService.Api:     http://localhost:$(get_local_config_value LEDGER_SERVICE_HOST_PORT 5226)/
  BalanceService.Api:    http://localhost:$(get_local_config_value BALANCE_SERVICE_HOST_PORT 5228)/
  Portal Nginx:          https://localhost:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)/
  Ledger Swagger Nginx:  https://ledger.localhost:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)/swagger
  Balance Swagger Nginx: https://balance.localhost:$(get_local_config_value NGINX_HTTPS_HOST_PORT 7443)/swagger
  Grafana:               http://localhost:$(get_local_config_value GRAFANA_HOST_PORT 3000)/
  Jaeger:                http://localhost:$(get_local_config_value JAEGER_UI_HOST_PORT 16686)/
  Prometheus:            http://localhost:$(get_local_config_value PROMETHEUS_HOST_PORT 9090)/
  Alertmanager:          http://localhost:$(get_local_config_value ALERTMANAGER_HOST_PORT 9093)/

Este script nao remove volumes, nao executa testes, k6 nem scanners.
EOF
