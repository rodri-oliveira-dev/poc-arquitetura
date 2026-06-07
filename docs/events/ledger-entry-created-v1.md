# LedgerEntryCreated.v1

## Identificacao

| Item | Valor |
| --- | --- |
| Nome do evento | `LedgerEntryCreated` |
| Versao | `v1` |
| `event_type` | `LedgerEntryCreated.v1` |
| Produtor | `LedgerService` |
| Consumidores | `BalanceService.Worker` por Pub/Sub ou Kafka |
| Natureza | Integracao entre servicos |
| Documento de schema existente | [`../contracts/events/LedgerEntryCreated.v1.md`](../contracts/events/LedgerEntryCreated.v1.md) |
| JSON Schema versionado | [`../../contracts/events/ledger-entry-created.v1.schema.json`](../../contracts/events/ledger-entry-created.v1.schema.json) |
| Exemplos versionados | [`valido`](../../contracts/events/examples/ledger-entry-created.v1.valid.json), [`invalido`](../../contracts/events/examples/ledger-entry-created.v1.invalid.json) |

Este documento descreve o contrato logico atual. O payload logico deve ser o mesmo em Pub/Sub e Kafka. As diferencas de topic, subscription, attributes, headers, key, ack, nack, commit e DLQ pertencem aos adapters de transporte.

O JSON Schema versionado valida somente o payload logico do evento. Metadados tecnicos como `event_id`, `event_type`, headers, attributes, key, offset e DLQ ficam fora do schema.

## Status de compatibilidade

`LedgerEntryCreated.v1` e contrato legado. Ele permanece aceito pelo `BalanceService.Worker` para mensagens antigas e para o Kafka legado, mas nao e mais o contrato produzido pelos fluxos novos do Ledger.

Nao foi adicionado `currency` em v1 porque isso mudaria a semantica do contrato e quebraria consumidores que rejeitam propriedades desconhecidas. O contrato atual produzido e [`LedgerEntryCreated.v2`](ledger-entry-created-v2.md), com `currency` explicito e obrigatorio.

## Proposito

Representar um fato financeiro final persistido pelo Ledger para que o Balance atualize a projecao de saldos diarios por merchant no contrato legado.

O mesmo contrato e usado para:

- lancamentos normais criados pelo endpoint de escrita;
- lancamentos compensatorios gerados por estorno concluido;
- republicacao de lancamentos elegiveis em reprocessamento.

## Quando e emitido

- Apos criacao transacional de um lancamento no `LedgerService`.
- Apos processamento de um estorno, quando o lancamento compensatorio e criado.
- Durante reprocessamento, para republicar o fato financeiro persistido com o mesmo identificador logico.

O evento e gravado no Outbox do Ledger na mesma transacao da mudanca de dominio e depois publicado pelo `LedgerService.Worker`.

## Garantias esperadas

| Garantia | Estado atual |
| --- | --- |
| Entrega | At-least-once pelo Outbox e pelo transporte selecionado. |
| Persistencia antes da publicacao | Sim, via Outbox. |
| Idempotencia no consumidor | Sim, por `payload.id` em `processed_events`. |
| Ordenacao global | Nao garantida. |
| Ordenacao por agregado | Possivel somente quando o provider e configurado para isso. |
| Consistencia | Eventual entre Ledger e Balance. |

## Payload logico

| Campo | Obrigatorio | Tipo de dado | Semantica |
| --- | --- | --- | --- |
| `id` | Sim | string | Identificador logico estavel do lancamento, no formato `lan_` mais 8 caracteres hexadecimais. |
| `type` | Sim | string | Tipo financeiro. Valores atuais: `CREDIT` ou `DEBIT`. |
| `amount` | Sim | string decimal | Valor com duas casas decimais. Positivo para `CREDIT` e negativo para `DEBIT`. |
| `createdAt` | Sim | string date-time ISO 8601 | Instante de criacao no Ledger. |
| `merchantId` | Sim | string | Merchant dono do lancamento e da projecao. |
| `occurredAt` | Sim | string date-time ISO 8601 | Instante do fato financeiro. O offset define o dia consolidado. |
| `description` | Nao | string ou null | Descricao opcional. |
| `correlationId` | Sim | string UUID | Correlacao logica do fluxo de origem. |
| `externalReference` | Nao | string ou null | Referencia externa opcional. |

## Campos obrigatorios

- `id`
- `type`
- `amount`
- `createdAt`
- `merchantId`
- `occurredAt`
- `correlationId`

## Campos opcionais

- `description`
- `externalReference`

## Exemplo de payload valido

```json
{
  "id": "lan_1a2b3c4d",
  "type": "CREDIT",
  "amount": "150.00",
  "createdAt": "2026-06-06T12:34:56.0000000Z",
  "merchantId": "merchant-001",
  "occurredAt": "2026-06-06T12:34:56.0000000Z",
  "description": "Venda aprovada",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
  "externalReference": "order-123"
}
```

## Exemplo de payload invalido

```json
{
  "id": "lan_1a2b3c4d",
  "type": "CREDIT",
  "amount": "150.00",
  "createdAt": "2026-06-06T12:34:56.0000000Z",
  "merchantId": "merchant-001",
  "occurredAt": "2026-06-06T12:34:56.0000000Z",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237",
  "currency": "BRL"
}
```

