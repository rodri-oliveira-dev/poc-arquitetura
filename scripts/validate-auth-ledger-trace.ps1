[CmdletBinding()]
param(
  [string]$AuthBaseUrl = "http://localhost:5030",
  [string]$LedgerBaseUrl = "http://localhost:5226",
  [string]$JaegerBaseUrl = "http://localhost:16686",
  [string]$Username = "poc-usuario",
  [string]$Password = "Poc#123",
  [string]$Scope = "ledger.write",
  [string]$MerchantId = "tese",
  [ValidateSet("CREDIT", "DEBIT")]
  [string]$Type = "CREDIT",
  [decimal]$Amount = 10.00
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$getTokenScript = Join-Path $scriptDir "get-token.ps1"

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

Write-Host "Obtendo token no Auth.Api..."
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

Write-Host "Consultando traces recentes no Jaeger..."
Start-Sleep -Seconds 3

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
