[CmdletBinding()]
param(
    [switch]$Check,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$expectedHooksPath = ".githooks"
$requiredHooks = @("commit-msg", "post-merge", "pre-push")

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & git @Arguments 2>$null
    return @{
        ExitCode = $LASTEXITCODE
        Output = ($output -join "`n")
    }
}

$insideWorkTree = Invoke-Git -Arguments @("rev-parse", "--is-inside-work-tree")
if ($insideWorkTree.ExitCode -ne 0) {
    [Console]::Error.WriteLine("Erro: o diretorio atual nao esta dentro de um repositorio Git.")
    exit 1
}

$repoRootResult = Invoke-Git -Arguments @("rev-parse", "--show-toplevel")
if ($repoRootResult.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($repoRootResult.Output)) {
    [Console]::Error.WriteLine("Erro: nao foi possivel identificar a raiz do repositorio Git.")
    exit 1
}

$repoRoot = $repoRootResult.Output.Trim()
$hooksDir = Join-Path $repoRoot $expectedHooksPath

function Test-UnixRuntime {
    return -not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows
    )
}

function Test-ExecutableBit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-UnixRuntime)) {
        return $true
    }

    $mode = (Get-Item -LiteralPath $Path).UnixFileMode
    return (($mode -band [System.IO.UnixFileMode]::UserExecute) -ne 0) -or
        (($mode -band [System.IO.UnixFileMode]::GroupExecute) -ne 0) -or
        (($mode -band [System.IO.UnixFileMode]::OtherExecute) -ne 0)
}

function Set-ExecutableBit {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    & chmod +x -- $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao aplicar chmod +x em $Path."
    }
}

function Test-Hooks {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$AllowChmod
    )

    $hasError = $false

    if (-not (Test-Path -LiteralPath $hooksDir -PathType Container)) {
        [Console]::Error.WriteLine("Erro: diretorio $expectedHooksPath nao encontrado em $repoRoot.")
        return $false
    }

    foreach ($hook in $requiredHooks) {
        $hookPath = Join-Path $hooksDir $hook

        if (-not (Test-Path -LiteralPath $hookPath -PathType Leaf)) {
            [Console]::Error.WriteLine("Erro: hook obrigatorio ausente: $expectedHooksPath/$hook.")
            $hasError = $true
            continue
        }

        if ((Test-UnixRuntime) -and -not (Test-ExecutableBit -Path $hookPath)) {
            if ($AllowChmod) {
                Set-ExecutableBit -Path $hookPath
                Write-Host "Permissao executavel aplicada: $expectedHooksPath/$hook."
            }
            else {
                [Console]::Error.WriteLine("Erro: hook sem bit executavel em Unix: $expectedHooksPath/$hook.")
                [Console]::Error.WriteLine("Execute explicitamente: chmod +x $expectedHooksPath/$hook")
                $hasError = $true
            }
        }
    }

    return -not $hasError
}

$currentHooksPathResult = Invoke-Git -Arguments @("-C", $repoRoot, "config", "--local", "--get", "core.hooksPath")
$currentHooksPath = if ($currentHooksPathResult.ExitCode -eq 0) { $currentHooksPathResult.Output.Trim() } else { "" }

if ($Check) {
    $status = 0

    if ($currentHooksPath -eq $expectedHooksPath) {
        Write-Host "OK: core.hooksPath local esta configurado como $expectedHooksPath."
    }
    elseif ([string]::IsNullOrEmpty($currentHooksPath)) {
        [Console]::Error.WriteLine("Erro: core.hooksPath local nao esta configurado.")
        $status = 1
    }
    else {
        [Console]::Error.WriteLine("Erro: core.hooksPath local esta configurado como '$currentHooksPath', nao '$expectedHooksPath'.")
        $status = 1
    }

    if (-not (Test-Hooks -AllowChmod $false)) {
        $status = 1
    }

    exit $status
}

if (-not (Test-Hooks -AllowChmod $true)) {
    exit 1
}

if ([string]::IsNullOrEmpty($currentHooksPath)) {
    & git -C $repoRoot config --local core.hooksPath $expectedHooksPath
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("Erro: falha ao configurar core.hooksPath local.")
        exit 1
    }

    Write-Host "Git hooks locais configurados com sucesso: core.hooksPath=$expectedHooksPath."
    exit 0
}

if ($currentHooksPath -eq $expectedHooksPath) {
    Write-Host "Git hooks locais ja estao configurados: core.hooksPath=$expectedHooksPath."
    exit 0
}

if (-not $Force) {
    [Console]::Error.WriteLine(@"
Erro: core.hooksPath local ja esta configurado como '$currentHooksPath'.
Sobrescrever esse valor pode desativar hooks pessoais, corporativos ou de outras ferramentas.
Revise a configuracao atual e execute novamente com -Force apenas se quiser substitui-la por '$expectedHooksPath'.
"@)
    exit 1
}

& git -C $repoRoot config --local core.hooksPath $expectedHooksPath
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Erro: falha ao configurar core.hooksPath local.")
    exit 1
}

Write-Host "core.hooksPath local substituido com -Force."
Write-Host "Valor anterior: $currentHooksPath"
Write-Host "Novo valor: $expectedHooksPath"
