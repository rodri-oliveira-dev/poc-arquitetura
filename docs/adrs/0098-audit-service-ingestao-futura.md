# ADR-0098: Pontos de extensao para ingestao futura do AuditService

## Status
Aceito

## Data
2026-07-01

## Contexto
O `AuditService` ja existe como bounded context separado, com contrato HTTP,
schema `audit`, seguranca e testes proprios. Ele ainda nao deve ser integrado a
`LedgerService`, `BalanceService` ou `TransferService`.

A proxima evolucao precisava preparar pontos de extensao para integracao futura
por HTTP interno, Kafka ou outro adapter, sem criar producer, consumer, worker,
topico ativo ou dependencia dos dominios financeiros.

## Decisao
Criar contratos canonicos internos e portas na camada Application do
`AuditService`, em `FunctionalAuditing/Ingestion`:

- `AuditRecordEnvelope`;
- `AuditRecordPayload`;
- `AuditActor`;
- `AuditMetadata`;
- `IAuditRecordIngestionService`;
- `IAuditRecordMapper`;
- `IAuditRecordValidator`;
- `IAuditRecordSerializer`;
- `IAuditIngestionSource`.

O fluxo de ingestao futura deve validar o envelope, traduzir para
`CreateAuditRecordCommand` e delegar ao caso de uso existente
`CreateAuditRecord`. A regra de criacao, idempotencia, persistencia e
invariantes do registro continuam no caso de uso e no dominio atuais.

Reservar namespaces/pastas para adapters futuros:

- `AuditService.Api/Ingestion/Http`, sem endpoint interno ativo;
- `AuditService.Infrastructure/Ingestion/Kafka`, sem worker ou consumer ativo.

Documentar o evento conceitual `AuditRecordRequested.v1` apenas como exemplo de
contrato futuro. Nenhum servico publica esse evento hoje.

## Consequencias positivas
- A futura integracao ganha um ponto de entrada canonico sem conhecer tipos de
  Ledger, Balance ou Transfer.
- O endpoint HTTP atual permanece funcionando como antes.
- Adapters futuros podem reaproveitar o mesmo caso de uso de criacao.
- Evita criar Kafka, worker, DLQ ou eventos reais antes de haver chamador
  integrado.

## Consequencias negativas
- Existem contratos internos ainda sem adapter ativo, exigindo disciplina para
  nao trata-los como contrato inter-servicos publicado.
- Uma integracao futura ainda exigira ADR propria para topico, headers,
  idempotencia, retry, DLQ, observabilidade, seguranca e rollout.

## Alternativas consideradas

### 1. Criar consumer Kafka agora
Rejeitada. Seria arquitetura especulativa: nao ha produtores, topico aprovado,
DLQ ou requisito atual de captura assincrona.

### 2. Fazer os servicos financeiros publicarem auditoria agora
Rejeitada. A etapa atual exige explicitamente nao alterar Ledger, Balance ou
Transfer.

### 3. Manter apenas o endpoint HTTP publico
Rejeitada parcialmente. O endpoint continua sendo o unico contrato ativo, mas
as portas internas reduzem friccao para adapters futuros sem duplicar regra de
criacao.

## Documentacao relacionada
- [AuditService](../architecture/audit-service.md)
- [AuditService API](../development/audit-api.md)
- [ADR-0097](./0097-functional-audit-service.md)
