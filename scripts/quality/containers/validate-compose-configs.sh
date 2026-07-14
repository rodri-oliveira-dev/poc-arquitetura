#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/common.sh
. "$SCRIPT_DIR/../../lib/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"
ENV_FILE="${COMPOSE_ENV_FILE:-$ROOT_DIR/.env.local.example}"

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/quality/containers/validate-compose-configs.sh [--env-file <arquivo>]

Valida as combinacoes Docker Compose oficialmente suportadas com
`docker compose config --quiet`.

Nao sobe containers, nao faz pull/push de imagens e nao usa secrets reais.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env-file)
      require_option_value "$1" "${2:-}"
      ENV_FILE="$2"
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

require_command docker "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'."
docker compose version >/dev/null

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Arquivo de ambiente nao encontrado: $ENV_FILE" >&2
  exit 2
fi

cd "$ROOT_DIR"

# Placeholders nao secretos usados apenas para satisfazer interpolacoes obrigatorias
# durante `docker compose config`. Eles nao inicializam containers nem conectam em GCP.
export CLOUDSQL_INSTANCE_CONNECTION_NAME="${CLOUDSQL_INSTANCE_CONNECTION_NAME:-local-project:local-region:local-instance}"
export GOOGLE_APPLICATION_CREDENTIALS="${GOOGLE_APPLICATION_CREDENTIALS:-./secrets/cloudsql/application_default_credentials.json}"

K6_AUTO_ENV_FILE="$ROOT_DIR/.env.k6.auto"
REMOVE_K6_AUTO_ENV_FILE=false
if [[ ! -f "$K6_AUTO_ENV_FILE" ]]; then
  printf '# Placeholder temporario para docker compose config.\n' > "$K6_AUTO_ENV_FILE"
  REMOVE_K6_AUTO_ENV_FILE=true
fi

cleanup() {
  if [[ "$REMOVE_K6_AUTO_ENV_FILE" == "true" ]]; then
    rm -f "$K6_AUTO_ENV_FILE"
  fi
}
trap cleanup EXIT

# Matriz oficial de validacao:
# - suportadas: compose.yaml base, overlays opcionais aplicados sobre a base e
#   stack completa local com observabilidade + Nginx;
# - alternativas: compose.kafka.yaml e apenas alias compativel do default Kafka;
#   compose.pubsub.yaml seleciona o caminho explicito/legado de Pub/Sub;
# - incompatibilidades deliberadas: Kafka explicito nao e combinado com Pub/Sub;
#   Pub/Sub nao e combinado com k6, porque os runners de carga versionados usam Kafka;
#   Cloud SQL nao e combinado com Pub/Sub/k6/Sonar por ser smoke manual/local isolado.
COMBINATIONS=(
  "stack-base|compose.yaml||Core funcional local com Kafka padrao"
  "stack-base-kafka-alias|compose.yaml,compose.kafka.yaml||Alias compativel; Kafka ja esta no Compose base"
  "stack-observability|compose.yaml,compose.observability.yaml|observability|Core funcional com observabilidade local"
  "stack-nginx|compose.yaml,compose.nginx.yaml||Borda local Nginx com duas instancias do Ledger"
  "stack-full-nginx-observability|compose.yaml,compose.observability.yaml,compose.nginx.yaml|observability|Stack completa local usada por scripts/local/start-full-stack.sh"
  "stack-k6|compose.yaml,compose.k6.yaml|k6|Overlay k6 padrao para cenarios de carga Kafka"
  "stack-kafka-k6|compose.yaml,compose.kafka.yaml,compose.k6.yaml|k6|Caminho k6 full-stack que aplica o alias Kafka explicito"
  "stack-cloudsql|compose.yaml,compose.cloudsql.yaml||Smoke manual/local com Cloud SQL Auth Proxy"
  "stack-sonar|compose.yaml,compose.sonar.yaml|quality|SonarQube local junto da rede Compose do projeto"
  "stack-pubsub-legacy|compose.yaml,compose.pubsub.yaml|legacy-pubsub|Provider alternativo/legado Pub/Sub"
)

for combination in "${COMBINATIONS[@]}"; do
  IFS='|' read -r name files profiles description <<<"$combination"

  args=(--env-file "$ENV_FILE")
  IFS=',' read -ra compose_files <<<"$files"
  for compose_file in "${compose_files[@]}"; do
    args+=(-f "$compose_file")
  done

  if [[ -n "$profiles" ]]; then
    IFS=',' read -ra compose_profiles <<<"$profiles"
    for profile in "${compose_profiles[@]}"; do
      args+=(--profile "$profile")
    done
  fi

  echo "Validando Compose: $name"
  echo "  descricao: $description"
  echo "  arquivos: $files"
  if [[ -n "$profiles" ]]; then
    echo "  profiles: $profiles"
  else
    echo "  profiles: nenhum"
  fi

  if ! docker compose "${args[@]}" config --quiet; then
    echo "Falha ao validar a combinacao Compose '$name'." >&2
    echo "Comando: docker compose ${args[*]} config --quiet" >&2
    exit 1
  fi

  echo "OK: $name"
done

echo "Todas as combinacoes Docker Compose suportadas foram validadas."
