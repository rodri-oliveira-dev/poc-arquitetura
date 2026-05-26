[CmdletBinding()]
param(
  [string]$AuthBaseUrl = "http://localhost:5030",
  [string]$LedgerBaseUrl = "http://localhost:5226",
  [string]$BalanceBaseUrl = "http://localhost:5228",
  [string]$JaegerBaseUrl = "http://localhost:16686",
  [string]$Username = "local_user",
  [string]$Password = "local_password",
  [string]$Scope = "ledger.write balance.read",
  [string]$MerchantId = "tese",
  [ValidateSet("CREDIT", "DEBIT")]
  [string]$Type = "CREDIT",
  [decimal]$Amount = 10.00,
  [int]$PollingTimeoutSeconds = 45,
  [int]$PollingIntervalSeconds = 2
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir "common-validation.ps1")

Write-Host "Obtendo token pelo provider local configurado..."
$token = Get-ValidationToken $AuthBaseUrl $Username $Password $Scope

$correlationId = [Guid]::NewGuid().ToString()
$idempotencyKey = [Guid]::NewGuid().ToString()
$externalReference = "local-auth-ledger-$idempotencyKey"

$ledgerUrl = $LedgerBaseUrl.TrimEnd("/") + "/api/v1/lancamentos"
$ledgerBody = @{
  merchantId = $MerchantId
  type = $Type
  amount = $Amount
  description = "Validacao local Auth -> Ledger com OpenTelemetry"
  externalReference = $externalReference
} | ConvertTo-Json

$headers = @{
  Authorization = "Bearer $token"
  "Idempotency-Key" = $idempotencyKey
  "X-Correlation-Id" = $correlationId
}

Write-Host "Chamando LedgerService.Api..."
$response = Invoke-HttpJson "Post" $ledgerUrl $headers $ledgerBody
$statusCode = [int]$response.StatusCode
$responseCorrelationId = Get-HeaderValue $response.Headers "X-Correlation-Id"

Write-Host "Ledger status HTTP: $statusCode"
Write-Host "X-Correlation-Id enviado:   $correlationId"
Write-Host "X-Correlation-Id response:  $responseCorrelationId"

if ($statusCode -lt 200 -or $statusCode -ge 300) {
  throw "Chamada ao Ledger falhou com HTTP $statusCode"
}

if ($responseCorrelationId -ne $correlationId) {
  throw "Ledger nao preservou o X-Correlation-Id enviado."
}

