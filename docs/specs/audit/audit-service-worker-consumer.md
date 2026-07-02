# AuditService.Worker consumer

## Objetivo

Implementar o consumo Kafka de `AuditRecordRequested.v1` no
`AuditService.Worker`, persistindo registros em
`audit.functional_audit_records` de forma idempotente.

Esta fatia habilita apenas o consumidor do AuditService. `LedgerService`,
`BalanceService` e `TransferService` continuam sem producer real de auditoria.

## Fluxo

```text
Kafka topic audit.record.requested
  -> AuditService.Worker
  -> valida AuditRecordRequested.v1
  -> mapeia para CreateAuditRecordCommand
  -> persiste em audit.functional_audit_records
  -> deduplica por source_event_id
  -> publica DLQ para erro definitivo
  -> commita offset
```

## Decisao tecnica

Classificacao: necessaria para cumprir o requisito explicito desta etapa.

O consumer fica em `AuditService.Worker`, mantendo Kafka fora de `Domain` e
`Application`. A persistencia continua pelo caso de uso existente
`CreateAuditRecordCommand`.

Para nao misturar a semantica do `Idempotency-Key` HTTP com a idempotencia de
evento, foi adicionada a coluna nullable `source_event_id` em
`audit.functional_audit_records`, com indice unico
`ux_audit_functional_audit_records_source_event_id`.

O campo `eventId` de `AuditRecordRequested.v1` e mapeado para
`SourceEventId`. Eventos duplicados com o mesmo payload retornam o registro ja
persistido; reuso do mesmo `eventId` com payload diferente gera conflito
definitivo e e enviado para DLQ antes do commit.

## Configuracao

O consumer fica desabilitado por padrao:

```json
{
  "AuditService": {
    "Worker": {
      "Enabled": false
    }
  },
  "Kafka": {
    "AuditRecordRequestedConsumer": {
      "Enabled": false,
      "BootstrapServers": "127.0.0.1:19092",
      "Topic": "audit.record.requested",
      "GroupId": "audit-record-requested-consumer",
      "EnableAutoCommit": false,
      "EnableAutoOffsetStore": false,
      "DeadLetterTopic": "audit.record.requested.dlq",
      "MaxProcessingAttempts": 3
    }
  }
}
```

Para iniciar consumo real, `AuditService:Worker:Enabled` e
`Kafka:AuditRecordRequestedConsumer:Enabled` devem estar `true`.

## Tratamento de erros e DLQ

- JSON invalido ou contrato incompatível: erro definitivo, publicado em
  `audit.record.requested.dlq` e commitado apenas apos a DLQ confirmar publish.
- Falha transitoria de persistencia: retry local controlado e offset sem commit
  enquanto a persistencia nao concluir.
- Conflito de `source_event_id` com payload diferente: erro definitivo de
  idempotencia, publicado em DLQ antes do commit.
- Commit Kafka acontece apenas depois que o processador conclui o tratamento da
  mensagem.
- O payload bruto nao e publicado na DLQ; o worker envia hash SHA-256 e
  metadados de transporte.

## Validacao

Valido para esta etapa:

```powershell
dotnet restore ./src/audit/AuditService.Worker/AuditService.Worker.csproj
dotnet build ./src/audit/AuditService.Worker/AuditService.Worker.csproj --configuration Release --no-restore
dotnet test ./tests/audit/AuditService.Worker.Tests/AuditService.Worker.Tests.csproj --configuration Release
```

Como a idempotencia adicionou coluna, indice e contrato de Application, tambem
devem ser executados os builds/testes isolados de `AuditService.Api`,
`AuditService.Domain.Tests`, `AuditService.Application.Tests`,
`AuditService.Infrastructure.Tests` e `AuditService.Api.Tests`.

## Fora de escopo

- Criar producer Kafka em qualquer bounded context.
- Alterar Ledger, Balance ou Transfer.
- Criar eventos financeiros novos.
- Criar redrive automatico de DLQ.
- Executar testes dos demais dominios.
