#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TARGET_FILE="$ROOT_DIR/.env.local"
FORCE=false

usage() {
  cat >&2 <<'EOF'
Uso: ./scripts/local/create-env-local.sh [--force] [--output PATH]

Cria .env.local com valores locais descartaveis para compose.yaml.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force)
      FORCE=true
      shift
      ;;
    --output)
      if [[ $# -lt 2 ]]; then
        echo "--output exige um caminho." >&2
        exit 2
      fi
      TARGET_FILE="$2"
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

if [[ -f "$TARGET_FILE" && "$FORCE" != "true" ]]; then
  echo "$TARGET_FILE ja existe. Use --force para recriar conscientemente." >&2
  exit 1
fi

new_local_secret() {
  local name="$1"
  local normalized suffix
  normalized="$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/_/g')"

  if command -v openssl >/dev/null 2>&1; then
    suffix="$(openssl rand -hex 24)"
  else
    suffix="$(od -An -N24 -tx1 /dev/urandom | tr -d ' \n')"
  fi

  printf 'local_%s_%s' "$normalized" "$suffix"
}

{
  cat <<EOF
# Valores locais descartaveis para compose.yaml.
# Gerado por scripts/local/create-env-local.sh.
# Nao versione .env.local e nao reutilize estes valores em ambientes compartilhados.

# PostgreSQL
POSTGRES_PASSWORD=$(new_local_secret POSTGRES_PASSWORD)
POSTGRES_HOST_PORT=15432
LEDGER_DB_PASSWORD=$(new_local_secret LEDGER_DB_PASSWORD)
LEDGER_DB_MIGRATOR_PASSWORD=$(new_local_secret LEDGER_DB_MIGRATOR_PASSWORD)
BALANCE_DB_READ_PASSWORD=$(new_local_secret BALANCE_DB_READ_PASSWORD)
BALANCE_DB_WRITE_PASSWORD=$(new_local_secret BALANCE_DB_WRITE_PASSWORD)
BALANCE_DB_MIGRATOR_PASSWORD=$(new_local_secret BALANCE_DB_MIGRATOR_PASSWORD)
TRANSFER_DB_PASSWORD=$(new_local_secret TRANSFER_DB_PASSWORD)
TRANSFER_DB_MIGRATOR_PASSWORD=$(new_local_secret TRANSFER_DB_MIGRATOR_PASSWORD)
PAYMENT_DB_PASSWORD=$(new_local_secret PAYMENT_DB_PASSWORD)
PAYMENT_DB_MIGRATOR_PASSWORD=$(new_local_secret PAYMENT_DB_MIGRATOR_PASSWORD)
IDENTITY_DB_PASSWORD=$(new_local_secret IDENTITY_DB_PASSWORD)
IDENTITY_DB_MIGRATOR_PASSWORD=$(new_local_secret IDENTITY_DB_MIGRATOR_PASSWORD)

# Keycloak/JWT
KEYCLOAK_HOST_PORT=8081
KEYCLOAK_BASE_URL=http://localhost:8081
KEYCLOAK_INTERNAL_BASE_URL=http://keycloak:8080
KEYCLOAK_REALM=poc
KEYCLOAK_TOKEN_ENDPOINT=/realms/poc/protocol/openid-connect/token
KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME=local_admin
KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD=$(new_local_secret KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD)
KEYCLOAK_CLIENT_ID=poc-automation
KEYCLOAK_CLIENT_SECRET=$(new_local_secret KEYCLOAK_CLIENT_SECRET)
KEYCLOAK_IDENTITY_ADMIN_CLIENT_ID=identity-service-admin
TRANSFER_WORKER_LEDGER_AUTH_SCOPE=ledger.write
KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD=$(new_local_secret KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD)
KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD=$(new_local_secret KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD)
KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD=$(new_local_secret KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD)
JWT_ISSUER=http://localhost:8081/realms/poc
JWT_JWKS_URL=http://keycloak:8080/realms/poc/protocol/openid-connect/certs
JWT_REQUIRE_HTTPS_METADATA=false
TOKEN_PROVIDER=keycloak

# Kafka/PubSub
PUBSUB_PROJECT_ID=poc-local
PUBSUB_EMULATOR_HOST_PORT=8085
PUBSUB_LEDGER_EVENTS_TOPIC_ID=ledger.ledgerentry.created.local
PUBSUB_LEDGER_EVENTS_DLQ_TOPIC_ID=ledger.ledgerentry.created.dlq.local
PUBSUB_BALANCE_SUBSCRIPTION_ID=balance-service-ledger-events-local
PUBSUB_LEDGER_EVENTS_DLQ_INSPECTION_SUBSCRIPTION_ID=ledger-events-application-dlq-inspection-local

# Services
TRANSFER_SERVICE_HOST_PORT=5230
PAYMENT_SERVICE_HOST_PORT=5234
IDENTITY_SERVICE_HOST_PORT=5232

# Mailpit/Resend
MAILPIT_SMTP_HOST_PORT=1025
MAILPIT_UI_HOST_PORT=8025
Email__Provider=Mailpit
Email__AuthenticationUrl=http://localhost:8081/realms/poc/account
Mailpit__Host=localhost
Mailpit__Port=1025
Mailpit__EnableSsl=false
Mailpit__From=noreply@poc-arquitetura.local
Mailpit__FromName=POC Arquitetura
Resend__ApiKey=
Resend__From=onboarding@seudominio.example
Resend__FromName=POC Arquitetura
Resend__ReplyTo=

# PaymentService
PaymentGateway__Provider=Fake
PaymentGateway__Stripe__SecretKey=
PaymentGateway__Stripe__WebhookSigningSecret=

# Observability
GRAFANA_ADMIN_PASSWORD=$(new_local_secret GRAFANA_ADMIN_PASSWORD)
OTEL_ENABLED=false
DOCKER_LOG_MAX_SIZE=10m
DOCKER_LOG_MAX_FILE=3
EOF
} > "$TARGET_FILE"

echo "Arquivo $TARGET_FILE criado com valores locais descartaveis."
