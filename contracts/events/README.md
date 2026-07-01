# Contratos versionados de eventos

Esta pasta contem JSON Schemas versionados para os payloads logicos dos eventos.

Os schemas validam somente o JSON compartilhado entre produtor e consumidor. Detalhes de Pub/Sub e Kafka, como topic, subscription, attributes, headers, key, ack, commit, offset e DLQ, ficam fora destes arquivos.

## Schemas

| Evento | Schema | Exemplo valido | Exemplo invalido |
| --- | --- | --- | --- |
| `AuditRecordRequested.v1` | [audit-record-requested.v1.schema.json](audit-record-requested.v1.schema.json) | [audit-record-requested.v1.valid.json](examples/audit-record-requested.v1.valid.json) | [audit-record-requested.v1.invalid.json](examples/audit-record-requested.v1.invalid.json) |
| `LedgerEntryCreated.v1` | [ledger-entry-created.v1.schema.json](ledger-entry-created.v1.schema.json) | [ledger-entry-created.v1.valid.json](examples/ledger-entry-created.v1.valid.json) | [ledger-entry-created.v1.invalid.json](examples/ledger-entry-created.v1.invalid.json) |
| `LedgerEntryCreated.v2` | [ledger-entry-created.v2.schema.json](ledger-entry-created.v2.schema.json) | [ledger-entry-created.v2.valid.json](examples/ledger-entry-created.v2.valid.json) | [ledger-entry-created.v2.invalid.json](examples/ledger-entry-created.v2.invalid.json) |
| `LancamentoEstornoSolicitado.v1` | [lancamento-estorno-solicitado.v1.schema.json](lancamento-estorno-solicitado.v1.schema.json) | [lancamento-estorno-solicitado.v1.valid.json](examples/lancamento-estorno-solicitado.v1.valid.json) | [lancamento-estorno-solicitado.v1.invalid.json](examples/lancamento-estorno-solicitado.v1.invalid.json) |
| `ReprocessamentoLancamentosSolicitado.v1` | [reprocessamento-lancamentos-solicitado.v1.schema.json](reprocessamento-lancamentos-solicitado.v1.schema.json) | [reprocessamento-lancamentos-solicitado.v1.valid.json](examples/reprocessamento-lancamentos-solicitado.v1.valid.json) | [reprocessamento-lancamentos-solicitado.v1.invalid.json](examples/reprocessamento-lancamentos-solicitado.v1.invalid.json) |

## Uso

Use o schema correspondente ao `event_type` versionado do transporte para validar o `Data` do Pub/Sub ou o `Value` do Kafka depois de decodificar o JSON.

Estes schemas refletem os contratos documentados. A maioria dos eventos nao adiciona envelope, `eventId`, `eventName`, `eventVersion` ou `idempotencyKey` ao payload logico. `AuditRecordRequested.v1` e uma excecao deliberada: ele e o contrato canonico de auditoria e inclui `eventId`, `eventType`, `schemaVersion` e `idempotencyKey` no proprio payload para preservar semantica funcional, idempotencia e versionamento entre produtores futuros e o `AuditService`. `currency` existe somente em `LedgerEntryCreated.v2`; `LedgerEntryCreated.v1` permanece legado sem moeda no payload.
