#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/compose.yaml"
COMPOSE_OBSERVABILITY_FILE="$ROOT_DIR/compose.observability.yaml"
COMPOSE_NGINX_FILE="$ROOT_DIR/compose.nginx.yaml"
CERT_FILE="$ROOT_DIR/infra/nginx/certs/localhost.crt"
KEY_FILE="$ROOT_DIR/infra/nginx/certs/localhost.key"
NO_BUILD="${NO_BUILD:-false}"
SKIP_HEALTH_CHECKS="${SKIP_HEALTH_CHECKS:-false}"
CLEANUP="${CLEANUP:-false}"
PROJECT_NETWORK_NAME="poc-arquitetura_poc-net"
PROJECT_CONTAINER_PREFIX="poc-"
OVERLAY_CONTAINER_NAMES=(poc-nginx-edge poc-ledger-service-1 poc-ledger-service-2)

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/start-full-stack.sh [--no-build] [--skip-health-checks] [--cleanup]

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

container_exists() {
  local name="$1"
  docker ps -a --filter "name=^/$name$" --format "{{.Names}}|{{.Status}}"
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
  docker ps --format "{{.Names}}|{{.Ports}}" |
    awk -F'|' -v port="$port" '$2 ~ "(^|,|[[:space:]])(0\\.0\\.0\\.0|\\[::\\]|127\\.0\\.0\\.1):" port "->" { print $1; exit }'
}

assert_no_external_port_conflicts() {
  local conflicts=()
  local item
  local name
  local port
  local owner

  local required_ports=(
    "LedgerService.Api:5226"
    "BalanceService.Api:5228"
    "Portal Nginx HTTPS:7443"
    "Grafana:3000"
    "Jaeger UI:16686"
    "Prometheus:9090"
    "Alertmanager:9093"
    "Loki:3100"
    "Grafana Alloy:12345"
    "Pub/Sub emulator:8085"
    "PostgreSQL Ledger:15432"
    "PostgreSQL Balance:15433"
    "Jaeger OTLP gRPC:4317"
    "Jaeger OTLP HTTP:4318"
  )

  for item in "${required_ports[@]}"; do
    name="${item%:*}"
    port="${item##*:}"
    if ! port_is_open "$port"; then
      continue
    fi

    owner="$(port_owner_container "$port" || true)"
    if [[ "$owner" == "$PROJECT_CONTAINER_PREFIX"* ]]; then
      continue
    fi

    if [[ -z "$owner" ]]; then
      owner="processo local fora do Docker ou container sem publicacao detectavel"
    else
      owner="container Docker '$owner'"
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
  local name
  for name in "${OVERLAY_CONTAINER_NAMES[@]}"; do
    while IFS= read -r row; do
      [[ -n "$row" ]] && overlay_details+=("  - $row")
    done < <(container_exists "$name")
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

  if docker network ls --filter "name=^$PROJECT_NETWORK_NAME$" --format "{{.Name}}" | grep -qx "$PROJECT_NETWORK_NAME"; then
    local network_containers
    network_containers="$(docker network inspect "$PROJECT_NETWORK_NAME" --format "{{json .Containers}}" 2>/dev/null || true)"
    if [[ -n "$network_containers" && "$network_containers" != "null" && "$network_containers" != "{}" ]]; then
      local reason="A rede local $PROJECT_NETWORK_NAME ja existe com containers conectados. Isso pode indicar stack anterior ou estado parcial."
      if confirm_project_cleanup "$reason"; then
        run_non_destructive_project_cleanup
        return
      fi

      echo "Subida interrompida. Libere os recursos da rede $PROJECT_NETWORK_NAME ou execute novamente com --cleanup." >&2
      exit 1
    fi
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
  NO_BUILD=true "$ROOT_DIR/scripts/start-local-stack.sh"
else
  "$ROOT_DIR/scripts/start-local-stack.sh"
fi

nginx_up=(docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_OBSERVABILITY_FILE" -f "$COMPOSE_NGINX_FILE" --profile observability up -d)
if [[ "$NO_BUILD" != "true" ]]; then
  nginx_up+=(--build)
fi
nginx_up+=(ledger-service-1 ledger-service-2 nginx-edge)
"${nginx_up[@]}"

docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_OBSERVABILITY_FILE" -f "$COMPOSE_NGINX_FILE" --profile observability ps

if [[ "$SKIP_HEALTH_CHECKS" != "true" ]]; then
  http_check "LedgerService.Api direta" "http://localhost:5226/health"
  http_check "BalanceService.Api direta" "http://localhost:5228/health"
  http_check "Portal Nginx" "https://localhost:7443/" true
  http_check "Ledger via Nginx" "https://ledger.localhost:7443/health" true
  http_check "Balance via Nginx" "https://balance.localhost:7443/health" true
  http_check "Grafana" "http://localhost:3000/api/health"
  http_check "Jaeger" "http://localhost:16686/"
  http_check "Prometheus" "http://localhost:9090/-/ready"
  http_check "Alertmanager" "http://localhost:9093/-/ready"
fi

cat <<'EOF'

OK. Stack completa local pronta.

URLs uteis:
  LedgerService.Api:     http://localhost:5226/
  BalanceService.Api:    http://localhost:5228/
  Portal Nginx:          https://localhost:7443/
  Ledger Swagger Nginx:  https://ledger.localhost:7443/swagger
  Balance Swagger Nginx: https://balance.localhost:7443/swagger
  Grafana:               http://localhost:3000/
  Jaeger:                http://localhost:16686/
  Prometheus:            http://localhost:9090/
  Alertmanager:          http://localhost:9093/

Este script nao remove volumes, nao executa testes, k6 nem scanners.
EOF
