[CmdletBinding()]
param(
  [switch]$NoBuild,
  [switch]$SkipHealthChecks
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))
$composeFile = Join-Path $root "compose.yaml"
$composeNginxFile = Join-Path $root "compose.nginx.yaml"
$certFile = Join-Path $root "infra\nginx\certs\localhost.crt"
$keyFile = Join-Path $root "infra\nginx\certs\localhost.key"
$startLocalScript = Join-Path $scriptDir "start-local-stack.ps1"

function Assert-CommandAvailable([string]$CommandName, [string]$InstallHint) {
  if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
    throw "$CommandName nao encontrado. $InstallHint"
  }
}

function Invoke-External([string]$CommandName, [string[]]$Arguments) {
  & $CommandName @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "$CommandName falhou: $LASTEXITCODE"
  }
}

function Assert-DockerComposeAvailable {
  Assert-CommandAvailable "docker" "Instale/configure Docker CLI com suporte a 'docker compose'."
  Invoke-External "docker" @("compose", "version")
}

function Assert-NginxCertificates {
  if ((Test-Path $certFile) -and (Test-Path $keyFile)) {
    return
  }

  throw @"
Certificados locais do Nginx nao encontrados.

Arquivos esperados:
  infra/nginx/certs/localhost.crt
  infra/nginx/certs/localhost.key

Gere os certificados conforme docs/development/local-development.md ou infra/nginx/README.md.
O script nao gera certificados automaticamente.
"@
}

function Invoke-HttpCheck([string]$Name, [string]$Url, [switch]$Insecure) {
  Write-Host "Validando ${Name}: $Url"

  if ($Insecure) {
    if (-not (Get-Command "curl.exe" -ErrorAction SilentlyContinue)) {
      Write-Warning "curl.exe nao encontrado; pulando validacao HTTPS local de $Name."
      return
    }

    & curl.exe -k -fsS --max-time 10 $Url 1>$null
    if ($LASTEXITCODE -ne 0) {
      throw "Health check falhou para $Name em $Url"
    }

    return
  }

  Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10 | Out-Null
}

Push-Location $root
$previousOtelEnabled = [System.Environment]::GetEnvironmentVariable("OTEL_ENABLED", "Process")
try {
  Assert-DockerComposeAvailable
  Assert-CommandAvailable "dotnet" "Instale o .NET SDK definido em global.json."
  Assert-NginxCertificates

  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", "true", "Process")

  $localArgs = @("-File", $startLocalScript, "-Observability")
  if ($NoBuild) {
    $localArgs += "-NoBuild"
  }
  $powershellArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass") + $localArgs
  Invoke-External "powershell" $powershellArgs

  $nginxArgs = @(
    "compose",
    "-f", $composeFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "up",
    "-d"
  )

  if (-not $NoBuild) {
    $nginxArgs += "--build"
  }

  $nginxArgs += @("ledger-service-1", "ledger-service-2", "nginx-edge")
  Invoke-External "docker" $nginxArgs

  Invoke-External "docker" @(
    "compose",
    "-f", $composeFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "ps"
  )

  if (-not $SkipHealthChecks) {
    Invoke-HttpCheck "Auth.Api direta" "http://localhost:5030/health"
    Invoke-HttpCheck "LedgerService.Api direta" "http://localhost:5226/health"
    Invoke-HttpCheck "BalanceService.Api direta" "http://localhost:5228/health"
    Invoke-HttpCheck "Portal Nginx" "https://localhost:7443/" -Insecure
    Invoke-HttpCheck "Ledger via Nginx" "https://ledger.localhost:7443/health" -Insecure
    Invoke-HttpCheck "Balance via Nginx" "https://balance.localhost:7443/health" -Insecure
    Invoke-HttpCheck "Auth via Nginx" "https://auth.localhost:7443/health" -Insecure
    Invoke-HttpCheck "Grafana" "http://localhost:3000/api/health"
    Invoke-HttpCheck "Jaeger" "http://localhost:16686/"
    Invoke-HttpCheck "Prometheus" "http://localhost:9090/-/ready"
    Invoke-HttpCheck "Alertmanager" "http://localhost:9093/-/ready"
  }

  Write-Host ""
  Write-Host "OK. Stack completa local pronta."
  Write-Host ""
  Write-Host "URLs uteis:"
  Write-Host "  Auth.Api:              http://localhost:5030/"
  Write-Host "  LedgerService.Api:     http://localhost:5226/"
  Write-Host "  BalanceService.Api:    http://localhost:5228/"
  Write-Host "  Portal Nginx:          https://localhost:7443/"
  Write-Host "  Ledger Swagger Nginx:  https://ledger.localhost:7443/swagger"
  Write-Host "  Balance Swagger Nginx: https://balance.localhost:7443/swagger"
  Write-Host "  Auth Swagger Nginx:    https://auth.localhost:7443/swagger"
  Write-Host "  Grafana:               http://localhost:3000/"
  Write-Host "  Jaeger:                http://localhost:16686/"
  Write-Host "  Prometheus:            http://localhost:9090/"
  Write-Host "  Alertmanager:          http://localhost:9093/"
  Write-Host ""
  Write-Host "Este script nao remove volumes, nao executa testes, k6 nem scanners."
}
finally {
  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", $previousOtelEnabled, "Process")
  Pop-Location
}
