#!/usr/bin/env bash
set -euo pipefail

# Runner reprodutível:
#  a) gera .env.k6.auto a partir do compose.yaml
#  b) obtém TOKEN conforme README
#  c) roda k6 dentro do compose network via nerdctl compose

MODE="${1:-}"
if [[ -z "$MODE" ]]; then
  echo "Uso: ./scripts/run-loadtests.sh <smoke|balance50|resilience>" 1>&2
  exit 2
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
COMPOSE_K6_FILE="${COMPOSE_K6_FILE:-$ROOT_DIR/compose.k6.yaml}"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env.k6.auto}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$ROOT_DIR/artifacts/k6}"

mkdir -p "$ARTIFACTS_DIR"

# a) gerar env
"$ROOT_DIR/scripts/compose-env.sh" >/dev/null

# b) obter token (por padrão via localhost conforme README)
TOKEN="$($ROOT_DIR/scripts/get-token.sh)"
if [[ -z "$TOKEN" ]]; then
  echo "Falha ao obter TOKEN. Você pode informar manualmente via env TOKEN=..." 1>&2
  exit 1
fi

ts="$(date +%Y%m%d-%H%M%S)"

run_k6() {
  local scriptPath="$1"; shift
  nerdctl compose -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" run --rm \
    -e "TOKEN=$TOKEN" \
    "$@" \
    k6 run "$scriptPath" --summary-export "/artifacts/summary-$MODE-$ts.json"
}

case "$MODE" in
  smoke)
    run_k6 scenarios/ledger_resilience.js -e VUS=1 -e DURATION=10s
    run_k6 scenarios/balance_daily_50rps.js -e RATE=1 -e DURATION=10s -e PREALLOCATED_VUS=2 -e MAX_VUS=10
    ;;
  balance50)
    run_k6 scenarios/balance_daily_50rps.js -e RATE=50 -e DURATION=1m
    ;;
  resilience)
    run_k6 scenarios/ledger_resilience.js -e VUS=5 -e DURATION=1m
    ;;
  *)
    echo "Modo inválido: $MODE (use smoke|balance50|resilience)" 1>&2
    exit 2
    ;;
esac

echo "OK. Artifacts em: $ARTIFACTS_DIR" 1>&2

# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).
