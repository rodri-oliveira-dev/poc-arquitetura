#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
THRESHOLD="${2:-80}"

echo "==> Running solution tests with coverage gate (line >= ${THRESHOLD}%)"

RESULTS_DIR="$(pwd)/TestResults"
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

SUMMARY_JSON="$REPORT_DIR/Summary.json"
if [[ ! -f "$SUMMARY_JSON" ]]; then
  echo "Summary.json não encontrado em $SUMMARY_JSON" >&2
  exit 1
fi

LINE_COVERAGE=$(python - <<'PY'
import json
with open(r"'"$SUMMARY_JSON"'", "r", encoding="utf-8") as f:
    data = json.load(f)
print(data["summary"]["linecoverage"])
PY
)

echo "==> Global line coverage: ${LINE_COVERAGE}%"

python - <<PY
lc=float("$LINE_COVERAGE")
thr=float("$THRESHOLD")
import sys
sys.exit(0 if lc >= thr else 1)
PY

echo "==> Done"
