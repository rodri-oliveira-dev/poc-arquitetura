[CmdletBinding()]
param(
  [string]$ComposeFile = "",
  [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))

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
      return $trimmed.Substring($separatorIndex + 1).Trim()
    }
  }

  return ""
}

$postgresPassword = [System.Environment]::GetEnvironmentVariable("POSTGRES_PASSWORD", "Process")
if ([string]::IsNullOrWhiteSpace($postgresPassword)) {
  $postgresPassword = Get-LocalEnvValue "POSTGRES_PASSWORD"
}
if ([string]::IsNullOrWhiteSpace($postgresPassword)) {
  $postgresPassword = "local_dev_password"
}

if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
  $ComposeFile = (Join-Path $root "compose.yaml")
}

function Invoke-DockerCompose([string[]]$Arguments) {
  & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "docker compose falhou: $LASTEXITCODE"
  }
}

function Invoke-Dotnet([string[]]$Arguments) {
  & dotnet @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet falhou: $LASTEXITCODE"
  }
}

function Wait-Database([string]$Service, [string]$User, [string]$Database) {
  for ($i = 1; $i -le 60; $i++) {
    & docker compose -f $ComposeFile exec -T $Service pg_isready -U $User -d $Database *> $null
    if ($LASTEXITCODE -eq 0) {
      return
    }

    Start-Sleep -Seconds 2
  }

  throw "Banco indisponivel apos timeout: $Service"
}

function Invoke-Migration([string]$ConnectionString, [string]$Project, [string]$StartupProject, [string]$DbContext) {
  $previous = [System.Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Process")

  try {
    [System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $ConnectionString, "Process")

    Invoke-Dotnet @(
      "tool", "run", "dotnet-ef", "--", "database", "update",
      "-p", $Project,
      "-s", $StartupProject,
      "-c", $DbContext,
      "--", "--environment", "Development"
    )
  }
  finally {
    [System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $previous, "Process")
  }
}

Push-Location $root
try {
  Invoke-Dotnet @("tool", "restore")

  $infraArgs = @("compose", "-f", $ComposeFile, "up", "-d")
  if (-not $NoBuild) {
    $infraArgs += "--build"
  }

  $infraArgs += @(
    "ledger-db",
    "balance-db",
    "kafka",
    "kafka-init-topics",
    "jaeger",
    "otel-collector",
    "prometheus",
    "alertmanager",
    "loki",
    "alloy",
    "grafana",
    "auth-api"
  )
  Invoke-DockerCompose $infraArgs

  Wait-Database "ledger-db" "appuser" "appdb"
  Wait-Database "balance-db" "userBalance" "dbBalance"

  Invoke-Migration `
    "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=$postgresPassword" `
    "src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj" `
    "src/LedgerService.Api/LedgerService.Api.csproj" `
    "AppDbContext"

  Invoke-Migration `
    "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=$postgresPassword" `
    "src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj" `
    "src/BalanceService.Api/BalanceService.Api.csproj" `
    "BalanceDbContext"

  $apiArgs = @("compose", "-f", $ComposeFile, "up", "-d")
  if (-not $NoBuild) {
    $apiArgs += "--build"
  }

  $apiArgs += @("ledger-service", "ledger-worker", "balance-service", "balance-worker")
  Invoke-DockerCompose $apiArgs

  Write-Host "OK. Stack local pronta."
}
finally {
  Pop-Location
}
