$ErrorActionPreference = "Stop"
$target = Join-Path $PSScriptRoot "local\generate-certs.ps1"
& $target @args
if ($global:LASTEXITCODE -is [int] -and $global:LASTEXITCODE -ne 0) { exit $global:LASTEXITCODE }
