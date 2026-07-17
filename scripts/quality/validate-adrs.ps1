param(
    [string]$AdrsPath = "docs/adrs"
)

$ErrorActionPreference = "Stop"

$allowedStatuses = @(
    "Proposto",
    "Aceito",
    "Rejeitado",
    "Substituido",
    "Parcialmente substituido",
    "Parcialmente implementado"
)

if (-not (Test-Path -LiteralPath $AdrsPath)) {
    throw "Diretorio de ADRs nao encontrado: $AdrsPath"
}

$files = Get-ChildItem -LiteralPath $AdrsPath -Filter "*.md" |
    Where-Object { $_.Name -ne "README.md" }

$errors = New-Object System.Collections.Generic.List[string]

foreach ($file in $files) {
    $status = $null

    if ($file.Name -notmatch '^\d{4}-[a-z0-9]+(?:-[a-z0-9]+)*\.md$') {
        $errors.Add("$($file.Name): nome deve seguir NNNN-titulo-kebab-case.md")
    }

    $content = Get-Content -Raw -LiteralPath $file.FullName
    $statusMatch = [regex]::Match($content, '(?im)^\s*(?:-\s*)?\*\*Status\*\*\s*:\s*(.+?)\s*$|^\s*Status\s*:\s*(.+?)\s*$')
    if ($statusMatch.Success) {
        $status = if ($statusMatch.Groups[1].Success) { $statusMatch.Groups[1].Value } else { $statusMatch.Groups[2].Value }
    }
    else {
        $headingMatch = [regex]::Match($content, '(?ims)^##\s+Status\s*\r?\n\s*(.+?)\s*(?:\r?\n|$)')
        if ($headingMatch.Success) {
            $status = $headingMatch.Groups[1].Value
        }
    }

    if ([string]::IsNullOrWhiteSpace($status)) {
        $errors.Add("$($file.Name): status nao encontrado")
        continue
    }

    $status = $status.Trim().TrimEnd(".")
    $status = [regex]::Replace($status, '\s*\(.+\)\s*$', '')
    if ($allowedStatuses -notcontains $status) {
        $errors.Add("$($file.Name): status '$status' fora do conjunto canonico")
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "ADRs validos: $($files.Count) arquivo(s)."
