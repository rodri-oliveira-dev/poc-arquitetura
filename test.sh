#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
THRESHOLD="${2:-85}"

echo "==> Running solution tests with coverage gate (line >= ${THRESHOLD}%)"

if command -v git >/dev/null 2>&1; then
  REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
else
  REPO_ROOT="$(pwd)"
fi

cd "$REPO_ROOT"

RESULTS_DIR="$REPO_ROOT/TestResults"
rm -rf "$RESULTS_DIR"
mkdir -p "$RESULTS_DIR"

dotnet test ./LedgerService.slnx -c "$CONFIGURATION" \
  --collect:"XPlat Code Coverage" \
  --settings ./coverlet.runsettings \
  --results-directory "$RESULTS_DIR"

dotnet tool restore >/dev/null

REPORT_DIR="$RESULTS_DIR/coverage-report"
dotnet tool run reportgenerator \
  -reports:"$RESULTS_DIR/**/coverage.cobertura.xml" \
  -targetdir:"$REPORT_DIR" \
  -reporttypes:"JsonSummary;TextSummary" >/dev/null

SUMMARY_TXT="$REPORT_DIR/Summary.txt"
if [[ ! -f "$SUMMARY_TXT" ]]; then
  echo "Summary.txt nao encontrado em $SUMMARY_TXT" >&2
  exit 1
fi

LINE_COVERAGE=$(awk -F'[:%]' '/^[[:space:]]*Line coverage:/ { gsub(/^[[:space:]]+|[[:space:]]+$/, "", $2); print $2; exit }' "$SUMMARY_TXT")
if [[ -z "$LINE_COVERAGE" ]]; then
  echo "Nao foi possivel ler a cobertura de linhas em $SUMMARY_TXT" >&2
  exit 1
fi

echo "==> Global line coverage: ${LINE_COVERAGE}%"

awk -v coverage="$LINE_COVERAGE" -v threshold="$THRESHOLD" 'BEGIN { exit (coverage + 0 >= threshold + 0 ? 0 : 1) }'

assert_assembly_coverage() {
  assembly_name="$1"

  assembly_coverage="$(awk -v assembly="$assembly_name" '$1 == assembly { gsub(/%/, "", $NF); print $NF; exit }' "$SUMMARY_TXT")"
  if [[ -z "$assembly_coverage" ]]; then
    echo "Nao foi possivel encontrar a cobertura de $assembly_name em $SUMMARY_TXT" >&2
    exit 1
  fi

  echo "==> ${assembly_name} line coverage: ${assembly_coverage}%"

  awk -v coverage="$assembly_coverage" -v threshold="$THRESHOLD" 'BEGIN { exit (coverage + 0 >= threshold + 0 ? 0 : 1) }' || {
    echo "Coverage de $assembly_name abaixo do threshold. Atual=${assembly_coverage}% Threshold=${THRESHOLD}%" >&2
    exit 1
  }
}

assert_assembly_coverage "LedgerService.Worker"
assert_assembly_coverage "BalanceService.Worker"

echo "==> Done"
