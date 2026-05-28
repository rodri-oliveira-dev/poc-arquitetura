[CmdletBinding()]
param(
  [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))
$composeFile = Join-Path $root "compose.yaml"
$composeObservabilityFile = Join-Path $root "compose.observability.yaml"
$composeNginxFile = Join-Path $root "compose.nginx.yaml"

function Assert-DockerComposeAvailable {
  if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
    throw "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'."
  }

  & docker compose version 1>$null
  if ($LASTEXITCODE -ne 0) {
    throw "docker compose nao esta disponivel."
  }
}

function Invoke-DockerCompose([string[]]$Arguments) {
  & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "docker compose falhou: $LASTEXITCODE"
  }
}

Push-Location $root
try {
  Assert-DockerComposeAvailable

  Write-Host "Parando overlay Nginx da stack completa..."
  Invoke-DockerCompose @(
    "compose",
    "-f", $composeFile,
    "-f", $composeObservabilityFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "stop",
    "--timeout", $TimeoutSeconds.ToString(),
    "nginx-edge",
    "ledger-service-1",
    "ledger-service-2"
  )

  Write-Host "Parando stack base e observabilidade..."
  Invoke-DockerCompose @(
    "compose",
    "-f", $composeFile,
    "-f", $composeObservabilityFile,
    "--profile", "observability",
    "stop",
    "--timeout", $TimeoutSeconds.ToString()
  )

  Write-Host ""
  Write-Host "OK. Stack completa parada."
  Write-Host "Volumes, bancos locais, imagens e certificados nao foram removidos."
}
finally {
  Pop-Location
}
