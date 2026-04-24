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

# a) gerar env
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\compose-env.ps1") -ComposeFile $ComposeFile -OutFile $EnvFile | Out-Host

# b) obter token (por padrão via localhost conforme README). Pode sobrescrever via env AUTH_BASE_URL.
$token = powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\get-token.ps1")
$token = ($token | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($token)) {
  Write-Error "Falha ao obter TOKEN. Você pode informar manualmente via env TOKEN=..."
  exit 1
}

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

  & nerdctl @args
  if ($LASTEXITCODE -ne 0) { throw "k6 falhou: $LASTEXITCODE" }

  Assert-K6Summary $hostSummary
}

switch ($Mode) {
  "smoke" {
    Run-K6 "ledger_resilience" "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "1"; DURATION = "10s" }
    Run-K6 "balance_daily_50rps" "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "1"; DURATION = "10s"; PREALLOCATED_VUS = "2"; MAX_VUS = "10" }
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
