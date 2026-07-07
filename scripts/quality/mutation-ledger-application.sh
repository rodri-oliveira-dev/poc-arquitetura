#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/../lib"
# shellcheck source=../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"
REPO_ROOT="$(resolve_repo_root "$SCRIPT_DIR")"
TEST_PROJECT_DIR="$REPO_ROOT/tests/ledger/LedgerService.UnitTests"

cd "$REPO_ROOT"
dotnet tool restore

cd "$TEST_PROJECT_DIR"
dotnet stryker
