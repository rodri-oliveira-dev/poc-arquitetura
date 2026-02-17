# ADR-0006: Projeção de leitura no Balance (daily_balances) e queries específicas

## Status
Aceito

## Data
2026-02-16

## Contexto
Balance recebe consultas por dia e por período, com filtro por merchantId. O README define projeção `daily_balances`.

## Decisão
Modelar o Balance como “read model”:
- Atualizado pelo consumer Kafka
- Endpoints de consulta:
  - diário: `/v1/consolidados/diario/{date}?merchantId=...`
  - período: `/v1/consolidados/periodo?from=...&to=...&merchantId=...`
- Quando não há dados: retornar `200` com zeros (comportamento documentado)

## Consequências
- Leituras rápidas e previsíveis (consulta em tabela pronta).
- Escala leitura separadamente do write.
- Consistência eventual: pode haver atraso entre lançamento e projeção.
- Requer estratégia para reprocessamento, duplicidade e correção de projeção.

## Alternativas consideradas
- Consultar Ledger em tempo real para consolidar: acopla e pode ficar caro em pico.
- Materialized view no mesmo banco: quebra separação e não isola falhas.
