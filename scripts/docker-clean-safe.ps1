[CmdletBinding()]
param(
  [switch]$Yes
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))
$composeFile = Join-Path $root "compose.yaml"
$composeObservabilityFile = Join-Path $root "compose.observability.yaml"
$composeKafkaFile = Join-Path $root "compose.kafka.yaml"
$composeNginxFile = Join-Path $root "compose.nginx.yaml"
$composeK6File = Join-Path $root "compose.k6.yaml"
$composeAuthLegacyFile = Join-Path $root "compose.auth-legacy.yaml"
$composeSonarFile = Join-Path $root "compose.sonar.yaml"

function Invoke-Docker([string[]]$Arguments) {
  & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "docker falhou: $($Arguments -join ' ')"
  }
}

function Confirm-Step([string]$Message) {
  if ($Yes) {
    return $true
  }

  $answer = Read-Host "$Message [s/N]"
  return $answer -match "^(s|sim|y|yes)$"
}

if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
  throw "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'."
}

Push-Location $root
try {
  Write-Host "Parando/removendo containers e redes do projeto sem remover volumes..."
  Invoke-Docker @(
    "compose",
    "-f", $composeFile,
    "-f", $composeObservabilityFile,
    "-f", $composeKafkaFile,
    "-f", $composeNginxFile,
    "-f", $composeK6File,
    "-f", $composeAuthLegacyFile,
    "--profile", "observability",
    "--profile", "direct-ledger",
    "--profile", "k6",
    "--profile", "legacy-auth",
    "--profile", "legacy-kafka",
    "down",
    "--remove-orphans"
  )

  Invoke-Docker @(
    "compose",
    "-f", $composeSonarFile,
    "--profile", "quality",
    "down",
    "--remove-orphans"
  )

  if (Confirm-Step "Executar docker builder prune para remover cache de build nao usado?") {
    Invoke-Docker @("builder", "prune", "--force")
  } else {
    Write-Host "docker builder prune ignorado."
  }

  if (Confirm-Step "Executar docker image prune para remover imagens dangling?") {
    Invoke-Docker @("image", "prune", "--force")
  } else {
    Write-Host "docker image prune ignorado."
  }

  Write-Host "OK. Limpeza segura concluida. Volumes Docker foram preservados."
}
finally {
  Pop-Location
}
