[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$libDir = Join-Path $scriptDir "lib"
if (-not (Test-Path -LiteralPath (Join-Path $libDir "common.ps1") -PathType Leaf)) {
  $libDir = Join-Path $scriptDir "..\lib"
}
. (Join-Path $libDir "common.ps1")
$script:RootDir = Resolve-RepositoryRoot -StartPath $scriptDir
$root = $script:RootDir

function Normalize-EnvValue([string]$value) {
  if ($null -eq $value) { return "" }
  $trimmed = $value.Trim()
  if ($trimmed.Length -ge 2) {
    if (($trimmed.StartsWith('"') -and $trimmed.EndsWith('"')) -or ($trimmed.StartsWith("'") -and $trimmed.EndsWith("'"))) {
      return $trimmed.Substring(1, $trimmed.Length - 2)
    }
  }
  return $trimmed
}

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
    $v = Normalize-EnvValue $t.Substring($idx + 1)
    if (![string]::IsNullOrWhiteSpace($k)) { $map[$k] = $v }
  }
  return $map
}

function Get-EnvOrEmpty([string]$name) {
  $v = [System.Environment]::GetEnvironmentVariable($name)
  if ($null -eq $v) { return "" }
  return $v
}

function Get-ConfigValue([string]$name, [string]$defaultValue = "") {
  $value = Get-EnvOrEmpty $name
  if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
  if ($script:localEnvFile.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace($script:localEnvFile[$name])) {
    return $script:localEnvFile[$name]
  }
  if ($script:envFile.ContainsKey($name) -and -not [string]::IsNullOrWhiteSpace($script:envFile[$name])) {
    return $script:envFile[$name]
  }
  return $defaultValue
}

function Combine-Url([string]$baseUrl, [string]$path) {
  if ([string]::IsNullOrWhiteSpace($baseUrl)) { return $path }
  if ([string]::IsNullOrWhiteSpace($path)) { return $baseUrl }
  if ($path.StartsWith("http://") -or $path.StartsWith("https://")) { return $path }
  return ($baseUrl.TrimEnd("/") + "/" + $path.TrimStart("/"))
}

function Fail([string]$msg) {
  [Console]::Error.WriteLine($msg)
  exit 1
}

function Get-ErrorSummary($errorRecord) {
  $status = ""
  try {
    if ($errorRecord.Exception.Response -and $errorRecord.Exception.Response.StatusCode) {
      $status = "HTTP $([int]$errorRecord.Exception.Response.StatusCode)"
    }
  } catch { }

  $details = ""
  try {
    if ($errorRecord.ErrorDetails -and $errorRecord.ErrorDetails.Message) {
      $json = $errorRecord.ErrorDetails.Message | ConvertFrom-Json
      if ($json.error) { $details = [string]$json.error }
      if ($json.error_description) {
        if ([string]::IsNullOrWhiteSpace($details)) {
          $details = [string]$json.error_description
        } else {
          $details = "$details - $($json.error_description)"
        }
      }
    }
  } catch { }

  if (-not [string]::IsNullOrWhiteSpace($status) -and -not [string]::IsNullOrWhiteSpace($details)) {
    return "$status - $details"
  }
  if (-not [string]::IsNullOrWhiteSpace($status)) { return $status }
  return $errorRecord.Exception.Message
}

function Get-TokenFromResponse($response) {
  if ($null -eq $response) {
    Fail "Resposta vazia do provedor de token"
  }

  $token = $response.access_token
  if ([string]::IsNullOrWhiteSpace($token)) {
    $token = $response.accessToken
  }

  if ([string]::IsNullOrWhiteSpace($token)) {
    Fail "Token nao encontrado no response. Campos esperados: accessToken|access_token"
  }

  return $token
}

function Request-KeycloakToken {
  $keycloakBaseUrl = Get-ConfigValue "KEYCLOAK_BASE_URL"
  if ([string]::IsNullOrWhiteSpace($keycloakBaseUrl)) {
    $keycloakHostPort = Get-ConfigValue "KEYCLOAK_HOST_PORT" "8081"
    $keycloakBaseUrl = "http://localhost:$keycloakHostPort"
  }

  $realm = Get-ConfigValue "KEYCLOAK_REALM" "poc"
  $tokenUrl = Get-ConfigValue "KEYCLOAK_TOKEN_URL"
  if ([string]::IsNullOrWhiteSpace($tokenUrl)) {
    $tokenUrl = "/realms/$realm/protocol/openid-connect/token"
  }

  $clientId = Get-ConfigValue "KEYCLOAK_CLIENT_ID" "poc-automation"
  $clientSecret = Get-ConfigValue "KEYCLOAK_CLIENT_SECRET"
  $scope = Get-ConfigValue "KEYCLOAK_SCOPE"

  if ([string]::IsNullOrWhiteSpace($clientId)) { Fail "KEYCLOAK_CLIENT_ID nao informado" }
  if ([string]::IsNullOrWhiteSpace($clientSecret)) { Fail "KEYCLOAK_CLIENT_SECRET nao informado" }

  $url = Combine-Url $keycloakBaseUrl $tokenUrl
  $body = @{
    grant_type = "client_credentials"
    client_id = $clientId
    client_secret = $clientSecret
  }
  if (-not [string]::IsNullOrWhiteSpace($scope)) {
    $body["scope"] = $scope
  }

  try {
    $resp = Invoke-RestMethod -Method Post -Uri $url -ContentType "application/x-www-form-urlencoded" -Body $body
  } catch {
    $summary = Get-ErrorSummary $_
    Fail "Falha ao obter token Keycloak em '$url': $summary"
  }

  return Get-TokenFromResponse $resp
}

