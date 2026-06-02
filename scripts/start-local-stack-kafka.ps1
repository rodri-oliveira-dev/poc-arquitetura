[CmdletBinding()]
param(
  [switch]$NoBuild,
  [switch]$Observability
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))
$startLocalScript = Join-Path $scriptDir "start-local-stack.ps1"
$composeKafkaFile = Join-Path $root "compose.kafka.yaml"

$arguments = @("-MessagingProvider", "Kafka", "-OverlayFile", $composeKafkaFile)
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
