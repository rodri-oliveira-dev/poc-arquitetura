[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$script:ValidationScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$script:RootDir = (Resolve-Path (Join-Path $script:ValidationScriptDir ".."))
$script:GetTokenScript = Join-Path $script:ValidationScriptDir "get-token.ps1"

function Get-LocalEnvValue([string]$Name) {
  $envPath = Join-Path $script:RootDir ".env"
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

$script:BalanceDbUser = Get-LocalConfigValue "BALANCE_DB_USER" "userBalance"
$script:BalanceDbName = Get-LocalConfigValue "BALANCE_DB_NAME" "dbBalance"

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

function Get-ValidationToken(
  [string]$AuthBaseUrl,
  [string]$Username,
  [string]$Password,
  [string]$Scope
) {
  Write-Host "Obtendo token no Auth.Api..."
  $token = Invoke-WithEnv @{
    AUTH_BASE_URL = $AuthBaseUrl
    USERNAME = $Username
    PASSWORD = $Password
    SCOPE = $Scope
  } {
    & $script:GetTokenScript
  }

  if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Token vazio retornado por $script:GetTokenScript"
  }

  return $token
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

function Wait-Until(
  [string]$Description,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds,
  [scriptblock]$Probe,
  [scriptblock]$IsReady
) {
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

function Convert-ToLedgerEventId([string]$LedgerEntryGuid) {
  $normalized = $LedgerEntryGuid.Replace("-", "")
  if ($normalized.Length -lt 8) {
    throw "Guid de lancamento invalido para gerar event id: $LedgerEntryGuid"
  }

  return "lan_$($normalized.Substring(0, 8))"
}

function New-ValidationLedgerEntry(
  [string]$LedgerBaseUrl,
  [string]$Token,
  [string]$MerchantId,
  [string]$Type,
  [decimal]$Amount,
  [string]$Description,
  [string]$ExternalReference,
  [string]$CorrelationId,
  [string]$IdempotencyKey
) {
  $ledgerUrl = $LedgerBaseUrl.TrimEnd("/") + "/api/v1/lancamentos"
  $body = @{
    merchantId = $MerchantId
    type = $Type
    amount = $Amount
    description = $Description
    externalReference = $ExternalReference
  } | ConvertTo-Json

  $headers = @{
    Authorization = "Bearer $Token"
    "Idempotency-Key" = $IdempotencyKey
    "X-Correlation-Id" = $CorrelationId
  }

  Write-Host "Criando lancamento base no LedgerService.Api..."
  $response = Invoke-HttpJson "Post" $ledgerUrl $headers $body
  $statusCode = [int]$response.StatusCode
  $responseCorrelationId = Get-HeaderValue $response.Headers "X-Correlation-Id"

  Write-Host "Ledger status HTTP: $statusCode"
  Write-Host "X-Correlation-Id enviado:   $CorrelationId"
  Write-Host "X-Correlation-Id response:  $responseCorrelationId"

  if ($statusCode -ne 201) {
    throw "Criacao de lancamento falhou com HTTP $statusCode. Body: $($response.Content)"
  }

  if ($responseCorrelationId -ne $CorrelationId) {
    throw "Ledger nao preservou o X-Correlation-Id na criacao do lancamento."
  }

  $responseBody = $response.Content | ConvertFrom-Json
  return $responseBody
}

function Wait-LedgerEntryInternalId(
  [string]$ExternalReference,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds
) {
  $sql = @"
SELECT id
FROM ledger_entries
WHERE external_reference = '$ExternalReference'
LIMIT 1;
"@

  $row = Wait-Until "ledger_entries por external_reference=$ExternalReference" $PollingTimeoutSeconds $PollingIntervalSeconds {
    Invoke-PostgresScalar "ledger-db" "appuser" "appdb" $sql
  } {
    param($value)
    -not [string]::IsNullOrWhiteSpace($value)
  }

  return $row.Trim()
}

function Wait-OutboxSentByCorrelationAndEvent(
  [string]$CorrelationId,
  [string]$EventType,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds
) {
  $sql = @"
SELECT id, aggregate_type, aggregate_id, event_type, status, attempts, correlation_id, processed_at
FROM outbox_messages
WHERE correlation_id = '$CorrelationId' AND event_type = '$EventType'
ORDER BY occurred_at DESC
LIMIT 1;
"@

  Wait-Until "Outbox Sent para $EventType" $PollingTimeoutSeconds $PollingIntervalSeconds {
    Invoke-PostgresScalar "ledger-db" "appuser" "appdb" $sql
  } {
    param($value)
    $value -match "\|$([Regex]::Escape($EventType))\|Sent\|"
  }
}

function Wait-OutboxSentByAggregateAndEvent(
  [string]$AggregateId,
  [string]$EventType,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds
) {
  $sql = @"
SELECT id, aggregate_type, aggregate_id, event_type, status, attempts, correlation_id, processed_at
FROM outbox_messages
WHERE aggregate_id = '$AggregateId' AND event_type = '$EventType'
ORDER BY occurred_at DESC
LIMIT 1;
"@

  Wait-Until "Outbox Sent para aggregate_id=$AggregateId event_type=$EventType" $PollingTimeoutSeconds $PollingIntervalSeconds {
    Invoke-PostgresScalar "ledger-db" "appuser" "appdb" $sql
  } {
    param($value)
    $value -match "\|$([Regex]::Escape($AggregateId))\|$([Regex]::Escape($EventType))\|Sent\|"
  }
}

function Wait-OutboxSentCountByAggregateAndEvent(
  [string]$AggregateId,
  [string]$EventType,
  [int]$ExpectedCount,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds
) {
  $sql = @"
SELECT COUNT(*)
FROM outbox_messages
WHERE aggregate_id = '$AggregateId' AND event_type = '$EventType' AND status = 'Sent';
"@

  Wait-Until "Outbox Sent count >= $ExpectedCount para aggregate_id=$AggregateId event_type=$EventType" $PollingTimeoutSeconds $PollingIntervalSeconds {
    Invoke-PostgresScalar "ledger-db" "appuser" "appdb" $sql
  } {
    param($value)
    [int]($value.Trim()) -ge $ExpectedCount
  }
}

function Wait-BalanceProcessedEvent(
  [string]$EventId,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds
) {
  $sql = @"
SELECT event_id, merchant_id, processed_at
FROM processed_events
WHERE event_id = '$EventId'
LIMIT 1;
"@

  Wait-Until "processed_events no Balance para event_id=$EventId" $PollingTimeoutSeconds $PollingIntervalSeconds {
    Invoke-PostgresScalar "balance-db" $script:BalanceDbUser $script:BalanceDbName $sql
  } {
    param($value)
    $value -match [Regex]::Escape($EventId)
  }
}

function Get-BalanceProcessedEventCount([string]$EventId) {
  $sql = @"
SELECT COUNT(*)
FROM processed_events
WHERE event_id = '$EventId';
"@

  $value = Invoke-PostgresScalar "balance-db" $script:BalanceDbUser $script:BalanceDbName $sql
  return [int]($value.Trim())
}

function Get-DailyBalanceRow([string]$MerchantId, [string]$BalanceDate) {
  $sql = @"
SELECT merchant_id, date, currency, total_credits, total_debits, net_balance
FROM daily_balances
WHERE merchant_id = '$MerchantId' AND date = DATE '$BalanceDate'
LIMIT 1;
"@

  Invoke-PostgresScalar "balance-db" $script:BalanceDbUser $script:BalanceDbName $sql
}

function Get-DailyBalanceNet([string]$DailyBalanceRow) {
  if ([string]::IsNullOrWhiteSpace($DailyBalanceRow)) {
    return [decimal]0
  }

  $parts = $DailyBalanceRow.Trim().Split("|")
  if ($parts.Length -lt 6) {
    throw "Linha de daily_balances em formato inesperado: $DailyBalanceRow"
  }

  return [decimal]::Parse($parts[5], [Globalization.CultureInfo]::InvariantCulture)
}

function Assert-BalanceApi(
  [string]$BalanceBaseUrl,
  [string]$Token,
  [string]$MerchantId,
  [string]$BalanceDate,
  [string]$CorrelationId
) {
  Write-Host "Consultando BalanceService.Api..."
  $balanceUrl = $BalanceBaseUrl.TrimEnd("/") + "/v1/consolidados/diario/$BalanceDate" + "?merchantId=$MerchantId"
  $headers = @{
    Authorization = "Bearer $Token"
    "X-Correlation-Id" = $CorrelationId
  }

  $response = Invoke-WebRequest -UseBasicParsing -Method Get -Uri $balanceUrl -Headers $headers
  $statusCode = [int]$response.StatusCode
  $responseCorrelationId = Get-HeaderValue $response.Headers "X-Correlation-Id"

  Write-Host "Balance status HTTP: $statusCode"
  Write-Host "X-Correlation-Id Balance: $responseCorrelationId"
  Write-Host "Balance body: $($response.Content)"

  if ($statusCode -ne 200) {
    throw "Chamada ao Balance falhou com HTTP $statusCode"
  }

  if ($responseCorrelationId -ne $CorrelationId) {
    throw "Balance nao preservou o X-Correlation-Id enviado."
  }

  return ($response.Content | ConvertFrom-Json)
}

function Assert-JaegerHasRecentTraces(
  [string]$JaegerBaseUrl,
  [string]$ServiceName,
  [int]$PollingTimeoutSeconds,
  [int]$PollingIntervalSeconds
) {
  Write-Host "Consultando traces recentes do $ServiceName no Jaeger..."
  $null = Wait-Until "exportacao de traces para o Jaeger ($ServiceName)" $PollingTimeoutSeconds $PollingIntervalSeconds {
    try {
      $query = $JaegerBaseUrl.TrimEnd("/") + "/api/traces?service=$ServiceName&lookback=1h&limit=20"
      $traces = Invoke-RestMethod -UseBasicParsing -Method Get -Uri $query
      return ($traces.data | Measure-Object).Count.ToString()
    }
    catch {
      return "0"
    }
  } {
    param($value)
    [int]$value -gt 0
  }
}

function Assert-RecentLogsContainCorrelationId([string]$Service, [string]$CorrelationId) {
  try {
    $logLines = & docker compose logs $Service --since 10m 2>$null | Select-String -SimpleMatch $CorrelationId
    if ($logLines) {
      Write-Host "CorrelationId encontrado nos logs do $Service."
    } else {
      Write-Warning "Nao encontrei o CorrelationId nos logs recentes do $Service. Verifique com: docker compose logs $Service --since 10m"
    }
  }
  catch {
    Write-Warning "Nao foi possivel consultar logs do $Service via docker compose: $($_.Exception.Message)"
  }
}
