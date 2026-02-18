# ADR-0003: Integração assíncrona via Kafka com Outbox (at-least-once)

## Status
Aceito

## Data
2026-02-17

## Contexto
O `LedgerService` precisa publicar eventos (ex.: `LedgerEntryCreated`) para alimentar o `BalanceService`.

Publicar diretamente no Kafka dentro do mesmo fluxo da transação do banco traz risco de inconsistência:

- se o DB commit ocorre e o publish falha, o evento se perde;
- se o publish ocorre e o DB commit falha, consumidores podem ver um evento “fantasma”.

Precisamos de um mecanismo que preserve consistência entre escrita no DB e publicação do evento, com boa resiliência a falhas temporárias do Kafka.

## Decisão
Adotar o padrão **Outbox** no `LedgerService`:

- O caso de uso grava o lançamento e também grava uma linha em `outbox_messages` na **mesma transação**.
- Um `BackgroundService` (`OutboxKafkaPublisherService`) faz polling, **claim** de mensagens pendentes e publica no Kafka.
- A entrega é **at-least-once** (podem existir duplicatas), e o pipeline trata falhas com retentativas/backoff.

Detalhes relevantes (estado atual):

- Tópico dedicado: `ledger.ledgerentry.created` (criado no compose por um init job).
- Headers incluem `event_id`, `event_type`, `correlation_id` e (quando houver `Activity`) `traceparent`/`baggage`.

## Justificativa: escolha de mensageria (Kafka) vs alternativas
Nesta PoC, a escolha por **Kafka** (via `Confluent.Kafka`) é adequada porque:

- **Delivery at-least-once** combina com Outbox + consumidor idempotente, modelo comum em arquiteturas orientadas a eventos.
- **Retenção e reprocessamento**: Kafka facilita reprocessar eventos por offset (útil para “rebuild” de projeções como `daily_balances`).
- **Ordenação por chave**: usamos `AggregateId` como key (particionamento), o que tende a preservar ordem por entidade/agregado.
- **Ecossistema e didática**: é um padrão bem conhecido para PoCs e demonstração de integrações assíncronas.

Alternativas típicas e por que não foram escolhidas aqui:

1) **RabbitMQ/AMQP**
   - Prós: excelente para filas e roteamento; operação simples em alguns cenários.
   - Contras: retenção/replay não é nativo como Kafka (depende de desenho); para projeções/replay, Kafka costuma ser mais direto.

2) **Service Bus gerenciado (cloud)**
   - Prós: reduz esforço operacional.
   - Contras: adiciona acoplamento com cloud e não é objetivo deste repo “rodar local via compose”.

3) **CDC (Debezium/Postgres logical replication)**
   - Prós: remove polling do app.
   - Contras: infra extra e complexidade; a PoC prioriza mostrar o padrão Outbox no código.

> TODO: se o objetivo evoluir de PoC para baseline de produção, reavaliar: particionamento/throughput, DLQ, schema registry/compatibilidade, e estratégia de reprocessamento controlado.

## Consequências

### Benefícios
- Consistência entre DB e evento (eventual): o evento é “derivado” de um registro durável.
- Resiliência: falhas no Kafka não derrubam a API; mensagens ficam pendentes e são reenviadas.
- Observabilidade melhor: cada evento tem `event_id` e pode ser rastreado.

### Trade-offs / custos
- **Duplicatas possíveis**: consumidores devem ser idempotentes.
- **Atraso** entre o commit e a publicação (polling interval).
- Complexidade extra: tabela outbox, job/background, locks e retentativas.

## Alternativas consideradas

1) **Publicar direto no Kafka no request**
   - Prós: menor latência e menos componentes.
   - Contras: inconsistência em falhas; necessidade de transação distribuída (não desejável).

2) **Two-phase commit / transação distribuída**
   - Prós: consistência forte.
   - Contras: complexidade operacional e acoplamento; não é objetivo da PoC.

3) **Change Data Capture (CDC)** (Debezium/Postgres logical replication)
   - Prós: remove polling do app.
   - Contras: infra adicional significativa; menor “didática” para a PoC.
