# Change Report - ACT 03 (JWT Bearer via JWKS)

Este documento registra as mudanças realizadas para exigir **autenticação JWT Bearer** e **autorização por scopes** em:

- `src/LedgerService.Api`
- `src/BalanceService.Api`

## Objetivo atendido

Ambos os serviços agora:

1) **Validam JWT** usando chaves obtidas do **JWKS do Auth.Api** (`GET /.well-known/jwks.json`).
2) **Exigem autenticação por padrão** (fallback policy) nas rotas de negócio.
3) **Exigem autorização por scope por endpoint** (policy-based).

## Decisões técnicas e compatibilidade

### JWKS cacheado (sem chamada por request)

Foi adotado `ConfigurationManager<OpenIdConnectConfiguration>` com um `IConfigurationRetriever` customizado que entende JWKS “puro” (sem metadata OIDC):

- A URL configurada é **diretamente** o JWKS (`Jwt:JwksUrl`).
- O `ConfigurationManager` mantém cache e faz refresh automático quando necessário.
- Não existe introspecção síncrona nem chamadas ao Auth.Api a cada request.

### Validação estrita de issuer e audience

- `iss` é validado estritamente contra `Jwt:Issuer`.
- `aud` é validado para conter a audience específica do serviço:
  - Ledger: `ledger-api`
  - Balance: `balance-api`

**Compatibilidade com o Auth.Api atual:** o `Auth.Api` emite `aud` como **uma string com audiences separadas por espaço** (ex.: `"ledger-api balance-api"`).

Como o `JwtSecurityTokenHandler` trata isso como 1 audiência, foi implementado `AudienceValidator` que tokeniza por espaço e valida se contém a audience esperada.

### Scope claim

- Foi mantido o requisito: claim `scope` (string, scopes separados por espaço).
- Não foi usado `scp`.

## Policies por scope aplicadas

### LedgerService.Api

- Policy: `scope:ledger.write` aplicada no endpoint:
  - `POST /api/v1/lancamentos`

> TODO: `ledger.read` permanece definido no catálogo local, mas ainda não há endpoints de leitura no LedgerService.Api.

### BalanceService.Api

- Policy: `scope:balance.read` aplicada nos endpoints:
  - `GET /v1/consolidados/diario/{date}`
  - `GET /v1/consolidados/periodo`

> TODO: `balance.write` permanece definido no catálogo local, mas ainda não há endpoints de escrita no BalanceService.Api.

## Swagger / OpenAPI

### O que foi atualizado

- Adicionado `securityDefinition` do tipo HTTP Bearer (`Authorization: Bearer {token}`) em ambas as APIs.
- Adicionado `OperationFilter` para:
  - marcar endpoints com `[Authorize]` com requirement do esquema Bearer;
  - descrever quais scopes são requeridos (derivado da policy `scope:{scopeName}`).

## Arquivos alterados / adicionados

### LedgerService.Api

- `src/LedgerService.Api/Program.cs` (habilitado `UseAuthentication()` e registro do JWT)
- `src/LedgerService.Api/appsettings.json` (seção `Jwt`)
- `src/LedgerService.Api/Options/JwtAuthOptions.cs`
- `src/LedgerService.Api/Extensions/JwtAuthServiceCollectionExtensions.cs`
- `src/LedgerService.Api/Security/ScopePolicies.cs`
- `src/LedgerService.Api/Security/ScopeAuthorizationExtensions.cs`
- `src/LedgerService.Api/Controllers/LancamentosController.cs` (policy `scope:ledger.write`)
- `src/LedgerService.Api/Swagger/AuthorizeOperationFilter.cs`
- `src/LedgerService.Api/Extensions/ServiceCollectionExtensions.cs` (Swagger Bearer + OperationFilter)
- `src/LedgerService.Api/LedgerService.Api.csproj` (pacote JwtBearer)

### BalanceService.Api

- `src/BalanceService.Api/Program.cs` (habilitado `UseAuthentication()` e registro do JWT)
- `src/BalanceService.Api/appsettings.json` (seção `Jwt`)
- `src/BalanceService.Api/Options/JwtAuthOptions.cs`
- `src/BalanceService.Api/Extensions/JwtAuthServiceCollectionExtensions.cs`
- `src/BalanceService.Api/Security/ScopePolicies.cs`
- `src/BalanceService.Api/Security/ScopeAuthorizationExtensions.cs`
- `src/BalanceService.Api/Controllers/ConsolidadosController.cs` (policy `scope:balance.read`)
- `src/BalanceService.Api/Swagger/AuthorizeOperationFilter.cs`
- `src/BalanceService.Api/Extensions/ServiceCollectionExtensions.cs` (Swagger Bearer + OperationFilter)
- `src/BalanceService.Api/BalanceService.Api.csproj` (pacote JwtBearer)

### Docs

- `README.md` (seção 4.6 com autenticação e scopes)

## Evidências de validação

- `dotnet build src\\LedgerService.Api\\LedgerService.Api.csproj -c Release` ✅
- `dotnet build src\\BalanceService.Api\\BalanceService.Api.csproj -c Release` ✅
- `dotnet test -c Release` ✅ (testes existentes continuam passando)

## Observações / TODOs explícitos

1) **Scopes suportados pelo Auth.Api**: atualmente o `Auth.Api` valida apenas `ledger.write` e `balance.read` (ver `ScopeCatalog`).
   - TODO: alinhar catálogo com os novos scopes documentados (`ledger.read`, `balance.write`) caso/when existirem endpoints que os exijam.
