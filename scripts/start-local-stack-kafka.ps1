[CmdletBinding()]
param(
  [switch]$NoBuild,
  [switch]$Observability
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$startLocalScript = Join-Path $scriptDir "start-local-stack.ps1"

$arguments = @("-MessagingProvider", "Kafka")
if ($NoBuild) {
  $arguments += "-NoBuild"
}
if ($Observability) {
  $arguments += "-Observability"
}

& $startLocalScript @arguments
if ($LASTEXITCODE -ne 0) {
  throw "start-local-stack.ps1 falhou: $LASTEXITCODE"
}
