#!/usr/bin/env bash
set -euo pipefail

# Gera um arquivo .env.k6.auto a partir do compose.yaml
# - Descobre service names e portas internas (container port)
# - Escreve BASE_URL_* para uso dentro da rede do compose (http://<service>:<port>)

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT_DIR/compose.yaml}"
OUT_FILE="${OUT_FILE:-$ROOT_DIR/.env.k6.auto}"

dotnet run --project "$ROOT_DIR/tools/ComposeEnvGen/ComposeEnvGen.csproj" -- \
  --compose "$COMPOSE_FILE" \
  --out "$OUT_FILE"
