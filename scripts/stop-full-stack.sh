#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/compose.yaml"
COMPOSE_OBSERVABILITY_FILE="$ROOT_DIR/compose.observability.yaml"
COMPOSE_NGINX_FILE="$ROOT_DIR/compose.nginx.yaml"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-20}"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/stop-full-stack.sh [--timeout <segundos>]

Para a stack local completa sem remover containers, redes, volumes, bancos,
imagens ou certificados locais.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --timeout)
      if [[ $# -lt 2 ]]; then
        echo "Parametro --timeout exige um valor em segundos." >&2
        exit 2
      fi
      TIMEOUT_SECONDS="$2"
      shift 2
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

docker compose version >/dev/null

cd "$ROOT_DIR"

echo "Parando overlay Nginx da stack completa..."
docker compose \
  -f "$COMPOSE_FILE" \
  -f "$COMPOSE_OBSERVABILITY_FILE" \
  -f "$COMPOSE_NGINX_FILE" \
  --profile observability \
  stop \
  --timeout "$TIMEOUT_SECONDS" \
  nginx-edge \
  ledger-service-1 \
  ledger-service-2

echo "Parando stack base e observabilidade..."
docker compose \
  -f "$COMPOSE_FILE" \
  -f "$COMPOSE_OBSERVABILITY_FILE" \
  --profile observability \
  stop \
  --timeout "$TIMEOUT_SECONDS"

cat <<'EOF'

OK. Stack completa parada.
Volumes, bancos locais, imagens e certificados nao foram removidos.
EOF
