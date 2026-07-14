#!/usr/bin/env bash
set -euo pipefail

EXPECTED_HOOKS_PATH=".githooks"
REQUIRED_HOOKS="commit-msg post-merge pre-push"
FORCE=false
CHECK=false

usage() {
  cat <<'USAGE'
Uso: scripts/setup/configure-git-hooks.sh [--check] [--force]

Configura core.hooksPath localmente para .githooks de forma explicita e segura.

Opcoes:
  --check   Valida a configuracao e os arquivos de hook sem alterar nada.
  --force   Substitui um core.hooksPath local existente por .githooks.
  -h, --help
USAGE
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --check)
      CHECK=true
      ;;
    --force)
      FORCE=true
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Opcao desconhecida: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Erro: o diretorio atual nao esta dentro de um repositorio Git." >&2
  exit 1
fi

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOKS_DIR="$REPO_ROOT/$EXPECTED_HOOKS_PATH"

is_unix_runtime() {
  case "$(uname -s 2>/dev/null || printf unknown)" in
    Linux|Darwin|FreeBSD|OpenBSD|NetBSD)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

validate_hooks() {
  local allow_chmod="$1"
  local has_error=0

  if [ ! -d "$HOOKS_DIR" ]; then
    echo "Erro: diretorio $EXPECTED_HOOKS_PATH nao encontrado em $REPO_ROOT." >&2
    return 1
  fi

  for hook in $REQUIRED_HOOKS; do
    local hook_path="$HOOKS_DIR/$hook"

    if [ ! -f "$hook_path" ]; then
      echo "Erro: hook obrigatorio ausente: $EXPECTED_HOOKS_PATH/$hook." >&2
      has_error=1
      continue
    fi

    if is_unix_runtime && [ ! -x "$hook_path" ]; then
      if [ "$allow_chmod" = "true" ]; then
        chmod +x "$hook_path"
        echo "Permissao executavel aplicada: $EXPECTED_HOOKS_PATH/$hook."
      else
        echo "Erro: hook sem bit executavel em Unix: $EXPECTED_HOOKS_PATH/$hook." >&2
        echo "Execute explicitamente: chmod +x $EXPECTED_HOOKS_PATH/$hook" >&2
        has_error=1
      fi
    fi
  done

  return "$has_error"
}

CURRENT_HOOKS_PATH="$(git -C "$REPO_ROOT" config --local --get core.hooksPath || true)"

if [ "$CHECK" = "true" ]; then
  status=0

  if [ "$CURRENT_HOOKS_PATH" = "$EXPECTED_HOOKS_PATH" ]; then
    echo "OK: core.hooksPath local esta configurado como $EXPECTED_HOOKS_PATH."
  elif [ -z "$CURRENT_HOOKS_PATH" ]; then
    echo "Erro: core.hooksPath local nao esta configurado." >&2
    status=1
  else
    echo "Erro: core.hooksPath local esta configurado como '$CURRENT_HOOKS_PATH', nao '$EXPECTED_HOOKS_PATH'." >&2
    status=1
  fi

  if ! validate_hooks false; then
    status=1
  fi

  exit "$status"
fi

validate_hooks true

if [ -z "$CURRENT_HOOKS_PATH" ]; then
  git -C "$REPO_ROOT" config --local core.hooksPath "$EXPECTED_HOOKS_PATH"
  echo "Git hooks locais configurados com sucesso: core.hooksPath=$EXPECTED_HOOKS_PATH."
  exit 0
fi

if [ "$CURRENT_HOOKS_PATH" = "$EXPECTED_HOOKS_PATH" ]; then
  echo "Git hooks locais ja estao configurados: core.hooksPath=$EXPECTED_HOOKS_PATH."
  exit 0
fi

if [ "$FORCE" != "true" ]; then
  cat >&2 <<EOF
Erro: core.hooksPath local ja esta configurado como '$CURRENT_HOOKS_PATH'.
Sobrescrever esse valor pode desativar hooks pessoais, corporativos ou de outras ferramentas.
Revise a configuracao atual e execute novamente com --force apenas se quiser substitui-la por '$EXPECTED_HOOKS_PATH'.
EOF
  exit 1
fi

git -C "$REPO_ROOT" config --local core.hooksPath "$EXPECTED_HOOKS_PATH"
echo "core.hooksPath local substituido com --force."
echo "Valor anterior: $CURRENT_HOOKS_PATH"
echo "Novo valor: $EXPECTED_HOOKS_PATH"
