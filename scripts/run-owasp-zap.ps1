[CmdletBinding()]
param(
  [string]$AuthUrl = "",
  [string]$LedgerUrl = "",
  [string]$BalanceUrl = "",
  [switch]$UseNginx,
  [string]$ZapImage = "ghcr.io/zaproxy/zaproxy:stable",
  [string]$OutputRoot = "",
  [switch]$StartStack,
  [switch]$NoBuild,
  [int]$HealthTimeoutSeconds = 90,
  [int]$HealthIntervalSeconds = 3,
  [string]$SwaggerPath = "/swagger/v1/swagger.json",
  [switch]$UseAuthentication,
  [switch]$IncludeLegacyAuth,
  [string]$Token = "",
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
$scanCommand = "zap-api-scan.py"
$scanType = if ($ActiveScan) { "api-active" } else { "api-baseline" }
$zapOptions = "-config connection.sslAcceptAll=true"

if ([string]::IsNullOrWhiteSpace($AuthUrl)) {
  $AuthUrl = "http://localhost:5030"
}
if ([string]::IsNullOrWhiteSpace($LedgerUrl)) {
  $LedgerUrl = if ($UseNginx) { "https://ledger.localhost:7443" } else { "http://localhost:5226" }
}
if ([string]::IsNullOrWhiteSpace($BalanceUrl)) {
  $BalanceUrl = if ($UseNginx) { "https://balance.localhost:7443" } else { "http://localhost:5228" }
}

$apis = @(
  [pscustomobject]@{ Name = "LedgerService.Api"; Slug = "ledger-service-api"; Url = $LedgerUrl },
  [pscustomobject]@{ Name = "BalanceService.Api"; Slug = "balance-service-api"; Url = $BalanceUrl }
)
if ($IncludeLegacyAuth) {
  $apis = @([pscustomobject]@{ Name = "Auth.Api"; Slug = "auth-api"; Url = $AuthUrl }) + $apis
}

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

function Start-LocalStackForZap {
  if ($UseNginx) {
    throw "-StartStack sobe apenas a stack local direta. Para Nginx, execute ./scripts/start-full-stack.ps1 antes e rode este script com -UseNginx."
  }

  $startScript = Join-Path $scriptDir "start-local-stack.ps1"
  $args = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $startScript)
  if ($NoBuild) {
    $args += "-NoBuild"
  }

  Write-Host "Iniciando stack local antes do scan ZAP..."
  & powershell @args
  if ($LASTEXITCODE -ne 0) {
    throw "Falha ao iniciar a stack local para o ZAP. Exit code: $LASTEXITCODE"
  }
}

function Get-HealthUri([string]$BaseUrl) {
  return $BaseUrl.TrimEnd("/") + "/health"
}

function Get-SwaggerUri([string]$BaseUrl) {
  $path = $SwaggerPath
  if ([string]::IsNullOrWhiteSpace($path)) {
    $path = "/swagger/v1/swagger.json"
  }
  if (-not $path.StartsWith("/")) {
    $path = "/" + $path
  }

  return $BaseUrl.TrimEnd("/") + $path
}

function Get-ZapToken {
  if (-not [string]::IsNullOrWhiteSpace($Token)) {
    return $Token
  }

  $getTokenScript = Join-Path $scriptDir "get-token.ps1"
  Write-Host "Obtendo token para o ZAP pelo provider local configurado..."
  $value = powershell -NoProfile -ExecutionPolicy Bypass -File $getTokenScript
  if ($LASTEXITCODE -ne 0) {
    throw "Falha ao obter token para o ZAP. Exit code: $LASTEXITCODE"
  }

  $value = ($value | Out-String).Trim()
  if ([string]::IsNullOrWhiteSpace($value)) {
    throw "Token vazio retornado por $getTokenScript"
  }

  return $value
}

