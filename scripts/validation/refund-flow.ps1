[CmdletBinding()]
param(
  [string]$PaymentBaseUrl = "http://localhost:5234",
  [string]$MerchantId = "m1",
  [decimal]$Amount = 100.00,
  [string]$Currency = "BRL",
  [string]$BearerToken = $env:PAYMENT_SMOKE_TOKEN,
  [string]$WebhookSigningSecret = $env:PAYMENT_WEBHOOK_SIGNING_SECRET,
  [int]$PollingTimeoutSeconds = 90,
  [int]$PollingIntervalSeconds = 2
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir "..\lib\common-validation.ps1")

function New-StripeSignature([string]$Payload, [string]$Secret) {
  $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
  $hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($Secret))
  try {
    $hash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$timestamp.$Payload"))
    return "t=$timestamp,v1=$([Convert]::ToHexString($hash).ToLowerInvariant())"
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

function Wait-PaymentStatus([string]$PaymentId, [string]$ExpectedStatus, [hashtable]$Headers) {
  $body = Wait-Until "Payment $ExpectedStatus" $PollingTimeoutSeconds $PollingIntervalSeconds {
    $response = Invoke-PaymentJson "Get" "$paymentBase/api/v1/payments/$PaymentId" $Headers
    if ([int]$response.StatusCode -ne 200) {
      return "http=$([int]$response.StatusCode)"
    }

    return $response.Content
  } {
    param($value)
    try {
      $json = $value | ConvertFrom-Json
      return [string]$json.status -eq $ExpectedStatus
    }
    catch {
      return $false
    }
  }

  return $body | ConvertFrom-Json
}

if ([string]::IsNullOrWhiteSpace($BearerToken)) {
  $BearerToken = Get-ValidationToken
}

if ([string]::IsNullOrWhiteSpace($WebhookSigningSecret)) {
  throw "Defina PAYMENT_WEBHOOK_SIGNING_SECRET ou informe -WebhookSigningSecret antes de enviar o webhook. O mesmo valor deve estar em PaymentGateway__Stripe__WebhookSigningSecret no container payment-service; se alterar .env.local, recrie o container com docker compose --env-file .env.local up -d --force-recreate payment-service. Use somente whsec de teste/local."
}

$paymentBase = $PaymentBaseUrl.TrimEnd("/")
$correlationId = [Guid]::NewGuid().ToString()
$paymentKey = [Guid]::NewGuid().ToString()
$headers = @{
  Authorization = "Bearer $BearerToken"
  "Idempotency-Key" = $paymentKey
  "X-Correlation-Id" = $correlationId
}

$createBody = @{
  merchantId = $MerchantId
  amount = $Amount
  currency = $Currency
  description = "Smoke local Refund fake"
  externalReference = "smoke-refund-payment-$paymentKey"
} | ConvertTo-Json

Write-Host "Criando Payment base para smoke de refund ..."
$create = Invoke-PaymentJson "Post" "$paymentBase/api/v1/payments" $headers $createBody
if ([int]$create.StatusCode -ne 202) {
  throw "POST /payments retornou HTTP $([int]$create.StatusCode)."
}

$created = $create.Content | ConvertFrom-Json
$paymentId = [string]$created.paymentId
$providerPaymentId = [string]$created.providerPaymentId

$paymentPayload = @{
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
  "Stripe-Signature" = New-StripeSignature $paymentPayload $WebhookSigningSecret
  "X-Correlation-Id" = $correlationId
}

$paymentWebhook = Invoke-PaymentJson "Post" "$paymentBase/api/v1/webhooks/stripe" $webhookHeaders $paymentPayload
if ([int]$paymentWebhook.StatusCode -ne 200) {
  throw "Webhook de Payment retornou HTTP $([int]$paymentWebhook.StatusCode)."
}

$readHeaders = @{
  Authorization = "Bearer $BearerToken"
  "X-Correlation-Id" = $correlationId
}
$completed = Wait-PaymentStatus $paymentId "Completed" $readHeaders
Write-Host "Payment base Completed. ledgerEntryId=$($completed.ledgerEntryId)"

$refundKey = [Guid]::NewGuid().ToString()
$refundHeaders = @{
  Authorization = "Bearer $BearerToken"
  "Idempotency-Key" = $refundKey
  "X-Correlation-Id" = $correlationId
}
$refundBody = @{
  amount = $Amount
  reason = "requested_by_customer"
  externalReference = "smoke-refund-$refundKey"
} | ConvertTo-Json

Write-Host "Solicitando refund total ..."
$refundResponse = Invoke-PaymentJson "Post" "$paymentBase/api/v1/payments/$paymentId/refunds" $refundHeaders $refundBody
if ([int]$refundResponse.StatusCode -ne 202) {
  throw "POST refund retornou HTTP $([int]$refundResponse.StatusCode)."
}

$refund = $refundResponse.Content | ConvertFrom-Json
$refundId = [string]$refund.refundId
$providerRefundId = if ($refund.providerRefundId) { [string]$refund.providerRefundId } else { "re_smoke_$($refundId.Replace('-', ''))" }

$refundPayload = @{
  id = "evt_smoke_refund_$($refundId.Replace('-', ''))"
  object = "event"
  type = "refund.updated"
  data = @{
    object = @{
      id = $providerRefundId
      object = "refund"
      payment_intent = $providerPaymentId
      amount = [int]($Amount * 100)
      currency = $Currency.ToLowerInvariant()
      status = "succeeded"
      metadata = @{
        payment_id = $paymentId
        refund_id = $refundId
      }
    }
  }
} | ConvertTo-Json -Depth 8 -Compress

$refundWebhookHeaders = @{
  "Stripe-Signature" = New-StripeSignature $refundPayload $WebhookSigningSecret
  "X-Correlation-Id" = $correlationId
}

$refundWebhook = Invoke-PaymentJson "Post" "$paymentBase/api/v1/webhooks/stripe" $refundWebhookHeaders $refundPayload
if ([int]$refundWebhook.StatusCode -ne 200) {
  throw "Webhook de Refund retornou HTTP $([int]$refundWebhook.StatusCode)."
}

$refunded = Wait-PaymentStatus $paymentId "Refunded" $readHeaders
Write-Host "Payment Refunded. paymentId=$paymentId refundId=$refundId correlationId=$correlationId status=$($refunded.status)"
