#!/usr/bin/env bash
set -euo pipefail

SCRIPT_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

is_repo_root() {
  local candidate="$1"

  [[ -f "$candidate/Directory.Build.props" &&
    -f "$candidate/Directory.Packages.props" &&
    -d "$candidate/scripts" ]]
}

resolve_repo_root() {
  local start_dir="${1:-$SCRIPT_LIB_DIR}"
  local root=""

  if command -v git >/dev/null 2>&1; then
    root="$(git -C "$start_dir" rev-parse --show-toplevel 2>/dev/null || true)"
    if [[ -n "$root" ]]; then
      printf '%s' "$root"
      return 0
    fi
  fi

  local current
  current="$(cd "$start_dir" && pwd)"
  while [[ "$current" != "/" ]]; do
    if is_repo_root "$current"; then
      printf '%s' "$current"
      return 0
    fi
    current="$(dirname "$current")"
  done

  echo "Nao foi possivel resolver a raiz do repositorio a partir de $start_dir." >&2
  return 1
}

find_scripts_lib_dir() {
  local start_dir="$1"
  local current
  current="$(cd "$start_dir" && pwd)"

  while [[ "$current" != "/" ]]; do
    if [[ -f "$current/scripts/lib/common.sh" ]]; then
      printf '%s' "$current/scripts/lib"
      return 0
    fi
    if [[ -f "$current/lib/common.sh" ]]; then
      printf '%s' "$current/lib"
      return 0
    fi
    current="$(dirname "$current")"
  done

  echo "Nao foi possivel localizar scripts/lib a partir de $start_dir." >&2
  return 1
}

read_env_value() {
  local file="$1"
  local key="$2"

  if [[ ! -f "$file" ]]; then
    return 0
  fi

  sed -nE "s/^[[:space:]]*${key}[[:space:]]*=[[:space:]]*(.*)[[:space:]]*$/\1/p" "$file" |
    tail -n 1 |
    sed -E "s/^[[:space:]]+//; s/[[:space:]]+$//; s/^['\"]//; s/['\"]$//"
}

get_local_env_value() {
  local name="$1"
  local repo_root="${ROOT_DIR:?ROOT_DIR nao definido}"
  local env_file
  local value

  for env_file in "$repo_root/.env.local" "$repo_root/.env"; do
    value="$(read_env_value "$env_file" "$name")"
    if [[ -n "$value" ]]; then
      printf '%s' "$value"
      return 0
    fi
  done
}

get_local_config_value() {
  local name="$1"
  local default_value="${2:-}"
  local value="${!name:-}"

  if [[ -z "$value" ]]; then
    value="$(get_local_env_value "$name")"
  fi
  if [[ -z "$value" ]]; then
    value="$default_value"
  fi

  printf '%s' "$value"
}

get_required_local_config_value() {
  local name="$1"
  local value
  value="$(get_local_config_value "$name" "")"

  if [[ -z "$value" ]]; then
    echo "Defina $name no ambiente, em .env.local ou em .env." >&2
    return 1
  fi

  printf '%s' "$value"
}

get_compose_env_file() {
  local repo_root="${ROOT_DIR:?ROOT_DIR nao definido}"
  local env_file

  for env_file in "$repo_root/.env.local" "$repo_root/.env"; do
    if [[ -f "$env_file" ]]; then
      printf '%s' "$env_file"
      return 0
    fi
  done
}

print_compose_env_args() {
  local env_file
  env_file="$(get_compose_env_file)"
  if [[ -n "$env_file" ]]; then
    printf '%s\n' "--env-file"
    printf '%s\n' "$env_file"
  fi
}

require_command() {
  local command_name="$1"
  local message="${2:-Comando obrigatorio nao encontrado: $command_name}"

  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "$message" >&2
    return 1
  fi
}

fail() {
  echo "$1" >&2
  exit "${2:-1}"
}

require_option_value() {
  local option_name="$1"
  local value="${2:-}"

  if [[ -z "$value" || "$value" == -* ]]; then
    echo "Opcao $option_name exige um valor." >&2
    return 2
  fi
}
