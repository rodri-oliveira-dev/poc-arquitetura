[CmdletBinding()]
param(
  [string]$CertDirectory = (Join-Path $PSScriptRoot "..\..\infra\nginx\certs"),
  [int]$Days = 365
)

$ErrorActionPreference = "Stop"

$certDirectoryPath = [System.IO.Path]::GetFullPath($CertDirectory)
$certPath = Join-Path $certDirectoryPath "localhost.crt"
$keyPath = Join-Path $certDirectoryPath "localhost.key"
$hosts = @("localhost", "ledger.localhost", "balance.localhost")

New-Item -ItemType Directory -Force -Path $certDirectoryPath | Out-Null

if (Get-Command mkcert -ErrorAction SilentlyContinue) {
  mkcert -install
  if ($LASTEXITCODE -ne 0) { throw "mkcert -install falhou: $LASTEXITCODE" }

  mkcert -cert-file $certPath -key-file $keyPath @hosts
  if ($LASTEXITCODE -ne 0) { throw "mkcert falhou ao gerar certificados: $LASTEXITCODE" }
} elseif (Get-Command openssl -ErrorAction SilentlyContinue) {
  openssl req -x509 -newkey rsa:2048 -nodes -days $Days `
    -keyout $keyPath `
    -out $certPath `
    -subj "/CN=localhost" `
    -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost"
  if ($LASTEXITCODE -ne 0) { throw "openssl falhou ao gerar certificados: $LASTEXITCODE" }
} else {
  throw "Instale mkcert ou OpenSSL para gerar os certificados locais do Nginx."
}

if (-not (Test-Path $certPath) -or -not (Test-Path $keyPath)) {
  throw "Falha ao gerar localhost.crt e localhost.key em $certDirectoryPath."
}

Write-Host "Certificado local gerado em $certPath"
Write-Host "Chave privada local gerada em $keyPath"
Write-Host "Esses arquivos sao ignorados pelo Git e nao devem ser versionados."
