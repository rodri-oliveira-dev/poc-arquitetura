[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Resolve-RepositoryRoot {
  param(
    [string]$StartPath = $PSScriptRoot
  )

  if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitRoot = & git -C $StartPath rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitRoot)) {
      $candidate = (Resolve-Path -LiteralPath ([string]$gitRoot)).Path
      if (Test-Path -LiteralPath (Join-Path $candidate "LedgerService.slnx") -PathType Leaf) {
        return $candidate
      }
    }
  }

  $current = (Resolve-Path -LiteralPath $StartPath).Path
  while (-not [string]::IsNullOrWhiteSpace($current)) {
    if (Test-Path -LiteralPath (Join-Path $current "LedgerService.slnx") -PathType Leaf) {
      return $current
    }

    $parent = Split-Path -Parent $current
    if ($parent -eq $current) { break }
    $current = $parent
  }

  throw "Nao foi possivel resolver a raiz do repositorio a partir de $StartPath."
}

function Find-ScriptsLibDirectory {
  param(
    [string]$StartPath
  )

  $current = (Resolve-Path -LiteralPath $StartPath).Path
  while (-not [string]::IsNullOrWhiteSpace($current)) {
    $repoStyle = Join-Path $current "scripts\lib\common.ps1"
    if (Test-Path -LiteralPath $repoStyle -PathType Leaf) {
      return (Join-Path $current "scripts\lib")
    }

    $localStyle = Join-Path $current "lib\common.ps1"
    if (Test-Path -LiteralPath $localStyle -PathType Leaf) {
      return (Join-Path $current "lib")
    }

    $parent = Split-Path -Parent $current
    if ($parent -eq $current) { break }
    $current = $parent
  }

  throw "Nao foi possivel localizar scripts/lib a partir de $StartPath."
}

function Normalize-EnvValue {
  param([string]$Value)

  if ($null -eq $Value) { return "" }
  $trimmed = $Value.Trim()
  if ($trimmed.Length -ge 2) {
    if (($trimmed.StartsWith('"') -and $trimmed.EndsWith('"')) -or ($trimmed.StartsWith("'") -and $trimmed.EndsWith("'"))) {
      return $trimmed.Substring(1, $trimmed.Length - 2)
    }
  }

  return $trimmed
}

function Read-EnvFile {
  param([string]$Path)

  $map = @{}
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $map }

  foreach ($line in Get-Content -LiteralPath $Path) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
      continue
    }

    $separatorIndex = $trimmed.IndexOf("=")
    if ($separatorIndex -le 0) {
      continue
    }

    $key = $trimmed.Substring(0, $separatorIndex).Trim()
    if (-not [string]::IsNullOrWhiteSpace($key)) {
      $map[$key] = Normalize-EnvValue $trimmed.Substring($separatorIndex + 1)
    }
  }

  return $map
}

function Get-LocalEnvValue {
  param(
    [string]$Name,
    [string]$RootDir = $script:RootDir
  )

  foreach ($envPath in @((Join-Path $RootDir ".env.local"), (Join-Path $RootDir ".env"))) {
    $values = Read-EnvFile $envPath
    if ($values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($values[$Name])) {
      return $values[$Name]
    }
  }

  return ""
}

function Get-LocalConfigValue {
  param(
    [string]$Name,
    [string]$DefaultValue = "",
    [string]$RootDir = $script:RootDir
  )

  $value = [System.Environment]::GetEnvironmentVariable($Name, "Process")
  if ([string]::IsNullOrWhiteSpace($value)) {
    $value = Get-LocalEnvValue -Name $Name -RootDir $RootDir
  }
  if ([string]::IsNullOrWhiteSpace($value)) {
    return $DefaultValue
  }

  return $value
}

function Get-RequiredLocalConfigValue {
  param(
    [string]$Name,
    [string]$RootDir = $script:RootDir
  )

  $value = Get-LocalConfigValue -Name $Name -DefaultValue "" -RootDir $RootDir
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "Defina $Name no ambiente, em .env.local ou em .env."
  }

  return $value
}

function Get-ComposeEnvFile {
  param([string]$RootDir = $script:RootDir)

  foreach ($envPath in @((Join-Path $RootDir ".env.local"), (Join-Path $RootDir ".env"))) {
    if (Test-Path -LiteralPath $envPath -PathType Leaf) {
      return $envPath
    }
  }

  return ""
}

function Get-ComposeEnvArguments {
  param([string]$RootDir = $script:RootDir)

  $envPath = Get-ComposeEnvFile -RootDir $RootDir
  if ([string]::IsNullOrWhiteSpace($envPath)) {
    return @()
  }

  return @("--env-file", $envPath)
}

function Assert-CommandAvailable {
  param(
    [string]$Command,
    [string[]]$Arguments = @(),
    [string]$FailureMessage = "Comando obrigatorio nao encontrado: $Command"
  )

  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & $Command @Arguments 1>$null 2>$null
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($exitCode -ne 0) {
    throw $FailureMessage
  }
}
