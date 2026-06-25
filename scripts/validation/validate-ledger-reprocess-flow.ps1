[CmdletBinding()]
param(
  [string]$AuthBaseUrl = "http://localhost:5030",
  [string]$LedgerBaseUrl = "http://localhost:5226",
  [string]$BalanceBaseUrl = "http://localhost:5228",
  [string]$JaegerBaseUrl = "http://localhost:16686",
  [string]$Username = "local_user",
  [string]$Password = "",
  [string]$Scope = "ledger.write balance.read",
  [string]$MerchantId = "tese",
  [ValidateSet("CREDIT", "DEBIT")]
  [string]$Type = "CREDIT",
  [decimal]$Amount = 10.00,
  [int]$PollingTimeoutSeconds = 60,
  [int]$PollingIntervalSeconds = 2
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir "..\lib\common-validation.ps1")

if ($Type -eq "CREDIT" -and $Amount -le 0) {
  throw "Para CREDIT, Amount deve ser maior que zero."
}

if ($Type -eq "DEBIT" -and $Amount -ge 0) {
  throw "Para DEBIT, Amount deve ser menor que zero."
}

$credential = [pscredential]::new($Username, (ConvertTo-LocalSecureString $Password))
$token = Get-ValidationToken $AuthBaseUrl $credential $Scope

$correlationId = [Guid]::NewGuid().ToString()
$createIdempotencyKey = [Guid]::NewGuid().ToString()
$reprocessIdempotencyKey = [Guid]::NewGuid().ToString()
$externalReference = "local-reprocess-base-$createIdempotencyKey"

