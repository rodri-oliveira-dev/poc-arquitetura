[CmdletBinding()]
param(
  [switch]$Apply,
  [ValidateRange(1, 3650)]
  [int]$RetentionDays = 7
)

$ErrorActionPreference = "Stop"

function Invoke-DockerJson([string[]]$Arguments) {
  $output = & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "docker falhou: $($Arguments -join ' ')"
  }

  return $output
}

if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
  throw "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'."
}

$volumeNames = @(& docker volume ls --filter "label=auto-prune=true" --format "{{.Name}}")
if ($LASTEXITCODE -ne 0) {
  throw "docker volume ls falhou."
}

if (-not $volumeNames) {
  Write-Host "Nenhum volume com label auto-prune=true encontrado."
  exit 0
}

$now = Get-Date
$threshold = New-TimeSpan -Days $RetentionDays
$mode = if ($Apply) { "apply" } else { "dry-run" }
Write-Host "Modo: $mode"
Write-Host "Retencao: ${RetentionDays}d"

foreach ($volume in $volumeNames) {
  $inspectRaw = Invoke-DockerJson @("volume", "inspect", $volume)
  $inspect = $inspectRaw | ConvertFrom-Json
  $metadata = $inspect[0]
  $retentionLabel = $metadata.Labels.retention

  if ($retentionLabel -ne "${RetentionDays}d") {
    Write-Host "SKIP ${volume}: retention=$retentionLabel nao corresponde a ${RetentionDays}d."
    continue
  }

  $createdAt = [DateTimeOffset]::Parse($metadata.CreatedAt, [Globalization.CultureInfo]::InvariantCulture)
  $age = $now - $createdAt.LocalDateTime
  $ageDays = [Math]::Floor($age.TotalDays)

  if ($age -lt $threshold) {
    Write-Host "SKIP ${volume}: idade ${ageDays}d menor que ${RetentionDays}d."
    continue
  }

  $containers = @(& docker ps -a --filter "volume=$volume" --format "{{.ID}}")
  if ($LASTEXITCODE -ne 0) {
    throw "docker ps falhou ao verificar uso do volume $volume."
  }

  if ($containers.Count -gt 0) {
    Write-Host "SKIP ${volume}: volume ainda associado a container existente."
    continue
  }

  if ($Apply) {
    Write-Host "REMOVE ${volume}: idade ${ageDays}d."
    & docker volume rm $volume
    if ($LASTEXITCODE -ne 0) {
      throw "docker volume rm falhou para $volume."
    }
  } else {
    Write-Host "DRY-RUN removeria ${volume}: idade ${ageDays}d."
  }
}
