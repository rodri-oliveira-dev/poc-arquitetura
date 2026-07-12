#!/usr/bin/env bash
set -euo pipefail

payment_base_url="http://localhost:5234"
merchant_id="m1"
amount="100.00"
currency="BRL"
bearer_token="${PAYMENT_SMOKE_TOKEN:-}"
webhook_signing_secret="${PAYMENT_WEBHOOK_SIGNING_SECRET:-}"
timeout_seconds="90"
interval_seconds="2"

usage() {
  cat <<'EOF'
Uso: scripts/validation/refund-flow.sh [opcoes]

Opcoes:
  --payment-base-url <url>        Default: http://localhost:5234
  --merchant-id <merchant>        Default: m1
  --amount <valor>                Default: 100.00
  --currency <moeda>              Default: BRL
  --bearer-token <token>          Tambem aceito via PAYMENT_SMOKE_TOKEN.
  --webhook-signing-secret <sec>  Tambem aceito via PAYMENT_WEBHOOK_SIGNING_SECRET.
  --timeout-seconds <n>           Default: 90
  --interval-seconds <n>          Default: 2
EOF
}

while [ "$#" -gt 0 ]; do
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

new_uuid() {
  uuidgen 2>/dev/null || cat /proc/sys/kernel/random/uuid
}

sign_payload() {
  local payload="$1"
  local timestamp
  local signature
  timestamp="$(date +%s)"
  signature="$(printf '%s.%s' "$timestamp" "$payload" | openssl dgst -sha256 -hmac "$webhook_signing_secret" -binary | od -An -tx1 | tr -d ' \n')"
  printf 't=%s,v1=%s' "$timestamp" "$signature"
}

post_json() {
  local url="$1"
  local body="$2"
  local output="$3"
  shift 3
  curl -sS -o "$output" -w '%{http_code}' -X POST "$url" -H "Content-Type: application/json" "$@" --data "$body"
}

