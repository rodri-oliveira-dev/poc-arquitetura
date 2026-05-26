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
$root = (Resolve-Path (Join-Path $scriptDir ".."))
$getTokenScript = Join-Path $scriptDir "get-token.ps1"

function Get-LocalEnvValue([string]$Name) {
  $envPath = Join-Path $root ".env"
  if (-not (Test-Path $envPath)) {
    return ""
  }

  foreach ($line in Get-Content -Path $envPath) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
      continue
    }

    $separatorIndex = $trimmed.IndexOf("=")
    if ($separatorIndex -le 0) {
      continue
    }

    $key = $trimmed.Substring(0, $separatorIndex).Trim()
    if ($key -eq $Name) {
      return $trimmed.Substring($separatorIndex + 1).Trim().Trim('"').Trim("'")
    }
  }

  return ""
}

function Get-LocalConfigValue([string]$Name, [string]$DefaultValue) {
  $value = [System.Environment]::GetEnvironmentVariable($Name, "Process")
  if ([string]::IsNullOrWhiteSpace($value)) {
    $value = Get-LocalEnvValue $Name
  }
  if ([string]::IsNullOrWhiteSpace($value)) {
    return $DefaultValue
  }

  return $value
}

$balanceDbUser = Get-LocalConfigValue "BALANCE_DB_USER" "userBalance"
$balanceDbName = Get-LocalConfigValue "BALANCE_DB_NAME" "dbBalance"

function Invoke-WithEnv([hashtable]$Values, [scriptblock]$Script) {
  $previous = @{}

  foreach ($key in $Values.Keys) {
    $previous[$key] = [System.Environment]::GetEnvironmentVariable($key, "Process")
    [System.Environment]::SetEnvironmentVariable($key, $Values[$key], "Process")
  }

  try {
    & $Script
  }
  finally {
    foreach ($key in $Values.Keys) {
      [System.Environment]::SetEnvironmentVariable($key, $previous[$key], "Process")
    }
  }
}

function Invoke-HttpJson([string]$Method, [string]$Uri, [hashtable]$Headers, [string]$Body) {
  try {
    Invoke-WebRequest -UseBasicParsing -Method $Method -Uri $Uri -Headers $Headers -ContentType "application/json" -Body $Body
  }
  catch {
    if ($_.Exception.Response) {
      return $_.Exception.Response
    }

    throw
  }
}

function Get-HeaderValue($Headers, [string]$Name) {
  if ($null -eq $Headers) { return "" }

  $value = $Headers[$Name]
  if ($value -is [array]) { return ($value -join ",") }
  if ($null -eq $value) { return "" }

  return $value.ToString()
}

function Invoke-DockerComposeText([string[]]$Arguments) {
  $output = & docker @Arguments 2>$null
  if ($LASTEXITCODE -ne 0) {
    throw "docker compose falhou: $($Arguments -join ' ')"
  }

  return ($output -join "`n")
}

function Invoke-PostgresScalar([string]$Service, [string]$User, [string]$Database, [string]$Sql) {
  Invoke-DockerComposeText @(
    "compose", "exec", "-T", $Service,
    "psql", "-U", $User, "-d", $Database,
    "-t", "-A", "-F", "|",
    "-c", $Sql
  )
}

function Wait-Until([string]$Description, [scriptblock]$Probe, [scriptblock]$IsReady) {
  $deadline = [DateTimeOffset]::UtcNow.AddSeconds($PollingTimeoutSeconds)
  $last = ""

  while ([DateTimeOffset]::UtcNow -lt $deadline) {
    $last = & $Probe
    if (& $IsReady $last) {
      return $last
    }

    Start-Sleep -Seconds $PollingIntervalSeconds
  }

  throw "Timeout aguardando $Description. Ultimo resultado: $last"
}

Write-Host "Obtendo token pelo provider local configurado..."
$token = Invoke-WithEnv @{
  AUTH_BASE_URL = $AuthBaseUrl
  USERNAME = $Username
  PASSWORD = $Password
  SCOPE = $Scope
} {
  & $getTokenScript
}

if ([string]::IsNullOrWhiteSpace($token)) {
  throw "Token vazio retornado por $getTokenScript"
}

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

$outboxRow = Wait-Until "Outbox Processed" {
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

$processedRow = Wait-Until "processed_events no Balance" {
  Invoke-PostgresScalar "balance-db" $balanceDbUser $balanceDbName $processedSql
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

$balanceRow = Invoke-PostgresScalar "balance-db" $balanceDbUser $balanceDbName $balanceSql
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
$null = Wait-Until "exportacao de traces para o Jaeger" {
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
