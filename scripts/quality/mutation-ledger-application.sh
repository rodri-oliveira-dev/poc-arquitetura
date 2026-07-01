#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_PROJECT_DIR="$REPO_ROOT/tests/ledger/LedgerService.UnitTests"

cd "$REPO_ROOT"
dotnet tool restore

cd "$TEST_PROJECT_DIR"
dotnet stryker
