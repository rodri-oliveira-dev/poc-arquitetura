# ReprocessamentoLancamentosSolicitado.v1

## Identificacao

| Item | Valor |
| --- | --- |
| Nome do evento | `ReprocessamentoLancamentosSolicitado` |
| Versao | `v1` |
| `event_type` | `ReprocessamentoLancamentosSolicitado.v1` |
| Produtor | `LedgerService` |
| Consumidores | `LedgerService.Worker` no modo Kafka |
| Natureza | Operacional do Ledger |
| JSON Schema versionado | [`../../contracts/events/reprocessamento-lancamentos-solicitado.v1.schema.json`](../../contracts/events/reprocessamento-lancamentos-solicitado.v1.schema.json) |
| Exemplos versionados | [`valido`](../../contracts/events/examples/reprocessamento-lancamentos-solicitado.v1.valid.json), [`invalido`](../../contracts/events/examples/reprocessamento-lancamentos-solicitado.v1.invalid.json) |

Este documento descreve o contrato logico atual. O payload logico deve ser o mesmo caso o evento seja publicado por Pub/Sub ou Kafka. As diferencas de transporte pertencem aos adapters.

O JSON Schema versionado valida somente o payload logico do evento. Metadados tecnicos como `event_id`, `event_type`, headers, attributes, key, offset e DLQ ficam fora do schema.

## Proposito

Registrar a intencao operacional de reprocessar lancamentos de um merchant em um periodo controlado. Este evento nao representa conclusao de reprocessamento nem alteracao direta de saldo.

O Balance continua consumindo apenas `LedgerEntryCreated.v1`. Durante o reprocessamento, o Ledger republica `LedgerEntryCreated.v1` para os lancamentos elegiveis.

## Quando e emitido

E gravado no Outbox quando `LedgerService.Api` recebe uma solicitacao valida de reprocessamento e persiste a solicitacao com status inicial.

No modo Kafka, `LedgerService.Worker` consome este evento e delega o processamento ao caso de uso de reprocessamento.

## Garantias esperadas

| Garantia | Estado atual |
| --- | --- |
| Entrega | At-least-once quando publicado pelo Outbox. |
| Persistencia antes da publicacao | Sim, via Outbox. |
| Idempotencia no consumidor | Baseada no estado persistido da solicitacao no Ledger. |
| Ordenacao global | Nao garantida. |
| Efeito financeiro direto | Nenhum. |

## Payload logico

Serializado com `JsonSerializerDefaults.Web`, portanto em camelCase.

| Campo | Obrigatorio | Tipo de dado | Semantica |
| --- | --- | --- | --- |
| `reprocessamentoId` | Sim | string UUID | Identificador da solicitacao de reprocessamento. |
| `merchantId` | Sim | string | Merchant a reprocessar. |
| `dataInicial` | Sim | string date | Inicio do periodo. |
| `dataFinal` | Sim | string date | Fim do periodo. |
| `motivo` | Sim | string | Motivo informado na solicitacao. |
| `status` | Sim | string | Status inicial da solicitacao. |
| `requestedAt` | Sim | string date-time | Instante de criacao da solicitacao. |
| `correlationId` | Sim | string UUID | Correlacao logica da requisicao. |

## Campos obrigatorios

- `reprocessamentoId`
- `merchantId`
- `dataInicial`
- `dataFinal`
- `motivo`
- `status`
- `requestedAt`
- `correlationId`

## Campos opcionais

Nenhum campo opcional foi identificado no payload atual.

## Exemplo de payload valido

```json
{
  "reprocessamentoId": "8cbf5b69-8d08-4a4b-bf1a-c0612b6a0a41",
  "merchantId": "merchant-001",
  "dataInicial": "2026-06-01",
  "dataFinal": "2026-06-06",
  "motivo": "Reconciliacao operacional",
  "status": "Pending",
  "requestedAt": "2026-06-06T12:45:00.0000000Z",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
}
```

## Exemplo de payload invalido

```json
{
  "reprocessamentoId": "8cbf5b69-8d08-4a4b-bf1a-c0612b6a0a41",
  "merchantId": "merchant-001",
  "dataInicial": "2026-06-06",
  "dataFinal": "2026-06-01",
  "motivo": "Reconciliacao operacional",
  "status": "Pending",
  "requestedAt": "2026-06-06T12:45:00.0000000Z",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
}
```

Motivo: `dataInicial` posterior a `dataFinal` invalida o intervalo logico de reprocessamento.

## Idempotencia

