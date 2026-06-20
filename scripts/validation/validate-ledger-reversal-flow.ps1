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

$token = Get-ValidationToken $AuthBaseUrl $Username $Password $Scope

$correlationId = [Guid]::NewGuid().ToString()
$createIdempotencyKey = [Guid]::NewGuid().ToString()
$reversalIdempotencyKey = [Guid]::NewGuid().ToString()
$externalReference = "local-reversal-base-$createIdempotencyKey"

$created = New-ValidationLedgerEntry `
  -LedgerBaseUrl $LedgerBaseUrl `
  -Token $token `
  -MerchantId $MerchantId `
  -Type $Type `
  -Amount $Amount `
  -Description "Validacao local de fluxo de estorno" `
  -ExternalReference $externalReference `
  -CorrelationId $correlationId `
  -IdempotencyKey $createIdempotencyKey

$createdEventId = [string]$created.id
$occurredAt = [DateTimeOffset]::Parse([string]$created.occurredAt, [Globalization.CultureInfo]::InvariantCulture)
$balanceDate = $occurredAt.ToString("yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture)

Write-Host "Lancamento base criado: $createdEventId"
Write-Host "Data do consolidado: $balanceDate"

$originalLedgerEntryId = Wait-LedgerEntryInternalId $externalReference $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Lancamento base interno: $originalLedgerEntryId"

Write-Host "Aguardando fluxo normal do lancamento base chegar ao Balance..."
$baseOutboxRow = Wait-OutboxProcessedByCorrelationAndEvent $correlationId "LedgerEntryCreated.v1" $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Outbox base: $baseOutboxRow"

$baseProcessedRow = Wait-BalanceProcessedEvent $createdEventId $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Processed event base: $baseProcessedRow"

$balanceBefore = Get-DailyBalanceRow $MerchantId $balanceDate
Write-Host "Daily balance antes do estorno: $balanceBefore"

$reversalUrl = $LedgerBaseUrl.TrimEnd("/") + "/api/v1/lancamentos/$originalLedgerEntryId/estornos"
$reversalBody = @{
  motivo = "Validacao operacional local de estorno"
} | ConvertTo-Json
$reversalHeaders = @{
  Authorization = "Bearer $token"
  "Idempotency-Key" = $reversalIdempotencyKey
  "X-Correlation-Id" = $correlationId
}

Write-Host "Solicitando estorno no LedgerService.Api..."
$reversalResponse = Invoke-HttpJson "Post" $reversalUrl $reversalHeaders $reversalBody
$reversalStatusCode = [int]$reversalResponse.StatusCode
$reversalCorrelationId = Get-HeaderValue $reversalResponse.Headers "X-Correlation-Id"

Write-Host "Estorno status HTTP: $reversalStatusCode"
Write-Host "X-Correlation-Id estorno: $reversalCorrelationId"

if ($reversalStatusCode -ne 202) {
  throw "Solicitacao de estorno falhou com HTTP $reversalStatusCode. Body: $($reversalResponse.Content)"
}

if ($reversalCorrelationId -ne $correlationId) {
  throw "Ledger nao preservou o X-Correlation-Id na solicitacao de estorno."
}

$reversalBodyResponse = $reversalResponse.Content | ConvertFrom-Json
$estornoId = [string]$reversalBodyResponse.estornoId
Write-Host "Solicitacao de estorno criada: $estornoId"

$estornoSql = @"
SELECT id, lancamento_original_id, merchant_id, status, lancamento_compensatorio_id, correlation_id
FROM estornos_lancamentos
WHERE id = '$estornoId'
LIMIT 1;
"@

$estornoCreatedRow = Wait-Until "registro em estornos_lancamentos" $PollingTimeoutSeconds $PollingIntervalSeconds {
  Invoke-PostgresScalar $script:PostgresService $script:LedgerDbUser $script:LedgerDbName $estornoSql
} {
  param($value)
  $value -match [Regex]::Escape($estornoId)
}
Write-Host "Estorno registrado: $estornoCreatedRow"

Write-Host "Aguardando evento operacional de solicitacao de estorno chegar a Processed..."
$reversalRequestedOutbox = Wait-OutboxProcessedByAggregateAndEvent $estornoId "LancamentoEstornoSolicitado.v1" $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Outbox solicitacao estorno: $reversalRequestedOutbox"

Write-Host "Aguardando processamento assincrono do estorno..."
$estornoCompletedRow = Wait-Until "estorno em estado final" $PollingTimeoutSeconds $PollingIntervalSeconds {
  Invoke-PostgresScalar $script:PostgresService $script:LedgerDbUser $script:LedgerDbName $estornoSql
} {
  param($value)
  $value -match '\|Completed\|'
}
Write-Host "Estorno final: $estornoCompletedRow"

$estornoParts = $estornoCompletedRow.Trim().Split("|")
$compensatingLedgerEntryId = $estornoParts[4]
if ([string]::IsNullOrWhiteSpace($compensatingLedgerEntryId)) {
  throw "Estorno terminou sem lancamento_compensatorio_id."
}

$compensatingEventId = Convert-ToLedgerEventId $compensatingLedgerEntryId
Write-Host "Lancamento compensatorio interno: $compensatingLedgerEntryId"
Write-Host "Evento financeiro compensatorio: $compensatingEventId"

$compensatingSql = @"
SELECT id, merchant_id, type, amount, external_reference, correlation_id
FROM ledger_entries
WHERE id = '$compensatingLedgerEntryId'
LIMIT 1;
"@
$compensatingRow = Invoke-PostgresScalar $script:PostgresService $script:LedgerDbUser $script:LedgerDbName $compensatingSql
Write-Host "Lancamento compensatorio: $compensatingRow"

$expectedCompensatingReference = "estorno:$($originalLedgerEntryId.Replace('-', ''))"
if ($compensatingRow -notmatch [Regex]::Escape($expectedCompensatingReference)) {
  throw "Lancamento compensatorio nao usa external_reference esperada $expectedCompensatingReference"
}

Write-Host "Aguardando evento financeiro final do compensatorio chegar a Processed..."
$compensatingOutbox = Wait-OutboxProcessedByAggregateAndEvent $compensatingLedgerEntryId "LedgerEntryCreated.v1" $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Outbox compensatorio: $compensatingOutbox"

Write-Host "Aguardando Balance processar evento compensatorio..."
$compensatingProcessed = Wait-BalanceProcessedEvent $compensatingEventId $PollingTimeoutSeconds $PollingIntervalSeconds
Write-Host "Processed event compensatorio: $compensatingProcessed"

$balanceAfter = Get-DailyBalanceRow $MerchantId $balanceDate
Write-Host "Daily balance depois do estorno: $balanceAfter"

$balanceApi = Assert-BalanceApi $BalanceBaseUrl $token $MerchantId $balanceDate $correlationId
$expectedNetBalance = (Get-DailyBalanceNet $balanceBefore) - $Amount
$actualNetBalance = [decimal]::Parse([string]$balanceApi.netBalance, [Globalization.CultureInfo]::InvariantCulture)
if ($actualNetBalance -ne $expectedNetBalance) {
  throw "Saldo liquido inesperado apos estorno. Esperado: $expectedNetBalance. Retornado: $actualNetBalance"
}

Assert-JaegerHasRecentTraces $JaegerBaseUrl "LedgerService.Api" $PollingTimeoutSeconds $PollingIntervalSeconds
Assert-JaegerHasRecentTraces $JaegerBaseUrl "BalanceService.Api" $PollingTimeoutSeconds $PollingIntervalSeconds
Assert-RecentLogsContainCorrelationId "ledger-service" $correlationId
Assert-RecentLogsContainCorrelationId "balance-service" $correlationId

Write-Host "Fluxo de estorno validado com sucesso."