$responseBody = $response.Content | ConvertFrom-Json
$ledgerEntryId = [string]$responseBody.id
$occurredAt = [DateTimeOffset]::Parse([string]$responseBody.occurredAt, [Globalization.CultureInfo]::InvariantCulture)
$balanceDate = $occurredAt.ToString("yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture)

Write-Host "Lancamento criado: $ledgerEntryId"
Write-Host "Data do consolidado: $balanceDate"

Write-Host "Aguardando Outbox marcar mensagem como Processed..."
$outboxSql = @"
SELECT id, event_type, status, retry_count, correlation_id, processed_at
FROM outbox_messages
WHERE correlation_id = '$correlationId'
ORDER BY occurred_at DESC
LIMIT 1;
"@

$outboxRow = Wait-Until "Outbox Processed" $PollingTimeoutSeconds $PollingIntervalSeconds {
  Invoke-PostgresScalar "ledger-db" "appuser" "appdb" $outboxSql
} {
  param($value)
  $value -match '\|LedgerEntryCreated\.v1\|Processed\|'
}

Write-Host "Outbox: $outboxRow"

Write-Host "Aguardando Balance processar evento..."
$processedSql = @"
SELECT event_id, merchant_id, processed_at
FROM processed_events
WHERE event_id = '$ledgerEntryId'
LIMIT 1;
"@

$processedRow = Wait-Until "processed_events no Balance" $PollingTimeoutSeconds $PollingIntervalSeconds {
  Invoke-PostgresScalar "balance-db" $script:BalanceDbUser $script:BalanceDbName $processedSql
} {
  param($value)
  $value -match [Regex]::Escape($ledgerEntryId)
}

Write-Host "Processed event: $processedRow"

$balanceSql = @"
SELECT merchant_id, date, currency, total_credits, total_debits, net_balance
FROM daily_balances
WHERE merchant_id = '$MerchantId' AND date = DATE '$balanceDate'
LIMIT 1;
"@

$balanceRow = Invoke-PostgresScalar "balance-db" $script:BalanceDbUser $script:BalanceDbName $balanceSql
Write-Host "Daily balance DB: $balanceRow"

Write-Host "Consultando BalanceService.Api..."
$balanceUrl = $BalanceBaseUrl.TrimEnd("/") + "/v1/consolidados/diario/$balanceDate" + "?merchantId=$MerchantId"
$balanceHeaders = @{
  Authorization = "Bearer $token"
  "X-Correlation-Id" = $correlationId
}
$balanceResponse = Invoke-WebRequest -UseBasicParsing -Method Get -Uri $balanceUrl -Headers $balanceHeaders
$balanceStatusCode = [int]$balanceResponse.StatusCode
$balanceCorrelationId = Get-HeaderValue $balanceResponse.Headers "X-Correlation-Id"

Write-Host "Balance status HTTP: $balanceStatusCode"
Write-Host "X-Correlation-Id Balance: $balanceCorrelationId"
Write-Host "Balance body: $($balanceResponse.Content)"

if ($balanceStatusCode -lt 200 -or $balanceStatusCode -ge 300) {
  throw "Chamada ao Balance falhou com HTTP $balanceStatusCode"
}

if ($balanceCorrelationId -ne $correlationId) {
  throw "Balance nao preservou o X-Correlation-Id enviado."
}

Write-Host "Consultando traces recentes no Jaeger..."
$null = Wait-Until "exportacao de traces para o Jaeger" $PollingTimeoutSeconds $PollingIntervalSeconds {
  try {
    $jaegerQuery = $JaegerBaseUrl.TrimEnd("/") + "/api/traces?service=LedgerService.Api&lookback=1h&limit=20"
    $traces = Invoke-RestMethod -UseBasicParsing -Method Get -Uri $jaegerQuery
    return ($traces.data | Measure-Object).Count.ToString()
  }
  catch {
    return "0"
  }
} {
  param($value)
  [int]$value -gt 0
}

try {
  $jaegerQuery = $JaegerBaseUrl.TrimEnd("/") + "/api/traces?service=LedgerService.Api&lookback=1h&limit=20"
  $traces = Invoke-RestMethod -UseBasicParsing -Method Get -Uri $jaegerQuery
  $matchingTrace = $null

  foreach ($trace in $traces.data) {
    foreach ($span in $trace.spans) {
      $operationName = [string]$span.operationName
      $tags = @($span.tags | ForEach-Object { "$($_.key)=$($_.value)" })
      $tagText = $tags -join ";"

      if ($operationName.Contains("POST") -or $tagText.Contains("/api/v1/lancamentos")) {
        $matchingTrace = $trace
        break
      }
    }

    if ($null -ne $matchingTrace) { break }
  }

  if ($null -ne $matchingTrace) {
    Write-Host "Trace encontrado no Jaeger: $($matchingTrace.traceID)"
  } else {
    Write-Warning "Jaeger respondeu, mas nao encontrei trace recente do POST /api/v1/lancamentos. Consulte a UI em $JaegerBaseUrl."
  }
}
catch {
  Write-Warning "Nao foi possivel consultar a API do Jaeger: $($_.Exception.Message)"
}

try {
  $jaegerQuery = $JaegerBaseUrl.TrimEnd("/") + "/api/traces?service=BalanceService.Api&lookback=1h&limit=20"
  $traces = Invoke-RestMethod -UseBasicParsing -Method Get -Uri $jaegerQuery
  $traceCount = ($traces.data | Measure-Object).Count
  Write-Host "Traces recentes do BalanceService.Api no Jaeger: $traceCount"
}
catch {
  Write-Warning "Nao foi possivel consultar traces do BalanceService.Api no Jaeger: $($_.Exception.Message)"
}

try {
  $logLines = & docker compose logs ledger-service --since 10m 2>$null | Select-String -SimpleMatch $correlationId
  if ($logLines) {
    Write-Host "CorrelationId encontrado nos logs do ledger-service."
  } else {
    Write-Warning "Nao encontrei o CorrelationId nos logs recentes do ledger-service. Verifique com: docker compose logs ledger-service --since 10m"
  }
}
catch {
  Write-Warning "Nao foi possivel consultar logs via docker compose: $($_.Exception.Message)"
}

try {
  $logLines = & docker compose logs balance-service --since 10m 2>$null | Select-String -SimpleMatch $correlationId
  if ($logLines) {
    Write-Host "CorrelationId encontrado nos logs do balance-service."
  } else {
    Write-Warning "Nao encontrei o CorrelationId nos logs recentes do balance-service. Verifique com: docker compose logs balance-service --since 10m"
  }
}
catch {
  Write-Warning "Nao foi possivel consultar logs do Balance via docker compose: $($_.Exception.Message)"
}
