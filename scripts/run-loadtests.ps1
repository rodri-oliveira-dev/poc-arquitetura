[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("smoke", "balance50", "resilience")]
  [string]$Mode,

  [string]$ComposeFile = "",
  [string]$ComposeK6File = "",

  [string]$ArtifactsDir = "",
  [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))

if ([string]::IsNullOrWhiteSpace($ComposeFile)) { $ComposeFile = (Join-Path $root "compose.yaml") }
if ([string]::IsNullOrWhiteSpace($ComposeK6File)) { $ComposeK6File = (Join-Path $root "compose.k6.yaml") }
if ([string]::IsNullOrWhiteSpace($ArtifactsDir)) { $ArtifactsDir = (Join-Path $root "artifacts\k6") }
if ([string]::IsNullOrWhiteSpace($EnvFile)) { $EnvFile = (Join-Path $root ".env.k6.auto") }

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

function Write-BalanceDatabaseAuthFailure([string]$User, [string]$Database) {
  $hostName = Get-LocalConfigValue "BALANCE_DB_HOST" "balance-db"
  [Console]::Error.WriteLine(@"
Falha de autenticacao no banco Balance para o usuario "$User" e database "$Database".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs balance-db
  docker compose logs balance-service
  docker compose exec -T balance-db psql -h "$hostName" -U "$User" -d "$Database" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie somente o volume local do Balance se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
"@)
}

function Assert-BalanceDatabaseAuthentication {
  $hostName = Get-LocalConfigValue "BALANCE_DB_HOST" "balance-db"
  $user = Get-LocalConfigValue "BALANCE_DB_USER" "userBalance"
  $database = Get-LocalConfigValue "BALANCE_DB_NAME" "dbBalance"
  $password = Get-LocalConfigValue "BALANCE_DB_PASSWORD" "local_dev_password"

  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & docker compose -f $ComposeFile exec -T `
      -e "PGPASSWORD=$password" `
      "balance-db" `
      psql -h $hostName -U $user -d $database -v "ON_ERROR_STOP=1" -c "select 1;" 1>$null 2>$null
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($exitCode -ne 0) {
    Write-BalanceDatabaseAuthFailure $user $database
    exit 1
  }
}

# a) gerar env
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\compose-env.ps1") -ComposeFile $ComposeFile -OutFile $EnvFile | Out-Host

# Aplica o override de carga nas APIs antes de executar o k6. O compose.k6.yaml
# mantem os testes apontando para as APIs HTTP e aumenta apenas limites tecnicos
# que poderiam transformar o cenario de throughput em teste de rate limiting.
& docker compose -f $ComposeFile -f $ComposeK6File up -d --no-build --force-recreate auth-api ledger-service balance-service
if ($LASTEXITCODE -ne 0) { throw "docker compose falhou ao aplicar override k6: $LASTEXITCODE" }

Assert-BalanceDatabaseAuthentication

# b) obter token (por padrão via localhost conforme README). Pode sobrescrever via env AUTH_BASE_URL.
function Get-LoadTestToken {
  $getTokenScript = Join-Path $root "scripts\get-token.ps1"

  for ($i = 1; $i -le 30; $i++) {
    $candidate = powershell -NoProfile -ExecutionPolicy Bypass -File $getTokenScript 2>$null
    if ($LASTEXITCODE -eq 0) {
      $candidate = ($candidate | Out-String).Trim()
      if (-not [string]::IsNullOrWhiteSpace($candidate)) {
        return $candidate
      }
    }

    Start-Sleep -Seconds 2
  }

  $finalAttempt = powershell -NoProfile -ExecutionPolicy Bypass -File $getTokenScript
  return ($finalAttempt | Out-String).Trim()
}

$token = Get-LoadTestToken
if ([string]::IsNullOrWhiteSpace($token)) {
  Write-Error "Falha ao obter TOKEN. Você pode informar manualmente via env TOKEN=..."
  exit 1
}

function Invoke-BalanceWarmup([string]$token) {
  $date = [System.Environment]::GetEnvironmentVariable("DATE")
  if ([string]::IsNullOrWhiteSpace($date)) {
    $date = (Get-Date).ToString("yyyy-MM-dd")
  }

  $merchantId = [System.Environment]::GetEnvironmentVariable("MERCHANT_ID")
  if ([string]::IsNullOrWhiteSpace($merchantId)) {
    $merchantId = "tese"
  }

  $encodedMerchantId = [System.Uri]::EscapeDataString($merchantId)
  $url = "http://localhost:5228/v1/consolidados/diario/${date}?merchantId=$encodedMerchantId"
  $headers = @{ Authorization = "Bearer $token" }

  for ($i = 1; $i -le 30; $i++) {
    try {
      Invoke-WebRequest -Method Get -Uri $url -Headers $headers -UseBasicParsing | Out-Null
      return
    }
    catch {
      if ($i -eq 30) { throw }
      Start-Sleep -Seconds 1
    }
  }
}

Invoke-BalanceWarmup $token

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
$ts = Get-Date -Format "yyyyMMdd-HHmmss"

function Assert-K6Summary([string]$summaryPath) {
  if (-not (Test-Path $summaryPath)) {
    throw "Summary k6 nao encontrado: $summaryPath"
  }

  $summary = Get-Content -Raw -Path $summaryPath | ConvertFrom-Json
  $checksFailed = 0
  $httpFailedRate = 0.0
  $droppedIterations = 0

  if ($summary.metrics.checks -and $null -ne $summary.metrics.checks.fails) {
    $checksFailed = [int]$summary.metrics.checks.fails
  }
  if ($summary.metrics.http_req_failed -and $null -ne $summary.metrics.http_req_failed.value) {
    $httpFailedRate = [double]$summary.metrics.http_req_failed.value
  }
  if ($summary.metrics.dropped_iterations -and $null -ne $summary.metrics.dropped_iterations.count) {
    $droppedIterations = [int]$summary.metrics.dropped_iterations.count
  }

  if ($checksFailed -gt 0 -or $httpFailedRate -gt 0.05 -or $droppedIterations -gt 0) {
    throw "k6 falhou: checks_failed=$checksFailed; http_req_failed=$httpFailedRate; dropped_iterations=$droppedIterations"
  }
}

function Run-K6([string]$scenarioName, [string]$scriptPath, [hashtable]$envVars) {
  $summaryFile = "summary-$Mode-$scenarioName-$ts.json"
  $summary = "/artifacts/$summaryFile"
  $hostSummary = Join-Path $ArtifactsDir $summaryFile

  $args = @(
    "compose",
    "-f", $ComposeFile,
    "-f", $ComposeK6File,
    "--profile", "k6",
    "run",
    "--interactive=false",
    "--rm"
  )

  foreach ($k in $envVars.Keys) {
    $args += "-e"; $args += "$k=$($envVars[$k])"
  }

  $args += @(
    "k6",
    "run",
    $scriptPath,
    "--summary-export", $summary
  )

  & docker @args
  if ($LASTEXITCODE -ne 0) { throw "k6 falhou: $LASTEXITCODE" }

  Assert-K6Summary $hostSummary
}

switch ($Mode) {
  "smoke" {
    Run-K6 "ledger_resilience" "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "1"; DURATION = "10s" }
    Run-K6 "balance_daily_50rps" "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "1"; DURATION = "10s"; PREALLOCATED_VUS = "5"; MAX_VUS = "10" }
  }
  "balance50" {
    Run-K6 "balance_daily_50rps" "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "50"; DURATION = "1m" }
  }
  "resilience" {
    Run-K6 "ledger_resilience" "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "5"; DURATION = "1m" }
  }
}

Write-Host "OK. Artifacts em: $ArtifactsDir"

# Execução local aqui não consegue (e não deve) validar thresholds (k6 já retorna != 0 em caso de falha).
# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).