wait_payment_status() {
  local payment_id="$1"
  local expected="$2"
  local deadline=$(( $(date +%s) + timeout_seconds ))
  local last_status=""

  while [ "$(date +%s)" -lt "$deadline" ]; do
    local output
    local status
    output="$(mktemp)"
    status="$(curl -sS -o "$output" -w '%{http_code}' \
      -H "Authorization: Bearer $bearer_token" \
      -H "X-Correlation-Id: $correlation_id" \
      "$payment_base_url/api/v1/payments/$payment_id")"
    if [ "$status" = "200" ]; then
      last_status="$(jq -r '.status' "$output")"
      if [ "$last_status" = "$expected" ]; then
        cat "$output"
        return 0
      fi
    else
      last_status="http=$status"
    fi
    sleep "$interval_seconds"
  done

  echo "Timeout aguardando Payment $expected. Ultimo status: $last_status" >&2
  return 1
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
if [ -z "$bearer_token" ]; then
  bearer_token="$("$repo_root/scripts/validation/get-token.sh")"
fi

if [ -z "$webhook_signing_secret" ]; then
  echo "Defina PAYMENT_WEBHOOK_SIGNING_SECRET ou use --webhook-signing-secret. Use somente whsec de teste/local." >&2
  exit 2
fi

payment_base_url="${payment_base_url%/}"
correlation_id="$(new_uuid)"
payment_key="$(new_uuid)"

create_body="$(jq -cn \
  --arg merchantId "$merchant_id" \
  --arg amount "$amount" \
  --arg currency "$currency" \
  --arg externalReference "smoke-refund-payment-$payment_key" \
  '{merchantId:$merchantId,amount:($amount|tonumber),currency:$currency,description:"Smoke local Refund fake",externalReference:$externalReference}')"

echo "Criando Payment base para smoke de refund ..."
create_response="$(mktemp)"
create_status="$(post_json "$payment_base_url/api/v1/payments" "$create_body" "$create_response" \
  -H "Authorization: Bearer $bearer_token" \
  -H "Idempotency-Key: $payment_key" \
  -H "X-Correlation-Id: $correlation_id")"

if [ "$create_status" != "202" ]; then
  echo "POST /payments retornou HTTP $create_status." >&2
  exit 1
fi

payment_id="$(jq -r '.paymentId' "$create_response")"
provider_payment_id="$(jq -r '.providerPaymentId' "$create_response")"
payment_payload="$(jq -cn \
  --arg eventId "evt_smoke_payment_${payment_id//-/}" \
  --arg paymentId "$payment_id" \
  --arg providerPaymentId "$provider_payment_id" \
  '{id:$eventId,object:"event",type:"payment_intent.succeeded",data:{object:{id:$providerPaymentId,object:"payment_intent",metadata:{payment_id:$paymentId}}}}')"

payment_webhook_status="$(post_json "$payment_base_url/api/v1/webhooks/stripe" "$payment_payload" /dev/null \
  -H "Stripe-Signature: $(sign_payload "$payment_payload")" \
  -H "X-Correlation-Id: $correlation_id")"
if [ "$payment_webhook_status" != "200" ]; then
  echo "Webhook de Payment retornou HTTP $payment_webhook_status." >&2
  exit 1
fi

completed="$(wait_payment_status "$payment_id" "Completed")"
ledger_entry_id="$(printf '%s' "$completed" | jq -r '.ledgerEntryId')"
echo "Payment base Completed. ledgerEntryId=$ledger_entry_id"

refund_key="$(new_uuid)"
refund_body="$(jq -cn \
  --arg amount "$amount" \
  --arg externalReference "smoke-refund-$refund_key" \
  '{amount:($amount|tonumber),reason:"requested_by_customer",externalReference:$externalReference}')"

echo "Solicitando refund total ..."
refund_response="$(mktemp)"
refund_status="$(post_json "$payment_base_url/api/v1/payments/$payment_id/refunds" "$refund_body" "$refund_response" \
  -H "Authorization: Bearer $bearer_token" \
  -H "Idempotency-Key: $refund_key" \
  -H "X-Correlation-Id: $correlation_id")"
if [ "$refund_status" != "202" ]; then
  echo "POST refund retornou HTTP $refund_status." >&2
  exit 1
fi

refund_id="$(jq -r '.refundId' "$refund_response")"
provider_refund_id="re_smoke_${refund_id//-/}"
amount_cents="$(jq -n --arg amount "$amount" '($amount|tonumber * 100)|floor')"
refund_payload="$(jq -cn \
  --arg eventId "evt_smoke_refund_${refund_id//-/}" \
  --arg paymentId "$payment_id" \
  --arg refundId "$refund_id" \
  --arg providerPaymentId "$provider_payment_id" \
  --arg providerRefundId "$provider_refund_id" \
  --argjson amount "$amount_cents" \
  --arg currency "$(printf '%s' "$currency" | tr '[:upper:]' '[:lower:]')" \
  '{id:$eventId,object:"event",type:"refund.updated",data:{object:{id:$providerRefundId,object:"refund",payment_intent:$providerPaymentId,amount:$amount,currency:$currency,status:"succeeded",metadata:{payment_id:$paymentId,refund_id:$refundId}}}}')"

refund_webhook_status="$(post_json "$payment_base_url/api/v1/webhooks/stripe" "$refund_payload" /dev/null \
  -H "Stripe-Signature: $(sign_payload "$refund_payload")" \
  -H "X-Correlation-Id: $correlation_id")"
if [ "$refund_webhook_status" != "200" ]; then
  echo "Webhook de Refund retornou HTTP $refund_webhook_status." >&2
  exit 1
fi

refunded="$(wait_payment_status "$payment_id" "Refunded")"
final_status="$(printf '%s' "$refunded" | jq -r '.status')"
echo "Payment Refunded. paymentId=$payment_id refundId=$refund_id correlationId=$correlation_id status=$final_status"
