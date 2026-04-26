# ADR-0031: Baseline de observabilidade

## Status
Aceito

## Data
2026-04-26

## Contexto
O repositorio ja possuia correlacao por `X-Correlation-Id`, logging scope e uma base opcional de OpenTelemetry. O README apontava para `docs/observability.md`, mas o documento operacional nao existia.

Tambem havia drift entre os servicos: `LedgerService.Api` e `BalanceService.Api` registravam traces e metricas opcionais, enquanto `Auth.Api` registrava apenas traces. Alem disso, nao havia configuracao padronizada para endpoint OTLP, o que dificultava habilitar coleta minima por ambiente sem alterar codigo.

## Decisao
Padronizar um baseline minimo de observabilidade para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`:

- manter OpenTelemetry desabilitado por padrao;
- manter logs padrao do ASP.NET Core e correlacao via `X-Correlation-Id`;
- registrar `CorrelationId` nos logging scopes dos middlewares existentes;
- habilitar traces OpenTelemetry para ASP.NET Core e `HttpClient` somente quando `Observability:OpenTelemetry:Enabled=true`;
- habilitar metricas OpenTelemetry para ASP.NET Core, `HttpClient` e runtime .NET somente quando `Observability:OpenTelemetry:Enabled=true`;
- manter exporter de console opcional para validacao local com `UseConsoleExporter=true`;
- adicionar exporter OTLP opcional quando `Observability:OpenTelemetry:OtlpEndpoint` estiver configurado;
- documentar operacao minima em `docs/observability.md`;
- atualizar o indice de ADRs e manter o README apontando para o documento operacional.

Arquivos afetados:

- `src/Auth.Api`
- `src/LedgerService.Api`
- `src/BalanceService.Api`
- `docs/observability.md`
- `docs/adrs`
- `README.md`

## Consequencias

### Beneficios
- Reduz drift entre as APIs na instrumentacao minima.
- Permite habilitar traces e metricas por ambiente sem alterar codigo.
- Permite validar localmente via console exporter sem provisionar stack pesada.
- Permite integrar collector OTLP externo quando a plataforma fornecer endpoint.
- Completa a documentacao operacional referenciada pelo README.

### Trade-offs / custos
- Adiciona dependencia do exporter OTLP aos projetos de API.
- OpenTelemetry continua opt-in; ambientes que nao habilitarem a configuracao permanecem apenas com logs e correlation id.
- O repositorio nao passa a provisionar collector, storage ou dashboard de observabilidade.
- Logs estruturados continuam dependentes do provider configurado pela plataforma.

## Alternativas consideradas

1. **Criar apenas a documentacao**
   Pros: menor mudanca possivel.
   Contras: manteria drift no `Auth.Api` e nao resolveria a configuracao de OTLP solicitada.

2. **Adicionar uma stack completa de observabilidade ao compose**
   Pros: experiencia local mais visual.
   Contras: escopo maior, mais custo operacional e contrario ao objetivo de nao adicionar stack pesada.

3. **Habilitar OpenTelemetry por padrao**
   Pros: coleta automatica em todos os ambientes.
   Contras: pode gerar ruido, custo e dependencia de backend externo; a POC decidiu manter opt-in por ambiente.

