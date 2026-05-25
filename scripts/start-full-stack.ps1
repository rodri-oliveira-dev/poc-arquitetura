[CmdletBinding()]
param(
  [switch]$NoBuild,
  [switch]$SkipHealthChecks,
  [switch]$Cleanup
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))
$composeFile = Join-Path $root "compose.yaml"
$composeNginxFile = Join-Path $root "compose.nginx.yaml"
$certFile = Join-Path $root "infra\nginx\certs\localhost.crt"
$keyFile = Join-Path $root "infra\nginx\certs\localhost.key"
$startLocalScript = Join-Path $scriptDir "start-local-stack.ps1"
$projectNetworkName = "poc-arquitetura_poc-net"
$overlayContainerNames = @("poc-nginx-edge", "poc-ledger-service-1", "poc-ledger-service-2")
$projectContainerPrefix = "poc-"
$requiredPorts = @(
  @{ Name = "Auth.Api"; Port = 5030 },
  @{ Name = "LedgerService.Api"; Port = 5226 },
  @{ Name = "BalanceService.Api"; Port = 5228 },
  @{ Name = "Portal Nginx HTTPS"; Port = 7443 },
  @{ Name = "Grafana"; Port = 3000 },
  @{ Name = "Jaeger UI"; Port = 16686 },
  @{ Name = "Prometheus"; Port = 9090 },
  @{ Name = "Alertmanager"; Port = 9093 },
  @{ Name = "Loki"; Port = 3100 },
  @{ Name = "Grafana Alloy"; Port = 12345 },
  @{ Name = "Kafka"; Port = 19092 },
  @{ Name = "PostgreSQL Ledger"; Port = 15432 },
  @{ Name = "PostgreSQL Balance"; Port = 15433 },
  @{ Name = "Jaeger OTLP gRPC"; Port = 4317 },
  @{ Name = "Jaeger OTLP HTTP"; Port = 4318 }
)

function Assert-CommandAvailable([string]$CommandName, [string]$InstallHint) {
  if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
    throw "$CommandName nao encontrado. $InstallHint"
  }
}

function Invoke-External([string]$CommandName, [string[]]$Arguments) {
  & $CommandName @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "$CommandName falhou: $LASTEXITCODE"
  }
}

function Assert-DockerComposeAvailable {
  Assert-CommandAvailable "docker" "Instale/configure Docker CLI com suporte a 'docker compose'."
  Invoke-External "docker" @("compose", "version")
}

function Assert-NginxCertificates {
  if ((Test-Path $certFile) -and (Test-Path $keyFile)) {
    return
  }

  throw @"
Certificados locais do Nginx nao encontrados.

Arquivos esperados:
  infra/nginx/certs/localhost.crt
  infra/nginx/certs/localhost.key

Gere os certificados conforme docs/development/local-development.md ou infra/nginx/README.md.
O script nao gera certificados automaticamente.
"@
}

function Get-DockerContainerRows([string[]]$Arguments) {
  $output = & docker @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "docker falhou: $($Arguments -join ' ')"
  }

  return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-ContainersByName([string[]]$Names) {
  $containers = @()
  foreach ($name in $Names) {
    $rows = Get-DockerContainerRows @("ps", "-a", "--filter", "name=^/$name$", "--format", "{{.Names}}|{{.Status}}")
    foreach ($row in $rows) {
      $parts = $row.Split("|", 2)
      $containers += [pscustomobject]@{
        Name = $parts[0]
        Status = if ($parts.Length -gt 1) { $parts[1] } else { "" }
      }
    }
  }

  return $containers
}

function Get-PortOwnerContainer([int]$Port) {
  $rows = Get-DockerContainerRows @("ps", "--format", "{{.Names}}|{{.Ports}}")
  foreach ($row in $rows) {
    $parts = $row.Split("|", 2)
    if ($parts.Length -lt 2) {
      continue
    }

    if ($parts[1] -match "(^|,|\s)(0\.0\.0\.0|\[::\]|127\.0\.0\.1):$Port->") {
      return $parts[0]
    }
  }

  return ""
}

