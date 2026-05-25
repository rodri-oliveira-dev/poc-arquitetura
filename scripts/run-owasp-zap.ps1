[CmdletBinding()]
param(
  [string]$AuthUrl = "",
  [string]$LedgerUrl = "",
  [string]$BalanceUrl = "",
  [switch]$UseNginx,
  [string]$ZapImage = "ghcr.io/zaproxy/zaproxy:stable",
  [string]$OutputRoot = "",
  [switch]$ActiveScan,
  [switch]$FailOnAlerts
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
  $OutputRoot = Join-Path $root "zap-reports"
}

$containerName = "poc-arquitetura-zap"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDir = Join-Path $OutputRoot $timestamp
$scanCommand = if ($ActiveScan) { "zap-full-scan.py" } else { "zap-baseline.py" }
$scanType = if ($ActiveScan) { "active" } else { "baseline" }

if ([string]::IsNullOrWhiteSpace($AuthUrl)) {
  $AuthUrl = if ($UseNginx) { "https://auth.localhost:7443" } else { "http://localhost:5030" }
}
if ([string]::IsNullOrWhiteSpace($LedgerUrl)) {
  $LedgerUrl = if ($UseNginx) { "https://ledger.localhost:7443" } else { "http://localhost:5226" }
}
if ([string]::IsNullOrWhiteSpace($BalanceUrl)) {
  $BalanceUrl = if ($UseNginx) { "https://balance.localhost:7443" } else { "http://localhost:5228" }
}

$apis = @(
  [pscustomobject]@{ Name = "Auth.Api"; Slug = "auth-api"; Url = $AuthUrl },
  [pscustomobject]@{ Name = "LedgerService.Api"; Slug = "ledger-service-api"; Url = $LedgerUrl },
  [pscustomobject]@{ Name = "BalanceService.Api"; Slug = "balance-service-api"; Url = $BalanceUrl }
)

$scanResults = New-Object System.Collections.Generic.List[object]

function Assert-CommandAvailable([string]$Command, [string[]]$Arguments, [string]$FailureMessage) {
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

function Remove-ZapContainer {
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & docker rm -f $containerName 1>$null 2>$null
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }
}

function Test-DockerImageExists([string]$Image) {
  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & docker image inspect $Image 1>$null 2>$null
    return ($LASTEXITCODE -eq 0)
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }
}

function Get-HealthUri([string]$BaseUrl) {
  return $BaseUrl.TrimEnd("/") + "/health"
}

