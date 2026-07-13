[CmdletBinding()]
param(
  [string]$PaymentBaseUrl = "http://localhost:5234",
  [string]$MerchantId = "m1",
  [decimal]$Amount = 100.00,
  [string]$Currency = "BRL",
  [string]$BearerToken = $env:PAYMENT_SMOKE_TOKEN,
  [string]$WebhookSigningSecret = $env:PAYMENT_WEBHOOK_SIGNING_SECRET,
  [int]$PollingTimeoutSeconds = 60,
  [int]$PollingIntervalSeconds = 2
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir "..\lib\common-validation.ps1")

function New-StripeSignature([string]$Payload, [string]$Secret) {
  $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
  $signedPayload = "$timestamp.$Payload"
  $hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret))
  try {
    $hash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($signedPayload))
    $signature = [Convert]::ToHexString($hash).ToLowerInvariant()
    return "t=$timestamp,v1=$signature"
  }
  finally {
    $hmac.Dispose()
  }
}

function Invoke-PaymentJson([string]$Method, [string]$Uri, [hashtable]$Headers, [string]$Body = "") {
  try {
    if ([string]::IsNullOrWhiteSpace($Body)) {
      return Invoke-WebRequest -UseBasicParsing -Method $Method -Uri $Uri -Headers $Headers
    }

    return Invoke-WebRequest -UseBasicParsing -Method $Method -Uri $Uri -Headers $Headers -ContentType "application/json" -Body $Body
  }
  catch {
    if ($_.Exception.Response) {
      return $_.Exception.Response
    }

    throw
  }
}

if ([string]::IsNullOrWhiteSpace($BearerToken)) {
  $BearerToken = Get-ValidationToken
}

if ([string]::IsNullOrWhiteSpace($WebhookSigningSecret)) {
  throw "Defina PAYMENT_WEBHOOK_SIGNING_SECRET ou informe -WebhookSigningSecret antes de enviar o webhook. O mesmo valor deve estar em PaymentGateway__Stripe__WebhookSigningSecret no container payment-service; se alterar .env.local, recrie o container com docker compose --env-file .env.local up -d --force-recreate payment-service. Use somente whsec de teste/local."
}

$paymentBase = $PaymentBaseUrl.TrimEnd("/")
$correlationId = [Guid]::NewGuid().ToString()
$idempotencyKey = [Guid]::NewGuid().ToString()
$headers = @{
  Authorization = "Bearer $BearerToken"
  "Idempotency-Key" = $idempotencyKey
  "X-Correlation-Id" = $correlationId
}

$body = @{
  merchantId = $MerchantId
  amount = $Amount
  currency = $Currency
  description = "Smoke local Payment fake"
  externalReference = "smoke-payment-$idempotencyKey"
} | ConvertTo-Json

Write-Host "Criando Payment em $paymentBase ..."
$create = Invoke-PaymentJson "Post" "$paymentBase/api/v1/payments" $headers $body
if ([int]$create.StatusCode -ne 202) {
  throw "POST /payments retornou HTTP $([int]$create.StatusCode)."
}

$created = $create.Content | ConvertFrom-Json
$paymentId = [string]$created.paymentId
$providerPaymentId = [string]$created.providerPaymentId
Write-Host "Payment aceito: $paymentId"

$payload = @{
  id = "evt_smoke_payment_$($paymentId.Replace('-', ''))"
  object = "event"
  type = "payment_intent.succeeded"
  data = @{
    object = @{
      id = $providerPaymentId
      object = "payment_intent"
      metadata = @{
        payment_id = $paymentId
      }
    }
  }
} | ConvertTo-Json -Depth 8 -Compress

$webhookHeaders = @{
  "Stripe-Signature" = New-StripeSignature $payload $WebhookSigningSecret
  "X-Correlation-Id" = $correlationId
}

Write-Host "Enviando webhook local assinado para PaymentService.Api ..."
$webhook = Invoke-PaymentJson "Post" "$paymentBase/api/v1/webhooks/stripe" $webhookHeaders $payload
if ([int]$webhook.StatusCode -ne 200) {
  throw "Webhook retornou HTTP $([int]$webhook.StatusCode)."
}

$readHeaders = @{
  Authorization = "Bearer $BearerToken"
  "X-Correlation-Id" = $correlationId
}

$final = Wait-Until "Payment Completed" $PollingTimeoutSeconds $PollingIntervalSeconds {
  $response = Invoke-PaymentJson "Get" "$paymentBase/api/v1/payments/$paymentId" $readHeaders
  if ([int]$response.StatusCode -ne 200) {
    return "http=$([int]$response.StatusCode)"
  }

  return $response.Content
} {
  param($value)
  try {
    $json = $value | ConvertFrom-Json
    return [string]$json.status -eq "Completed"
  }
  catch {
    return $false
  }
}

$payment = $final | ConvertFrom-Json
Write-Host "Payment Completed. ledgerEntryId=$($payment.ledgerEntryId)"
Write-Host "Resumo: paymentId=$paymentId providerPaymentId=$providerPaymentId correlationId=$correlationId"
