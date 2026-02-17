# ADR-0009: Propagar X-Correlation-Id para rastreabilidade mínima

## Status
Aceito

## Data
2026-02-16

## Contexto
O README define middleware de correlation id:
- lê `X-Correlation-Id`
- se ausente/inválido, gera UUID
- devolve no response
- adiciona escopo de log

## Decisão
Padronizar `X-Correlation-Id` nos serviços e incluir em logs e headers Kafka (`correlation_id`).

## Consequências
- Ajuda a rastrear requisição -> evento -> projeção com baixa complexidade.
- Não substitui tracing distribuído completo (W3C trace context) sem instrumentação adicional.
- Requer disciplina para propagar em chamadas internas (se existirem no futuro).

## Alternativas consideradas
- Apenas traceId (W3C): melhor no longo prazo, mas exige stack de observabilidade completo.
- Não correlacionar: dificulta troubleshooting e auditoria.
