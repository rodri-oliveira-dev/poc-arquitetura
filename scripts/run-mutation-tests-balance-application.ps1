$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$testProjectDir = Join-Path $repoRoot "tests/BalanceService.UnitTests"

Push-Location $repoRoot
try {
    dotnet tool restore

    Push-Location $testProjectDir
    try {
        dotnet stryker
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
