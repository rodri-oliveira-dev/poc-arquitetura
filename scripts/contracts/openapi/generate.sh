#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_LIB_DIR="$SCRIPT_DIR/lib"
if [[ ! -f "$SCRIPTS_LIB_DIR/common.sh" ]]; then
  SCRIPTS_LIB_DIR="$SCRIPT_DIR/../../lib"
fi
# shellcheck source=../../lib/common.sh
. "$SCRIPTS_LIB_DIR/common.sh"

ROOT_DIR="$(resolve_repo_root "$SCRIPT_DIR")"
CONFIGURATION="${CONFIGURATION:-Release}"
FRAMEWORK="${FRAMEWORK:-net10.0}"
SWAGGER_DOCUMENT="${SWAGGER_DOCUMENT:-v1}"
OUTPUT_DIR="${OUTPUT_DIR:-$ROOT_DIR/docs/openapi}"
SERVICE="${SERVICE:-}"

usage() {
  cat >&2 <<EOF
Uso: ./scripts/contracts/openapi/generate.sh [--configuration Release] [--framework net10.0] [--document v1] [--service audit]

Variaveis aceitas:
  CONFIGURATION, FRAMEWORK, SWAGGER_DOCUMENT, OUTPUT_DIR, SERVICE

Servicos aceitos em --service:
  ledger, balance, transfer, payment, identity, audit
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    -f|--framework)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      FRAMEWORK="${2:-}"
      shift 2
      ;;
    -d|--document)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      SWAGGER_DOCUMENT="${2:-}"
      shift 2
      ;;
    -s|--service)
      require_option_value "$1" "${2:-}" || {
        usage
        exit 2
      }
      SERVICE="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Parametro desconhecido: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ -z "$CONFIGURATION" || -z "$FRAMEWORK" || -z "$SWAGGER_DOCUMENT" ]]; then
  echo "Configuration, framework e documento Swagger nao podem ser vazios." >&2
  usage
  exit 2
fi

generate_contract() {
  local service_name="$1"
  local assembly_name="$2"
  local output_name="$3"
  local assembly_path="$ROOT_DIR/src/$service_name/bin/$CONFIGURATION/$FRAMEWORK/$assembly_name.dll"
  local output_path="$OUTPUT_DIR/$output_name"

  if [[ ! -f "$assembly_path" ]]; then
    local project_path="./src/$service_name/$assembly_name.csproj"
    cat >&2 <<EOF
Assembly esperado nao encontrado:
  $assembly_path

Execute antes:
  dotnet build $project_path --configuration $CONFIGURATION --no-restore
EOF
    exit 1
  fi

  echo "Gerando $output_path"
  ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-OpenApi}" \
  OPENAPI_GENERATION="${OPENAPI_GENERATION:-true}" \
  Swagger__Enabled="${Swagger__Enabled:-true}" \
  Jwt__Issuer="${Jwt__Issuer:-https://openapi.local}" \
  Jwt__Audience="${Jwt__Audience:-openapi-generation}" \
  Jwt__JwksUrl="${Jwt__JwksUrl:-https://openapi.local/.well-known/jwks.json}" \
  Jwt__RequireHttpsMetadata="${Jwt__RequireHttpsMetadata:-true}" \
  ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-Host=localhost;Database=openapi;Username=openapi;Password=openapi}" \
  ForwardedHeaders__TrustedProxies__0="${ForwardedHeaders__TrustedProxies__0:-127.0.0.1}" \
  ForwardedHeaders__AllowedHosts__0="${ForwardedHeaders__AllowedHosts__0:-openapi.local}" \
  ApiLimits__MaxRequestBodySizeBytes="${ApiLimits__MaxRequestBodySizeBytes:-1048576}" \
  ApiLimits__MaxBalancePeriodDays="${ApiLimits__MaxBalancePeriodDays:-31}" \
  ApiLimits__RateLimitPermitLimit="${ApiLimits__RateLimitPermitLimit:-100}" \
  ApiLimits__RateLimitWindowSeconds="${ApiLimits__RateLimitWindowSeconds:-60}" \
  ApiLimits__RateLimitQueueLimit="${ApiLimits__RateLimitQueueLimit:-10}" \
    dotnet tool run swagger -- tofile --output "$output_path" "$assembly_path" "$SWAGGER_DOCUMENT"

  perl -0pi -e 's/\\r\\n/\\n/g' "$output_path"
}

generate_selected_contracts() {
  case "${SERVICE,,}" in
    "")
      generate_contract "ledger/LedgerService.Api" "LedgerService.Api" "ledger.v1.json"
      generate_contract "balance/BalanceService.Api" "BalanceService.Api" "balance.v1.json"
      generate_contract "transfer/TransferService.Api" "TransferService.Api" "transfer.v1.json"
      generate_contract "payment/PaymentService.Api" "PaymentService.Api" "payment.v1.json"
      generate_contract "identity/IdentityService.Api" "IdentityService.Api" "identity.v1.json"
      generate_contract "audit/AuditService.Api" "AuditService.Api" "audit.v1.json"
      ;;
    ledger)
      generate_contract "ledger/LedgerService.Api" "LedgerService.Api" "ledger.v1.json"
      ;;
    balance)
      generate_contract "balance/BalanceService.Api" "BalanceService.Api" "balance.v1.json"
      ;;
    transfer)
      generate_contract "transfer/TransferService.Api" "TransferService.Api" "transfer.v1.json"
      ;;
    payment)
      generate_contract "payment/PaymentService.Api" "PaymentService.Api" "payment.v1.json"
      ;;
    identity)
      generate_contract "identity/IdentityService.Api" "IdentityService.Api" "identity.v1.json"
      ;;
    audit)
      generate_contract "audit/AuditService.Api" "AuditService.Api" "audit.v1.json"
      ;;
    *)
      echo "Servico OpenAPI desconhecido: $SERVICE" >&2
      usage
      exit 2
      ;;
  esac
}

mkdir -p "$OUTPUT_DIR"

generate_selected_contracts

echo "Contratos OpenAPI gerados em: $OUTPUT_DIR"
