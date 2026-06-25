$ErrorActionPreference = "Stop"
$target = Join-Path $PSScriptRoot "local\start-stack-kafka.ps1"
& $target @args
if ($global:LASTEXITCODE -is [int] -and $global:LASTEXITCODE -ne 0) { exit $global:LASTEXITCODE }
