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

function Run-K6([string]$scriptPath, [hashtable]$envVars) {
  $summary = "/artifacts/summary-$Mode-$ts.json"

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
}

switch ($Mode) {
  "smoke" {
    Run-K6 "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "1"; DURATION = "10s" }
    Run-K6 "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "1"; DURATION = "10s"; PREALLOCATED_VUS = "2"; MAX_VUS = "10" }
  }
  "balance50" {
    Run-K6 "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "50"; DURATION = "1m" }
  }
  "resilience" {
    Run-K6 "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "5"; DURATION = "1m" }
  }
}

Write-Host "OK. Artifacts em: $ArtifactsDir"

# Execução local aqui não consegue (e não deve) validar thresholds (k6 já retorna != 0 em caso de falha).
# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).
