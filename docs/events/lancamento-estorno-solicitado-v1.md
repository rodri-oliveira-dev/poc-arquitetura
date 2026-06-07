# LancamentoEstornoSolicitado.v1

## Identificacao

| Item | Valor |
| --- | --- |
| Nome do evento | `LancamentoEstornoSolicitado` |
| Versao | `v1` |
| `event_type` | `LancamentoEstornoSolicitado.v1` |
| Produtor | `LedgerService` |
| Consumidores | Nenhum consumer de mensageria encontrado |
| Natureza | Operacional do Ledger |
| JSON Schema versionado | [`../../contracts/events/lancamento-estorno-solicitado.v1.schema.json`](../../contracts/events/lancamento-estorno-solicitado.v1.schema.json) |
| Exemplos versionados | [`valido`](../../contracts/events/examples/lancamento-estorno-solicitado.v1.valid.json), [`invalido`](../../contracts/events/examples/lancamento-estorno-solicitado.v1.invalid.json) |

Este documento descreve o contrato logico atual. O payload logico deve ser o mesmo caso o evento seja publicado por Pub/Sub ou Kafka. As diferencas de transporte pertencem aos adapters.

O JSON Schema versionado valida somente o payload logico do evento. Metadados tecnicos como `event_id`, `event_type`, headers, attributes, key, offset e DLQ ficam fora do schema.

## Proposito

Registrar a intencao operacional de estornar um lancamento. Este evento nao representa um fato financeiro final e nao deve atualizar saldos no Balance.

O saldo so muda quando o processamento do estorno cria um lancamento compensatorio e publica `LedgerEntryCreated.v2`.

## Quando e emitido

E gravado no Outbox quando `LedgerService.Api` recebe uma solicitacao de estorno valida e persiste a solicitacao com status inicial.

O processamento financeiro efetivo ocorre por polling no banco em `EstornoLancamentoProcessorService`, nao por consumer de mensageria deste evento.

## Garantias esperadas

| Garantia | Estado atual |
| --- | --- |
| Entrega | At-least-once quando publicado pelo Outbox. |
| Persistencia antes da publicacao | Sim, via Outbox. |
| Idempotencia no consumidor | Nao ha consumer de mensageria atual. |
| Ordenacao global | Nao garantida. |
| Efeito financeiro direto | Nenhum. |

## Payload logico

Serializado com `JsonSerializerDefaults.Web`, portanto em camelCase.

| Campo | Obrigatorio | Tipo de dado | Semantica |
| --- | --- | --- | --- |
| `estornoId` | Sim | string UUID | Identificador da solicitacao de estorno. |
| `lancamentoOriginalId` | Sim | string UUID | Identificador do lancamento original. |
| `merchantId` | Sim | string | Merchant do lancamento original. |
| `motivo` | Sim | string | Motivo informado na solicitacao. |
| `status` | Sim | string | Status inicial da solicitacao. |
| `requestedAt` | Sim | string date-time | Instante de criacao da solicitacao. |
| `correlationId` | Sim | string UUID | Correlacao logica da requisicao. |

## Campos obrigatorios

- `estornoId`
- `lancamentoOriginalId`
- `merchantId`
- `motivo`
- `status`
- `requestedAt`
- `correlationId`

## Campos opcionais

Nenhum campo opcional foi identificado no payload atual.

## Exemplo de payload valido

```json
{
  "estornoId": "6f1c08d2-0616-4b35-b0f2-2d32f11fbc11",
  "lancamentoOriginalId": "f6ab2e42-1b91-4af1-b232-7c7fdc43b17a",
  "merchantId": "merchant-001",
  "motivo": "Solicitacao do cliente",
  "status": "Pending",
  "requestedAt": "2026-06-06T12:40:00.0000000Z",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
}
```

## Exemplo de payload invalido

```json
{
  "estornoId": "6f1c08d2-0616-4b35-b0f2-2d32f11fbc11",
  "merchantId": "merchant-001",
  "motivo": "Solicitacao do cliente",
  "status": "Pending",
  "requestedAt": "2026-06-06T12:40:00.0000000Z",
  "correlationId": "2cbdd495-586f-4565-a807-c5dc6710d237"
}
```

Motivo: `lancamentoOriginalId` ausente, impossibilitando identificar o lancamento a estornar.

## Idempotencia

- A idempotencia da solicitacao ocorre no fluxo HTTP e na persistencia do Ledger, nao por consumer de mensageria.
- Como nao ha consumer de mensageria atual, nao existe chave de deduplicacao downstream para este evento.
- O eventual lancamento compensatorio usa `LedgerEntryCreated.v2` e segue a idempotencia propria desse contrato no Balance.

## Ordenacao

- O contrato nao depende de ordenacao global.
- A ordem relevante para processamento de estorno e controlada pelo estado persistido no Ledger.
- Se publicado por Kafka, a message key deriva do `AggregateId` sem hifens.
- Se publicado por Pub/Sub com ordering habilitado, a ordering key deriva do `AggregateId` sem hifens.

## Compatibilidade

Mudancas compativeis em `v1`:

- manter nome, tipos e semantica dos campos existentes;
- adicionar metadados opcionais de transporte.

Mudancas que exigem nova versao ou rollout coordenado:

- remover ou renomear campo;
- mudar tipo ou semantica de campo;
- transformar a solicitacao operacional em fato financeiro;
- fazer o Balance consumir este evento como fonte de saldo.

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
| `event_type` | Sim | `LancamentoEstornoSolicitado.v1`. |
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
- Se publicado pelo `DefaultTopicId`, o comportamento operacional dependera de configuracao futura.

## Transporte Kafka

| Item | Valor atual |
| --- | --- |
| Topic | `ledger.lancamento.estorno.solicitado` |
| Consumer | Nenhum consumer de mensageria encontrado |
| Payload | `Message.Value` com JSON do payload logico. |

Headers esperados:

| Header | Obrigatorio | Origem |
| --- | --- | --- |
| `event_id` | Sim | `OutboxMessage.Id`. |
| `event_type` | Sim | `LancamentoEstornoSolicitado.v1`. |
| `correlation_id` | Nao | `OutboxMessage.CorrelationId`, quando presente. |
| `traceparent` | Nao | Outbox ou Activity atual. |
| `tracestate` | Nao | Outbox ou Activity atual. |
| `baggage` | Nao | Outbox ou baggage atual. |

Message key:

- Usa `AggregateId` sem hifens.

Commit:

- Nao ha consumer Kafka atual para este evento.

DLQ:

- Nao ha DLQ de aplicacao especifica identificada para consumo deste evento.

## Riscos conhecidos

1. O evento e publicado no Kafka, mas nao ha consumer de mensageria encontrado.
2. O processamento real do estorno usa polling no banco, o que pode confundir a leitura do fluxo.
3. No Pub/Sub principal, o evento nao tem mapeamento versionado no `TopicMap`.
4. Consumir este evento como fato financeiro causaria semantica incorreta no Balance.

## Dividas tecnicas

- Decidir se este evento deve continuar sendo publicado externamente ou permanecer apenas como registro operacional persistido.
- Documentar ou implementar um consumer se a intencao futura for processar estorno por mensageria.
- Manter explicito que o fato financeiro final do estorno e `LedgerEntryCreated.v2`.
