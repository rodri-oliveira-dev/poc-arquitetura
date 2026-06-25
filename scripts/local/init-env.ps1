[CmdletBinding()]
param(
  [switch]$Force
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Resolve-Path (Join-Path $scriptDir "..\..")
$examplePath = Join-Path $root ".env.local.example"
$targetPath = Join-Path $root ".env.local"

if (-not (Test-Path $examplePath)) {
  throw "Arquivo .env.local.example nao encontrado."
}

if ((Test-Path $targetPath) -and -not $Force) {
  throw ".env.local ja existe. Use -Force para recriar conscientemente."
}

function New-LocalSecret([string]$Name) {
  $normalized = $Name.ToLowerInvariant() -replace '[^a-z0-9]+', '_'
  return "local_${normalized}_" + [Guid]::NewGuid().ToString("N")
}

$lines = Get-Content -Path $examplePath
$output = foreach ($line in $lines) {
  if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line) -or $line -notmatch '^\s*([^=\s]+)\s*=(.*)$') {
    $line
    continue
  }

  $key = $Matches[1]
  $value = $Matches[2].Trim()
  if ($value -eq "<$key>") {
    "$key=$(New-LocalSecret $key)"
    continue
  }

  $line
}

Set-Content -Path $targetPath -Value $output -Encoding utf8
Write-Host "Arquivo .env.local criado a partir de .env.local.example."
Write-Host "Revise os valores antes de usar em qualquer ambiente compartilhado."
