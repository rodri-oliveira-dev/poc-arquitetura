# AuditService.Worker structure

## Objetivo

Criar a estrutura inicial do `AuditService.Worker` dentro do bounded context AuditService, sem consumo Kafka real nesta etapa.

## Decisao

O worker usa `Microsoft.NET.Sdk.Worker`, referencia apenas `AuditService.Application` e `AuditService.Infrastructure`, registra a composition root do proprio contexto e mantem observabilidade OpenTelemetry opcional alinhada aos demais workers.

O hosted service inicial e um placeholder baseado em `IHostedService`. Ele nao executa loop, nao conecta em topicos, nao cria DLQ e nao processa eventos. A opcao `AuditService:Worker:Enabled` nasce com default seguro `false`; quando o consumer real de `AuditRecordRequested.v1` for implementado, esta composition root deve ser substituida ou estendida com o processamento efetivo.

Nao foram adicionados endpoints de health/readiness porque os workers atuais do repositorio nao expoem HTTP para probes. Se a plataforma de execucao futura exigir probes HTTP, a decisao deve ser documentada e implementada com estado interno leve.

## Escopo criado

- `src/audit/AuditService.Worker`
- `tests/audit/AuditService.Worker.Tests`

## Fora de escopo confirmado

- Consumo Kafka real.
- Producer em outros servicos.
- DLQ, retry ou backoff de mensagens.
- Integracao com LedgerService, BalanceService ou TransferService.
- Mudancas em outros bounded contexts.