function Invoke-HealthCheck([string]$ApiName, [string]$BaseUrl) {
  $uri = Get-HealthUri $BaseUrl
  try {
    if ($BaseUrl.StartsWith("https://")) {
      $previousCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
      [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
      try {
        $response = Invoke-WebRequest -UseBasicParsing -Method Get -Uri $uri -TimeoutSec 15
      }
      finally {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousCallback
      }
    } else {
      $response = Invoke-WebRequest -UseBasicParsing -Method Get -Uri $uri -TimeoutSec 15
    }
  }
  catch {
    $suggestion = if ($UseNginx) {
      "Suba a stack completa com Nginx, por exemplo ./scripts/start-full-stack.ps1, e confirme os certificados locais."
    } else {
      "Suba a stack local, por exemplo ./scripts/start-local-stack.ps1, ou informe -UseNginx para validar via borda local."
    }

    throw "$ApiName indisponivel em $uri. $suggestion Erro: $($_.Exception.Message)"
  }

  $statusCode = [int]$response.StatusCode
  if ($statusCode -lt 200 -or $statusCode -ge 400) {
    throw "$ApiName retornou HTTP $statusCode em $uri. Confirme que a stack local esta pronta antes de executar o ZAP."
  }
}

function ConvertTo-ZapTargetUrl([string]$BaseUrl) {
  $builder = [System.UriBuilder]::new($BaseUrl)
  if ($builder.Host -eq "localhost" -or $builder.Host -eq "127.0.0.1" -or $builder.Host -eq "::1") {
    $builder.Host = "host.docker.internal"
  }

  return $builder.Uri.AbsoluteUri.TrimEnd("/")
}

function Get-DockerHostMappings([object[]]$Apis) {
  $hosts = New-Object System.Collections.Generic.HashSet[string]
  [void]$hosts.Add("host.docker.internal")

  foreach ($api in $Apis) {
    $uri = [System.Uri]$api.Url
    if ($uri.Host.EndsWith(".localhost") -or $uri.Host -eq "localhost") {
      [void]$hosts.Add($uri.Host)
    }
  }

  $args = @()
  foreach ($hostName in $hosts) {
    $args += "--add-host"
    $args += "${hostName}:host-gateway"
  }

  return $args
}

function Invoke-ZapScan([object]$Api) {
  Remove-ZapContainer

  $slug = [string]$Api.Slug
  $targetUrl = ConvertTo-ZapTargetUrl ([string]$Api.Url)
  $html = "$slug.html"
  $json = "$slug.json"
  $markdown = "$slug.md"

  $dockerArgs = @(
    "run",
    "--name", $containerName,
    "-v", "${outputDir}:/zap/wrk:rw"
  )
  $dockerArgs += Get-DockerHostMappings $apis
  $dockerArgs += @(
    $ZapImage,
    $scanCommand,
    "-t", $targetUrl,
    "-r", $html,
    "-J", $json,
    "-w", $markdown,
    "-z", "-config connection.sslAcceptAll=true"
  )

  if (-not $FailOnAlerts) {
    $dockerArgs += "-I"
  }

  Write-Host "Executando ZAP $scanType em $($Api.Name): $($Api.Url)"
  & docker @dockerArgs
  $exitCode = $LASTEXITCODE

  Remove-ZapContainer

  $operationalFailure = $exitCode -ge 3
  if ($FailOnAlerts -and $exitCode -ne 0) {
    $status = "failed-alerts-or-error"
  } elseif ($operationalFailure) {
    $status = "failed-operational"
  } elseif ($exitCode -eq 0) {
    $status = "completed"
  } else {
    $status = "completed-with-alerts"
  }

  $scanResults.Add([pscustomobject]@{
    Name = $Api.Name
    Url = $Api.Url
    TargetUrlFromContainer = $targetUrl
    Status = $status
    ExitCode = $exitCode
    Files = @($html, $json, $markdown)
  })

  if ($operationalFailure) {
    throw "Falha operacional no ZAP para $($Api.Name). Exit code: $exitCode"
  }

  if ($FailOnAlerts -and $exitCode -ne 0) {
    throw "ZAP encontrou alertas para $($Api.Name) e -FailOnAlerts esta ativo. Exit code: $exitCode"
  }
}

function Write-Summary {
  $summaryPath = Join-Path $outputDir "summary.md"
  $lines = New-Object System.Collections.Generic.List[string]
  $lines.Add("# OWASP ZAP local scan")
  $lines.Add("")
  $lines.Add(("- Data/hora: {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")))
  $lines.Add(("- Imagem ZAP: ``{0}``" -f $ZapImage))
  $lines.Add(("- Tipo de scan: ``{0}``" -f $scanType))
  $lines.Add(("- Container temporario: ``{0}``" -f $containerName))
  $lines.Add(("- Diretorio de saida: ``{0}``" -f $outputDir))
  $lines.Add("")
  $lines.Add("## APIs analisadas")
  $lines.Add("")

  foreach ($result in $scanResults) {
    $lines.Add(("- {0}: ``{1}``" -f $result.Name, $result.Url))
    $lines.Add(("  - Alvo visto pelo container: ``{0}``" -f $result.TargetUrlFromContainer))
    $lines.Add(("  - Status: ``{0}``" -f $result.Status))
    $lines.Add(("  - Exit code ZAP: ``{0}``" -f $result.ExitCode))
    $lines.Add("  - Arquivos: $($result.Files -join ", ")")
  }

  $lines.Add("")
  $lines.Add("## Observacoes")
  $lines.Add("")
  $lines.Add("- Resultado de DAST local para apoio de desenvolvimento; nao substitui pentest, threat modeling ou validacao de seguranca em ambiente representativo.")
  $lines.Add("- Relatorios gerados em ``zap-reports/<timestamp>/`` nao devem ser versionados.")
  $lines.Add("- Por padrao, alertas do ZAP nao tornam o script falho; use ``-FailOnAlerts`` quando quiser propagar alertas como falha.")
  if ($ActiveScan) {
    $lines.Add("- Active scan foi executado por parametro explicito e pode gerar trafego mais invasivo que o baseline scan.")
  }

  Set-Content -Path $summaryPath -Value $lines -Encoding UTF8
}

try {
  Assert-CommandAvailable "docker" @("version") "Docker nao esta disponivel. Instale/inicie um runtime com Docker-compatible API."
  Assert-CommandAvailable "docker" @("compose", "version") "docker compose nao esta disponivel. Atualize a CLI Docker ou habilite o plugin compose."

  foreach ($api in $apis) {
    Invoke-HealthCheck $api.Name $api.Url
  }

  if (-not (Test-DockerImageExists $ZapImage)) {
    Write-Host "Imagem ZAP nao encontrada localmente. Baixando $ZapImage..."
    & docker pull $ZapImage
    if ($LASTEXITCODE -ne 0) {
      throw "docker pull falhou para $ZapImage"
    }
  }

  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

  foreach ($api in $apis) {
    Invoke-ZapScan $api
  }
}
finally {
  Remove-ZapContainer
  if (Test-Path $outputDir) {
    Write-Summary
  }
}

Write-Host "OK. Relatorios OWASP ZAP em: $outputDir"
