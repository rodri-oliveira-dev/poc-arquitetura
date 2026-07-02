# AuditService.Worker

Worker do bounded context `AuditService` para ingestao assincrona de registros
funcionais de auditoria.

## Consumer Kafka

O worker consome `AuditRecordRequested.v1` do topico
`audit.record.requested`, valida o contrato canonico, mapeia para
`CreateAuditRecordCommand` e persiste em `audit.functional_audit_records`.

O consumo fica desabilitado por padrao. Para ativar:

```json
{
  "AuditService": {
    "Worker": {
      "Enabled": true
    }
  },
  "Kafka": {
    "AuditRecordRequestedConsumer": {
      "Enabled": true,
      "BootstrapServers": "127.0.0.1:19092",
      "Topic": "audit.record.requested"
    }
  }
}
```

`EnableAutoCommit` e `EnableAutoOffsetStore` permanecem `false`. O offset e
commitado somente depois que a mensagem e tratada pelo processador.

## Idempotencia

Eventos Kafka usam `eventId` como chave idempotente, persistida em
`source_event_id` com indice unico. Essa chave e separada do `Idempotency-Key`
HTTP usado por `AuditService.Api`.

## Escopo atual

Nenhum outro bounded context publica eventos reais nesta etapa. Nao ha producer
em Ledger, Balance ou Transfer, e nao ha DLQ sofisticada para auditoria.
