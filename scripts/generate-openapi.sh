#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
FRAMEWORK="${FRAMEWORK:-net10.0}"
SWAGGER_DOCUMENT="${SWAGGER_DOCUMENT:-v1}"
OUTPUT_DIR="${OUTPUT_DIR:-$ROOT_DIR/docs/openapi}"

usage() {
  cat >&2 <<EOF
Uso: ./scripts/generate-openapi.sh [--configuration Release] [--framework net10.0] [--document v1]

Variaveis aceitas:
  CONFIGURATION, FRAMEWORK, SWAGGER_DOCUMENT, OUTPUT_DIR
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    -f|--framework)
      FRAMEWORK="${2:-}"
      shift 2
      ;;
    -d|--document)
      SWAGGER_DOCUMENT="${2:-}"
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
    cat >&2 <<EOF
Assembly esperado nao encontrado:
  $assembly_path

Execute antes:
  dotnet build ./LedgerService.slnx --configuration $CONFIGURATION --no-restore
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
  ApiLimits__MaxRequestBodySizeBytes="${ApiLimits__MaxRequestBodySizeBytes:-1048576}" \
  ApiLimits__MaxBalancePeriodDays="${ApiLimits__MaxBalancePeriodDays:-31}" \
  ApiLimits__RateLimitPermitLimit="${ApiLimits__RateLimitPermitLimit:-100}" \
  ApiLimits__RateLimitWindowSeconds="${ApiLimits__RateLimitWindowSeconds:-60}" \
  ApiLimits__RateLimitQueueLimit="${ApiLimits__RateLimitQueueLimit:-10}" \
    dotnet tool run swagger -- tofile --output "$output_path" "$assembly_path" "$SWAGGER_DOCUMENT"
}

mkdir -p "$OUTPUT_DIR"

generate_contract "LedgerService.Api" "LedgerService.Api" "ledger.v1.json"
generate_contract "BalanceService.Api" "BalanceService.Api" "balance.v1.json"

echo "Contratos OpenAPI gerados em: $OUTPUT_DIR"
