# ADR-0005: Publicação confiável com Outbox + BackgroundService (at-least-once)

## Status
Aceito

## Data
2026-02-16

## Contexto
Publicar no Kafka direto no request pode falhar e comprometer consistência. O README define outbox com status `Pending` -> `Sent`, polling e backoff.

## Decisão
Implementar Outbox no Ledger:
- Persistir evento em `outbox_messages` na mesma transação do lançamento
- Publicar em background (polling)
- Garantia at-least-once
- Retentativas com backoff e lock por janela (`LockDurationSeconds`)

## Consequências
- Evita perda de eventos quando Kafka está indisponível.
- Mantém Ledger responsivo mesmo com falhas do broker.
- Pode gerar duplicatas no consumidor (at-least-once), exigindo dedup no Balance.
- Aumenta carga no banco por polling (configurável).

## Alternativas consideradas
- Dual write (DB + Kafka) no mesmo request: frágil sem 2PC.
- Publicação transacional no broker: mais complexa e fora do escopo PoC.
