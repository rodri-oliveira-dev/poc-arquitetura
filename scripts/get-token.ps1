[CmdletBinding()]
param(
  [string]$ReadmePath = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path (Join-Path $scriptDir ".."))

if ([string]::IsNullOrWhiteSpace($ReadmePath)) {
  $ReadmePath = (Join-Path $root "README.md")
}

if (!(Test-Path $ReadmePath)) {
  Write-Error "README não encontrado em '$ReadmePath'"
  exit 2
}

$readme = Get-Content -Raw -Path $ReadmePath

function Read-EnvFile([string]$path) {
  $map = @{}
  if (!(Test-Path $path)) { return $map }
  $lines = Get-Content -Path $path
  foreach ($line in $lines) {
    $t = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($t)) { continue }
    if ($t.StartsWith('#')) { continue }
    $idx = $t.IndexOf('=')
    if ($idx -le 0) { continue }
    $k = $t.Substring(0, $idx).Trim()
    $v = $t.Substring($idx + 1).Trim()
    if (![string]::IsNullOrWhiteSpace($k)) { $map[$k] = $v }
  }
  return $map
}

function Get-EnvOrEmpty([string]$name) {
  $v = [System.Environment]::GetEnvironmentVariable($name)
  if ($null -eq $v) { return "" }
  return $v
}

function Combine-Url([string]$baseUrl, [string]$path) {
  if ([string]::IsNullOrWhiteSpace($baseUrl)) { return $path }
  if ([string]::IsNullOrWhiteSpace($path)) { return $baseUrl }
  if ($path.StartsWith("http://") -or $path.StartsWith("https://")) { return $path }
  return ($baseUrl.TrimEnd("/") + "/" + $path.TrimStart("/"))
}

function Fail([string]$msg) {
  Write-Error $msg
  exit 1
}

# Overrides via env
$tokenOverride = (Get-EnvOrEmpty "TOKEN")
$envFilePath = (Get-EnvOrEmpty "ENV_FILE")

if ([string]::IsNullOrWhiteSpace($envFilePath)) {
  $envFilePath = (Join-Path $root ".env.k6.auto")
}

$envFile = Read-EnvFile $envFilePath

$authBaseUrl = (Get-EnvOrEmpty "AUTH_BASE_URL")
$tokenUrl = (Get-EnvOrEmpty "TOKEN_URL")
$username = (Get-EnvOrEmpty "USERNAME")
$password = (Get-EnvOrEmpty "PASSWORD")
$scope = (Get-EnvOrEmpty "SCOPE")

# Atenção: em Windows, a variável de ambiente USERNAME existe por padrão (username do SO).
# Para evitar colidir com isso, só consideramos USERNAME/PASSWORD como override se AMBOS vierem preenchidos.
if (-not [string]::IsNullOrWhiteSpace($username) -and [string]::IsNullOrWhiteSpace($password)) {
  $username = ""
}
if (-not [string]::IsNullOrWhiteSpace($password) -and [string]::IsNullOrWhiteSpace($username)) {
  $password = ""
}

if (-not [string]::IsNullOrWhiteSpace($tokenOverride)) {
  Write-Output $tokenOverride
  exit 0
}

# Importante: por padrão, o get-token roda no HOST e deve seguir o README (localhost).
# O AUTH_BASE_URL do .env.k6.auto normalmente aponta para o hostname do compose network (ex.: http://auth-api:8080)
# e não resolve no host. Só use o AUTH_BASE_URL do env file se explicitamente solicitado.
$useEnvFileAuthBaseUrl = (Get-EnvOrEmpty "USE_ENVFILE_AUTH_BASE_URL").ToLowerInvariant() -eq "true"
if ($useEnvFileAuthBaseUrl -and [string]::IsNullOrWhiteSpace($authBaseUrl) -and $envFile.ContainsKey("AUTH_BASE_URL")) { $authBaseUrl = $envFile["AUTH_BASE_URL"] }
if ([string]::IsNullOrWhiteSpace($tokenUrl) -and $envFile.ContainsKey("TOKEN_URL")) { $tokenUrl = $envFile["TOKEN_URL"] }
if ([string]::IsNullOrWhiteSpace($username) -and $envFile.ContainsKey("USERNAME")) { $username = $envFile["USERNAME"] }
if ([string]::IsNullOrWhiteSpace($password) -and $envFile.ContainsKey("PASSWORD")) { $password = $envFile["PASSWORD"] }
if ([string]::IsNullOrWhiteSpace($scope) -and $envFile.ContainsKey("SCOPE")) { $scope = $envFile["SCOPE"] }

if ([string]::IsNullOrWhiteSpace($username)) { $username = "poc-usuario" }
if ([string]::IsNullOrWhiteSpace($password)) { $password = "Poc#123" }
if ([string]::IsNullOrWhiteSpace($scope)) { $scope = "ledger.write balance.read" }

# Inferência simples: este repo documenta Auth.Api Minimal API em POST /auth/login
$isMinimalApi = $readme -match "POST\s+/auth/login" -or $readme -match "curl[\s\S]*?/auth/login"

if (-not $isMinimalApi) {
  Fail "Nao foi possivel inferir como obter token. Verifique README e informe TOKEN manualmente via env TOKEN=..."
}

if ([string]::IsNullOrWhiteSpace($tokenUrl)) { $tokenUrl = "/auth/login" }

if ([string]::IsNullOrWhiteSpace($authBaseUrl)) {
  # Fallback host (apenas para execução fora do compose, como no README)
  $authBaseUrl = "http://localhost:5030"
}

$url = Combine-Url $authBaseUrl $tokenUrl

try {
  $body = @{ username = $username; password = $password; scope = $scope } | ConvertTo-Json
  $resp = Invoke-RestMethod -Method Post -Uri $url -ContentType "application/json" -Body $body
} catch {
  $msg = $_.Exception.Message
  $status = ""
  try {
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
      $status = "HTTP $([int]$_.Exception.Response.StatusCode)"
    }
  } catch { }

  $details = ""
  try {
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $details = $_.ErrorDetails.Message }
  } catch { }

  $extra = ""
  if (-not [string]::IsNullOrWhiteSpace($status)) { $extra += " ($status)" }
  if (-not [string]::IsNullOrWhiteSpace($details)) { $extra += " - $details" }

  Fail "Falha ao obter token em '$url': $msg$extra"
}

if ($null -eq $resp) {
  Fail "Resposta vazia do auth"
}

# README antigo citava accessToken, mas o contrato atual do Auth.Api usa access_token
$token = $resp.access_token
if ([string]::IsNullOrWhiteSpace($token)) {
  # fallback
  $token = $resp.accessToken
}

if ([string]::IsNullOrWhiteSpace($token)) {
  Fail "Token nao encontrado no response. Campos esperados: accessToken|access_token"
}

# stdout somente token
Write-Output $token
