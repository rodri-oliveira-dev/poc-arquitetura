[CmdletBinding()]
param(
  [switch]$Help,
  [int]$Port = 5234,
  [string]$ForwardTo,
  [string]$Path = "/api/v1/webhooks/stripe",
  [string[]]$Events = @(
    "payment_intent.processing",
    "payment_intent.succeeded",
    "payment_intent.payment_failed",
    "payment_intent.canceled"
  )
)

$ErrorActionPreference = "Stop"

if ($Help) {
  @"
Uso: scripts/validation/stripe-listen-payment-webhook.ps1 [opcoes]

Opcoes:
  -Port <porta>          Porta local do PaymentService.Api. Default: 5234.
  -ForwardTo <url>       URL completa de forwarding. Sobrescreve -Port/-Path.
  -Path <path>           Path do webhook. Default: /api/v1/webhooks/stripe.
  -Events <lista>        Lista de eventos para --events. Use lista vazia para nao filtrar.
  -Help                  Mostra esta ajuda.
"@ | Write-Host
  exit 0
}

if ($PSBoundParameters.ContainsKey("ForwardTo") -and [string]::IsNullOrWhiteSpace($ForwardTo)) {
  throw "ForwardTo nao pode ser vazio."
}

$stripe = Get-Command stripe -ErrorAction SilentlyContinue
if (-not $stripe) {
  Write-Error "Stripe CLI nao encontrada. Instale pelo metodo oficial em https://docs.stripe.com/stripe-cli/install e valide com 'stripe version'."
  exit 127
}

if (-not $PSBoundParameters.ContainsKey("ForwardTo")) {
  $normalizedPath = if ($Path.StartsWith("/")) { $Path } else { "/$Path" }
  $ForwardTo = "http://localhost:$Port$normalizedPath"
}

Write-Host "Encaminhando webhooks Stripe para: $ForwardTo"
Write-Host "Quando a CLI imprimir o whsec_..., configure localmente:"
Write-Host '$env:PaymentGateway__Stripe__WebhookSigningSecret = "whsec_xxx"'
Write-Host "Este script nao salva secrets e nao executa em background."

$arguments = @("listen", "--forward-to", $ForwardTo)
if ($Events.Count -gt 0) {
  $arguments += @("--events", ($Events -join ","))
}

& $stripe.Source @arguments
