[CmdletBinding()]
param(
  [string]$ComposeFile = "",
  [string]$OutFile = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))

if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
  $ComposeFile = (Join-Path $root "compose.yaml")
}

if ([string]::IsNullOrWhiteSpace($OutFile)) {
  $OutFile = (Join-Path $root ".env.k6.auto")
}

$outDir = Split-Path -Parent $OutFile
if (-not [string]::IsNullOrWhiteSpace($outDir) -and (Test-Path $outDir) -eq $false) {
  New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

dotnet run --project (Join-Path $root "tools\ComposeEnvGen\ComposeEnvGen.csproj") -- `
  --compose $ComposeFile `
  --out $OutFile
