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
  local scenarioName="$1"; shift
  local scriptPath="$1"; shift
  local summaryFile="summary-$MODE-$scenarioName-$ts.json"
  local hostSummary="$ARTIFACTS_DIR/$summaryFile"

  nerdctl compose -f "$COMPOSE_FILE" -f "$COMPOSE_K6_FILE" run --rm \
    -e "TOKEN=$TOKEN" \
    "$@" \
    k6 run "$scriptPath" --summary-export "/artifacts/$summaryFile"

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
    run_k6 ledger_resilience scenarios/ledger_resilience.js -e VUS=1 -e DURATION=10s
    run_k6 balance_daily_50rps scenarios/balance_daily_50rps.js -e RATE=1 -e DURATION=10s -e PREALLOCATED_VUS=2 -e MAX_VUS=10
    ;;
  balance50)
    run_k6 balance_daily_50rps scenarios/balance_daily_50rps.js -e RATE=50 -e DURATION=1m
    ;;
  resilience)
    run_k6 ledger_resilience scenarios/ledger_resilience.js -e VUS=5 -e DURATION=1m
    ;;
  *)
    echo "Modo inválido: $MODE (use smoke|balance50|resilience)" 1>&2
    exit 2
    ;;
esac

echo "OK. Artifacts em: $ARTIFACTS_DIR" 1>&2

# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).