function Test-TcpPortOpen([int]$Port) {
  $client = [System.Net.Sockets.TcpClient]::new()
  try {
    $async = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
    if (-not $async.AsyncWaitHandle.WaitOne(250)) {
      return $false
    }

    $client.EndConnect($async)
    return $true
  }
  catch {
    return $false
  }
  finally {
    $client.Dispose()
  }
}

function Assert-NoExternalPortConflicts {
  $conflicts = @()
  foreach ($requiredPort in $requiredPorts) {
    $port = [int]$requiredPort.Port
    if (-not (Test-TcpPortOpen $port)) {
      continue
    }

    $ownerContainer = Get-PortOwnerContainer $port
    if ($ownerContainer.StartsWith($projectContainerPrefix)) {
      continue
    }

    $owner = if ([string]::IsNullOrWhiteSpace($ownerContainer)) {
      "processo local fora do Docker ou container sem publicacao detectavel"
    } else {
      "container Docker '$ownerContainer'"
    }
    $conflicts += "  - $($requiredPort.Name) usa a porta $port, ocupada por $owner"
  }

  if ($conflicts.Count -gt 0) {
    throw @"
Ha portas necessarias para a stack completa em uso por recursos externos ao projeto:
$($conflicts -join "`n")

Libere essas portas manualmente e execute o script novamente.
A limpeza automatica do script atua somente em containers/redes deste projeto e nao para processos externos.
"@
  }
}

function Invoke-NonDestructiveProjectCleanup {
  Write-Host "Executando limpeza nao destrutiva da stack local..."
  Invoke-External "docker" @(
    "compose",
    "-f", $composeFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "--profile", "direct-ledger",
    "down",
    "--remove-orphans"
  )
}

function Confirm-ProjectCleanup([string]$Reason) {
  if ($Cleanup) {
    return $true
  }

  Write-Host ""
  Write-Warning $Reason
  Write-Host "A limpeza proposta usa 'docker compose down --remove-orphans' sem '-v'."
  Write-Host "Ela para/remove containers e redes locais do projeto, mas preserva volumes, bancos, imagens e certificados."
  $answer = Read-Host "Pode liberar esses recursos antes de subir a stack completa? [s/N]"
  return $answer -match "^(s|sim|y|yes)$"
}

function Assert-StartupResourcesAvailable {
  Assert-NoExternalPortConflicts

  $overlayContainers = @(Get-ContainersByName $overlayContainerNames)
  if ($overlayContainers.Count -gt 0) {
    $details = ($overlayContainers | ForEach-Object { "  - $($_.Name): $($_.Status)" }) -join "`n"
    $reason = @"
Foram encontrados containers do overlay Nginx ja existentes antes da subida da stack base.
Esses containers podem aparecer como orfaos quando o script chama start-local-stack.ps1 e, em estados parciais, podem prender uma network antiga.
$details
"@

    if (Confirm-ProjectCleanup $reason) {
      Invoke-NonDestructiveProjectCleanup
      return
    }

    throw "Subida interrompida. Libere os recursos listados ou execute novamente com -Cleanup para aplicar a limpeza nao destrutiva."
  }

  $networkRows = Get-DockerContainerRows @("network", "ls", "--filter", "name=^$projectNetworkName$", "--format", "{{.Name}}")
  if ($networkRows.Count -gt 0) {
    $networkContainersJson = & docker network inspect $projectNetworkName --format "{{json .Containers}}" 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($networkContainersJson) -and $networkContainersJson -ne "null" -and $networkContainersJson -ne "{}") {
      $reason = "A rede local $projectNetworkName ja existe com containers conectados. Isso pode indicar stack anterior ou estado parcial."
      if (Confirm-ProjectCleanup $reason) {
        Invoke-NonDestructiveProjectCleanup
        return
      }

      throw "Subida interrompida. Libere os recursos da rede $projectNetworkName ou execute novamente com -Cleanup."
    }
  }
}

