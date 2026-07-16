# Relatorio

## Ordem anterior

Pipeline externo das seis APIs:

1. `UseForwardedHeaders`
2. Swagger/OpenAPI
3. `UseApiDefaults`
4. `UseAuthentication`
5. `UseAuthorization`
6. health endpoints
7. controllers ou minimal APIs

`UseApiDefaults` executava HSTS, exception handler, status code pages, HTTPS
redirection, correlation ID, limite de body, security headers, CORS e rate
limiting.

## Nova ordem

Pipeline externo das seis APIs:

1. `UseForwardedHeaders`
2. `UseApiDefaults`
3. Swagger/OpenAPI
4. `UseAuthentication`
5. `UseAuthorization`
6. health endpoints
7. controllers ou minimal APIs

`UseApiDefaults` executa HSTS, correlation ID, security headers, exception
handler, status code pages, HTTPS redirection, limite de body, CORS e rate
limiting.

## Riscos tratados

- Swagger/OpenAPI fora dos security headers.
- Middleware duplicado para `X-Content-Type-Options`.
- CSP global quebrando Swagger UI.
- Redirecionamento HTTPS sem correlation ID e security headers.

## Politica CSP adotada

OpenAPI JSON e endpoints normais usam CSP global restrita. Swagger UI usa CSP
especifica com `unsafe-inline` apenas para `style-src` e `script-src`, limitada
aos caminhos da documentacao interativa sob `/swagger`.

## Validacao

Executado com sucesso:

- `dotnet test ./tests/Shared/ApiDefaults.Tests/ApiDefaults.Tests.csproj --configuration Release`
- `dotnet restore ./PocArquitetura.Shared.slnx`
- `dotnet build ./PocArquitetura.Shared.slnx --configuration Release --no-restore`
- `dotnet test ./PocArquitetura.Shared.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
- `dotnet restore ./PocArquitetura.slnx`
- `dotnet build ./PocArquitetura.slnx --configuration Release --no-restore`
- `dotnet test ./tests/audit/AuditService.Api.Tests/AuditService.Api.Tests.csproj --configuration Release --no-build`
- `dotnet test ./tests/identity/IdentityService.UnitTests/IdentityService.UnitTests.csproj --configuration Release --no-build`
- `dotnet test ./tests/balance/BalanceService.UnitTests/BalanceService.UnitTests.csproj --configuration Release --no-build`
- `dotnet format whitespace ./PocArquitetura.slnx --include <arquivos-cs-alterados> --verify-no-changes`

Validacoes tentadas e nao concluidas:

- `dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
  excedeu o timeout local de 4 minutos antes de devolver resumo.
- `dotnet test ./tests/ledger/LedgerService.UnitTests/LedgerService.UnitTests.csproj --configuration Release --no-build`
  excedeu o timeout local de 2 minutos quando executado isoladamente.

Nao foi executada nova geracao OpenAPI porque a mudanca nao altera endpoints,
payloads, status codes, autenticacao, autorizacao, headers documentados em
contrato nem documentos OpenAPI gerados. A alteracao e de pipeline/runtime de
headers HTTP e Swagger UI.