Motivo: `currency` nao faz parte de `LedgerEntryCreated.v1`. O consumer rejeita `currency` em v1 e aceita esse campo somente em `LedgerEntryCreated.v2`.

## Idempotencia

- O Balance usa `payload.id` como chave unica em `processed_events`.
- O `event_id` de transporte representa atualmente o `OutboxMessage.Id` e serve para rastreabilidade tecnica.
- Reprocessamento deve republicar o mesmo `payload.id` do lancamento para evitar duplicidade.
- O `Idempotency-Key` HTTP do Ledger nao e propagado no payload atual.

## Ordenacao

- O contrato nao depende de ordenacao global.
- O Balance deve aceitar reentregas e processamentos fora de ordem entre merchants.
- Quando ordering estiver habilitado no Pub/Sub, a ordering key esperada deriva do `AggregateId` sem hifens.
- No Kafka, a message key esperada deriva do `AggregateId` sem hifens, influenciando a particao e a ordenacao dentro dela.

## Compatibilidade

Mudancas compativeis em `v1`:

- manter nome, tipos e semantica dos campos existentes;
- adicionar metadados opcionais de transporte;
- corrigir documentacao sem mudar payload.

Mudancas que exigem nova versao ou rollout coordenado:

- remover ou renomear campo;
- mudar tipo ou semantica de campo;
- tornar obrigatorio um campo antes opcional ou ausente;
- adicionar propriedades ao payload sem atualizar consumer, schema e testes de contrato;
- adicionar `currency` como campo obrigatorio.

## Transporte Pub/Sub

| Item | Valor atual |
| --- | --- |
| Topic local | `ledger.ledgerentry.created.local` |
| Topic GCP dev documentado | `ledger.ledgerentry.created.dev` |
| Subscription do Balance local | `balance-service-ledger-events-local` |
| DLQ de aplicacao local | `ledger.ledgerentry.created.dlq.local` |
| Payload | `PubsubMessage.Data` com JSON do payload logico. |

Attributes esperados:

| Attribute | Obrigatorio | Origem |
| --- | --- | --- |
| `event_id` | Sim | `OutboxMessage.Id`. |
| `event_type` | Sim | `LedgerEntryCreated.v1`. |
| `correlation_id` | Nao | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Nao | Outbox ou Activity atual. |
| `tracestate` | Nao | Outbox ou Activity atual. |
| `baggage` | Nao | Outbox ou baggage atual. |

Ordering key:

- Vazia por default, pois `EnableMessageOrdering=false`.
- Quando habilitada, usa `AggregateId` sem hifens.

Ack e nack:

- `Ack` quando o processor retorna sucesso, inclusive apos publicar DLQ de aplicacao.
- `Nack` em cancelamento ou falha recuperavel nao tratada pelo processor.

DLQ:

- A DLQ de aplicacao publica `DeadLetterMessage` no topic configurado.
- Attributes incluem `dlq_reason`, `original_source`, `original_provider`, `event_type`, `event_id`, `correlation_id`, tracing W3C e `original_metadata_*`.
- DLQ tecnica nativa do Pub/Sub nao e simulada no emulator local atual.

## Transporte Kafka

| Item | Valor atual |
| --- | --- |
| Topic | `ledger.ledgerentry.created` |
| Consumer group do Balance | `balance-service-consumer` |
| DLQ de aplicacao | `ledger.ledgerentry.created.dlq` |
| Payload | `Message.Value` com JSON do payload logico. |

Headers esperados:

| Header | Obrigatorio | Origem |
| --- | --- | --- |
| `event_id` | Sim | `OutboxMessage.Id`. |
| `event_type` | Sim | `LedgerEntryCreated.v1`. |
| `correlation_id` | Nao | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Nao | Outbox ou Activity atual. |
| `tracestate` | Nao | Outbox ou Activity atual. |
| `baggage` | Nao | Outbox ou baggage atual. |

Message key:

- Usa `AggregateId` sem hifens.

Commit:

- Manual, com `EnableAutoCommit=false`.
- O offset original e commitado apos processamento com sucesso ou apos publicacao bem sucedida na DLQ.

DLQ:

- A DLQ de aplicacao publica `DeadLetterMessage` em `ledger.ledgerentry.created.dlq`.
- A key da DLQ usa `originalTopic:originalPartition:originalOffset`.
- Headers incluem `dlq_reason`, `original_topic`, `original_partition`, `original_offset`, `event_type`, `event_id`, `correlation_id` e tracing W3C.

## Riscos conhecidos

1. `currency` nao existe no payload. O Balance aplica fallback documentado para `BRL` somente ao consumir v1 legado.
2. `event_id` de transporte e diferente da chave real de idempotencia do Balance.
3. `event_type` e obrigatorio no transporte, mas nao existe no payload logico.
4. `correlationId` aparece no payload e tambem pode aparecer no transporte como `correlation_id`.
5. O consumer rejeita propriedades desconhecidas.

## Dividas tecnicas

- Formalizar envelope logico ou convencao comum para nome, versao, id logico, id tecnico e correlacao.
- Decidir se `event_id` deve representar id tecnico, id logico ou ambos com nomes distintos.
- Usar `LedgerEntryCreated.v2` para o caminho novo com `currency` explicito e obrigatorio.
- Criar validacao automatizada que compare schema, producer e consumer antes de mudancas de contrato.