function Enable-ZapAuthorizationHeader([string]$AccessToken) {
  if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw "Token vazio para configurar Authorization no ZAP."
  }

  $script:zapOptions = @(
    "-config connection.sslAcceptAll=true",
    "-config replacer.full_list(0).description=authorization-header",
    "-config replacer.full_list(0).enabled=true",
    "-config replacer.full_list(0).matchtype=REQ_HEADER",
    "-config replacer.full_list(0).matchstr=Authorization",
    "-config replacer.full_list(0).regex=false",
    "-config replacer.full_list(0).replacement=Bearer $AccessToken"
  ) -join " "
}

function Invoke-HealthCheck([string]$ApiName, [string]$BaseUrl) {
  $uri = Get-HealthUri $BaseUrl
  $deadline = [DateTimeOffset]::UtcNow.AddSeconds($HealthTimeoutSeconds)
  $lastError = ""

  while ([DateTimeOffset]::UtcNow -lt $deadline) {
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

      $statusCode = [int]$response.StatusCode
      if ($statusCode -ge 200 -and $statusCode -lt 400) {
        return
      }

      $lastError = "HTTP $statusCode"
    }
    catch {
      $lastError = $_.Exception.Message
    }

    if ($HealthIntervalSeconds -gt 0) {
      Start-Sleep -Seconds $HealthIntervalSeconds
    }
  }

  $suggestion = if ($UseNginx) {
    "Suba a stack completa com Nginx, por exemplo ./scripts/start-full-stack.ps1, e confirme os certificados locais."
  } else {
    "Suba a stack local, por exemplo ./scripts/start-local-stack.ps1, ou execute este script com -StartStack."
  }

  throw "$ApiName indisponivel em $uri apos ${HealthTimeoutSeconds}s. $suggestion Ultimo erro: $lastError"
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
  $swaggerUrl = Get-SwaggerUri ([string]$Api.Url)
  $targetUrl = ConvertTo-ZapTargetUrl $swaggerUrl
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
    "-f", "openapi",
    "-r", $html,
    "-J", $json,
    "-w", $markdown,
    "-z", $zapOptions
  )

  if (-not $ActiveScan) {
    $dockerArgs += "-S"
  }

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
    SwaggerUrl = $swaggerUrl
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
  $lines.Add(("- Definicao OpenAPI: ``{0}``" -f $SwaggerPath))
  $lines.Add(("- Autenticacao Bearer: ``{0}``" -f ($(if ($UseAuthentication) { "habilitada" } else { "desabilitada" }))))
  $lines.Add(("- Container temporario: ``{0}``" -f $containerName))
  $lines.Add(("- Diretorio de saida: ``{0}``" -f $outputDir))
  $lines.Add("")
  $lines.Add("## APIs analisadas")
  $lines.Add("")

  foreach ($result in $scanResults) {
    $lines.Add(("- {0}: ``{1}``" -f $result.Name, $result.Url))
    $lines.Add(("  - Swagger/OpenAPI: ``{0}``" -f $result.SwaggerUrl))
    $lines.Add(("  - OpenAPI visto pelo container: ``{0}``" -f $result.TargetUrlFromContainer))
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
    $lines.Add("- API active scan foi executado por parametro explicito e pode gerar trafego mais invasivo que o baseline seguro.")
  } else {
    $lines.Add("- API scan foi executado em modo seguro (`-S`), importando OpenAPI sem active scan por padrao.")
  }
  if ($UseAuthentication) {
    $lines.Add("- Authorization Bearer foi injetado via ZAP Replacer usando token obtido por ``scripts/get-token.ps1`` ou pelo parametro ``-Token``.")
  }

  Set-Content -Path $summaryPath -Value $lines -Encoding UTF8
}

try {
  Assert-CommandAvailable "docker" @("version") "Docker nao esta disponivel. Instale/inicie um runtime com Docker-compatible API."
  Assert-CommandAvailable "docker" @("compose", "version") "docker compose nao esta disponivel. Atualize a CLI Docker ou habilite o plugin compose."

  if ($StartStack) {
    Start-LocalStackForZap
  }

  foreach ($api in $apis) {
    Invoke-HealthCheck $api.Name $api.Url
  }

  if ($UseAuthentication) {
    Enable-ZapAuthorizationHeader (Get-ZapToken)
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