- A idempotencia da solicitacao ocorre no fluxo HTTP e no estado persistido do Ledger.
- O consumer Kafka do Ledger valida a fonte logica e processa a solicitacao persistida.
- Os eventos financeiros republicados usam `LedgerEntryCreated.v1` com o mesmo `payload.id` logico do lancamento.
- A deduplicacao no Balance ocorre somente nos `LedgerEntryCreated.v1` republicados.

## Ordenacao

- O contrato nao depende de ordenacao global.
- A ordem relevante e controlada pelo estado da solicitacao e pelos lancamentos elegiveis persistidos no Ledger.
- Se publicado por Kafka, a message key deriva do `AggregateId` sem hifens.
- Se publicado por Pub/Sub com ordering habilitado, a ordering key deriva do `AggregateId` sem hifens.

## Compatibilidade

Mudancas compativeis em `v1`:

- manter nome, tipos e semantica dos campos existentes;
- adicionar metadados opcionais de transporte.

Mudancas que exigem nova versao ou rollout coordenado:

- remover ou renomear campo;
- mudar tipo ou semantica de campo;
- alterar a interpretacao do periodo;
- fazer o Balance consumir este evento como fonte de saldo;
- trocar a semantica de intencao operacional por fato financeiro final.

## Transporte Pub/Sub

| Item | Valor atual |
| --- | --- |
| Topic | Sem mapeamento versionado atual no `TopicMap` Pub/Sub do Ledger. |
| Subscription | Nenhuma subscription especifica identificada. |
| Payload | `PubsubMessage.Data` com JSON do payload logico, se publicado. |
| DLQ | Sem DLQ especifica para este evento no provider principal atual. |

Attributes esperados se publicado:

| Attribute | Obrigatorio | Origem |
| --- | --- | --- |
| `event_id` | Sim | `OutboxMessage.Id`. |
| `event_type` | Sim | `ReprocessamentoLancamentosSolicitado.v1`. |
| `correlation_id` | Nao | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Nao | Outbox ou Activity atual. |
| `tracestate` | Nao | Outbox ou Activity atual. |
| `baggage` | Nao | Outbox ou baggage atual. |

Ordering key:

- Vazia por default no provider Pub/Sub atual.
- Se habilitada, usa `AggregateId` sem hifens.

Ack e nack:

- Nao ha consumer Pub/Sub atual para este evento.

DLQ:

- Nao ha DLQ de aplicacao especifica para este evento no Pub/Sub atual.
- No modo Pub/Sub principal, nao ha adapter de consumer equivalente ao consumer Kafka atual.

## Transporte Kafka

| Item | Valor atual |
| --- | --- |
| Topic | `ledger.lancamentos.reprocessamento.solicitado` |
| Consumer | `LedgerService.Worker` |
| Consumer group | `ledger-reprocessamento-consumer` |
| Payload | `Message.Value` com JSON do payload logico. |

Headers esperados:

| Header | Obrigatorio | Origem |
| --- | --- | --- |
| `event_id` | Sim | `OutboxMessage.Id`. |
| `event_type` | Sim | `ReprocessamentoLancamentosSolicitado.v1`. |
| `correlation_id` | Nao | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Nao | Outbox ou Activity atual. |
| `tracestate` | Nao | Outbox ou Activity atual. |
| `baggage` | Nao | Outbox ou baggage atual. |

Message key:

- Usa `AggregateId` sem hifens.

Commit:

- Manual, com `EnableAutoCommit=false`.
- O offset e commitado apos processamento com sucesso.
- Falhas recuperaveis de processamento aguardam `ProcessingErrorRetryDelay` e nao commitam.

DLQ:

- Nao foi identificada DLQ de aplicacao dedicada para o consumer Kafka de reprocessamento do Ledger.
- Falhas recuperaveis tendem a reter o offset sem commit para nova tentativa.

## Riscos conhecidos

1. O consumer existe apenas no adapter Kafka do `LedgerService.Worker`.
2. No modo Pub/Sub principal, nao ha adapter equivalente para este fluxo.
3. O evento nao e um fato financeiro final e nao deve ser consumido pelo Balance.
4. No Pub/Sub principal, o evento nao tem mapeamento versionado no `TopicMap`.

## Dividas tecnicas

- Decidir se reprocessamento deve ser suportado no provider principal Pub/Sub ou permanecer Kafka only.
- Documentar explicitamente o modo operacional quando `Messaging:Provider=PubSub`.
- Manter a deduplicacao financeira no `LedgerEntryCreated.v1` republicado.
- Avaliar DLQ de aplicacao especifica para o consumer de reprocessamento se o fluxo continuar por mensageria.
