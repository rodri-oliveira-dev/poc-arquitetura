# Contratos logicos de eventos

Esta pasta documenta os contratos logicos atuais dos eventos de integracao e operacionais antes de qualquer mudanca de payload ou criacao de novos JSON Schemas.

O contrato logico do evento deve ser o mesmo quando publicado por Pub/Sub ou Kafka. As diferencas entre providers ficam nos adapters de transporte, incluindo topic fisico, subscription, headers, attributes, ordering key, message key, ack, nack, commit e DLQ.

## Eventos

| Evento | Natureza | Produtor | Consumidores atuais |
| --- | --- | --- | --- |
| [LedgerEntryCreated.v1](ledger-entry-created-v1.md) | Integracao Ledger para Balance | `LedgerService` | `BalanceService.Worker` por Pub/Sub ou Kafka |
| [LancamentoEstornoSolicitado.v1](lancamento-estorno-solicitado-v1.md) | Operacional do Ledger | `LedgerService` | Nenhum consumer de mensageria encontrado |
| [ReprocessamentoLancamentosSolicitado.v1](reprocessamento-lancamentos-solicitado-v1.md) | Operacional do Ledger | `LedgerService` | `LedgerService.Worker` no modo Kafka |

## JSON Schemas

Os JSON Schemas versionados ficam em [`../../contracts/events`](../../contracts/events) e representam somente o payload logico compartilhado pelos providers.

| Evento | Schema |
| --- | --- |
| `LedgerEntryCreated.v1` | [`ledger-entry-created.v1.schema.json`](../../contracts/events/ledger-entry-created.v1.schema.json) |
| `LancamentoEstornoSolicitado.v1` | [`lancamento-estorno-solicitado.v1.schema.json`](../../contracts/events/lancamento-estorno-solicitado.v1.schema.json) |
| `ReprocessamentoLancamentosSolicitado.v1` | [`reprocessamento-lancamentos-solicitado.v1.schema.json`](../../contracts/events/reprocessamento-lancamentos-solicitado.v1.schema.json) |

## Regra de separacao

- Payload logico: representa o fato ou a solicitacao de negocio e deve permanecer independente do provider.
- Transporte Pub/Sub: usa `Data`, attributes, topic, subscription, ordering key, `Ack`, `Nack` e DLQ de aplicacao.
- Transporte Kafka: usa `Value`, headers, topic, message key, partition, offset, commit manual e DLQ de aplicacao.
- Metadados tecnicos podem variar por provider, mas nao devem mudar a semantica do evento.

## Documentos relacionados

- [Diagnostico de contratos de eventos](../reports/event-contracts-diagnostics.md)
- [Contrato LedgerEntryCreated.v1 com schema existente](../contracts/events/LedgerEntryCreated.v1.md)
- [JSON Schemas versionados](../../contracts/events/README.md)
- [Mensageria, Outbox e DLQ](../development/kafka-outbox.md)
