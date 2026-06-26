#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/lib"
if [[ ! -f "$SCRIPTS_LIB_DIR/common.sh" ]]; then
  SCRIPTS_LIB_DIR="$SCRIPT_DIR/../../lib"
fi
# shellcheck source=../../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"
BASE_OPENAPI_DIR="${BASE_OPENAPI_DIR:-$ROOT_DIR/.openapi-main}"
CURRENT_OPENAPI_DIR="${CURRENT_OPENAPI_DIR:-$ROOT_DIR/docs/openapi}"
OASDIFF_FORMAT="${OASDIFF_FORMAT:-githubactions}"
OASDIFF_FAIL_ON="${OASDIFF_FAIL_ON:-ERR}"

contracts=(
  "ledger.v1.json"
  "balance.v1.json"
  "transfer.v1.json"
  "identity.v1.json"
)

if ! command -v oasdiff >/dev/null 2>&1; then
  echo "oasdiff nao encontrado no PATH." >&2
  echo "Instale o oasdiff ou execute pelo workflow openapi-contracts." >&2
  exit 127
fi

for contract in "${contracts[@]}"; do
  base_contract="$BASE_OPENAPI_DIR/$contract"
  current_contract="$CURRENT_OPENAPI_DIR/$contract"

  if [[ ! -f "$base_contract" ]]; then
    echo "Contrato base nao encontrado: $base_contract" >&2
    exit 1
  fi

  if [[ ! -f "$current_contract" ]]; then
    echo "Contrato atual nao encontrado: $current_contract" >&2
    exit 1
  fi

  echo "Comparando breaking changes OpenAPI: $contract"
  oasdiff breaking \
    --fail-on "$OASDIFF_FAIL_ON" \
    --format "$OASDIFF_FORMAT" \
    "$base_contract" \
    "$current_contract"
done
