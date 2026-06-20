[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("smoke", "smoke-kafka", "balance50", "load-kafka", "balance-load-kafka", "resilience", "ledger-load-kafka", "transfer-smoke", "transfer-smoke-kafka", "transfer-load", "transfer-load-kafka", "transfer-fullstack-kafka")]
  [string]$Mode,

  [string]$Provider = "",

  [string]$ComposeFile = "",
  [string]$ComposeKafkaFile = "",
  [string]$ComposeK6File = "",

  [string]$ArtifactsDir = "",
  [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $scriptDir "lib"
if (-not (Test-Path -LiteralPath (Join-Path $libDir "common.ps1") -PathType Leaf)) {
  $libDir = Join-Path $scriptDir "..\lib"
}
. (Join-Path $libDir "common.ps1")
$script:RootDir = Resolve-RepositoryRoot -StartPath $scriptDir
$root = $script:RootDir

if ([string]::IsNullOrWhiteSpace($Provider)) {
  $Provider = [System.Environment]::GetEnvironmentVariable("MESSAGING_PROVIDER", "Process")
}
if ([string]::IsNullOrWhiteSpace($Provider)) {
  $Provider = "Kafka"
}
if ($Provider -ieq "Kafka") {
  $Provider = "Kafka"
}
elseif ($Provider -ieq "PubSub") {
  $Provider = "PubSub"
}
if ($Provider -notin @("Kafka", "PubSub")) {
  throw "Provider invalido: $Provider (use Kafka ou PubSub)."
}
if ($Provider -eq "PubSub") {
  throw "Os testes de carga padrao usam Kafka. Pub/Sub e legado/opt-in para a stack local; nao ha runner k6 Pub/Sub versionado. Use Provider=Kafka ou suba ./scripts/local/start-stack-pubsub.ps1 para validacoes manuais legadas."
}

switch ($Mode) {
  "smoke-kafka" { $Mode = "smoke" }
  "balance-load-kafka" { $Mode = "balance50" }
  "ledger-load-kafka" { $Mode = "resilience" }
  "transfer-smoke-kafka" { $Mode = "transfer-smoke" }
  "transfer-load-kafka" { $Mode = "transfer-load" }
}

if ([string]::IsNullOrWhiteSpace($ComposeFile)) { $ComposeFile = (Join-Path $root "compose.yaml") }
if ([string]::IsNullOrWhiteSpace($ComposeKafkaFile)) { $ComposeKafkaFile = (Join-Path $root "compose.kafka.yaml") }
if ([string]::IsNullOrWhiteSpace($ComposeK6File)) { $ComposeK6File = (Join-Path $root "compose.k6.yaml") }
if ([string]::IsNullOrWhiteSpace($ArtifactsDir)) { $ArtifactsDir = (Join-Path $root "artifacts\k6") }
if ([string]::IsNullOrWhiteSpace($EnvFile)) { $EnvFile = (Join-Path $root ".env.k6.auto") }

function Get-ComposeArguments([switch]$IncludeK6, [switch]$IncludeKafka) {
  $arguments = @("compose") + (Get-ComposeEnvArguments) + @("-f", $ComposeFile)
  if ($IncludeKafka) {
    $arguments += @("-f", $ComposeKafkaFile)
  }
  if ($IncludeK6) {
    $arguments += @("-f", $ComposeK6File)
  }

  return $arguments
}

function Get-ThresholdValue([string]$Prefix, [string]$Percentile, [string]$DefaultValue) {
  $specificName = "${Prefix}_HTTP_REQ_DURATION_${Percentile}_MS"
  $globalName = "K6_HTTP_REQ_DURATION_${Percentile}_MS"
  $value = Get-LocalConfigValue $specificName ""
  if ([string]::IsNullOrWhiteSpace($value)) {
    $value = Get-LocalConfigValue $globalName ""
  }
  if ([string]::IsNullOrWhiteSpace($value)) {
    return $DefaultValue
  }

  return $value
}

function Write-BalanceDatabaseAuthFailure([string]$User, [string]$Database) {
  $hostName = Get-LocalConfigValue "BALANCE_DB_HOST" "postgres-db"
  [Console]::Error.WriteLine(@"
Falha de autenticacao no banco Balance para o usuario "$User" e database "$Database".

O volume local do PostgreSQL pode ter sido inicializado com uma senha diferente.
Alterar .env ou compose.yaml nao atualiza credenciais dentro de um volume PostgreSQL existente.

Verifique:
  docker compose logs postgres-db
  docker compose logs balance-service
  docker compose exec -T postgres-db psql -h "$hostName" -U "$User" -d "$Database" -c "select 1;"

Para corrigir, atualize a senha manualmente dentro do PostgreSQL quando a senha antiga for conhecida,
ou recrie manualmente o volume local do PostgreSQL se os dados forem descartaveis.
Nenhuma acao destrutiva foi executada automaticamente.
"@)
}

function Assert-BalanceDatabaseAuthentication {
  $hostName = Get-LocalConfigValue "BALANCE_DB_HOST" "postgres-db"
  $user = Get-LocalConfigValue "BALANCE_DB_WRITE_USER" (Get-LocalConfigValue "BALANCE_DB_USER" "balance_write_user")
  $database = Get-LocalConfigValue "BALANCE_DB_NAME" "appdb"
  $password = Get-RequiredLocalConfigValue "BALANCE_DB_WRITE_PASSWORD"

  $previousErrorActionPreference = $ErrorActionPreference
  try {
    $ErrorActionPreference = "Continue"
    & docker @(Get-ComposeArguments) exec -T `
      -e "PGPASSWORD=$password" `
      "postgres-db" `
      psql -h $hostName -U $user -d $database -v "ON_ERROR_STOP=1" -c "select 1;" 1>$null 2>$null
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  if ($exitCode -ne 0) {
    Write-BalanceDatabaseAuthFailure $user $database
    exit 1
  }
}

function Wait-ComposeServiceHealthy([string]$Service, [int]$TimeoutSeconds = 240) {
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  $lastHealth = ""

  do {
    $json = & docker @(Get-ComposeArguments -IncludeK6) ps $Service --format json
    if ($LASTEXITCODE -ne 0) { throw "docker compose ps falhou para $Service" }

    $serviceState = $json | ConvertFrom-Json
    $lastHealth = [string]$serviceState.Health
    if ($lastHealth -eq "healthy") {
      return
    }

    if ($lastHealth -eq "unhealthy") {
      throw "$Service ficou unhealthy durante a preparacao do k6."
    }

    Start-Sleep -Seconds 5
  } while ((Get-Date) -lt $deadline)

  throw "Timeout aguardando $Service ficar healthy. Ultimo health: $lastHealth"
}

function Wait-HttpEndpoint([string]$Url, [int]$TimeoutSeconds = 120) {
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    try {
      Invoke-WebRequest -Method Get -Uri $Url -UseBasicParsing -TimeoutSec 5 | Out-Null
      return
    }
    catch {
      Start-Sleep -Seconds 2
    }
  } while ((Get-Date) -lt $deadline)

  throw "Timeout aguardando endpoint HTTP: $Url"
}

function Assert-LocalKafkaStack {
  $config = (& docker @(Get-ComposeArguments) config | Out-String)
  if ($LASTEXITCODE -ne 0) { throw "docker compose config falhou ao validar Kafka local." }

  foreach ($expected in @(
    "Messaging__Provider: Kafka",
    "Kafka__Producer__BootstrapServers: kafka:9092",
    "Kafka__Consumer__BootstrapServers: kafka:9092"
  )) {
    if (-not $config.Contains($expected)) {
      throw "Stack k6 deve usar Kafka local. Configuracao ausente: $expected"
    }
  }

  $runningServices = @(& docker @(Get-ComposeArguments) ps --status running --services)
  if ($LASTEXITCODE -ne 0) { throw "docker compose ps falhou ao validar Kafka local." }

  foreach ($service in @("kafka", "ledger-worker", "balance-worker")) {
    if ($runningServices -notcontains $service) {
      throw "Kafka indisponivel para o caminho padrao k6: servico obrigatorio nao esta em execucao ($service). Suba ./scripts/local/start-stack.ps1 antes do teste."
    }
  }

  & docker @(Get-ComposeArguments) exec -T kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --list 1>$null
  if ($LASTEXITCODE -ne 0) {
    throw "Kafka indisponivel em kafka:9092. Confira docker compose logs kafka e rode ./scripts/local/start-stack.ps1."
  }

  $kafkaInitJson = & docker @(Get-ComposeArguments) ps -a kafka-init-topics --format json
  if ($LASTEXITCODE -ne 0) { throw "docker compose ps falhou ao validar kafka-init-topics." }
  $kafkaInit = $kafkaInitJson | ConvertFrom-Json
  if ([string]$kafkaInit.State -ne "exited" -or [int]$kafkaInit.ExitCode -ne 0) {
    throw "Topicos Kafka ausentes ou inicializacao incompleta. Rode ./scripts/local/start-stack.ps1 ou confira: docker compose logs kafka-init-topics"
  }
}

function Assert-LocalTransferKafkaStack {
  $config = (& docker @(Get-ComposeArguments -IncludeKafka) config | Out-String)
  if ($LASTEXITCODE -ne 0) { throw "docker compose config falhou ao validar Kafka local do TransferService." }

  foreach ($expected in @(
    "Messaging__Provider: Kafka",
    "TransferService__Worker__Kafka__BootstrapServers: kafka:9092",
    "TransferService__Worker__Topics__Solicitada: transfer.transferencia.solicitada"
  )) {
    if (-not $config.Contains($expected)) {
      throw "Stack full-stack do TransferService deve usar Kafka. Configuracao ausente: $expected"
    }
  }

  $runningServices = @(& docker @(Get-ComposeArguments -IncludeKafka) ps --status running --services)
  if ($LASTEXITCODE -ne 0) { throw "docker compose ps falhou ao validar Kafka local do TransferService." }

  foreach ($service in @("kafka", "ledger-service", "transfer-service", "transfer-worker")) {
    if ($runningServices -notcontains $service) {
      throw "Kafka full-stack indisponivel: servico obrigatorio nao esta em execucao ($service). Suba ./scripts/local/start-stack.ps1 antes do teste."
    }
  }

  & docker @(Get-ComposeArguments -IncludeKafka) exec -T kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --list 1>$null
  if ($LASTEXITCODE -ne 0) {
    throw "Kafka indisponivel em kafka:9092 para full-stack TransferService. Confira docker compose logs kafka e rode ./scripts/local/start-stack.ps1."
  }

  $kafkaInitJson = & docker @(Get-ComposeArguments -IncludeKafka) ps -a kafka-init-topics --format json
  if ($LASTEXITCODE -ne 0) { throw "docker compose ps falhou ao validar kafka-init-topics." }
  $kafkaInit = $kafkaInitJson | ConvertFrom-Json
  if ([string]$kafkaInit.State -ne "exited" -or [int]$kafkaInit.ExitCode -ne 0) {
    throw "Topicos Kafka ausentes ou inicializacao incompleta. Rode ./scripts/local/start-stack.ps1 ou confira: docker compose logs kafka-init-topics"
  }
}

function Get-KafkaTopicEndOffset([string]$Topic, [switch]$IncludeKafka) {
  $composeArgs = if ($IncludeKafka) { Get-ComposeArguments -IncludeKafka } else { Get-ComposeArguments }
  $output = & docker @($composeArgs) exec -T kafka `
    /opt/kafka/bin/kafka-get-offsets.sh `
    --bootstrap-server kafka:9092 `
    --topic $Topic `
    --time latest 2>&1

  if ($LASTEXITCODE -ne 0) {
    throw "Falha ao consultar offset Kafka do topico $Topic. Se o topico estiver ausente, rode ./scripts/local/start-stack.ps1 para executar kafka-init-topics. Detalhe: $($output | Out-String)"
  }

  $sum = [Int64]0
  foreach ($line in $output) {
    $parts = ([string]$line).Trim().Split(":")
    $parsed = [Int64]0
    if ($parts.Length -ge 3 -and [Int64]::TryParse($parts[$parts.Length - 1], [ref]$parsed)) {
      $sum += $parsed
    }
  }

  return $sum
}

function Get-TransferKafkaOffsets {
  $topics = @(
    "transfer.transferencia.solicitada",
    "transfer.transferencia.debito-criado",
    "transfer.transferencia.credito-criado",
    "transfer.transferencia.concluida",
    "transfer.transferencia.dlq"
  )

  $offsets = @{}
  foreach ($topic in $topics) {
    $offsets[$topic] = Get-KafkaTopicEndOffset $topic -IncludeKafka
  }

  return $offsets
}

function Get-LedgerKafkaOffsets {
  $topics = @(
    "ledger.ledgerentry.created",
    "ledger.ledgerentry.created.dlq"
  )

  $offsets = @{}
  foreach ($topic in $topics) {
    $offsets[$topic] = Get-KafkaTopicEndOffset $topic
  }

  return $offsets
}

function Assert-LedgerKafkaSmoke([hashtable]$Before, [hashtable]$After) {
  $topic = "ledger.ledgerentry.created"
  if ([Int64]$After[$topic] -le [Int64]$Before[$topic]) {
    throw "Kafka nao recebeu evento esperado no topico $topic durante smoke Ledger/Balance."
  }

  $dlqTopic = "ledger.ledgerentry.created.dlq"
  if ([Int64]$After[$dlqTopic] -ne [Int64]$Before[$dlqTopic]) {
    throw "DLQ Kafka recebeu mensagem no fluxo feliz Ledger/Balance: topic=$dlqTopic antes=$($Before[$dlqTopic]) depois=$($After[$dlqTopic]). Inspecione topic/key/correlation_id com kafka-console-consumer."
  }

  Write-Output "OK. Smoke Kafka publicou LedgerEntryCreated e manteve DLQ do Balance sem crescimento."
}

function Read-KafkaTopicSample([string]$Topic, [int]$MaxMessages = 1000, [int]$TimeoutMs = 10000) {
  $previousErrorActionPreference = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $output = & docker @(Get-ComposeArguments -IncludeKafka) exec -T kafka `
      /opt/kafka/bin/kafka-console-consumer.sh `
      --bootstrap-server kafka:9092 `
      --topic $Topic `
      --from-beginning `
      --timeout-ms $TimeoutMs `
      --max-messages $MaxMessages `
      --property print.key=true `
      --property key.separator="@@KEY@@" 2>&1
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  return @($output)
}

function Find-TransferKafkaEvent([string]$Topic, [string]$ExpectedEventType, [string]$CorrelationId, [string]$ExpectedTransferenciaId) {
  $lines = Read-KafkaTopicSample $Topic
  foreach ($line in $lines) {
    $text = [string]$line
    if (-not $text.Contains("@@KEY@@")) { continue }

    $parts = $text -split [regex]::Escape("@@KEY@@"), 2
    $key = $parts[0].Trim()
    $payload = $parts[1]

    try {
      $event = $payload | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
      continue
    }

    if ($event.eventType -ne $ExpectedEventType) { continue }
    if ($event.correlationId -ne $CorrelationId) { continue }

    $transferenciaId = [string]$event.transferenciaId
    if ([string]::IsNullOrWhiteSpace($transferenciaId)) {
      throw "Evento Kafka $ExpectedEventType em $Topic nao contem transferenciaId no payload."
    }

    if ($key -ne $transferenciaId) {
      throw "Kafka publicou $ExpectedEventType com message key divergente: topic=$Topic key=$key transferenciaId=$transferenciaId correlationId=$CorrelationId"
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedTransferenciaId) -and $transferenciaId -ne $ExpectedTransferenciaId) {
      throw "Kafka publicou $ExpectedEventType para transferenciaId inesperado: topic=$Topic esperado=$ExpectedTransferenciaId recebido=$transferenciaId correlationId=$CorrelationId"
    }

    return $transferenciaId
  }

  throw "Kafka nao encontrou $ExpectedEventType com correlationId=$CorrelationId no topico $Topic. Se o topico estiver ausente, rode ./scripts/local/start-stack.ps1 para executar kafka-init-topics."
}

function Assert-TransferKafkaEventsPublished([hashtable]$Before, [hashtable]$After, [string]$CorrelationId) {
  $expectedTransferenciaId = ""
  foreach ($entry in @(
    @{ Topic = "transfer.transferencia.solicitada"; EventType = "TransferenciaSolicitada.v1" },
    @{ Topic = "transfer.transferencia.debito-criado"; EventType = "TransferenciaDebitoCriado.v1" },
    @{ Topic = "transfer.transferencia.credito-criado"; EventType = "TransferenciaCreditoCriado.v1" },
    @{ Topic = "transfer.transferencia.concluida"; EventType = "TransferenciaConcluida.v1" }
  )) {
    $topic = $entry["Topic"]
    if ([Int64]$After[$topic] -le [Int64]$Before[$topic]) {
      throw "Kafka nao recebeu evento esperado no topico $topic durante o full-stack smoke."
    }

    $expectedTransferenciaId = Find-TransferKafkaEvent $topic $entry["EventType"] $CorrelationId $expectedTransferenciaId
  }

  $dlqTopic = "transfer.transferencia.dlq"
  if ([Int64]$After[$dlqTopic] -ne [Int64]$Before[$dlqTopic]) {
    throw "DLQ Kafka recebeu mensagem no fluxo feliz do TransferService: topic=$dlqTopic antes=$($Before[$dlqTopic]) depois=$($After[$dlqTopic]) correlationId=$CorrelationId. Inspecione topic/key/correlation_id com kafka-console-consumer."
  }

  Write-Output "OK. Full-stack Kafka publicou eventos da Saga com key=transferenciaId, correlationId esperado e DLQ sem crescimento."
}

function Get-PostgresCount([string]$Service, [string]$User, [string]$Database, [string]$Sql, [string]$Password) {
  $value = & docker @(Get-ComposeArguments) exec -T `
    -e "PGPASSWORD=$Password" `
    $Service `
    psql -h $Service -U $User -d $Database -t -A -c $Sql
  if ($LASTEXITCODE -ne 0) { throw "Falha ao consultar $Service durante validacao do smoke Pub/Sub." }
  return [int](($value | Out-String).Trim())
}

function Get-AsyncFlowCounts {
  $ledgerUser = Get-LocalConfigValue "LEDGER_DB_USER" "ledger_app_user"
  $ledgerDatabase = Get-LocalConfigValue "LEDGER_DB_NAME" "appdb"
  $ledgerPassword = Get-RequiredLocalConfigValue "LEDGER_DB_PASSWORD"
  $balanceUser = Get-LocalConfigValue "BALANCE_DB_READ_USER" (Get-LocalConfigValue "BALANCE_DB_USER" "balance_read_user")
  $balanceDatabase = Get-LocalConfigValue "BALANCE_DB_NAME" "appdb"
  $balancePassword = Get-RequiredLocalConfigValue "BALANCE_DB_READ_PASSWORD"
  return @{
    OutboxTotal = (Get-PostgresCount "postgres-db" $ledgerUser $ledgerDatabase "SELECT COUNT(*) FROM outbox_messages WHERE event_type IN ('LedgerEntryCreated.v1', 'LedgerEntryCreated.v2');" $ledgerPassword)
    OutboxProcessed = (Get-PostgresCount "postgres-db" $ledgerUser $ledgerDatabase "SELECT COUNT(*) FROM outbox_messages WHERE event_type IN ('LedgerEntryCreated.v1', 'LedgerEntryCreated.v2') AND status = 'Processed';" $ledgerPassword)
    BalanceProcessed = (Get-PostgresCount "postgres-db" $balanceUser $balanceDatabase "SELECT COUNT(*) FROM processed_events;" $balancePassword)
  }
}

function Wait-AsyncFlowProgress([hashtable]$Before, [int]$TimeoutSeconds = 90) {
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    $current = Get-AsyncFlowCounts
    if ($current.OutboxProcessed -gt $Before.OutboxProcessed -and $current.BalanceProcessed -gt $Before.BalanceProcessed) {
      Write-Output "OK. Smoke Kafka publicou Outbox e projetou evento no Balance."
      return
    }

    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)

  throw "Timeout aguardando publish/consume via Kafka apos smoke k6."
}

function Wait-AsyncFlowIdle([int]$TimeoutSeconds = 120) {
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  do {
    $current = Get-AsyncFlowCounts
    if ($current.OutboxProcessed -ge $current.OutboxTotal -and $current.BalanceProcessed -ge $current.OutboxProcessed) {
      Write-Output "OK. Fluxo assincrono local sem backlog antes do k6."
      return
    }

    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)

  throw "Timeout aguardando drenagem do fluxo assincrono antes do k6."
}

# a) gerar env
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\lib\compose-env.ps1") -ComposeFile $ComposeFile -OutFile $EnvFile | Out-Host

$isTransferFullStackKafka = $Mode -eq "transfer-fullstack-kafka"
$isTransferMode = $Mode -in @("transfer-smoke", "transfer-load", "transfer-fullstack-kafka")

if (-not $isTransferMode) {
  Assert-LocalKafkaStack
}

# Aplica o override de carga nas APIs antes de executar o k6. O compose.k6.yaml
# mantem os testes apontando para as APIs HTTP e aumenta apenas limites tecnicos
# que poderiam transformar o cenario de throughput em teste de rate limiting.
if ($isTransferMode) {
  if ($isTransferFullStackKafka) {
    & docker @(Get-ComposeArguments -IncludeK6 -IncludeKafka) up -d --no-build --force-recreate kafka kafka-init-topics ledger-service transfer-service transfer-worker
  }
  else {
    & docker @(Get-ComposeArguments -IncludeK6) up -d --no-build --force-recreate transfer-service
  }
}
else {
  & docker @(Get-ComposeArguments -IncludeK6) up -d --no-build --force-recreate ledger-service balance-service
}
if ($LASTEXITCODE -ne 0) { throw "docker compose falhou ao aplicar override k6: $LASTEXITCODE" }

Wait-ComposeServiceHealthy "keycloak"
if ($isTransferMode) {
  $transferHostPort = Get-LocalConfigValue "TRANSFER_SERVICE_HOST_PORT" "5230"
  Wait-HttpEndpoint "http://localhost:$transferHostPort/health"
  if ($isTransferFullStackKafka) {
    $ledgerHostPort = Get-LocalConfigValue "LEDGER_SERVICE_HOST_PORT" "5226"
    Wait-HttpEndpoint "http://localhost:$ledgerHostPort/health"
    Assert-LocalTransferKafkaStack
  }
}
else {
  Assert-BalanceDatabaseAuthentication
}

# b) obter token pelo provider local configurado. Por padrao, Keycloak.
function Get-LoadTestToken {
  $getTokenScript = Join-Path $root "scripts\validation\get-token.ps1"
  $previousEnvFile = [System.Environment]::GetEnvironmentVariable("ENV_FILE", "Process")
  [System.Environment]::SetEnvironmentVariable("ENV_FILE", $EnvFile, "Process")

  try {
    for ($i = 1; $i -le 30; $i++) {
      $candidate = powershell -NoProfile -ExecutionPolicy Bypass -File $getTokenScript 2>$null
      if ($LASTEXITCODE -eq 0) {
        $candidate = ($candidate | Out-String).Trim()
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
          return $candidate
        }
      }

      Start-Sleep -Seconds 2
    }

    $finalAttempt = powershell -NoProfile -ExecutionPolicy Bypass -File $getTokenScript
    return ($finalAttempt | Out-String).Trim()
  }
  finally {
    [System.Environment]::SetEnvironmentVariable("ENV_FILE", $previousEnvFile, "Process")
  }
}

$token = Get-LoadTestToken
if ([string]::IsNullOrWhiteSpace($token)) {
  Write-Error "Falha ao obter TOKEN. Voce pode informar manualmente via env TOKEN=..."
  exit 1
}

function Invoke-BalanceWarmup([string]$token) {
  $date = [System.Environment]::GetEnvironmentVariable("DATE")
  if ([string]::IsNullOrWhiteSpace($date)) {
    $date = (Get-Date).ToString("yyyy-MM-dd")
  }

  $merchantId = [System.Environment]::GetEnvironmentVariable("MERCHANT_ID")
  if ([string]::IsNullOrWhiteSpace($merchantId)) {
    $merchantId = "tese"
  }

  $encodedMerchantId = [System.Uri]::EscapeDataString($merchantId)
  $url = "http://localhost:5228/api/v1/consolidados/diario/${date}?merchantId=$encodedMerchantId"
  $headers = @{ Authorization = "Bearer $token" }

  for ($i = 1; $i -le 30; $i++) {
    try {
      Invoke-WebRequest -Method Get -Uri $url -Headers $headers -UseBasicParsing | Out-Null
      return
    }
    catch {
      if ($i -eq 30) { throw }
      Start-Sleep -Seconds 1
    }
  }
}

if (-not $isTransferMode) {
  Invoke-BalanceWarmup $token
}

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
$ts = Get-Date -Format "yyyyMMdd-HHmmss"

function Assert-K6Summary([string]$summaryPath) {
  if (-not (Test-Path $summaryPath)) {
    throw "Summary k6 nao encontrado: $summaryPath"
  }

  $summary = Get-Content -Raw -Path $summaryPath | ConvertFrom-Json
  $checksFailed = 0
  $httpFailedRate = 0.0
  $droppedIterations = 0

  if ($summary.metrics.checks -and $null -ne $summary.metrics.checks.fails) {
    $checksFailed = [int]$summary.metrics.checks.fails
  }
  if ($summary.metrics.http_req_failed -and $null -ne $summary.metrics.http_req_failed.value) {
    $httpFailedRate = [double]$summary.metrics.http_req_failed.value
  }
  if ($summary.metrics.dropped_iterations -and $null -ne $summary.metrics.dropped_iterations.count) {
    $droppedIterations = [int]$summary.metrics.dropped_iterations.count
  }

  if ($checksFailed -gt 0 -or $httpFailedRate -gt 0.05 -or $droppedIterations -gt 0) {
    throw "k6 falhou: checks_failed=$checksFailed; http_req_failed=$httpFailedRate; dropped_iterations=$droppedIterations"
  }
}

function Run-K6([string]$scenarioName, [string]$scriptPath, [hashtable]$envVars) {
  $summaryFile = "summary-$Mode-$scenarioName-$ts.json"
  $summary = "/artifacts/$summaryFile"
  $hostSummary = Join-Path $ArtifactsDir $summaryFile

  $args = @(
    "compose"
  ) + (Get-ComposeEnvArguments) + @(
    "-f", $ComposeFile,
    "-f", $ComposeK6File,
    "--profile", "k6",
    "run",
    "--interactive=false",
    "--rm"
  )

  foreach ($k in $envVars.Keys) {
    $args += "-e"; $args += "$k=$($envVars[$k])"
  }

  if (-not $envVars.ContainsKey("MESSAGING_PROVIDER")) {
    $args += "-e"; $args += "MESSAGING_PROVIDER=Kafka"
  }
  if (-not $envVars.ContainsKey("KAFKA_BOOTSTRAP_SERVERS")) {
    $args += "-e"; $args += "KAFKA_BOOTSTRAP_SERVERS=kafka:9092"
  }

  $args += @(
    "k6",
    "run",
    $scriptPath,
    "--summary-export", $summary
  )

  & docker @args
  if ($LASTEXITCODE -ne 0) { throw "k6 falhou: $LASTEXITCODE" }

  Assert-K6Summary $hostSummary
}

switch ($Mode) {
  "smoke" {
    $asyncFlowBefore = Get-AsyncFlowCounts
    $ledgerKafkaBefore = Get-LedgerKafkaOffsets
    Run-K6 "ledger_resilience" "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "1"; DURATION = "10s"; LEDGER_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "LEDGER" "P95" "3000"); LEDGER_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "LEDGER" "P99" "6000") }
    Wait-AsyncFlowProgress $asyncFlowBefore
    $ledgerKafkaAfter = Get-LedgerKafkaOffsets
    Assert-LedgerKafkaSmoke $ledgerKafkaBefore $ledgerKafkaAfter
    Run-K6 "balance_daily_50rps" "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "1"; DURATION = "10s"; PREALLOCATED_VUS = "5"; MAX_VUS = "10"; BALANCE_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "BALANCE" "P95" "3000"); BALANCE_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "BALANCE" "P99" "6000") }
  }
  "balance50" {
    Wait-AsyncFlowIdle
    Run-K6 "balance_daily_50rps" "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "50"; DURATION = "1m"; BALANCE_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "BALANCE" "P95" "1000"); BALANCE_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "BALANCE" "P99" "2500") }
  }
  "load-kafka" {
    Wait-AsyncFlowIdle
    Run-K6 "balance_daily_light" "scenarios/balance_daily_50rps.js" @{ TOKEN = $token; RATE = "5"; DURATION = "30s"; PREALLOCATED_VUS = "10"; MAX_VUS = "30"; BALANCE_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "BALANCE" "P95" "1000"); BALANCE_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "BALANCE" "P99" "2500") }
  }
  "resilience" {
    Run-K6 "ledger_resilience" "scenarios/ledger_resilience.js" @{ TOKEN = $token; VUS = "5"; DURATION = "1m"; LEDGER_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "LEDGER" "P95" "2000"); LEDGER_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "LEDGER" "P99" "5000") }
  }
  "transfer-smoke" {
    Run-K6 "transfer_smoke" "scenarios/transfer_smoke.js" @{ TOKEN = $token; DURATION = "30s"; TRANSFER_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "TRANSFER" "P95" "500"); TRANSFER_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "TRANSFER" "P99" "1000") }
  }
  "transfer-load" {
    Run-K6 "transfer_load" "scenarios/transfer_load.js" @{ TOKEN = $token; VUS = "10"; TRANSFER_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "TRANSFER" "P95" "1000"); TRANSFER_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "TRANSFER" "P99" "2000") }
  }
  "transfer-fullstack-kafka" {
    $kafkaOffsetsBefore = Get-TransferKafkaOffsets
    $transferCorrelationId = [guid]::NewGuid().ToString()
    Run-K6 "transfer_fullstack_kafka" "scenarios/transfer_fullstack_kafka.js" @{ TOKEN = $token; VUS = "1"; ITERATIONS = "1"; DURATION = "90s"; TRANSFER_FINAL_STATUS_TIMEOUT_SECONDS = "60"; TRANSFER_CORRELATION_ID = $transferCorrelationId; TRANSFER_HTTP_REQ_DURATION_P95_MS = (Get-ThresholdValue "TRANSFER" "P95" "1000"); TRANSFER_HTTP_REQ_DURATION_P99_MS = (Get-ThresholdValue "TRANSFER" "P99" "2000") }
    $kafkaOffsetsAfter = Get-TransferKafkaOffsets
    Assert-TransferKafkaEventsPublished $kafkaOffsetsBefore $kafkaOffsetsAfter $transferCorrelationId
  }
}

Write-Output "OK. Artifacts em: $ArtifactsDir"

# Execucao local aqui nao consegue (e nao deve) validar thresholds (k6 ja retorna != 0 em caso de falha).
# TODO: opcionalmente parsear o summary JSON e imprimir um resumo (sem segredos).

