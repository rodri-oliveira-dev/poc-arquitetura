# SDD: hardening do AuditService.Worker

## Objetivo

Endurecer o `AuditService.Worker` apos a implementacao do consumer
`AuditRecordRequested.v1`, melhorando resiliencia, observabilidade, DLQ,
idempotencia e testes antes de qualquer integracao com outros bounded contexts.

## Decisoes implementadas

Classificacao: necessaria para corrigir risco operacional do consumer antes de
conectar produtores reais.

- Retry local controlado no consumer para falhas propagadas pelo processor.
- DLQ Kafka de aplicacao em `audit.record.requested.dlq` para falhas
  definitivas.
- Commit de offset apenas apos sucesso, duplicidade idempotente ou publicacao
  segura na DLQ.
- Payload bruto nao e enviado para DLQ; e armazenado apenas hash SHA-256 e
  metadados operacionais.
- `CreateAuditRecordResult` informa `Duplicate` para permitir log/metrica de
  idempotencia no worker.
- Metricas usam `System.Diagnostics.Metrics` no meter `AuditService.Worker`.
- Testes do worker usam fakes em memoria e nao sobem Kafka/Testcontainers.

## Fluxos esperados

### Evento valido

1. desserializa e valida `AuditRecordRequested.v1`;
2. mapeia para `CreateAuditRecordCommand`;
3. persiste em `audit.functional_audit_records`;
4. registra resultado `success`;
5. commita offset.

### Evento duplicado

1. `Application` resolve registro existente por `source_event_id`;
2. retorna `Duplicate=true`;
3. worker registra resultado `duplicate`;
4. commita offset.

### Erro transitorio

1. excecao e propagada pelo processor;
2. consumer nao commita offset;
3. aplica retry local ate `MaxProcessingAttempts`;
4. se continuar falhando, aplica backoff do loop externo;
5. offset segue pendente para nova entrega.

### Erro definitivo

1. processor classifica JSON, contrato ou conflito de idempotencia;
2. publica mensagem na DLQ;
3. registra resultado `dlq`;
4. commita offset original apenas apos publish bem sucedido.

## Contrato da DLQ

Campos persistidos no payload da DLQ:

- `eventId`, quando disponivel;
- `correlationId`, quando disponivel;
- `originalTopic`;
- `originalPartition`;
- `originalOffset`;
- `failureReason`;
- `failureCategory`;
- `occurredAt`;
- `payloadSha256`.

Headers Kafka incluem motivo, categoria, coordenadas originais e identificadores
quando disponiveis.

## Performance

- O consumo continua individual, coerente com o adapter atual.
- O payload e desserializado uma vez por tentativa de processamento.
- A idempotencia usa indice unico em `source_event_id`.
- Caminho feliz tem um log de sucesso e metricas de baixa cardinalidade.
- Testes unitarios cobrem retry, DLQ e commit sem broker real.

## Validacao

Escopo permitido:

```powershell
dotnet restore ./src/audit/AuditService.Worker/AuditService.Worker.csproj
dotnet build ./src/audit/AuditService.Worker/AuditService.Worker.csproj --configuration Release --no-restore
dotnet test ./tests/audit/AuditService.Worker.Tests/AuditService.Worker.Tests.csproj --configuration Release
```

Como houve ajuste pequeno em `AuditService.Application`, validar tambem os
projetos isolados do AuditService afetados.

## Fora de escopo

- Alterar Ledger, Balance ou Transfer.
- Criar producer Kafka.
- Consumir eventos financeiros especificos.
- Criar redrive automatico.
- Executar build/test da solution inteira.
