[CmdletBinding()]
param(
  [string]$ComposeFile = "",
  [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir "common.ps1")
$root = Resolve-RepositoryRoot -StartPath $scriptDir

if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
  $ComposeFile = Join-Path $root "compose.yaml"
}

if ([string]::IsNullOrWhiteSpace($OutFile)) {
  $OutFile = Join-Path $root ".env.k6.auto"
}

$outDir = Split-Path -Parent $OutFile
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
  New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

dotnet run --project (Join-Path $root "tools\ComposeEnvGen\ComposeEnvGen.csproj") -- `
  --compose $ComposeFile `
  --out $OutFile
