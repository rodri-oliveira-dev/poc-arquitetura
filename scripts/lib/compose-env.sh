#!/usr/bin/env bash
set -euo pipefail

# Gera .env usado pelo k6 a partir do compose:
# - Descobre service names e portas internas (container port)
# - Escreve BASE_URL_* para uso dentro da rede do compose (http://<service>:<port>)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
. "$SCRIPT_DIR/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
OUT_FILE="${OUT_FILE:-$ROOT_DIR/.env.k6.auto}"

dotnet run --project "$ROOT_DIR/tools/ComposeEnvGen/ComposeEnvGen.csproj" -- \
  --compose "$COMPOSE_FILE" \
  --out "$OUT_FILE"
