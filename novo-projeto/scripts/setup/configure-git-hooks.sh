#!/usr/bin/env bash
set -euo pipefail

EXPECTED_HOOKS_PATH=".githooks"
REQUIRED_HOOKS="commit-msg post-merge pre-push"
FORCE=false
CHECK=false

usage() {
  cat <<'USAGE'
Uso: scripts/setup/configure-git-hooks.sh [--check] [--force]

Configura core.hooksPath localmente para .githooks.

Opções:
  --check   Valida sem alterar.
  --force   Substitui um core.hooksPath local existente.
  -h, --help
USAGE
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --check) CHECK=true ;;
    --force) FORCE=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Opção desconhecida: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Erro: o diretório atual não está dentro de um repositório Git." >&2
  exit 1
fi

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOKS_DIR="$REPO_ROOT/$EXPECTED_HOOKS_PATH"

validate_hooks() {
  local allow_chmod="$1"
  local has_error=0

  if [ ! -d "$HOOKS_DIR" ]; then
    echo "Erro: diretório $EXPECTED_HOOKS_PATH não encontrado." >&2
    return 1
  fi

  for hook in $REQUIRED_HOOKS; do
    local hook_path="$HOOKS_DIR/$hook"
    if [ ! -f "$hook_path" ]; then
      echo "Erro: hook obrigatório ausente: $EXPECTED_HOOKS_PATH/$hook." >&2
      has_error=1
      continue
    fi

    case "$(uname -s 2>/dev/null || printf unknown)" in
      Linux|Darwin|FreeBSD|OpenBSD|NetBSD)
        if [ ! -x "$hook_path" ]; then
          if [ "$allow_chmod" = true ]; then
            chmod +x "$hook_path"
          else
            echo "Erro: hook sem permissão de execução: $EXPECTED_HOOKS_PATH/$hook." >&2
            has_error=1
          fi
        fi
        ;;
    esac
  done

  return "$has_error"
}

CURRENT_HOOKS_PATH="$(git -C "$REPO_ROOT" config --local --get core.hooksPath || true)"

if [ "$CHECK" = true ]; then
  status=0
  [ "$CURRENT_HOOKS_PATH" = "$EXPECTED_HOOKS_PATH" ] || status=1
  validate_hooks false || status=1
  if [ "$status" -eq 0 ]; then
    echo "OK: hooks configurados em $EXPECTED_HOOKS_PATH."
  else
    echo "Erro: hooks não estão configurados corretamente." >&2
  fi
  exit "$status"
fi

validate_hooks true

if [ -n "$CURRENT_HOOKS_PATH" ] && [ "$CURRENT_HOOKS_PATH" != "$EXPECTED_HOOKS_PATH" ] && [ "$FORCE" != true ]; then
  echo "Erro: core.hooksPath já está configurado como '$CURRENT_HOOKS_PATH'. Use --force somente após revisar esse valor." >&2
  exit 1
fi

git -C "$REPO_ROOT" config --local core.hooksPath "$EXPECTED_HOOKS_PATH"
echo "Git hooks configurados: core.hooksPath=$EXPECTED_HOOKS_PATH"
