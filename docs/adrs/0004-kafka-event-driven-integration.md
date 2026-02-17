# ADR-0004: Integração assíncrona via Kafka (event-driven)

## Status
Aceito

## Data
2026-02-16

## Contexto
Balance precisa ser alimentado por eventos do Ledger sem acoplar disponibilidade. O README define Kafka e tópico `ledger.ledgerentry.created`.

## Decisão
Usar Kafka como backbone de eventos:
- Ledger publica `LedgerEntryCreated`
- Balance consome e atualiza a projeção `daily_balances`
- `AllowAutoCreateTopics=false` no consumer e criação explícita do tópico no compose

## Consequências
- Desacopla write (Ledger) de read/projeção (Balance).
- Suporta reprocessamento (com cuidado) e escalabilidade do consumer group.
- Introduz complexidade operacional (broker, tópicos, particionamento, retenção).
- Exige idempotência e estratégia de falhas no consumidor (ver ADR-0102).

## Alternativas consideradas
- HTTP síncrono Ledger -> Balance: acopla disponibilidade e adiciona latência.
- Fila simples (ex.: RabbitMQ): viável, mas o stack já assume Kafka e tópicos.
