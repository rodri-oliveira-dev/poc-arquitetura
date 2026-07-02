# Operacao do AuditService.Worker

Este runbook descreve a operacao minima do consumer Kafka
`AuditRecordRequested.v1` do `AuditService.Worker`.

O worker permanece isolado: `LedgerService`, `BalanceService` e
`TransferService` nao publicam eventos reais de auditoria nesta etapa.

## Topicos

| Uso | Topico |
| --- | --- |
| Consumo principal | `audit.record.requested` |
| DLQ de aplicacao | `audit.record.requested.dlq` |

O consumer fica desabilitado por padrao. Para consumo real, habilite
`AuditService:Worker:Enabled` e `Kafka:AuditRecordRequestedConsumer:Enabled`.

## Retry

Falhas propagadas pelo processamento sao tratadas como recuperaveis pelo
consumer:

1. o offset nao e commitado;
2. o worker tenta novamente a mesma mensagem ate
   `Kafka:AuditRecordRequestedConsumer:MaxProcessingAttempts`;
3. entre tentativas, aguarda `ProcessingRetryDelay`;
4. se as tentativas locais acabarem, a excecao volta ao loop do worker e aplica
   `ProcessingErrorRetryDelay`;
5. o offset segue sem commit para permitir nova entrega.

Esse comportamento cobre indisponibilidade temporaria de banco, timeout e falhas
transitorias de infraestrutura. O `CancellationToken` e respeitado durante o
processamento e os delays.

## DLQ

Erros definitivos sao publicados em `audit.record.requested.dlq` antes do commit
da mensagem original:

- JSON invalido;
- `eventType` diferente de `AuditRecordRequested.v1`;
- `schemaVersion` diferente de `1`;
- campos obrigatorios ausentes;
- metadata acima do limite;
- conflito definitivo de idempotencia por `eventId` reutilizado com payload
  diferente.

O payload da DLQ contem `eventId`, `correlationId`, topico, particao, offset,
motivo, categoria, horario da falha e hash SHA-256 do payload original. O payload
original nao e publicado para reduzir risco de exposicao de dado sensivel.

Se a publicacao na DLQ falhar, o offset original nao e commitado.

## Idempotencia

O worker mapeia `eventId` para `source_event_id` em
`audit.functional_audit_records`. O indice unico
`ux_audit_functional_audit_records_source_event_id` garante deduplicacao.

Duplicidade com mesmo payload e tratada como sucesso idempotente: o offset e
commitado e o resultado tecnico e registrado como `duplicate`.

## Logs e metricas

Logs incluem `eventId` e `correlationId` quando o payload permite extracao. Logs
de mensagens invalidas usam topico, particao, offset e categoria de falha sem
registrar payload bruto.

Metricas usam labels de baixa cardinalidade:

- `audit.worker.consumer.messages` com `topic` e `result`;
- `audit.worker.consumer.processing.duration` com `topic` e `result`;
- `audit.worker.consumer.retries` com `topic` e `error_type`;
- `audit.worker.dlq.messages` com `source_topic` e `failure_category`;
- `audit.worker.dlq.publish.errors` com `source_topic` e `error_type`.

## Validacao isolada

```powershell
dotnet restore ./src/audit/AuditService.Worker/AuditService.Worker.csproj
dotnet build ./src/audit/AuditService.Worker/AuditService.Worker.csproj --configuration Release --no-restore
dotnet test ./tests/audit/AuditService.Worker.Tests/AuditService.Worker.Tests.csproj --configuration Release
```

Quando houver mudanca em `Application`, `Domain`, `Infrastructure` ou `Api`,
execute tambem os testes isolados correspondentes de `tests/audit`.

## Fora de escopo atual

- producers reais em Ledger, Balance ou Transfer;
- redrive automatizado da DLQ;
- dashboard ou SLO produtivo;
- replay operacional sem decisao especifica.
