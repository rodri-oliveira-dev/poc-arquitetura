param(
  [string]$Configuration = "Release",
  [int]$Threshold = 80
)

$ErrorActionPreference = "Stop"

Write-Host "==> Running solution tests with coverage gate (line >= $Threshold%)" -ForegroundColor Cyan

$resultsDir = Join-Path $PSScriptRoot "TestResults"
if (Test-Path $resultsDir) {
  Remove-Item -Recurse -Force $resultsDir
}
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

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

function Assert-AssemblyCoverage {
  param(
    [Parameter(Mandatory = $true)]
    [object]$Summary,
    [Parameter(Mandatory = $true)]
    [string]$AssemblyName,
    [Parameter(Mandatory = $true)]
    [int]$Threshold
  )

  $assembly = $Summary.coverage.assemblies | Where-Object { $_.name -eq $AssemblyName } | Select-Object -First 1
  if ($null -eq $assembly) {
    throw "Assembly de cobertura '$AssemblyName' nao encontrado em $summaryJson."
  }

  $coverage = [double]$assembly.coverage
  Write-Host ("==> {0} line coverage: {1:N2}%" -f $AssemblyName, $coverage) -ForegroundColor Cyan

  if ($coverage -lt $Threshold) {
    throw ("Coverage de {0} abaixo do threshold. Atual={1:N2}% Threshold={2}%" -f $AssemblyName, $coverage, $Threshold)
  }
}

Assert-AssemblyCoverage -Summary $summary -AssemblyName "LedgerService.Worker" -Threshold $Threshold
Assert-AssemblyCoverage -Summary $summary -AssemblyName "BalanceService.Worker" -Threshold $Threshold

Write-Host "==> Done" -ForegroundColor Green