function Invoke-HttpCheck([string]$Name, [string]$Url, [switch]$Insecure) {
  Write-Host "Validando ${Name}: $Url"

  if ($Insecure) {
    if (-not (Get-Command "curl.exe" -ErrorAction SilentlyContinue)) {
      Write-Warning "curl.exe nao encontrado; pulando validacao HTTPS local de $Name."
      return
    }

    & curl.exe -k -fsS --max-time 10 $Url 1>$null
    if ($LASTEXITCODE -ne 0) {
      throw "Health check falhou para $Name em $Url"
    }

    return
  }

  Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10 | Out-Null
}

Push-Location $root
$previousOtelEnabled = [System.Environment]::GetEnvironmentVariable("OTEL_ENABLED", "Process")
try {
  Assert-DockerComposeAvailable
  Assert-CommandAvailable "dotnet" "Instale o .NET SDK definido em global.json."
  Assert-NginxCertificates
  Assert-StartupResourcesAvailable

  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", "true", "Process")

  $localArgs = @("-File", $startLocalScript, "-Observability")
  if ($NoBuild) {
    $localArgs += "-NoBuild"
  }
  $powershellArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass") + $localArgs
  Invoke-External "powershell" $powershellArgs

  $nginxArgs = @(
    "compose",
    "-f", $composeFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "up",
    "-d"
  )

  if (-not $NoBuild) {
    $nginxArgs += "--build"
  }

  $nginxArgs += @("ledger-service-1", "ledger-service-2", "nginx-edge")
  Invoke-External "docker" $nginxArgs

  Invoke-External "docker" @(
    "compose",
    "-f", $composeFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "ps"
  )

  if (-not $SkipHealthChecks) {
    Invoke-HttpCheck "Auth.Api direta" "http://localhost:5030/health"
    Invoke-HttpCheck "LedgerService.Api direta" "http://localhost:5226/health"
    Invoke-HttpCheck "BalanceService.Api direta" "http://localhost:5228/health"
    Invoke-HttpCheck "Portal Nginx" "https://localhost:7443/" -Insecure
    Invoke-HttpCheck "Ledger via Nginx" "https://ledger.localhost:7443/health" -Insecure
    Invoke-HttpCheck "Balance via Nginx" "https://balance.localhost:7443/health" -Insecure
    Invoke-HttpCheck "Auth via Nginx" "https://auth.localhost:7443/health" -Insecure
    Invoke-HttpCheck "Grafana" "http://localhost:3000/api/health"
    Invoke-HttpCheck "Jaeger" "http://localhost:16686/"
    Invoke-HttpCheck "Prometheus" "http://localhost:9090/-/ready"
    Invoke-HttpCheck "Alertmanager" "http://localhost:9093/-/ready"
  }

  Write-Host ""
  Write-Host "OK. Stack completa local pronta."
  Write-Host ""
  Write-Host "URLs uteis:"
  Write-Host "  Auth.Api:              http://localhost:5030/"
  Write-Host "  LedgerService.Api:     http://localhost:5226/"
  Write-Host "  BalanceService.Api:    http://localhost:5228/"
  Write-Host "  Portal Nginx:          https://localhost:7443/"
  Write-Host "  Ledger Swagger Nginx:  https://ledger.localhost:7443/swagger"
  Write-Host "  Balance Swagger Nginx: https://balance.localhost:7443/swagger"
  Write-Host "  Auth Swagger Nginx:    https://auth.localhost:7443/swagger"
  Write-Host "  Grafana:               http://localhost:3000/"
  Write-Host "  Jaeger:                http://localhost:16686/"
  Write-Host "  Prometheus:            http://localhost:9090/"
  Write-Host "  Alertmanager:          http://localhost:9093/"
  Write-Host ""
  Write-Host "Este script nao remove volumes, nao executa testes, k6 nem scanners."
}
finally {
  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", $previousOtelEnabled, "Process")
  Pop-Location
}
