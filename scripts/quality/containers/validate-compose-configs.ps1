[CmdletBinding()]
param(
  [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir "..\..\lib\common.ps1")
$script:RootDir = Resolve-RepositoryRoot -StartPath $scriptDir
$root = $script:RootDir

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
  $envOverride = [System.Environment]::GetEnvironmentVariable("COMPOSE_ENV_FILE", "Process")
  $EnvFile = if ([string]::IsNullOrWhiteSpace($envOverride)) {
    Join-Path $root ".env.local.example"
  }
  else {
    $envOverride
  }
}

Assert-CommandAvailable "docker" @("compose", "version") "docker compose nao esta disponivel."

if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
  throw "Arquivo de ambiente nao encontrado: $EnvFile"
}

$previousCloudSqlInstance = [System.Environment]::GetEnvironmentVariable("CLOUDSQL_INSTANCE_CONNECTION_NAME", "Process")
$previousGoogleCredentials = [System.Environment]::GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "Process")

function Set-PlaceholderEnv {
  if ([string]::IsNullOrWhiteSpace([System.Environment]::GetEnvironmentVariable("CLOUDSQL_INSTANCE_CONNECTION_NAME", "Process"))) {
    [System.Environment]::SetEnvironmentVariable("CLOUDSQL_INSTANCE_CONNECTION_NAME", "local-project:local-region:local-instance", "Process")
  }
  if ([string]::IsNullOrWhiteSpace([System.Environment]::GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "Process"))) {
    [System.Environment]::SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "./secrets/cloudsql/application_default_credentials.json", "Process")
  }
}

# Matriz oficial de validacao:
# - suportadas: compose.yaml base, overlays opcionais aplicados sobre a base e
#   stack completa local com observabilidade + Nginx;
# - alternativas: compose.kafka.yaml e apenas alias compativel do default Kafka;
#   compose.pubsub.yaml seleciona o caminho explicito/legado de Pub/Sub;
# - incompatibilidades deliberadas: Kafka explicito nao e combinado com Pub/Sub;
#   Pub/Sub nao e combinado com k6, porque os runners de carga versionados usam Kafka;
#   Cloud SQL nao e combinado com Pub/Sub/k6/Sonar por ser smoke manual/local isolado.
$combinations = @(
  [pscustomobject]@{ Name = "stack-base"; Files = @("compose.yaml"); Profiles = @(); Description = "Core funcional local com Kafka padrao" },
  [pscustomobject]@{ Name = "stack-base-kafka-alias"; Files = @("compose.yaml", "compose.kafka.yaml"); Profiles = @(); Description = "Alias compativel; Kafka ja esta no Compose base" },
  [pscustomobject]@{ Name = "stack-observability"; Files = @("compose.yaml", "compose.observability.yaml"); Profiles = @("observability"); Description = "Core funcional com observabilidade local" },
  [pscustomobject]@{ Name = "stack-nginx"; Files = @("compose.yaml", "compose.nginx.yaml"); Profiles = @(); Description = "Borda local Nginx com duas instancias do Ledger" },
  [pscustomobject]@{ Name = "stack-full-nginx-observability"; Files = @("compose.yaml", "compose.observability.yaml", "compose.nginx.yaml"); Profiles = @("observability"); Description = "Stack completa local usada por scripts/local/start-full-stack.ps1" },
  [pscustomobject]@{ Name = "stack-k6"; Files = @("compose.yaml", "compose.k6.yaml"); Profiles = @("k6"); Description = "Overlay k6 padrao para cenarios de carga Kafka" },
  [pscustomobject]@{ Name = "stack-kafka-k6"; Files = @("compose.yaml", "compose.kafka.yaml", "compose.k6.yaml"); Profiles = @("k6"); Description = "Caminho k6 full-stack que aplica o alias Kafka explicito" },
  [pscustomobject]@{ Name = "stack-cloudsql"; Files = @("compose.yaml", "compose.cloudsql.yaml"); Profiles = @(); Description = "Smoke manual/local com Cloud SQL Auth Proxy" },
  [pscustomobject]@{ Name = "stack-sonar"; Files = @("compose.yaml", "compose.sonar.yaml"); Profiles = @("quality"); Description = "SonarQube local junto da rede Compose do projeto" },
  [pscustomobject]@{ Name = "stack-pubsub-legacy"; Files = @("compose.yaml", "compose.pubsub.yaml"); Profiles = @("legacy-pubsub"); Description = "Provider alternativo/legado Pub/Sub" }
)

Push-Location $root
try {
  Set-PlaceholderEnv

  foreach ($combination in $combinations) {
    $arguments = @("compose", "--env-file", $EnvFile)
    foreach ($file in $combination.Files) {
      $arguments += @("-f", $file)
    }
    foreach ($profile in $combination.Profiles) {
      $arguments += @("--profile", $profile)
    }
    $arguments += @("config", "--quiet")

    Write-Host "Validando Compose: $($combination.Name)"
    Write-Host "  descricao: $($combination.Description)"
    Write-Host "  arquivos: $($combination.Files -join ',')"
    if ($combination.Profiles.Count -gt 0) {
      Write-Host "  profiles: $($combination.Profiles -join ',')"
    }
    else {
      Write-Host "  profiles: nenhum"
    }

    & docker @arguments
    if ($LASTEXITCODE -ne 0) {
      throw "Falha ao validar a combinacao Compose '$($combination.Name)'. Comando: docker $($arguments -join ' ')"
    }

    Write-Host "OK: $($combination.Name)"
  }

  Write-Host "Todas as combinacoes Docker Compose suportadas foram validadas."
}
finally {
  [System.Environment]::SetEnvironmentVariable("CLOUDSQL_INSTANCE_CONNECTION_NAME", $previousCloudSqlInstance, "Process")
  [System.Environment]::SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", $previousGoogleCredentials, "Process")
  Pop-Location
}
