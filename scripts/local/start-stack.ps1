[CmdletBinding()]
param(
  [string]$ComposeFile = "",
  [string]$OverlayFile = "",
  [ValidateSet("PubSub", "Kafka")]
  [string]$MessagingProvider = "Kafka",
  [switch]$NoBuild,
  [switch]$Observability
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $scriptDir "lib"
if (-not (Test-Path -LiteralPath (Join-Path $libDir "common.ps1") -PathType Leaf)) {
  $libDir = Join-Path $scriptDir "..\lib"
}
. (Join-Path $libDir "common.ps1")
$script:RootDir = Resolve-RepositoryRoot -StartPath $scriptDir
$root = $script:RootDir
$composeObservabilityFile = Join-Path $root "compose.observability.yaml"
$composePubSubFile = Join-Path $root "compose.pubsub.yaml"

$composeEnvFile = Get-ComposeEnvFile
$postgresHostPort = Get-LocalConfigValue "POSTGRES_HOST_PORT" "15432"
$postgresDatabase = "appdb"
$ledgerRuntimePassword = Get-RequiredLocalConfigValue "LEDGER_DB_PASSWORD"
$ledgerMigratorPassword = Get-RequiredLocalConfigValue "LEDGER_DB_MIGRATOR_PASSWORD"
$balanceReadPassword = Get-RequiredLocalConfigValue "BALANCE_DB_READ_PASSWORD"
$balanceWritePassword = Get-RequiredLocalConfigValue "BALANCE_DB_WRITE_PASSWORD"
$balanceMigratorPassword = Get-RequiredLocalConfigValue "BALANCE_DB_MIGRATOR_PASSWORD"
$transferRuntimePassword = Get-RequiredLocalConfigValue "TRANSFER_DB_PASSWORD"
$transferMigratorPassword = Get-RequiredLocalConfigValue "TRANSFER_DB_MIGRATOR_PASSWORD"
$identityRuntimePassword = Get-RequiredLocalConfigValue "IDENTITY_DB_PASSWORD"
$identityMigratorPassword = Get-RequiredLocalConfigValue "IDENTITY_DB_MIGRATOR_PASSWORD"

if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
  $ComposeFile = (Join-Path $root "compose.yaml")
}
if ([string]::IsNullOrWhiteSpace($OverlayFile)) {
  $OverlayFile = if ($MessagingProvider -eq "PubSub") { $composePubSubFile } else { "" }
}

function Invoke-DockerCompose([string[]]$Arguments) {
  & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "docker compose falhou: $LASTEXITCODE"
  }
}

function Get-ComposeArguments {
  $arguments = @("compose")
  if (-not [string]::IsNullOrWhiteSpace($composeEnvFile)) {
    $arguments += @("--env-file", $composeEnvFile)
  }

  $arguments += @("-f", $ComposeFile)
  if (-not [string]::IsNullOrWhiteSpace($OverlayFile)) {
    $arguments += @("-f", $OverlayFile)
  }
  if ($MessagingProvider -eq "PubSub") {
    $arguments += @("--profile", "legacy-pubsub")
  }

  return $arguments
}

function Invoke-Dotnet([string[]]$Arguments) {
  & dotnet @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet falhou: $LASTEXITCODE"
  }
}

function Wait-Database([string]$Service, [string]$User, [string]$Database) {
  $composeArguments = @(Get-ComposeArguments)
  for ($i = 1; $i -le 60; $i++) {
    & docker @composeArguments exec -T $Service pg_isready -U $User -d $Database *> $null
    if ($LASTEXITCODE -eq 0) {
      return
    }

    Start-Sleep -Seconds 2
  }

  throw "Banco indisponivel apos timeout: $Service"
}

function ConvertFrom-SecureStringToPlainText([securestring]$SecureString) {
  $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
  try {
    return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
  }
  finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
  }
}

function ConvertTo-LocalSecureString([string]$Value) {
  $secureString = [securestring]::new()
  foreach ($character in $Value.ToCharArray()) {
    $secureString.AppendChar($character)
  }

  $secureString.MakeReadOnly()
  return $secureString
}