$created = New-ValidationLedgerEntry `
  -LedgerBaseUrl $LedgerBaseUrl `
  -Token $token `
  -MerchantId $MerchantId `
  -Type $Type `
  -Amount $Amount `
  -Description "Validacao local de fluxo de reprocessamento" `
  -ExternalReference $externalReference `
  -CorrelationId $correlationId `
  -IdempotencyKey $createIdempotencyKey

$createdEventId = [string]$created.id
$occurredAt = [DateTimeOffset]::Parse([string]$created.occurredAt, [Globalization.CultureInfo]::InvariantCulture)
$balanceDate = $occurredAt.ToString("yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture)

Write-Host "Lancamento base criado: $createdEventId"
Write-Host "Data do consolidado: $balanceDate"

$baseLedgerEntryId = Wait-LedgerEntryInternalId $externalReference $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Lancamento base interno: $baseLedgerEntryId"

Write-Host "Aguardando fluxo normal do lancamento base chegar ao Balance..."
$baseOutboxRow = Wait-OutboxProcessedByCorrelationAndEvent $correlationId "LedgerEntryCreated.v1" $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Outbox base: $baseOutboxRow"

$baseProcessedRow = Wait-BalanceProcessedEvent $createdEventId $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Processed event base: $baseProcessedRow"

$processedCountBefore = Get-BalanceProcessedEventCount $createdEventId
$balanceBefore = Get-DailyBalanceRow $MerchantId $balanceDate
Write-Host "Processed event count antes do reprocessamento: $processedCountBefore"
Write-Host "Daily balance antes do reprocessamento: $balanceBefore"

$reprocessUrl = $LedgerBaseUrl.TrimEnd("/") + "/api/v1/lancamentos/reprocessar"
$reprocessBody = @{
  merchantId = $MerchantId
  dataInicial = $balanceDate
  dataFinal = $balanceDate
  motivo = "Validacao operacional local de reprocessamento"
} | ConvertTo-Json
$reprocessHeaders = @{
  Authorization = "Bearer $token"
  "Idempotency-Key" = $reprocessIdempotencyKey
  "X-Correlation-Id" = $correlationId
}

Write-Host "Solicitando reprocessamento no LedgerService.Api..."
$reprocessResponse = Invoke-HttpJson "Post" $reprocessUrl $reprocessHeaders $reprocessBody
$reprocessStatusCode = [int]$reprocessResponse.StatusCode
$reprocessCorrelationId = Get-HeaderValue $reprocessResponse.Headers "X-Correlation-Id"

Write-Host "Reprocessamento status HTTP: $reprocessStatusCode"
Write-Host "X-Correlation-Id reprocessamento: $reprocessCorrelationId"

if ($reprocessStatusCode -ne 202) {
  throw "Solicitacao de reprocessamento falhou com HTTP $reprocessStatusCode. Body: $($reprocessResponse.Content)"
}

if ($reprocessCorrelationId -ne $correlationId) {
  throw "Ledger nao preservou o X-Correlation-Id na solicitacao de reprocessamento."
}

$reprocessBodyResponse = $reprocessResponse.Content | ConvertFrom-Json
$reprocessamentoId = [string]$reprocessBodyResponse.reprocessamentoId
Write-Host "Solicitacao de reprocessamento criada: $reprocessamentoId"

$reprocessSql = @"
SELECT id, merchant_id, data_inicial, data_final, status, correlation_id
FROM reprocessamentos_lancamentos
WHERE id = '$reprocessamentoId'
LIMIT 1;
"@

$reprocessCreatedRow = Wait-Until "registro em reprocessamentos_lancamentos" $PollingTimeoutSeconds $PollingIntervalSeconds {
  Invoke-PostgresScalar $script:PostgresService $script:LedgerDbUser $script:LedgerDbName $reprocessSql
} {
  param($value)
  $value -match [Regex]::Escape($reprocessamentoId)
}
Write-Host "Reprocessamento registrado: $reprocessCreatedRow"

Write-Host "Aguardando evento operacional de reprocessamento chegar a Processed..."
$reprocessRequestedOutbox = Wait-OutboxProcessedByAggregateAndEvent $reprocessamentoId "ReprocessamentoLancamentosSolicitado.v1" $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Outbox solicitacao reprocessamento: $reprocessRequestedOutbox"

Write-Host "Aguardando processamento assincrono do reprocessamento..."
$reprocessCompletedRow = Wait-Until "reprocessamento em estado final" $PollingTimeoutSeconds $PollingIntervalSeconds {
  Invoke-PostgresScalar $script:PostgresService $script:LedgerDbUser $script:LedgerDbName $reprocessSql
} {
  param($value)
  $value -match '\|Completed\|' -or $value -match '\|CompletedWithWarnings\|'
}
Write-Host "Reprocessamento final: $reprocessCompletedRow"

if ($reprocessCompletedRow -match '\|CompletedWithWarnings\|') {
  throw "Reprocessamento concluiu com warnings, mas o cenario criou lancamento elegivel. Linha: $reprocessCompletedRow"
}

Write-Host "Aguardando evento financeiro republicado chegar a Processed..."
$reprocessedOutboxCount = Wait-OutboxProcessedCountByAggregateAndEvent $baseLedgerEntryId "LedgerEntryCreated.v1" 2 $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Quantidade de eventos financeiros Processed para o lancamento base: $reprocessedOutboxCount"

$processedCountAfter = Get-BalanceProcessedEventCount $createdEventId
Write-Host "Processed event count depois do reprocessamento: $processedCountAfter"

if ($processedCountBefore -ne 1) {
  throw "Estado inicial inesperado: processed_events deveria ter 1 linha para $createdEventId, mas tem $processedCountBefore."
}

if ($processedCountAfter -ne 1) {
  throw "Idempotencia do Balance falhou: processed_events deveria continuar com 1 linha para $createdEventId, mas tem $processedCountAfter."
}

$balanceAfter = Get-DailyBalanceRow $MerchantId $balanceDate
Write-Host "Daily balance depois do reprocessamento: $balanceAfter"

if ($balanceBefore.Trim() -ne $balanceAfter.Trim()) {
  throw "Consolidado mudou apos reprocessamento duplicado. Antes: $balanceBefore Depois: $balanceAfter"
}

$null = Assert-BalanceApi $BalanceBaseUrl $token $MerchantId $balanceDate $correlationId

Assert-JaegerHasRecentTraces $JaegerBaseUrl "LedgerService.Api" $PollingTimeoutSeconds $PollingIntervalSeconds
Assert-JaegerHasRecentTraces $JaegerBaseUrl "BalanceService.Api" $PollingTimeoutSeconds $PollingIntervalSeconds
Assert-RecentLogsContainCorrelationId "ledger-service" $correlationId
Assert-RecentLogsContainCorrelationId "balance-service" $correlationId

Write-Host "Fluxo de reprocessamento validado com sucesso."



