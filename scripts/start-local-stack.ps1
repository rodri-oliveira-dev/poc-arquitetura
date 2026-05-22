[CmdletBinding()]
param(
  [string]$ComposeFile = "",
  [switch]$NoBuild,
  [switch]$Observability
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

$postgresPassword = [System.Environment]::GetEnvironmentVariable("POSTGRES_PASSWORD", "Process")
if ([string]::IsNullOrWhiteSpace($postgresPassword)) {
  $postgresPassword = Get-LocalEnvValue "POSTGRES_PASSWORD"
}
if ([string]::IsNullOrWhiteSpace($postgresPassword)) {
  $postgresPassword = "local_dev_password"
}

$balanceDbName = Get-LocalConfigValue "BALANCE_DB_NAME" "dbBalance"
$balanceDbHost = Get-LocalConfigValue "BALANCE_DB_HOST" "balance-db"
$balanceDbUser = Get-LocalConfigValue "BALANCE_DB_USER" "userBalance"
$balanceDbPassword = Get-LocalConfigValue "BALANCE_DB_PASSWORD" "local_dev_password"
$balanceDbHostPort = Get-LocalConfigValue "BALANCE_DB_HOST_PORT" "15433"

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

function Assert-BalanceDatabaseAuthentication {
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & docker compose -f $ComposeFile exec -T `
      -e "PGPASSWORD=$balanceDbPassword" `
      "balance-db" `
      psql -h $balanceDbHost -U $balanceDbUser -d $balanceDbName -v "ON_ERROR_STOP=1" -c "select 1;" 1>$null 2>$null
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($exitCode -ne 0) {
    throw @"
Falha de autenticacao no banco Balance para o usuario "$balanceDbUser" e database "$balanceDbName".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs balance-db
  docker compose logs balance-service
  docker compose exec -T balance-db psql -h "$balanceDbHost" -U "$balanceDbUser" -d "$balanceDbName" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie somente o volume local do Balance se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
"@
  }
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
$previousOtelEnabled = [System.Environment]::GetEnvironmentVariable("OTEL_ENABLED", "Process")
try {
  if ($Observability -and [string]::IsNullOrWhiteSpace($previousOtelEnabled)) {
    [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", "true", "Process")
  }

  Invoke-Dotnet @("tool", "restore")

  $infraArgs = @("compose", "-f", $ComposeFile, "up", "-d")
  if ($Observability) {
    $infraArgs = @("compose", "-f", $ComposeFile, "--profile", "observability", "up", "-d")
  }

  if (-not $NoBuild) {
    $infraArgs += "--build"
  }

  $infraArgs += @(
    "ledger-db",
    "balance-db",
    "kafka",
    "kafka-init-topics",
    "auth-api"
  )
  if ($Observability) {
    $infraArgs += @(
      "jaeger",
      "otel-collector",
      "prometheus",
      "alertmanager",
      "loki",
      "alloy",
      "grafana"
    )
  }
  Invoke-DockerCompose $infraArgs

  Wait-Database "ledger-db" "appuser" "appdb"
  Wait-Database "balance-db" $balanceDbUser $balanceDbName
  Assert-BalanceDatabaseAuthentication

  Invoke-Migration `
    "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=$postgresPassword" `
    "src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj" `
    "src/LedgerService.Api/LedgerService.Api.csproj" `
    "AppDbContext"

  Invoke-Migration `
    "Host=127.0.0.1;Port=$balanceDbHostPort;Database=$balanceDbName;Username=$balanceDbUser;Password=$balanceDbPassword" `
    "src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj" `
    "src/BalanceService.Api/BalanceService.Api.csproj" `
    "BalanceDbContext"

  $apiArgs = @("compose", "-f", $ComposeFile, "up", "-d")
  if ($Observability) {
    $apiArgs = @("compose", "-f", $ComposeFile, "--profile", "observability", "up", "-d")
  }

  if (-not $NoBuild) {
    $apiArgs += "--build"
  }

  $apiArgs += @("ledger-service", "ledger-worker", "balance-service", "balance-worker")
  Invoke-DockerCompose $apiArgs

  Write-Host "OK. Stack local pronta."
}
finally {
  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", $previousOtelEnabled, "Process")
  Pop-Location
}
