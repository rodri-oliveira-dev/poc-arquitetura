[CmdletBinding()]
param(
  [switch]$NoBuild,
  [switch]$SkipHealthChecks,
  [switch]$Cleanup
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir "..\.."))
$composeFile = Join-Path $root "compose.yaml"
$composeObservabilityFile = Join-Path $root "compose.observability.yaml"
$composeNginxFile = Join-Path $root "compose.nginx.yaml"
$certFile = Join-Path $root "infra\nginx\certs\localhost.crt"
$keyFile = Join-Path $root "infra\nginx\certs\localhost.key"
$startLocalScript = Join-Path $scriptDir "start-stack.ps1"
$projectName = if ([string]::IsNullOrWhiteSpace($env:COMPOSE_PROJECT_NAME)) { "poc-arquitetura" } else { $env:COMPOSE_PROJECT_NAME }
$projectNetworkService = "poc-net"
$overlayServices = @("nginx-edge", "ledger-service-1", "ledger-service-2")
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

function Get-ComposeEnvArguments {
  foreach ($envPath in @((Join-Path $root ".env.local"), (Join-Path $root ".env"))) {
    if (Test-Path $envPath) {
      return @("--env-file", $envPath)
    }
  }

  return @()
}

function Get-LocalConfigValue([string]$Key, [string]$DefaultValue) {
  $environmentValue = [System.Environment]::GetEnvironmentVariable($Key, "Process")
  if (-not [string]::IsNullOrWhiteSpace($environmentValue)) {
    return $environmentValue
  }

  foreach ($envPath in @((Join-Path $root ".env.local"), (Join-Path $root ".env"))) {
    if (-not (Test-Path $envPath)) {
      continue
    }

    foreach ($line in Get-Content $envPath) {
      if ($line.StartsWith("$Key=", [System.StringComparison]::Ordinal)) {
        return $line.Substring($Key.Length + 1)
      }
    }
  }

  return $DefaultValue
}

