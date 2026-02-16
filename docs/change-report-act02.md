# Change Report - ACT 02 (BalanceService.Api)

Este documento registra as mudanças realizadas na tarefa **ACT 02 - BalanceService.Api: 2 rotas (consulta por dia e por período) + Swagger**.

## Objetivo atendido

Foram criadas **somente** as duas rotas de consulta no `BalanceService.Api`, consultando o banco consolidado (tabela `daily_balances`) via `ConnectionStrings:DefaultConnection`.

Rotas implementadas:

1. `GET /v1/consolidados/diario/{date}?merchantId={merchantId}`
2. `GET /v1/consolidados/periodo?from=YYYY-MM-DD&to=YYYY-MM-DD&merchantId={merchantId}`

> Não foi criado nenhum terceiro endpoint.

## Decisão de contrato para ausência de dados

**Padrão adotado:** quando não houver dados para o dia/período, a API retorna **HTTP 200** com totais zerados.

- Diário: totais `0.00` e `asOf = null`.
- Período: totais `0.00`, `items = []` e `asOf` ausente nos itens (não há itens).

Esse comportamento está documentado no Swagger (XML comments do controller).

## Observabilidade

- Já existia `CorrelationIdMiddleware` no `BalanceService.Api` garantindo:
  - Leitura do header `X-Correlation-Id` (GUID) ou geração automática.
  - Propagação no response.
  - Logging scope com `CorrelationId` e, quando houver `Activity`, `TraceId`/`SpanId`.
- Foram adicionados spans (Activities) por endpoint:
  - `balance.api.daily`
  - `balance.api.period`

## Swagger

- O projeto já estava com `GenerateDocumentationFile=true`.
- As rotas foram documentadas com:
  - `summary` / `remarks`
  - parâmetros (`date`, `from`, `to`, `merchantId`, `X-Correlation-Id`)
  - exemplos de payload
  - response codes: `200`, `400`, `401`, `403`, `500`
- Contratos HTTP (DTOs) foram adicionados em `BalanceService.Api/Contracts` com comentários XML de semântica de negócio.

## Arquivos adicionados / alterados

### Api

- `src/BalanceService.Api/Controllers/ConsolidadosController.cs`
- `src/BalanceService.Api/Contracts/DailyBalanceResponse.cs`
- `src/BalanceService.Api/Contracts/PeriodBalanceResponse.cs`
- `src/BalanceService.Api/Contracts/PeriodBalanceItemResponse.cs`

### Application

- `src/BalanceService.Application/Abstractions/Persistence/IDailyBalanceReadRepository.cs`
- `src/BalanceService.Application/Balances/Queries/GetDailyBalanceQuery.cs`
- `src/BalanceService.Application/Balances/Services/IDailyBalanceService.cs`
- `src/BalanceService.Application/Balances/Services/DailyBalanceService.cs` (substitui o antigo handler)
- `src/BalanceService.Application/Balances/Queries/GetPeriodBalanceQuery.cs`
- `src/BalanceService.Application/Balances/Services/IPeriodBalanceService.cs`
- `src/BalanceService.Application/Balances/Services/PeriodBalanceService.cs` (substitui o antigo handler)
- `src/BalanceService.Application/Balances/Queries/GetDailyBalanceQueryValidator.cs`
- `src/BalanceService.Application/Balances/Queries/GetPeriodBalanceQueryValidator.cs`
- `src/BalanceService.Application/Balances/Queries/Models/DailyBalanceReadModel.cs`
- `src/BalanceService.Application/Balances/Queries/Models/PeriodBalanceReadModel.cs`
- `src/BalanceService.Application/DependencyInjection.cs` (registrar handlers)

### Infrastructure

- `src/BalanceService.Infrastructure/Persistence/Repositories/DailyBalanceReadRepository.cs`
- `src/BalanceService.Infrastructure/DependencyInjection.cs` (registrar repositório de leitura)

## Evidências de validação

- Build:
  - `dotnet build src\\BalanceService.Api\\BalanceService.Api.csproj` ✅
- Smoke test (curl):
  - `GET /v1/consolidados/diario/2026-02-14?merchantId=tese` ✅ (retornou consolidado)
  - `GET /v1/consolidados/periodo?from=2026-02-10&to=2026-02-14&merchantId=tese` ✅ (retornou itens e totais)

### Casos adicionais validados

- Sem dados no dia: `GET /v1/consolidados/diario/2026-02-13?merchantId=tese` ✅ (200 com zeros)
- Sem dados no período: `GET /v1/consolidados/periodo?from=2026-02-01&to=2026-02-02&merchantId=tese` ✅ (200 com zeros e items=[])
- Validação from>to: `GET /v1/consolidados/periodo?from=2026-02-03&to=2026-02-01&merchantId=tese` ✅ (400)

## TODOs explícitos (falta evidência no código)

1. **Moeda em consultas**: a API não recebe `currency`, porém `daily_balances` é indexada por `(merchant_id, date, currency)`.
   - Hoje a consolidação escreve `BRL` como default.
   - TODO: definir política quando houver múltiplas moedas (filtrar por currency, agrupar por moeda, etc.).
