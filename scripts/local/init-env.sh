#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
EXAMPLE_FILE="$ROOT_DIR/.env.local.example"
TARGET_FILE="$ROOT_DIR/.env.local"
FORCE="${FORCE:-false}"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/local/init-env.sh [--force]

Cria .env.local a partir de .env.local.example com valores locais descartaveis.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force)
      FORCE=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Parametro invalido: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ ! -f "$EXAMPLE_FILE" ]]; then
  echo "Arquivo .env.local.example nao encontrado." >&2
  exit 1
fi

if [[ -f "$TARGET_FILE" && "$FORCE" != "true" ]]; then
  echo ".env.local ja existe. Use --force para recriar conscientemente." >&2
  exit 1
fi

new_local_secret() {
  local name="$1"
  local normalized
  local suffix

  normalized="$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/_/g')"
  if command -v openssl >/dev/null 2>&1; then
    suffix="$(openssl rand -hex 16)"
  else
    suffix="$(od -An -N16 -tx1 /dev/urandom | tr -d ' \n')"
  fi

  printf 'local_%s_%s' "$normalized" "$suffix"
}

{
  while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$line" =~ ^[[:space:]]*# ]]; then
      printf '%s\n' "$line"
      continue
    fi
    if [[ -z "${line//[[:space:]]/}" || "$line" != *=* ]]; then
      printf '%s\n' "$line"
      continue
    fi

    key="${line%%=*}"
    value="${line#*=}"
    key="$(printf '%s' "$key" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')"
    value="$(printf '%s' "$value" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')"

    if [[ "$value" == "<$key>" ]]; then
      printf '%s=%s\n' "$key" "$(new_local_secret "$key")"
    else
      printf '%s\n' "$line"
    fi
  done < "$EXAMPLE_FILE"
} > "$TARGET_FILE"

echo "Arquivo .env.local criado a partir de .env.local.example."
echo "Revise os valores antes de usar em qualquer ambiente compartilhado."
