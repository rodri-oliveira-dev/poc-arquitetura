# Change Report - ACT04 (Health endpoints + README + Mermaid C4)

Este documento registra as mudanças realizadas na tarefa de **criar endpoints de health** para `BalanceService.Api` e `LedgerService.Api`, além de **revisar o README** e **incorporar um diagrama Mermaid C4**.

## Resumo do que foi alterado

### Endpoints de health

- Adicionado `GET /health` (público) em:
  - `LedgerService.Api`
  - `BalanceService.Api`
- Contrato:
  - **200 OK**
  - `Content-Type: text/plain`
  - body: `ok`
- O endpoint é:
  - **`[AllowAnonymous]`** (necessário porque as APIs possuem `FallbackPolicy` exigindo usuário autenticado)
  - **sem rate limiting** (`.DisableRateLimiting()`), para não impactar checks automatizados
  - documentado via `WithSummary/WithDescription` e incluído no Swagger `v1` (`WithGroupName("v1")`)

> Observação: o endpoint não valida dependências (DB/Kafka). É um **liveness** simples. Se for necessário readiness real, avaliar uma evolução futura.

### README

- README revisado para refletir o estado atual do repositório:
  - visão geral agora menciona Ledger + Balance + Kafka + projeção `daily_balances`
  - corrigido path de execução do `Auth.Api` (era `src\\auth-api...`, mas o projeto está em `src\\Auth.Api...`)
  - corrigidas URLs do `BalanceService.Api` (Swagger/OpenAPI em `5228`)
  - adicionada referência a `GET /health` nos microserviços

### Diagrama Mermaid C4

- Adicionado diagrama `C4Context` em Mermaid no README (seção "1.1 Diagrama").
- Não contém segredos.

## Arquivos alterados

### Alterados

- `src/LedgerService.Api/Program.cs`
- `src/BalanceService.Api/Program.cs`
- `README.md`

### Adicionados

- `docs/change-report-act04-health-readme-c4.md`

## Evidências de validação

### Build

- `dotnet build .` ✅

### Testes

- `dotnet test .` ✅

### Health endpoints

- `curl -i http://localhost:5226/health` ✅ (LedgerService.Api)
- `curl -i http://localhost:5228/health` ✅ (BalanceService.Api)

### Swagger/OpenAPI

- `GET /swagger/v1/swagger.json` contém `"/health"` ✅ em ambos os serviços.

## Pendências / TODOs

1. **Readiness vs liveness**:
   - TODO: se necessário, criar endpoint de readiness que valide DB/Kafka (com timeouts e degradação) e manter `/health` como liveness.
2. **Warning de HTTPS redirection em execução via `--urls http://...`**:
   - Observado log: `Failed to determine the https port for redirect.`
   - TODO (opcional): ajustar configuração/README para execução puramente HTTP (ex.: `ASPNETCORE_HTTPS_PORT`) ou orientar uso do profile https.
