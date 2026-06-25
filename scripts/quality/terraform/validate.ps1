[CmdletBinding()]
param(
    [string] $TerraformRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($TerraformRoot)) {
    $libDir = Join-Path $PSScriptRoot 'lib'
    if (-not (Test-Path -LiteralPath (Join-Path $libDir 'common.ps1') -PathType Leaf)) {
        $libDir = Join-Path $PSScriptRoot '..\..\lib'
    }
    . (Join-Path $libDir 'common.ps1')
    $repoRoot = Resolve-RepositoryRoot -StartPath $PSScriptRoot
    $TerraformRoot = Join-Path $repoRoot 'infra\terraform'
}

if (-not (Test-Path -LiteralPath $TerraformRoot -PathType Container)) {
    Write-Host '==> terraform: diretorio infra/terraform ausente; validacao ignorada'
    exit 0
}

$terraformFiles = @(Get-ChildItem -LiteralPath $TerraformRoot -Filter '*.tf' -File -Recurse)
if ($terraformFiles.Count -eq 0) {
    Write-Host '==> terraform: nenhum arquivo .tf encontrado; validacao ignorada'
    exit 0
}

foreach ($tool in @('terraform', 'tflint')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "terraform: ferramenta obrigatoria ausente: $tool. Consulte docs/development/terraform-gcp-local-setup.md."
    }
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string] $Command,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "terraform: comando falhou: $Command $($Arguments -join ' ')"
    }
}

$resolvedRoot = (Resolve-Path -LiteralPath $TerraformRoot).Path
$terraformDirectories = @($terraformFiles.DirectoryName | Sort-Object -Unique)

Push-Location $resolvedRoot
try {
    Write-Host '==> terraform: verificando formatacao'
    Invoke-CheckedCommand terraform @('fmt', '-check', '-recursive')

    foreach ($directory in $terraformDirectories) {
        Push-Location $directory
        try {
            Write-Host "==> terraform: inicializando sem backend para validacao sintatica em $directory"
            Invoke-CheckedCommand terraform @('init', '-backend=false', '-input=false')

            Write-Host "==> terraform: validando $directory"
            Invoke-CheckedCommand terraform @('validate')
        }
        finally {
            Pop-Location
        }
    }

    Write-Host '==> terraform: executando tflint recursivo'
    Invoke-CheckedCommand tflint @('--recursive')
}
finally {
    Pop-Location
}

Write-Host '==> terraform: validacoes aprovadas'
