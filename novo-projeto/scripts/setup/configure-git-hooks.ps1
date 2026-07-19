[CmdletBinding()]
param(
    [switch]$Check,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$expectedHooksPath = ".githooks"
$requiredHooks = @("commit-msg", "post-merge", "pre-push")

$insideWorkTree = & git rev-parse --is-inside-work-tree 2>$null
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Erro: o diretório atual não está dentro de um repositório Git.")
    exit 1
}

$repoRoot = (& git rev-parse --show-toplevel).Trim()
$hooksDir = Join-Path $repoRoot $expectedHooksPath

function Test-Hooks {
    param([bool]$AllowChmod)

    if (-not (Test-Path -LiteralPath $hooksDir -PathType Container)) {
        [Console]::Error.WriteLine("Erro: diretório $expectedHooksPath não encontrado.")
        return $false
    }

    $valid = $true
    foreach ($hook in $requiredHooks) {
        $hookPath = Join-Path $hooksDir $hook
        if (-not (Test-Path -LiteralPath $hookPath -PathType Leaf)) {
            [Console]::Error.WriteLine("Erro: hook obrigatório ausente: $expectedHooksPath/$hook.")
            $valid = $false
            continue
        }

        if (-not $IsWindows -and $AllowChmod) {
            & chmod +x -- $hookPath
            if ($LASTEXITCODE -ne 0) {
                throw "Falha ao aplicar chmod +x em $hookPath."
            }
        }
    }

    return $valid
}

$currentHooksPath = (& git -C $repoRoot config --local --get core.hooksPath 2>$null)
if ($LASTEXITCODE -ne 0) {
    $currentHooksPath = ""
}
$currentHooksPath = $currentHooksPath.Trim()

if ($Check) {
    $valid = Test-Hooks -AllowChmod $false
    if ($currentHooksPath -ne $expectedHooksPath) {
        [Console]::Error.WriteLine("Erro: core.hooksPath está configurado como '$currentHooksPath', não '$expectedHooksPath'.")
        $valid = $false
    }

    if ($valid) {
        Write-Host "OK: hooks configurados em $expectedHooksPath."
        exit 0
    }

    exit 1
}

if (-not (Test-Hooks -AllowChmod $true)) {
    exit 1
}

if (-not [string]::IsNullOrWhiteSpace($currentHooksPath) -and
    $currentHooksPath -ne $expectedHooksPath -and
    -not $Force) {
    [Console]::Error.WriteLine("Erro: core.hooksPath já está configurado como '$currentHooksPath'. Use -Force somente após revisar esse valor.")
    exit 1
}

& git -C $repoRoot config --local core.hooksPath $expectedHooksPath
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Erro: falha ao configurar core.hooksPath.")
    exit 1
}

Write-Host "Git hooks configurados: core.hooksPath=$expectedHooksPath"