function Assert-DatabaseAuthentication([string]$User, [securestring]$Password) {
  $composeArguments = @(Get-ComposeArguments)
  $plainPassword = ConvertFrom-SecureStringToPlainText $Password
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & docker @composeArguments exec -T `
      -e "PGPASSWORD=$plainPassword" `
      "postgres-db" `
      psql -h postgres-db -U $User -d $postgresDatabase -v "ON_ERROR_STOP=1" -c "select 1;" 1>$null 2>$null
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($exitCode -ne 0) {
    throw @"
Falha de autenticacao no PostgreSQL local para o usuario "$User" e database "$postgresDatabase".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs postgres-db
  docker compose exec -T postgres-db psql -h postgres-db -U "$User" -d "$postgresDatabase" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie somente o volume local do PostgreSQL se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
"@
  }
}

function Invoke-Migration([string]$ConnectionString, [string]$Project, [string]$StartupProject, [string]$DbContext, [string]$ConnectionStringEnvironmentVariable = "ConnectionStrings__DefaultConnection") {
  $previous = [System.Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Process")
  $previousSpecific = [System.Environment]::GetEnvironmentVariable($ConnectionStringEnvironmentVariable, "Process")

  try {
    [System.Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $ConnectionString, "Process")
    [System.Environment]::SetEnvironmentVariable($ConnectionStringEnvironmentVariable, $ConnectionString, "Process")

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
    [System.Environment]::SetEnvironmentVariable($ConnectionStringEnvironmentVariable, $previousSpecific, "Process")
  }
}

Push-Location $root
$previousOtelEnabled = [System.Environment]::GetEnvironmentVariable("OTEL_ENABLED", "Process")
try {
  if ($Observability -and [string]::IsNullOrWhiteSpace($previousOtelEnabled)) {
    [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", "true", "Process")
  }

  Invoke-Dotnet @("tool", "restore")

  $infraArgs = @(Get-ComposeArguments) + @("up", "-d")
  if ($Observability) {
    $infraArgs = @(Get-ComposeArguments) + @("-f", $composeObservabilityFile, "--profile", "observability", "up", "-d")
  }

  if (-not $NoBuild) {
    $infraArgs += "--build"
  }

  $infraArgs += @(
    "postgres-db",
    "keycloak"
  )
  if ($MessagingProvider -eq "PubSub") {
    $infraArgs += @("pubsub-emulator", "pubsub-init")
  }
  else {
    $infraArgs += @("kafka", "kafka-init-topics")
  }
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

  Wait-Database "postgres-db" "postgres_admin" $postgresDatabase
  Assert-DatabaseAuthentication "ledger_app_user" (ConvertTo-LocalSecureString $ledgerRuntimePassword)
  Assert-DatabaseAuthentication "ledger_migrator_user" (ConvertTo-LocalSecureString $ledgerMigratorPassword)
  Assert-DatabaseAuthentication "balance_read_user" (ConvertTo-LocalSecureString $balanceReadPassword)
  Assert-DatabaseAuthentication "balance_write_user" (ConvertTo-LocalSecureString $balanceWritePassword)
  Assert-DatabaseAuthentication "balance_migrator_user" (ConvertTo-LocalSecureString $balanceMigratorPassword)
  Assert-DatabaseAuthentication "transfer_app_user" (ConvertTo-LocalSecureString $transferRuntimePassword)
  Assert-DatabaseAuthentication "transfer_migrator_user" (ConvertTo-LocalSecureString $transferMigratorPassword)
  Assert-DatabaseAuthentication "identity_app_user" (ConvertTo-LocalSecureString $identityRuntimePassword)
  Assert-DatabaseAuthentication "identity_migrator_user" (ConvertTo-LocalSecureString $identityMigratorPassword)

  Invoke-Migration `
    "Host=127.0.0.1;Port=$postgresHostPort;Database=$postgresDatabase;Username=ledger_migrator_user;Password=$ledgerMigratorPassword" `
    "src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj" `
    "src/LedgerService.Api/LedgerService.Api.csproj" `
    "AppDbContext"

  Invoke-Migration `
    "Host=127.0.0.1;Port=$postgresHostPort;Database=$postgresDatabase;Username=balance_migrator_user;Password=$balanceMigratorPassword" `
    "src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj" `
    "src/BalanceService.Api/BalanceService.Api.csproj" `
    "BalanceDbContext"

  Invoke-Migration `
    "Host=127.0.0.1;Port=$postgresHostPort;Database=$postgresDatabase;Username=transfer_migrator_user;Password=$transferMigratorPassword" `
    "src/TransferService.Infrastructure/TransferService.Infrastructure.csproj" `
    "src/TransferService.Api/TransferService.Api.csproj" `
    "TransferServiceDbContext" `
    "TRANSFER_SERVICE_CONNECTION_STRING"

  Invoke-Migration `
    "Host=127.0.0.1;Port=$postgresHostPort;Database=$postgresDatabase;Username=identity_migrator_user;Password=$identityMigratorPassword" `
    "src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj" `
    "src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj" `
    "IdentityDbContext" `
    "IDENTITY_SERVICE_CONNECTION_STRING"

  $apiArgs = @(Get-ComposeArguments) + @("up", "-d")
  if ($Observability) {
    $apiArgs = @(Get-ComposeArguments) + @("-f", $composeObservabilityFile, "--profile", "observability", "up", "-d")
  }

  if (-not $NoBuild) {
    $apiArgs += "--build"
  }

  $apiArgs += @("ledger-service", "ledger-worker", "balance-service", "balance-worker", "transfer-service")
  if ($MessagingProvider -eq "Kafka") {
    $apiArgs += "transfer-worker"
  }
  Invoke-DockerCompose $apiArgs

  Write-Host "OK. Stack local pronta."
}
finally {
  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", $previousOtelEnabled, "Process")
  Pop-Location
}
