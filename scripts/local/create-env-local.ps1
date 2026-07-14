[CmdletBinding()]
param(
  [switch]$Force,
  [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Resolve-Path (Join-Path $scriptDir "..\..")
$targetPath = if ([string]::IsNullOrWhiteSpace($OutputPath)) {
  Join-Path $root ".env.local"
} else {
  $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
}

if ((Test-Path $targetPath) -and -not $Force) {
  throw "$targetPath ja existe. Use -Force para recriar conscientemente."
}

function New-LocalSecret([string]$Name) {
  $bytes = [byte[]]::new(24)
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  try {
    $rng.GetBytes($bytes)
  } finally {
    $rng.Dispose()
  }
  $suffix = [BitConverter]::ToString($bytes).Replace("-", "").ToLowerInvariant()
  $normalized = $Name.ToLowerInvariant() -replace '[^a-z0-9]+', '_'
  return "local_${normalized}_${suffix}"
}

$values = [ordered]@{
  POSTGRES_PASSWORD = New-LocalSecret "POSTGRES_PASSWORD"
  POSTGRES_HOST_PORT = "15432"
  LEDGER_DB_PASSWORD = New-LocalSecret "LEDGER_DB_PASSWORD"
  LEDGER_DB_MIGRATOR_PASSWORD = New-LocalSecret "LEDGER_DB_MIGRATOR_PASSWORD"
  BALANCE_DB_READ_PASSWORD = New-LocalSecret "BALANCE_DB_READ_PASSWORD"
  BALANCE_DB_WRITE_PASSWORD = New-LocalSecret "BALANCE_DB_WRITE_PASSWORD"
  BALANCE_DB_MIGRATOR_PASSWORD = New-LocalSecret "BALANCE_DB_MIGRATOR_PASSWORD"
  TRANSFER_DB_PASSWORD = New-LocalSecret "TRANSFER_DB_PASSWORD"
  TRANSFER_DB_MIGRATOR_PASSWORD = New-LocalSecret "TRANSFER_DB_MIGRATOR_PASSWORD"
  PAYMENT_DB_PASSWORD = New-LocalSecret "PAYMENT_DB_PASSWORD"
  PAYMENT_DB_MIGRATOR_PASSWORD = New-LocalSecret "PAYMENT_DB_MIGRATOR_PASSWORD"
  AUDIT_DB_PASSWORD = New-LocalSecret "AUDIT_DB_PASSWORD"
  AUDIT_DB_MIGRATOR_PASSWORD = New-LocalSecret "AUDIT_DB_MIGRATOR_PASSWORD"
  IDENTITY_DB_PASSWORD = New-LocalSecret "IDENTITY_DB_PASSWORD"
  IDENTITY_DB_MIGRATOR_PASSWORD = New-LocalSecret "IDENTITY_DB_MIGRATOR_PASSWORD"
  KEYCLOAK_HOST_PORT = "8081"
  KEYCLOAK_BASE_URL = "http://localhost:8081"
  KEYCLOAK_INTERNAL_BASE_URL = "http://keycloak:8080"
  KEYCLOAK_REALM = "poc"
  KEYCLOAK_TOKEN_ENDPOINT = "/realms/poc/protocol/openid-connect/token"
  KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME = "local_admin"
  KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD = New-LocalSecret "KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD"
  KEYCLOAK_CLIENT_ID = "poc-automation"
  KEYCLOAK_CLIENT_SECRET = New-LocalSecret "KEYCLOAK_CLIENT_SECRET"
  KEYCLOAK_IDENTITY_ADMIN_CLIENT_ID = "identity-service-admin"
  TRANSFER_WORKER_LEDGER_AUTH_SCOPE = "ledger.write"
  KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD = New-LocalSecret "KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD"
  KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD = New-LocalSecret "KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD"
  KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD = New-LocalSecret "KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD"
  JWT_ISSUER = "http://localhost:8081/realms/poc"
  JWT_JWKS_URL = "http://keycloak:8080/realms/poc/protocol/openid-connect/certs"
  JWT_REQUIRE_HTTPS_METADATA = "false"
  TOKEN_PROVIDER = "keycloak"
  KAFKA_HOST_PORT = "19092"
  PUBSUB_PROJECT_ID = "poc-local"
  PUBSUB_EMULATOR_HOST_PORT = "8085"
  PUBSUB_LEDGER_EVENTS_TOPIC_ID = "ledger.ledgerentry.created.local"
  PUBSUB_LEDGER_EVENTS_DLQ_TOPIC_ID = "ledger.ledgerentry.created.dlq.local"
  PUBSUB_BALANCE_SUBSCRIPTION_ID = "balance-service-ledger-events-local"
  PUBSUB_LEDGER_EVENTS_DLQ_INSPECTION_SUBSCRIPTION_ID = "ledger-events-application-dlq-inspection-local"
  LEDGER_SERVICE_HOST_PORT = "5226"
  BALANCE_SERVICE_HOST_PORT = "5228"
  TRANSFER_SERVICE_HOST_PORT = "5230"
  PAYMENT_SERVICE_HOST_PORT = "5234"
  AUDIT_SERVICE_HOST_PORT = "5235"
  IDENTITY_SERVICE_HOST_PORT = "5232"
  MAILPIT_SMTP_HOST_PORT = "1025"
  MAILPIT_UI_HOST_PORT = "8025"
  "Email__Provider" = "Mailpit"
  "Email__AuthenticationUrl" = "http://localhost:8081/realms/poc/account"
  "Mailpit__Host" = "localhost"
  "Mailpit__Port" = "1025"
  "Mailpit__EnableSsl" = "false"
  "Mailpit__From" = "noreply@poc-arquitetura.local"
  "Mailpit__FromName" = "POC Arquitetura"
  "Resend__ApiKey" = ""
  "Resend__From" = "onboarding@seudominio.example"
  "Resend__FromName" = "POC Arquitetura"
  "Resend__ReplyTo" = ""
  "PaymentGateway__Provider" = "Fake"
  "PaymentGateway__Stripe__SecretKey" = ""
  "PaymentGateway__Stripe__WebhookSigningSecret" = "whsec_local_smoke"
  GRAFANA_ADMIN_PASSWORD = New-LocalSecret "GRAFANA_ADMIN_PASSWORD"
  GRAFANA_HOST_PORT = "3000"
  JAEGER_UI_HOST_PORT = "16686"
  JAEGER_OTLP_GRPC_HOST_PORT = "4317"
  JAEGER_OTLP_HTTP_HOST_PORT = "4318"
  PROMETHEUS_HOST_PORT = "9090"
  ALERTMANAGER_HOST_PORT = "9093"
  LOKI_HOST_PORT = "3100"
  ALLOY_HOST_PORT = "12345"
  OTEL_ENABLED = "false"
  DOCKER_LOG_MAX_SIZE = "10m"
  DOCKER_LOG_MAX_FILE = "3"
}

$groups = @(
  @{ Name = "PostgreSQL"; Keys = @("POSTGRES_PASSWORD", "POSTGRES_HOST_PORT", "LEDGER_DB_PASSWORD", "LEDGER_DB_MIGRATOR_PASSWORD", "BALANCE_DB_READ_PASSWORD", "BALANCE_DB_WRITE_PASSWORD", "BALANCE_DB_MIGRATOR_PASSWORD", "TRANSFER_DB_PASSWORD", "TRANSFER_DB_MIGRATOR_PASSWORD", "PAYMENT_DB_PASSWORD", "PAYMENT_DB_MIGRATOR_PASSWORD", "AUDIT_DB_PASSWORD", "AUDIT_DB_MIGRATOR_PASSWORD", "IDENTITY_DB_PASSWORD", "IDENTITY_DB_MIGRATOR_PASSWORD") },
  @{ Name = "Keycloak/JWT"; Keys = @("KEYCLOAK_HOST_PORT", "KEYCLOAK_BASE_URL", "KEYCLOAK_INTERNAL_BASE_URL", "KEYCLOAK_REALM", "KEYCLOAK_TOKEN_ENDPOINT", "KEYCLOAK_BOOTSTRAP_ADMIN_USERNAME", "KEYCLOAK_BOOTSTRAP_ADMIN_PASSWORD", "KEYCLOAK_CLIENT_ID", "KEYCLOAK_CLIENT_SECRET", "KEYCLOAK_IDENTITY_ADMIN_CLIENT_ID", "TRANSFER_WORKER_LEDGER_AUTH_SCOPE", "KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD", "KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD", "KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD", "JWT_ISSUER", "JWT_JWKS_URL", "JWT_REQUIRE_HTTPS_METADATA", "TOKEN_PROVIDER") },
  @{ Name = "Kafka/PubSub"; Keys = @("KAFKA_HOST_PORT", "PUBSUB_PROJECT_ID", "PUBSUB_EMULATOR_HOST_PORT", "PUBSUB_LEDGER_EVENTS_TOPIC_ID", "PUBSUB_LEDGER_EVENTS_DLQ_TOPIC_ID", "PUBSUB_BALANCE_SUBSCRIPTION_ID", "PUBSUB_LEDGER_EVENTS_DLQ_INSPECTION_SUBSCRIPTION_ID") },
  @{ Name = "Services"; Keys = @("LEDGER_SERVICE_HOST_PORT", "BALANCE_SERVICE_HOST_PORT", "TRANSFER_SERVICE_HOST_PORT", "PAYMENT_SERVICE_HOST_PORT", "AUDIT_SERVICE_HOST_PORT", "IDENTITY_SERVICE_HOST_PORT") },
  @{ Name = "Mailpit/Resend"; Keys = @("MAILPIT_SMTP_HOST_PORT", "MAILPIT_UI_HOST_PORT", "Email__Provider", "Email__AuthenticationUrl", "Mailpit__Host", "Mailpit__Port", "Mailpit__EnableSsl", "Mailpit__From", "Mailpit__FromName", "Resend__ApiKey", "Resend__From", "Resend__FromName", "Resend__ReplyTo") },
  @{ Name = "PaymentService"; Keys = @("PaymentGateway__Provider", "PaymentGateway__Stripe__SecretKey", "PaymentGateway__Stripe__WebhookSigningSecret") },
  @{ Name = "Observability"; Keys = @("GRAFANA_ADMIN_PASSWORD", "GRAFANA_HOST_PORT", "JAEGER_UI_HOST_PORT", "JAEGER_OTLP_GRPC_HOST_PORT", "JAEGER_OTLP_HTTP_HOST_PORT", "PROMETHEUS_HOST_PORT", "ALERTMANAGER_HOST_PORT", "LOKI_HOST_PORT", "ALLOY_HOST_PORT", "OTEL_ENABLED", "DOCKER_LOG_MAX_SIZE", "DOCKER_LOG_MAX_FILE") }
)

$output = New-Object System.Collections.Generic.List[string]
$output.Add("# Valores locais descartaveis para compose.yaml.")
$output.Add("# Gerado por scripts/local/create-env-local.ps1.")
$output.Add("# Nao versione .env.local e nao reutilize estes valores em ambientes compartilhados.")

foreach ($group in $groups) {
  $output.Add("")
  $output.Add("# $($group.Name)")
  foreach ($key in $group.Keys) {
    $output.Add("${key}=$($values[$key])")
  }
}

Set-Content -Path $targetPath -Value $output -Encoding utf8
Write-Host "Arquivo $targetPath criado com valores locais descartaveis."
