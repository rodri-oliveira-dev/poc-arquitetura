#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PROJECT_KEY="poc-arquitetura"
PROJECT_NAME="poc-arquitetura"
SOLUTION_PATH="$REPO_ROOT/LedgerService.slnx"
RUNSETTINGS_PATH="$REPO_ROOT/coverlet.runsettings"
BUILD_CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
SONAR_HOST_URL="${SONAR_HOST_URL:-http://localhost:9000}"
DEFAULT_TEST_RESULTS_DIR="$REPO_ROOT/artifacts/test-results"
TEST_RESULTS_DIR="${TEST_RESULTS_DIR:-$DEFAULT_TEST_RESULTS_DIR}"
SONAR_PLACEHOLDER_SECRET_LINE_REGEX='.*(PASSWORD|[Pp]assword|PWD|PGPASSWORD|CLIENT_SECRET|[Cc]lient[_-]?[Ss]ecret|SECRET|[Ss]ecret|TOKEN|[Tt]oken|API[_-]?KEY|[Aa]pi[_-]?[Kk]ey).*<[A-Z0-9_]*(PASSWORD|SECRET|TOKEN|API_KEY)[A-Z0-9_]*>.*'

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "SONAR_TOKEN nao esta definido. Execute com SONAR_TOKEN=<token> bash scripts/quality/sonar-analyze.sh" >&2
  exit 1
fi

cd "$REPO_ROOT"

if [[ "$TEST_RESULTS_DIR" != /* ]]; then
  TEST_RESULTS_DIR="$REPO_ROOT/$TEST_RESULTS_DIR"
fi

mkdir -p "$DEFAULT_TEST_RESULTS_DIR" "$TEST_RESULTS_DIR"
DEFAULT_TEST_RESULTS_DIR="$(cd "$DEFAULT_TEST_RESULTS_DIR" && pwd)"
TEST_RESULTS_DIR="$(cd "$TEST_RESULTS_DIR" && pwd)"

case "$TEST_RESULTS_DIR" in
  "$DEFAULT_TEST_RESULTS_DIR"|"$DEFAULT_TEST_RESULTS_DIR"/*)
    rm -rf "$TEST_RESULTS_DIR"
    mkdir -p "$TEST_RESULTS_DIR"
    ;;
  *)
    echo "TEST_RESULTS_DIR esta fora de artifacts/test-results; resultados existentes serao reutilizados sem limpeza." >&2
    ;;
esac

echo "==> Restoring local dotnet tools"
dotnet tool restore

echo "==> Starting SonarQube analysis for $PROJECT_KEY at $SONAR_HOST_URL"
dotnet sonarscanner begin \
  /k:"$PROJECT_KEY" \
  /n:"$PROJECT_NAME" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.issue.ignore.block=placeholderSecrets \
  /d:sonar.issue.ignore.block.placeholderSecrets.beginBlockRegexp="$SONAR_PLACEHOLDER_SECRET_LINE_REGEX" \
  /d:sonar.issue.ignore.block.placeholderSecrets.endBlockRegexp="$SONAR_PLACEHOLDER_SECRET_LINE_REGEX" \
  /d:sonar.cs.opencover.reportsPaths="$TEST_RESULTS_DIR/**/coverage.opencover.xml"

echo "==> Building $SOLUTION_PATH with configuration $BUILD_CONFIGURATION"
dotnet build "$SOLUTION_PATH" \
  --configuration "$BUILD_CONFIGURATION" \
  --no-incremental

echo "==> Running tests with OpenCover coverage"
dotnet test "$SOLUTION_PATH" \
  --configuration "$BUILD_CONFIGURATION" \
  --no-build \
  --collect:"XPlat Code Coverage" \
  --settings "$RUNSETTINGS_PATH" \
  --results-directory "$TEST_RESULTS_DIR"

echo "==> Finishing SonarQube analysis"
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
