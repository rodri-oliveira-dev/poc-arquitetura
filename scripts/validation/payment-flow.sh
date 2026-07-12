#!/usr/bin/env bash
set -euo pipefail

payment_base_url="http://localhost:5234"
merchant_id="m1"
amount="100.00"
currency="BRL"
bearer_token="${PAYMENT_SMOKE_TOKEN:-}"
webhook_signing_secret="${PAYMENT_WEBHOOK_SIGNING_SECRET:-}"
timeout_seconds="60"
interval_seconds="2"

usage() {
  cat <<'EOF'
Uso: scripts/validation/payment-flow.sh [opcoes]

Opcoes:
  --payment-base-url <url>        Default: http://localhost:5234
  --merchant-id <merchant>        Default: m1
  --amount <valor>                Default: 100.00
  --currency <moeda>              Default: BRL
  --bearer-token <token>          Tambem aceito via PAYMENT_SMOKE_TOKEN.
  --webhook-signing-secret <sec>  Tambem aceito via PAYMENT_WEBHOOK_SIGNING_SECRET.
  --timeout-seconds <n>           Default: 60
  --interval-seconds <n>          Default: 2
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --payment-base-url) payment_base_url="${2:-}"; shift 2 ;;
    --merchant-id) merchant_id="${2:-}"; shift 2 ;;
    --amount) amount="${2:-}"; shift 2 ;;
    --currency) currency="${2:-}"; shift 2 ;;
    --bearer-token) bearer_token="${2:-}"; shift 2 ;;
    --webhook-signing-secret) webhook_signing_secret="${2:-}"; shift 2 ;;
    --timeout-seconds) timeout_seconds="${2:-}"; shift 2 ;;
    --interval-seconds) interval_seconds="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Opcao desconhecida: $1" >&2; usage >&2; exit 2 ;;
  esac
done

for tool in curl jq openssl; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "Pre-requisito ausente: $tool." >&2
    exit 127
  fi
done

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [[ -z "$bearer_token" ]]; then
  bearer_token="$("$repo_root/scripts/validation/get-token.sh")"
fi

if [[ -z "$webhook_signing_secret" ]]; then
  echo "Defina PAYMENT_WEBHOOK_SIGNING_SECRET ou use --webhook-signing-secret. Use somente whsec de teste/local." >&2
  exit 2
fi

payment_base_url="${payment_base_url%/}"
correlation_id="$(uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid)"
idempotency_key="$(uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid)"

create_body="$(jq -cn \
  --arg merchantId "$merchant_id" \
  --arg amount "$amount" \
  --arg currency "$currency" \
  --arg externalReference "smoke-payment-$idempotency_key" \
  '{merchantId:$merchantId,amount:($amount|tonumber),currency:$currency,description:"Smoke local Payment fake",externalReference:$externalReference}')"

echo "Criando Payment em $payment_base_url ..."
create_response="$(mktemp)"
create_status="$(curl -sS -o "$create_response" -w '%{http_code}' \
  -X POST "$payment_base_url/api/v1/payments" \
  -H "Authorization: Bearer $bearer_token" \
  -H "Idempotency-Key: $idempotency_key" \
  -H "X-Correlation-Id: $correlation_id" \
  -H "Content-Type: application/json" \
  --data "$create_body")"

if [[ "$create_status" != "202" ]]; then
  echo "POST /payments retornou HTTP $create_status." >&2
  exit 1
fi

payment_id="$(jq -r '.paymentId' "$create_response")"
provider_payment_id="$(jq -r '.providerPaymentId' "$create_response")"
echo "Payment aceito: $payment_id"

payload="$(jq -cn \
  --arg eventId "evt_smoke_payment_${payment_id//-/}" \
  --arg paymentId "$payment_id" \
  --arg providerPaymentId "$provider_payment_id" \
  '{id:$eventId,object:"event",type:"payment_intent.succeeded",data:{object:{id:$providerPaymentId,object:"payment_intent",metadata:{payment_id:$paymentId}}}}')"

timestamp="$(date +%s)"
signature="$(printf '%s.%s' "$timestamp" "$payload" | openssl dgst -sha256 -hmac "$webhook_signing_secret" -binary | od -An -tx1 | tr -d ' \n')"
webhook_status="$(curl -sS -o /dev/null -w '%{http_code}' \
  -X POST "$payment_base_url/api/v1/webhooks/stripe" \
  -H "Stripe-Signature: t=$timestamp,v1=$signature" \
  -H "X-Correlation-Id: $correlation_id" \
  -H "Content-Type: application/json" \
  --data "$payload")"

if [[ "$webhook_status" != "200" ]]; then
  echo "Webhook retornou HTTP $webhook_status." >&2
  exit 1
fi

deadline=$(( $(date +%s) + timeout_seconds ))
last_status=""
while [[ "$(date +%s)" -lt "$deadline" ]]; do
  get_response="$(mktemp)"
  get_status="$(curl -sS -o "$get_response" -w '%{http_code}' \
    -H "Authorization: Bearer $bearer_token" \
    -H "X-Correlation-Id: $correlation_id" \
    "$payment_base_url/api/v1/payments/$payment_id")"
  if [[ "$get_status" = "200" ]]; then
    last_status="$(jq -r '.status' "$get_response")"
    if [[ "$last_status" = "Completed" ]]; then
      ledger_entry_id="$(jq -r '.ledgerEntryId' "$get_response")"
      echo "Payment Completed. ledgerEntryId=$ledger_entry_id"
      echo "Resumo: paymentId=$payment_id providerPaymentId=$provider_payment_id correlationId=$correlation_id"
      exit 0
    fi
  else
    last_status="http=$get_status"
  fi
  sleep "$interval_seconds"
done

echo "Timeout aguardando Payment Completed. Ultimo status: $last_status" >&2
exit 1
