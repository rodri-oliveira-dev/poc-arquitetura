#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/compose.yaml"
COMPOSE_NGINX_FILE="$ROOT_DIR/compose.nginx.yaml"
CERT_FILE="$ROOT_DIR/infra/nginx/certs/localhost.crt"
KEY_FILE="$ROOT_DIR/infra/nginx/certs/localhost.key"
NO_BUILD="${NO_BUILD:-false}"
SKIP_HEALTH_CHECKS="${SKIP_HEALTH_CHECKS:-false}"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/start-full-stack.sh [--no-build] [--skip-health-checks]

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

cd "$ROOT_DIR"

export OTEL_ENABLED="${OTEL_ENABLED:-true}"
export OBSERVABILITY=true
if [[ "$NO_BUILD" == "true" ]]; then
  NO_BUILD=true "$ROOT_DIR/scripts/start-local-stack.sh"
else
  "$ROOT_DIR/scripts/start-local-stack.sh"
fi

nginx_up=(docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_NGINX_FILE" --profile observability up -d)
if [[ "$NO_BUILD" != "true" ]]; then
  nginx_up+=(--build)
fi
nginx_up+=(ledger-service-1 ledger-service-2 nginx-edge)
"${nginx_up[@]}"

docker compose -f "$COMPOSE_FILE" -f "$COMPOSE_NGINX_FILE" --profile observability ps

if [[ "$SKIP_HEALTH_CHECKS" != "true" ]]; then
  http_check "Auth.Api direta" "http://localhost:5030/health"
  http_check "LedgerService.Api direta" "http://localhost:5226/health"
  http_check "BalanceService.Api direta" "http://localhost:5228/health"
  http_check "Portal Nginx" "https://localhost:7443/" true
  http_check "Ledger via Nginx" "https://ledger.localhost:7443/health" true
  http_check "Balance via Nginx" "https://balance.localhost:7443/health" true
  http_check "Auth via Nginx" "https://auth.localhost:7443/health" true
  http_check "Grafana" "http://localhost:3000/api/health"
  http_check "Jaeger" "http://localhost:16686/"
  http_check "Prometheus" "http://localhost:9090/-/ready"
  http_check "Alertmanager" "http://localhost:9093/-/ready"
fi

cat <<'EOF'

OK. Stack completa local pronta.

URLs uteis:
  Auth.Api:              http://localhost:5030/
  LedgerService.Api:     http://localhost:5226/
  BalanceService.Api:    http://localhost:5228/
  Portal Nginx:          https://localhost:7443/
  Ledger Swagger Nginx:  https://ledger.localhost:7443/swagger
  Balance Swagger Nginx: https://balance.localhost:7443/swagger
  Auth Swagger Nginx:    https://auth.localhost:7443/swagger
  Grafana:               http://localhost:3000/
  Jaeger:                http://localhost:16686/
  Prometheus:            http://localhost:9090/
  Alertmanager:          http://localhost:9093/

Este script nao remove volumes, nao executa testes, k6 nem scanners.
EOF