function Get-RequiredPorts {
  return @(
    @{ Name = "PostgreSQL"; Port = [int](Get-LocalConfigValue "POSTGRES_HOST_PORT" "15432") },
    @{ Name = "Kafka"; Port = [int](Get-LocalConfigValue "KAFKA_HOST_PORT" "19092") },
    @{ Name = "Pub/Sub emulator"; Port = [int](Get-LocalConfigValue "PUBSUB_EMULATOR_HOST_PORT" "8085") },
    @{ Name = "Mailpit SMTP"; Port = [int](Get-LocalConfigValue "MAILPIT_SMTP_HOST_PORT" "1025") },
    @{ Name = "Mailpit UI"; Port = [int](Get-LocalConfigValue "MAILPIT_UI_HOST_PORT" "8025") },
    @{ Name = "Keycloak"; Port = [int](Get-LocalConfigValue "KEYCLOAK_HOST_PORT" "8081") },
    @{ Name = "LedgerService.Api"; Port = [int](Get-LocalConfigValue "LEDGER_SERVICE_HOST_PORT" "5226") },
    @{ Name = "BalanceService.Api"; Port = [int](Get-LocalConfigValue "BALANCE_SERVICE_HOST_PORT" "5228") },
    @{ Name = "TransferService.Api"; Port = [int](Get-LocalConfigValue "TRANSFER_SERVICE_HOST_PORT" "5230") },
    @{ Name = "PaymentService.Api"; Port = [int](Get-LocalConfigValue "PAYMENT_SERVICE_HOST_PORT" "5234") },
    @{ Name = "AuditService.Api"; Port = [int](Get-LocalConfigValue "AUDIT_SERVICE_HOST_PORT" "5235") },
    @{ Name = "IdentityService.Api"; Port = [int](Get-LocalConfigValue "IDENTITY_SERVICE_HOST_PORT" "5232") },
    @{ Name = "Portal Nginx HTTPS"; Port = [int](Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443") },
    @{ Name = "Grafana"; Port = [int](Get-LocalConfigValue "GRAFANA_HOST_PORT" "3000") },
    @{ Name = "Jaeger UI"; Port = [int](Get-LocalConfigValue "JAEGER_UI_HOST_PORT" "16686") },
    @{ Name = "Prometheus"; Port = [int](Get-LocalConfigValue "PROMETHEUS_HOST_PORT" "9090") },
    @{ Name = "Alertmanager"; Port = [int](Get-LocalConfigValue "ALERTMANAGER_HOST_PORT" "9093") },
    @{ Name = "Loki"; Port = [int](Get-LocalConfigValue "LOKI_HOST_PORT" "3100") },
    @{ Name = "Grafana Alloy"; Port = [int](Get-LocalConfigValue "ALLOY_HOST_PORT" "12345") },
    @{ Name = "Jaeger OTLP gRPC"; Port = [int](Get-LocalConfigValue "JAEGER_OTLP_GRPC_HOST_PORT" "4317") },
    @{ Name = "Jaeger OTLP HTTP"; Port = [int](Get-LocalConfigValue "JAEGER_OTLP_HTTP_HOST_PORT" "4318") }
  )
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

function Get-ComposeServiceContainers([string[]]$Services) {
  $containers = @()
  foreach ($service in $Services) {
    $rows = Get-DockerContainerRows @(
      "ps", "-a",
      "--filter", "label=com.docker.compose.project=$projectName",
      "--filter", "label=com.docker.compose.service=$service",
      "--format", "{{.Names}}|{{.Status}}"
    )
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
  $rows = Get-DockerContainerRows @("ps", "--format", "{{.Names}}|{{.Label ""com.docker.compose.project""}}|{{.Ports}}")
  foreach ($row in $rows) {
    $parts = $row.Split("|", 3)
    if ($parts.Length -lt 3) {
      continue
    }

    if ($parts[2] -match "(^|,|\s)(0\.0\.0\.0|\[::\]|127\.0\.0\.1):$Port->") {
      return [pscustomobject]@{
        Name = $parts[0]
        Project = $parts[1]
      }
    }
  }

  return $null
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
  foreach ($requiredPort in (Get-RequiredPorts)) {
    $port = [int]$requiredPort.Port
    if (-not (Test-TcpPortOpen $port)) {
      continue
    }

    $ownerContainer = Get-PortOwnerContainer $port
    if ($null -ne $ownerContainer -and $ownerContainer.Project -eq $projectName) {
      continue
    }

    $owner = if ($null -eq $ownerContainer) {
      "processo local fora do Docker ou container sem publicacao detectavel"
    } else {
      "container Docker '$($ownerContainer.Name)'"
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
    "-f", $composeObservabilityFile,
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

  $overlayContainers = @(Get-ComposeServiceContainers $overlayServices)
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

  $networkRows = Get-DockerContainerRows @(
    "network", "ls",
    "--filter", "label=com.docker.compose.project=$projectName",
    "--filter", "label=com.docker.compose.network=$projectNetworkService",
    "--format", "{{.Name}}"
  )
  if ($networkRows.Count -gt 0) {
    foreach ($networkName in $networkRows) {
      $networkContainersJson = & docker network inspect $networkName --format "{{json .Containers}}" 2>$null
      if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($networkContainersJson) -and $networkContainersJson -ne "null" -and $networkContainersJson -ne "{}") {
        $reason = "A rede local do projeto Compose '$projectName' ja existe com containers conectados. Isso pode indicar stack anterior ou estado parcial."
        if (Confirm-ProjectCleanup $reason) {
          Invoke-NonDestructiveProjectCleanup
          return
        }

        throw "Subida interrompida. Libere os recursos da rede do projeto '$projectName' ou execute novamente com -Cleanup."
      }
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

  $nginxArgs = @("compose") + (Get-ComposeEnvArguments) + @(
    "-f", $composeFile,
    "-f", $composeObservabilityFile,
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

  Invoke-External "docker" (@("compose") + (Get-ComposeEnvArguments) + @(
    "-f", $composeFile,
    "-f", $composeObservabilityFile,
    "-f", $composeNginxFile,
    "--profile", "observability",
    "ps"
  ))

  if (-not $SkipHealthChecks) {
    Invoke-HttpCheck "LedgerService.Api direta" "http://localhost:$(Get-LocalConfigValue "LEDGER_SERVICE_HOST_PORT" "5226")/health"
    Invoke-HttpCheck "BalanceService.Api direta" "http://localhost:$(Get-LocalConfigValue "BALANCE_SERVICE_HOST_PORT" "5228")/health"
    Invoke-HttpCheck "Portal Nginx" "https://localhost:$(Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443")/" -Insecure
    Invoke-HttpCheck "Ledger via Nginx" "https://ledger.localhost:$(Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443")/health" -Insecure
    Invoke-HttpCheck "Balance via Nginx" "https://balance.localhost:$(Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443")/health" -Insecure
    Invoke-HttpCheck "Grafana" "http://localhost:$(Get-LocalConfigValue "GRAFANA_HOST_PORT" "3000")/api/health"
    Invoke-HttpCheck "Jaeger" "http://localhost:$(Get-LocalConfigValue "JAEGER_UI_HOST_PORT" "16686")/"
    Invoke-HttpCheck "Prometheus" "http://localhost:$(Get-LocalConfigValue "PROMETHEUS_HOST_PORT" "9090")/-/ready"
    Invoke-HttpCheck "Alertmanager" "http://localhost:$(Get-LocalConfigValue "ALERTMANAGER_HOST_PORT" "9093")/-/ready"
  }

  Write-Host ""
  Write-Host "OK. Stack completa local pronta."
  Write-Host ""
  Write-Host "URLs uteis:"
  Write-Host "  LedgerService.Api:     http://localhost:$(Get-LocalConfigValue "LEDGER_SERVICE_HOST_PORT" "5226")/"
  Write-Host "  BalanceService.Api:    http://localhost:$(Get-LocalConfigValue "BALANCE_SERVICE_HOST_PORT" "5228")/"
  Write-Host "  Portal Nginx:          https://localhost:$(Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443")/"
  Write-Host "  Ledger Swagger Nginx:  https://ledger.localhost:$(Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443")/swagger"
  Write-Host "  Balance Swagger Nginx: https://balance.localhost:$(Get-LocalConfigValue "NGINX_HTTPS_HOST_PORT" "7443")/swagger"
  Write-Host "  Grafana:               http://localhost:$(Get-LocalConfigValue "GRAFANA_HOST_PORT" "3000")/"
  Write-Host "  Jaeger:                http://localhost:$(Get-LocalConfigValue "JAEGER_UI_HOST_PORT" "16686")/"
  Write-Host "  Prometheus:            http://localhost:$(Get-LocalConfigValue "PROMETHEUS_HOST_PORT" "9090")/"
  Write-Host "  Alertmanager:          http://localhost:$(Get-LocalConfigValue "ALERTMANAGER_HOST_PORT" "9093")/"
  Write-Host ""
  Write-Host "Este script nao remove volumes, nao executa testes, k6 nem scanners."
}
finally {
  [System.Environment]::SetEnvironmentVariable("OTEL_ENABLED", $previousOtelEnabled, "Process")
  Pop-Location
}

