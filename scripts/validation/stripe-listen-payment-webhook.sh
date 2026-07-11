#!/usr/bin/env bash
set -euo pipefail

port="5234"
forward_to=""
path="/api/v1/webhooks/stripe"
events="payment_intent.processing,payment_intent.succeeded,payment_intent.payment_failed,payment_intent.canceled"

usage() {
  cat <<'EOF'
Uso: scripts/validation/stripe-listen-payment-webhook.sh [opcoes]

Opcoes:
  --port <porta>          Porta local do PaymentService.Api. Default: 5234.
  --forward-to <url>      URL completa de forwarding. Sobrescreve --port/--path.
  --path <path>           Path do webhook. Default: /api/v1/webhooks/stripe.
  --events <lista>        Lista separada por virgula para --events. Use vazio para nao filtrar.
  -h, --help              Mostra esta ajuda.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --port)
      port="${2:-}"
      shift 2
      ;;
    --forward-to)
      forward_to="${2:-}"
      shift 2
      ;;
    --path)
      path="${2:-}"
      shift 2
      ;;
    --events)
      events="${2:-}"
      shift 2
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
done

if ! command -v stripe >/dev/null 2>&1; then
  echo "Stripe CLI nao encontrada. Instale pelo metodo oficial em https://docs.stripe.com/stripe-cli/install e valide com 'stripe version'." >&2
  exit 127
fi

if [ -z "$forward_to" ]; then
  case "$path" in
    /*) ;;
    *) path="/$path" ;;
  esac
  forward_to="http://localhost:${port}${path}"
fi

echo "Encaminhando webhooks Stripe para: $forward_to"
echo "Quando a CLI imprimir o whsec_..., configure localmente:"
echo 'export PaymentGateway__Stripe__WebhookSigningSecret=whsec_xxx'
echo "Este script nao salva secrets e nao executa em background."

args=(listen --forward-to "$forward_to")
if [ -n "$events" ]; then
  args+=(--events "$events")
fi

exec stripe "${args[@]}"
