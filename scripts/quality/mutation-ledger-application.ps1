[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$testProjectDir = Join-Path $repoRoot "tests/ledger/LedgerService.UnitTests"

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore falhou: $LASTEXITCODE" }

    Push-Location $testProjectDir
    try {
        dotnet stryker
        if ($LASTEXITCODE -ne 0) { throw "dotnet stryker falhou: $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
