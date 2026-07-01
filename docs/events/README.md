# Contratos logicos de eventos

Esta pasta documenta os contratos logicos atuais dos eventos de integracao e operacionais antes de qualquer mudanca de payload ou criacao de novos JSON Schemas.

O contrato logico do evento deve ser o mesmo quando publicado por Pub/Sub ou Kafka. As diferencas entre providers ficam nos adapters de transporte, incluindo topic fisico, subscription, headers, attributes, ordering key, message key, ack, nack, commit e DLQ.

## Eventos

| Evento | Natureza | Produtor | Consumidores atuais |
| --- | --- | --- | --- |
| [AuditRecordRequested.v1](audit-record-requested-v1.md) | Solicitacao canonica futura para auditoria funcional | Nenhum produtor atual | Nenhum consumidor atual |
| [LedgerEntryCreated.v1](ledger-entry-created-v1.md) | Integracao Ledger para Balance, legado | `LedgerService` historico | `BalanceService.Worker` por Pub/Sub ou Kafka |
| [LedgerEntryCreated.v2](ledger-entry-created-v2.md) | Integracao Ledger para Balance | `LedgerService` | `BalanceService.Worker` por Pub/Sub ou Kafka |
| [LancamentoEstornoSolicitado.v1](lancamento-estorno-solicitado-v1.md) | Operacional do Ledger | `LedgerService` | Nenhum consumer de mensageria encontrado |
| [ReprocessamentoLancamentosSolicitado.v1](reprocessamento-lancamentos-solicitado-v1.md) | Operacional do Ledger | `LedgerService` | `LedgerService.Worker` no modo Kafka |
| `TransferenciaSolicitada.v1` | Saga de transferencia | `TransferService.Api` | Topico Kafka para rastreabilidade da Saga |
| `TransferenciaDebitoCriado.v1` | Saga de transferencia | `TransferService.Worker` | Topico Kafka para rastreabilidade da Saga |
| `TransferenciaCreditoCriado.v1` | Saga de transferencia | `TransferService.Worker` | Topico Kafka para rastreabilidade da Saga |
| `TransferenciaConcluida.v1` | Saga de transferencia | `TransferService.Worker` | Topico Kafka para rastreabilidade da Saga |
| `TransferenciaCompensacaoSolicitada.v1` | Saga de transferencia | `TransferService.Worker` | Topico Kafka para rastreabilidade da compensacao |
| `TransferenciaCompensada.v1` | Saga de transferencia | `TransferService.Worker` | Topico Kafka para rastreabilidade da compensacao confirmada |
| `TransferenciaFalhou.v1` | Saga de transferencia | `TransferService.Worker` | Topico Kafka para falhas definitivas |

## Eventos da Saga do TransferService

O `TransferService` usa Kafka como transporte explicito para eventos da Saga. Pub/Sub nao faz parte deste fluxo. A API e o Worker gravam eventos logicos no Outbox transacional do schema `transfer`; o `TransferService.Worker` publica mensagens pendentes no Kafka e marca a Outbox como publicada somente apos confirmacao do producer.

| Event type | Topico Kafka | Message key |
| --- | --- | --- |
| `TransferenciaSolicitada.v1` | `transfer.transferencia.solicitada` | `transferenciaId` |
| `TransferenciaDebitoCriado.v1` | `transfer.transferencia.debito-criado` | `transferenciaId` |
| `TransferenciaCreditoCriado.v1` | `transfer.transferencia.credito-criado` | `transferenciaId` |
| `TransferenciaConcluida.v1` | `transfer.transferencia.concluida` | `transferenciaId` |
| `TransferenciaCompensacaoSolicitada.v1` | `transfer.transferencia.compensacao-solicitada` | `transferenciaId` |
| `TransferenciaCompensada.v1` | `transfer.transferencia.compensada` | `transferenciaId` |
| `TransferenciaFalhou.v1` | `transfer.transferencia.falhou` | `transferenciaId` |

Payload logico minimo dos eventos da Saga:

| Campo | Descricao |
| --- | --- |
| `transferenciaId` | Identificador da Saga e aggregate id. |
| `sourceMerchantId` | Merchant debitado. |
| `destinationMerchantId` | Merchant creditado. |
| `amount` | Valor positivo da transferencia. |
| `status` | Estado da Saga apos o evento. |
| `occurredAt` | Data/hora UTC do evento. |
| `correlationId` | Correlacao HTTP/worker quando disponivel. |
| `debitLancamentoId` | Lancamento de debito, quando ja criado. |
| `creditLancamentoId` | Lancamento de credito, quando ja criado. |
| `compensationEstornoId` | Estorno de compensacao, quando solicitado/registrado. |
| `failureReason` | Motivo tecnico/definitivo em eventos de falha. |

Mensagens com payload invalido ou erro definitivo de publicacao sao enviadas para a DLQ de aplicacao `transfer.transferencia.dlq`. Erros temporarios mantem a Outbox pendente para retry controlado pelo Worker.

## JSON Schemas

Os JSON Schemas versionados ficam em [`../../contracts/events`](../../contracts/events) e representam somente o payload logico compartilhado pelos providers.

| Evento | Schema |
| --- | --- |
| `AuditRecordRequested.v1` | [`audit-record-requested.v1.schema.json`](../../contracts/events/audit-record-requested.v1.schema.json) |
| `LedgerEntryCreated.v1` | [`ledger-entry-created.v1.schema.json`](../../contracts/events/ledger-entry-created.v1.schema.json) |
| `LedgerEntryCreated.v2` | [`ledger-entry-created.v2.schema.json`](../../contracts/events/ledger-entry-created.v2.schema.json) |
| `LancamentoEstornoSolicitado.v1` | [`lancamento-estorno-solicitado.v1.schema.json`](../../contracts/events/lancamento-estorno-solicitado.v1.schema.json) |
| `ReprocessamentoLancamentosSolicitado.v1` | [`reprocessamento-lancamentos-solicitado.v1.schema.json`](../../contracts/events/reprocessamento-lancamentos-solicitado.v1.schema.json) |

## Regra de separacao

- Payload logico: representa o fato ou a solicitacao de negocio e deve permanecer independente do provider.
- Transporte Pub/Sub: usa `Data`, attributes, topic, subscription, ordering key, `Ack`, `Nack` e DLQ de aplicacao.
- Transporte Kafka: usa `Value`, headers, topic, message key, partition, offset, commit manual e DLQ de aplicacao.
- Metadados tecnicos podem variar por provider, mas nao devem mudar a semantica do evento.

## Documentos relacionados

- [Politica de versionamento de contratos de eventos](../development/event-contract-versioning.md)
- [Diagnostico de contratos de eventos](../reports/event-contracts-diagnostics.md)
- [Contrato AuditRecordRequested.v1](audit-record-requested-v1.md)
- [Contrato LedgerEntryCreated.v1 com schema existente](../contracts/events/LedgerEntryCreated.v1.md)
- [Contrato LedgerEntryCreated.v2](ledger-entry-created-v2.md)
- [JSON Schemas versionados](../../contracts/events/README.md)
- [Mensageria, Outbox e DLQ](../development/kafka-outbox.md)
- [Runbook DLQ e replay da Saga do TransferService](../operations/transfer-saga-kafka.md)
