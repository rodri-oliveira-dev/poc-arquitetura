# ADR-0064: Processo dedicado para workers do Ledger

## Status

Aceito

## Contexto

O `LedgerService.Api` passou a hospedar apenas a superficie HTTP do Ledger. Os componentes assincronos do Ledger continuavam implementados em `LedgerService.Infrastructure`, mas precisavam de um host proprio para evitar que a API HTTP tambem executasse polling de Outbox, processamento de estornos e consumo Kafka de reprocessamentos.

Nota de evolucao: em reorganizacao posterior, os HostedServices e adapters tecnicos exclusivos de background foram movidos fisicamente para `LedgerService.Worker`; `LedgerService.Infrastructure` ficou com persistencia, repositories e componentes compartilhados.

## Decisao

Criar `LedgerService.Worker` como processo .NET Worker Service separado da API.

O Worker referencia `LedgerService.Application` e `LedgerService.Infrastructure` e registra explicitamente:

- `OutboxKafkaPublisherService`;
- `EstornoLancamentoProcessorService`;
- `ReprocessamentoLancamentosConsumerService`.

O processo tambem registra Application, persistencia, repositorios, produtor Kafka de Outbox e consumer Kafka de reprocessamentos. Ele nao expoe controllers, Swagger, JWT, CORS, rate limiting nem endpoints HTTP.

As configuracoes de Kafka, Outbox, estornos e reprocessamentos ficam no `appsettings.json` do Worker. O `LedgerService.Api` mantem configuracoes de HTTP, JWT, hardening, observabilidade da API e banco, porque ainda recebe comandos HTTP e grava dados transacionais no PostgreSQL.

## Consequencias

- O ciclo de vida dos jobs do Ledger passa a ser independente do ciclo de vida da API HTTP.
- A stack local precisa subir o novo container `ledger-worker` para publicar Outbox e processar estornos/reprocessamentos.
- O `LedgerService.Api` continua responsavel por gravar solicitacoes e mensagens Outbox, mas nao publica no Kafka nem consome topicos.
- Observabilidade do Worker fica preparada por configuracao com `ServiceName=LedgerService.Worker`; a instrumentacao OpenTelemetry compartilhada deve ser extraida em etapa propria, sem acoplar o Worker a extensoes ASP.NET Core.