function Request-AuthApiToken {
  $authBaseUrl = Get-EnvOrEmpty "AUTH_BASE_URL"
  $tokenUrl = Get-ConfigValue "TOKEN_URL"

  # Por padrao, o script roda no host. O AUTH_BASE_URL gerado para k6 usa o nome
  # do service dentro da rede Docker e so deve ser usado quando solicitado.
  $useEnvFileAuthBaseUrl = (Get-EnvOrEmpty "USE_ENVFILE_AUTH_BASE_URL").ToLowerInvariant() -eq "true"
  if ($useEnvFileAuthBaseUrl -and [string]::IsNullOrWhiteSpace($authBaseUrl) -and $script:envFile.ContainsKey("AUTH_BASE_URL")) {
    $authBaseUrl = $script:envFile["AUTH_BASE_URL"]
  }

  $authPocUsername = Get-ConfigValue "AUTH_POC_USERNAME"
  $authPocPassword = Get-ConfigValue "AUTH_POC_PASSWORD"
  $authPocScope = Get-ConfigValue "AUTH_POC_SCOPE"
  $username = Get-ConfigValue "USERNAME"
  $password = Get-ConfigValue "PASSWORD"
  $scope = Get-ConfigValue "SCOPE"

  # No Windows, USERNAME existe por padrao com o usuario do SO. Para evitar colisao,
  # USERNAME/PASSWORD so sao considerados override legado se ambos estiverem preenchidos.
  if (-not [string]::IsNullOrWhiteSpace($username) -and [string]::IsNullOrWhiteSpace($password)) {
    $username = ""
  }
  if (-not [string]::IsNullOrWhiteSpace($password) -and [string]::IsNullOrWhiteSpace($username)) {
    $password = ""
  }

  if ([string]::IsNullOrWhiteSpace($username)) { $username = $authPocUsername }
  if ([string]::IsNullOrWhiteSpace($password)) { $password = $authPocPassword }
  if ([string]::IsNullOrWhiteSpace($scope)) { $scope = $authPocScope }

  if ([string]::IsNullOrWhiteSpace($username)) { $username = "local_user" }
  if ([string]::IsNullOrWhiteSpace($password)) { Fail "AUTH_POC_PASSWORD ou PASSWORD nao informado" }
  if ([string]::IsNullOrWhiteSpace($scope)) { $scope = "ledger.write balance.read" }
  if ([string]::IsNullOrWhiteSpace($tokenUrl)) { $tokenUrl = "/auth/login" }
  if ([string]::IsNullOrWhiteSpace($authBaseUrl)) { $authBaseUrl = "http://localhost:5030" }

  $url = Combine-Url $authBaseUrl $tokenUrl

  try {
    $body = @{ username = $username; password = $password; scope = $scope } | ConvertTo-Json
    $resp = Invoke-RestMethod -Method Post -Uri $url -ContentType "application/json" -Body $body
  } catch {
    $summary = Get-ErrorSummary $_
    Fail "Falha ao obter token Auth.Api em '$url': $summary"
  }

  return Get-TokenFromResponse $resp
}

$tokenOverride = Get-EnvOrEmpty "TOKEN"
if (-not [string]::IsNullOrWhiteSpace($tokenOverride)) {
  Write-Output $tokenOverride
  exit 0
}

$envFilePath = Get-EnvOrEmpty "ENV_FILE"
if ([string]::IsNullOrWhiteSpace($envFilePath)) {
  $envFilePath = (Join-Path $root ".env.k6.auto")
}

$script:envFile = Read-EnvFile $envFilePath
$script:localEnvFile = Read-EnvFile (Join-Path $root ".env.local")
if ($script:localEnvFile.Count -eq 0) {
  $script:localEnvFile = Read-EnvFile (Join-Path $root ".env")
}

$provider = (Get-ConfigValue "TOKEN_PROVIDER" "keycloak").ToLowerInvariant()

switch ($provider) {
  "keycloak" {
    Write-Output (Request-KeycloakToken)
    exit 0
  }
  "auth-api" {
    Write-Output (Request-AuthApiToken)
    exit 0
  }
  default {
    Fail "TOKEN_PROVIDER invalido: '$provider'. Valores aceitos: keycloak|auth-api"
  }
}
