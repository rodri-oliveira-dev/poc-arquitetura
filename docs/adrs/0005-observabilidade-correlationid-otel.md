# ADR-0005: Observabilidade com CorrelationId e base para OpenTelemetry

## Status
Aceito

## Data
2026-02-17

## Contexto
Como PoC com múltiplos serviços, Kafka e jobs em background (Outbox), precisamos de rastreabilidade para:

- correlacionar uma requisição HTTP aos logs e eventos publicados;
- facilitar troubleshooting (ex.: falhas de publish/retry);
- preparar terreno para tracing/métricas sem “amarrar” a PoC em uma stack específica.

No código há evidências de:

- middleware de correlação via `X-Correlation-Id`;
- logging scopes com `CorrelationId`;
- propagação de contexto em headers Kafka (`correlation_id` e W3C quando existir `Activity`).

## Decisão
Padronizar rastreabilidade em dois níveis:

1) **Correlação por `X-Correlation-Id`** (obrigatório / sempre presente)
   - Se o client não enviar, o serviço gera.
   - O valor é devolvido no response e incluído nos logs via scope.
   - O valor é persistido em entidades relevantes (ex.: `ledger_entries`, `outbox_messages`) e propagado ao Kafka.

2) **Base para OpenTelemetry** (opcional, habilitada por configuração)
   - Instrumentação de tracing/métricas pode existir, mas fica **desabilitada por padrão**.
   - Quando habilitada, deve cobrir entrada HTTP e pontos críticos (ex.: outbox publisher).

## Consequências

### Benefícios
- Rastreabilidade mínima garantida mesmo sem tracing distribuído.
- `CorrelationId` viaja junto do evento e ajuda na análise ponta a ponta.
- OpenTelemetry opcional evita obrigar um backend específico na PoC.

### Trade-offs / custos
- `CorrelationId` não substitui trace distribuído completo (não captura árvore de spans automaticamente).
- Sem enrichment de logs com `traceId/spanId` por padrão, pode haver lacunas até configurar provider/enrichers.
- Exige disciplina para propagar `X-Correlation-Id` em chamadas HTTP externas (quando existirem).

## Alternativas consideradas

1) **Somente OpenTelemetry**
   - Prós: padrão de mercado.
   - Contras: exige stack/exporter e configurações; para PoC pode ser “pesado”.

2) **Somente logs** sem correlação padronizada
   - Prós: simples.
   - Contras: troubleshooting difícil em cenários concorrentes e multi-serviço.
