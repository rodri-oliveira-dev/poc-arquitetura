[CmdletBinding()]
param(
  [string]$Configuration = "",
  [string]$Framework = "",
  [string]$Document = "",
  [string]$OutputDir = "",
  [string]$Service = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $scriptDir "lib"
if (-not (Test-Path -LiteralPath (Join-Path $libDir "common.ps1") -PathType Leaf)) {
  $libDir = Join-Path $scriptDir "..\..\lib"
}
. (Join-Path $libDir "common.ps1")
$script:RootDir = Resolve-RepositoryRoot -StartPath $scriptDir
$root = $script:RootDir

if ([string]::IsNullOrWhiteSpace($Configuration)) {
  $Configuration = [System.Environment]::GetEnvironmentVariable("CONFIGURATION", "Process")
}
if ([string]::IsNullOrWhiteSpace($Configuration)) { $Configuration = "Release" }

if ([string]::IsNullOrWhiteSpace($Framework)) {
  $Framework = [System.Environment]::GetEnvironmentVariable("FRAMEWORK", "Process")
}
if ([string]::IsNullOrWhiteSpace($Framework)) { $Framework = "net10.0" }

if ([string]::IsNullOrWhiteSpace($Document)) {
  $Document = [System.Environment]::GetEnvironmentVariable("SWAGGER_DOCUMENT", "Process")
}
if ([string]::IsNullOrWhiteSpace($Document)) { $Document = "v1" }

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = [System.Environment]::GetEnvironmentVariable("OUTPUT_DIR", "Process")
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $root "docs\openapi"
}

function Invoke-OpenApiGeneration {
  param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,

    [Parameter(Mandatory = $true)]
    [string]$AssemblyName,

    [Parameter(Mandatory = $true)]
    [string]$OutputName
  )

  $assemblyPath = Join-Path $root "src\$ServiceName\bin\$Configuration\$Framework\$AssemblyName.dll"
  $outputPath = Join-Path $OutputDir $OutputName

  if (-not (Test-Path -Path $assemblyPath -PathType Leaf)) {
    $projectPath = "./src/$ServiceName/$AssemblyName.csproj".Replace("\", "/")
    [Console]::Error.WriteLine(@"
Assembly esperado nao encontrado:
  $assemblyPath

Execute antes:
  dotnet build $projectPath --configuration $Configuration --no-restore
"@)
    exit 1
  }

  Write-Output "Gerando $outputPath"
  & dotnet tool run swagger -- tofile --output $outputPath $assemblyPath $Document
  if ($LASTEXITCODE -ne 0) {
    throw "Falha ao gerar contrato OpenAPI para $ServiceName."
  }

  Normalize-OpenApiContract $outputPath
}

function Invoke-SelectedOpenApiGeneration {
  param(
    [Parameter(Mandatory = $true)]
    [string]$SelectedService
  )

  switch ($SelectedService.ToLowerInvariant()) {
    "" {
      Invoke-OpenApiGeneration "ledger\LedgerService.Api" "LedgerService.Api" "ledger.v1.json"
      Invoke-OpenApiGeneration "balance\BalanceService.Api" "BalanceService.Api" "balance.v1.json"
      Invoke-OpenApiGeneration "transfer\TransferService.Api" "TransferService.Api" "transfer.v1.json"
      Invoke-OpenApiGeneration "identity\IdentityService.Api" "IdentityService.Api" "identity.v1.json"
      Invoke-OpenApiGeneration "audit\AuditService.Api" "AuditService.Api" "audit.v1.json"
    }
    "ledger" { Invoke-OpenApiGeneration "ledger\LedgerService.Api" "LedgerService.Api" "ledger.v1.json" }
    "balance" { Invoke-OpenApiGeneration "balance\BalanceService.Api" "BalanceService.Api" "balance.v1.json" }
    "transfer" { Invoke-OpenApiGeneration "transfer\TransferService.Api" "TransferService.Api" "transfer.v1.json" }
    "identity" { Invoke-OpenApiGeneration "identity\IdentityService.Api" "IdentityService.Api" "identity.v1.json" }
    "audit" { Invoke-OpenApiGeneration "audit\AuditService.Api" "AuditService.Api" "audit.v1.json" }
    default {
      throw "Servico OpenAPI desconhecido '$SelectedService'. Valores aceitos: ledger, balance, transfer, identity, audit."
    }
  }
}

function Normalize-OpenApiContract {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  $content = [System.IO.File]::ReadAllText($Path)
  $normalized = $content.Replace('\r\n', '\n')
  $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
  [System.IO.File]::WriteAllText($Path, $normalized, $utf8NoBom)
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$previousAspNetCoreEnvironment = [System.Environment]::GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Process")
$previousOpenApiGeneration = [System.Environment]::GetEnvironmentVariable("OPENAPI_GENERATION", "Process")
$previousSwaggerEnabled = [System.Environment]::GetEnvironmentVariable("Swagger__Enabled", "Process")
$openApiEnvironmentDefaults = @{
  "Jwt__Issuer" = "https://openapi.local"
  "Jwt__Audience" = "openapi-generation"
  "Jwt__JwksUrl" = "https://openapi.local/.well-known/jwks.json"
  "Jwt__RequireHttpsMetadata" = "true"
  "ConnectionStrings__DefaultConnection" = "Host=localhost;Database=openapi;Username=openapi;Password=openapi"
  "ApiLimits__MaxRequestBodySizeBytes" = "1048576"
  "ApiLimits__MaxBalancePeriodDays" = "31"
  "ApiLimits__RateLimitPermitLimit" = "100"
  "ApiLimits__RateLimitWindowSeconds" = "60"
  "ApiLimits__RateLimitQueueLimit" = "10"
}
$previousOpenApiEnvironmentValues = @{}

try {
  if ([string]::IsNullOrWhiteSpace($previousAspNetCoreEnvironment)) {
    [System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "OpenApi", "Process")
  }
  if ([string]::IsNullOrWhiteSpace($previousOpenApiGeneration)) {
    [System.Environment]::SetEnvironmentVariable("OPENAPI_GENERATION", "true", "Process")
  }
  if ([string]::IsNullOrWhiteSpace($previousSwaggerEnabled)) {
    [System.Environment]::SetEnvironmentVariable("Swagger__Enabled", "true", "Process")
  }
  foreach ($name in $openApiEnvironmentDefaults.Keys) {
    $previousOpenApiEnvironmentValues[$name] = [System.Environment]::GetEnvironmentVariable($name, "Process")
    if ([string]::IsNullOrWhiteSpace($previousOpenApiEnvironmentValues[$name])) {
      [System.Environment]::SetEnvironmentVariable($name, $openApiEnvironmentDefaults[$name], "Process")
    }
  }

  Invoke-SelectedOpenApiGeneration $Service
}
finally {
  [System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", $previousAspNetCoreEnvironment, "Process")
  [System.Environment]::SetEnvironmentVariable("OPENAPI_GENERATION", $previousOpenApiGeneration, "Process")
  [System.Environment]::SetEnvironmentVariable("Swagger__Enabled", $previousSwaggerEnabled, "Process")
  foreach ($name in $previousOpenApiEnvironmentValues.Keys) {
    [System.Environment]::SetEnvironmentVariable($name, $previousOpenApiEnvironmentValues[$name], "Process")
  }
}

Write-Output "Contratos OpenAPI gerados em: $OutputDir"
