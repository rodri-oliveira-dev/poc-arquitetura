[CmdletBinding()]
param(
  [string]$ProjectName = $env:COMPOSE_PROJECT_NAME
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectName)) {
  $ProjectName = "poc-arquitetura"
}

function Write-Section([string]$Title) {
  Write-Host ""
  Write-Host "== $Title =="
}

function Invoke-DockerBestEffort([string]$Title, [string[]]$Arguments) {
  Write-Section $Title
  & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    Write-Warning "Comando indisponivel ou falhou: docker $($Arguments -join ' ')"
  }
}

if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
  throw "docker nao encontrado. Instale/configure Docker CLI com suporte a 'docker compose'."
}

Write-Section "Docker system df"
& docker system df
if ($LASTEXITCODE -ne 0) {
  throw "docker system df falhou: $LASTEXITCODE"
}

Invoke-DockerBestEffort "Docker builder du" @("builder", "du")

Write-Section "Volumes do projeto $ProjectName"
& docker volume ls `
  --filter "label=com.docker.compose.project=$ProjectName" `
  --format "table {{.Name}}\t{{.Driver}}\t{{.Scope}}"

Write-Section "Containers do projeto $ProjectName"
& docker ps -a `
  --filter "label=com.docker.compose.project=$ProjectName" `
  --format "table {{.Names}}\t{{.Status}}\t{{.Image}}\t{{.Size}}"

Write-Section "Imagens relacionadas ao projeto $ProjectName"
& docker images `
  --filter "reference=*$ProjectName*" `
  --format "table {{.Repository}}\t{{.Tag}}\t{{.ID}}\t{{.Size}}"

Write-Section "Imagens com label Compose do projeto $ProjectName"
& docker images `
  --filter "label=com.docker.compose.project=$ProjectName" `
  --format "table {{.Repository}}\t{{.Tag}}\t{{.ID}}\t{{.Size}}"
