param(
  [string]$Configuration = "Release",
  [int]$Threshold = 85
)

$ErrorActionPreference = "Stop"

Write-Host "==> Running tests with coverage gate (line >= $Threshold%)" -ForegroundColor Cyan

$resultsDir = Join-Path $PSScriptRoot "TestResults"
if (Test-Path $resultsDir) {
  Remove-Item -Recurse -Force $resultsDir
}
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

# MSBuild usa ';' como separador interno de propriedades em /p:.
# Para evitar que o valor seja quebrado, usamos ';' URL-encoded (%3B).
dotnet test .\LedgerService.slnx -c $Configuration `
  --collect:"XPlat Code Coverage" `
  --settings .\coverlet.runsettings `
  --results-directory $resultsDir

# Consolida e calcula coverage global via ReportGenerator
dotnet tool restore | Out-Null

$reportDir = Join-Path $resultsDir "coverage-report"
dotnet tool run reportgenerator `
  -reports:"$resultsDir\**\coverage.cobertura.xml" `
  -targetdir:"$reportDir" `
  -reporttypes:"JsonSummary;TextSummary" | Out-Null

$summaryJson = Join-Path $reportDir "Summary.json"
if (!(Test-Path $summaryJson)) {
  throw "Não foi possível encontrar $summaryJson (ReportGenerator)."
}

$summary = Get-Content $summaryJson -Raw | ConvertFrom-Json
$lineCoverage = [double]$summary.summary.linecoverage
Write-Host ("==> Global line coverage: {0:N2}%" -f $lineCoverage) -ForegroundColor Cyan

if ($lineCoverage -lt $Threshold) {
  throw ("Coverage abaixo do threshold. Atual={0:N2}% Threshold={1}%" -f $lineCoverage, $Threshold)
}

Write-Host "==> Done" -ForegroundColor Green
