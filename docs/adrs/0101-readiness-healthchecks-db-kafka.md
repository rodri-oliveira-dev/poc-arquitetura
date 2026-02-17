# ADR-0101: Implementar readiness e health checks de dependências (DB e Kafka)

## Status
Proposto

## Data
2026-02-16

## Contexto
Hoje existe apenas liveness (`/health`). O README já aponta TODO de readiness. Em cenários reais, é importante distinguir:
- processo vivo
- dependências prontas (DB, Kafka)
- capacidade de consumir/publicar

## Decisão
Adicionar:
- `/health/live`: liveness (igual ao atual)
- `/health/ready`: readiness que valida conectividade com DB e Kafka
- Timeouts e thresholds para evitar flapping
- Métricas associadas (sucesso/falha) para observabilidade

## Consequências
- Melhora estabilidade operacional (especialmente em orquestradores).
- Facilita automação de deploy/rollback.
- Requer cuidado para não causar indisponibilidade por verificações agressivas.

## Alternativas consideradas
- Manter apenas liveness: simples, porém insuficiente para operação robusta.
- Readiness parcial (só DB): melhora um pouco, mas Kafka impacta diretamente no Balance e no outbox.
