#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/compose.yaml"
COMPOSE_OBSERVABILITY_FILE="$ROOT_DIR/compose.observability.yaml"
COMPOSE_KAFKA_FILE="$ROOT_DIR/compose.kafka.yaml"
COMPOSE_NGINX_FILE="$ROOT_DIR/compose.nginx.yaml"
COMPOSE_K6_FILE="$ROOT_DIR/compose.k6.yaml"
COMPOSE_AUTH_LEGACY_FILE="$ROOT_DIR/compose.auth-legacy.yaml"
COMPOSE_SONAR_FILE="$ROOT_DIR/compose.sonar.yaml"
YES="${YES:-false}"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/docker-clean-safe.sh [--yes]

Executa limpeza nao destrutiva:
  - docker compose down --remove-orphans, sem -v
  - docker builder prune
  - docker image prune

Volumes Docker nao sao removidos.
EOF
}

confirm() {
  local message="$1"
  if [[ "$YES" == "true" ]]; then
    return 0
  fi

  printf '%s [s/N] ' "$message"
  read -r answer
  [[ "$answer" =~ ^([sS]|[sS][iI][mM]|[yY]|[yY][eE][sS])$ ]]
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --yes|-y)
      YES=true
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

if ! command -v docker >/dev/null 2>&1; then
  echo "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'." >&2
  exit 1
fi

cd "$ROOT_DIR"

echo "Parando/removendo containers e redes do projeto sem remover volumes..."
docker compose \
  -f "$COMPOSE_FILE" \
  -f "$COMPOSE_OBSERVABILITY_FILE" \
  -f "$COMPOSE_KAFKA_FILE" \
  -f "$COMPOSE_NGINX_FILE" \
  -f "$COMPOSE_K6_FILE" \
  -f "$COMPOSE_AUTH_LEGACY_FILE" \
  --profile observability \
  --profile direct-ledger \
  --profile k6 \
  --profile legacy-auth \
  --profile legacy-kafka \
  down \
  --remove-orphans

docker compose \
  -f "$COMPOSE_SONAR_FILE" \
  --profile quality \
  down \
  --remove-orphans

if confirm "Executar docker builder prune para remover cache de build nao usado?"; then
  docker builder prune --force
else
  echo "docker builder prune ignorado."
fi

if confirm "Executar docker image prune para remover imagens dangling?"; then
  docker image prune --force
else
  echo "docker image prune ignorado."
fi

echo "OK. Limpeza segura concluida. Volumes Docker foram preservados."
