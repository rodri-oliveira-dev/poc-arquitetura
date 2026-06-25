#!/usr/bin/env bash
set -euo pipefail

PROJECT_NAME="${COMPOSE_PROJECT_NAME:-poc-arquitetura}"

section() {
  printf '\n== %s ==\n' "$1"
}

run_if_available() {
  local description="$1"
  shift

  section "$description"
  if "$@"; then
    return 0
  fi

  echo "Comando indisponivel ou falhou: $*" >&2
}

if ! command -v docker >/dev/null 2>&1; then
  echo "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'." >&2
  exit 1
fi

section "Docker system df"
docker system df

run_if_available "Docker builder du" docker builder du

section "Volumes do projeto $PROJECT_NAME"
docker volume ls \
  --filter "label=com.docker.compose.project=$PROJECT_NAME" \
  --format "table {{.Name}}\t{{.Driver}}\t{{.Scope}}"

section "Containers do projeto $PROJECT_NAME"
docker ps -a \
  --filter "label=com.docker.compose.project=$PROJECT_NAME" \
  --format "table {{.Names}}\t{{.Status}}\t{{.Image}}\t{{.Size}}"

section "Imagens relacionadas ao projeto $PROJECT_NAME"
docker images \
  --filter "reference=*${PROJECT_NAME}*" \
  --format "table {{.Repository}}\t{{.Tag}}\t{{.ID}}\t{{.Size}}"

section "Imagens com label Compose do projeto $PROJECT_NAME"
docker images \
  --filter "label=com.docker.compose.project=$PROJECT_NAME" \
  --format "table {{.Repository}}\t{{.Tag}}\t{{.ID}}\t{{.Size}}"
