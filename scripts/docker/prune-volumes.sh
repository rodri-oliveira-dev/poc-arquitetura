#!/usr/bin/env bash
set -euo pipefail

APPLY=false
RETENTION_DAYS=7

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/docker/prune-volumes.sh [--apply] [--retention-days N]

Remove somente volumes Docker com label auto-prune=true mais antigos que a
retencao informada. O modo padrao e dry-run.

Labels esperadas:
  auto-prune=true
  retention=7d
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --apply)
      APPLY=true
      shift
      ;;
    --retention-days)
      if [[ $# -lt 2 || ! "$2" =~ ^[0-9]+$ || "$2" -lt 1 ]]; then
        echo "--retention-days exige um inteiro positivo." >&2
        exit 2
      fi
      RETENTION_DAYS="$2"
      shift 2
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

if ! command -v docker >/dev/null 2>&1; then
  echo "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'." >&2
  exit 1
fi

epoch_from_created_at() {
  local value="$1"

  if date -d "$value" +%s >/dev/null 2>&1; then
    date -d "$value" +%s
    return 0
  fi

  local value_without_fraction="${value%%.*}"
  if [[ "$value" == *Z ]]; then
    local normalized="${value_without_fraction}Z"
    if date -j -u -f "%Y-%m-%dT%H:%M:%SZ" "$normalized" +%s >/dev/null 2>&1; then
      date -j -u -f "%Y-%m-%dT%H:%M:%SZ" "$normalized" +%s
      return 0
    fi
  fi

  local offset="${value: -6}"
  local offset_without_colon="${offset/:/}"
  local normalized="${value_without_fraction}${offset_without_colon}"
  if date -j -f "%Y-%m-%dT%H:%M:%S%z" "$normalized" +%s >/dev/null 2>&1; then
    date -j -f "%Y-%m-%dT%H:%M:%S%z" "$normalized" +%s
    return 0
  fi

  return 1
}

now="$(date +%s)"
threshold_seconds=$((RETENTION_DAYS * 24 * 60 * 60))
volume_count="$(docker volume ls --filter "label=auto-prune=true" --format "{{.Name}}" | sed '/^[[:space:]]*$/d' | wc -l | tr -d ' ')"

if [[ "$volume_count" -eq 0 ]]; then
  echo "Nenhum volume com label auto-prune=true encontrado."
  exit 0
fi

echo "Modo: $([[ "$APPLY" == "true" ]] && echo "apply" || echo "dry-run")"
echo "Retencao: ${RETENTION_DAYS}d"

while IFS= read -r volume; do
  [[ -n "$volume" ]] || continue

  created_at="$(docker volume inspect "$volume" --format "{{.CreatedAt}}")"
  retention_label="$(docker volume inspect "$volume" --format '{{ index .Labels "retention" }}')"

  if [[ "$retention_label" != "${RETENTION_DAYS}d" ]]; then
    echo "SKIP $volume: retention=$retention_label nao corresponde a ${RETENTION_DAYS}d."
    continue
  fi

  if ! created_epoch="$(epoch_from_created_at "$created_at")"; then
    echo "SKIP $volume: nao foi possivel interpretar CreatedAt=$created_at." >&2
    continue
  fi

  age_seconds=$((now - created_epoch))
  age_days=$((age_seconds / 86400))

  if [[ "$age_seconds" -lt "$threshold_seconds" ]]; then
    echo "SKIP $volume: idade ${age_days}d menor que ${RETENTION_DAYS}d."
    continue
  fi

  if docker ps -a --filter "volume=$volume" --format "{{.ID}}" | grep -q .; then
    echo "SKIP $volume: volume ainda associado a container existente."
    continue
  fi

  if [[ "$APPLY" == "true" ]]; then
    echo "REMOVE $volume: idade ${age_days}d."
    docker volume rm "$volume"
  else
    echo "DRY-RUN removeria $volume: idade ${age_days}d."
  fi
done < <(docker volume ls --filter "label=auto-prune=true" --format "{{.Name}}")
