# ADR-0097: Bounded context de auditoria funcional

## Status
Aceito

## Data
2026-07-01

## Contexto
O repositorio passou a conter um `AuditService` em `src/audit`, com testes em
`tests/audit`, dominio, persistencia, endpoints HTTP, seguranca e OpenAPI
proprios.

Sistemas financeiros precisam de trilhas funcionais auditaveis por operacao,
mas a auditoria nao deve depender de detalhes internos de um unico bounded
context. Tambem nao deve ser confundida com logs tecnicos, tracing, metricas ou
Outbox de integracao.

A decisao precisava documentar o papel do `AuditService` sem criar integracao
prematura com `LedgerService`, `BalanceService` ou `TransferService`, e sem
introduzir worker, Kafka ou novos fluxos assincronos nesta etapa.

## Decisao
Adotar o `AuditService` como bounded context separado de auditoria funcional,
com contrato HTTP canonico e agnostico ao servico chamador.

A primeira etapa usa:

- projetos isolados em `src/audit`;
- testes isolados em `tests/audit`;
- schema PostgreSQL `audit` no database compartilhado da POC;
- tabela `audit.functional_audit_records`;
- endpoints HTTP versionados em `/api/v1/audit-records`;
- `Idempotency-Key` obrigatorio em `POST`;
- scopes `audit.write`, `audit.read` e `audit.admin`;
- `sourceService` e `operationType` como strings validadas por presenca e
  tamanho, nao como enums fechados;
- `metadata` como JSON simples de pares string/string, com limite de tamanho e
  sem payload bruto.

Nao integrar o `AuditService` aos demais dominios nesta etapa. Portanto:

- nao alterar Ledger, Balance ou Transfer para chamar auditoria;
- nao criar worker de auditoria;
- nao consumir Kafka;
- nao alterar contratos de eventos financeiros;
- nao acoplar o contrato do AuditService a tipos internos de outros bounded
  contexts.

## Consequencias positivas
- O bounded context de auditoria fica explicito, testavel e documentado sem
  contaminar os dominios financeiros.
- O contrato HTTP canonico permite qualquer chamador futuro, sem dependencias
  diretas de Ledger, Balance ou Transfer.
- O schema `audit` separa responsabilidade no PostgreSQL compartilhado da POC,
  mantendo baixo custo operacional local.
- A idempotencia por chave UUID torna retries de criacao seguros.
- `audit.read` e `audit.admin` permitem separar consulta por merchant de acesso
  administrativo.
- A ausencia de Kafka/worker reduz complexidade enquanto nao existe chamador
  real integrado.

## Consequencias negativas
- A auditoria ainda nao e populada automaticamente pelos demais servicos.
- Chamadores futuros precisarao implementar a chamada HTTP ou outro mecanismo de
  publicacao quando a integracao for decidida.
- O schema compartilhado reduz isolamento fisico em comparacao a um banco
  proprio.
- Sem worker ou fila, a captura inicial e sincrona para quem decidir chamar a
  API no futuro.
- Sem catalogo central, `sourceService` e `operationType` dependem de governanca
  documental ate que haja necessidade de taxonomia formal.

## Alternativas consideradas

### 1. Auditoria dentro de cada servico
Rejeitada para a trilha funcional canonica. Essa alternativa reduziria um
servico, mas espalharia consultas, formatos, politicas de metadata e
autorizacao. Tambem dificultaria reconstruir uma trilha transversal por
`operationId`.

### 2. AuditService separado com schema proprio
Escolhida para esta etapa. Mantem o bounded context separado, com schema
`audit`, migrations e contrato proprio, sem custo operacional de banco fisico
adicional no laboratorio local.

### 3. AuditService separado com banco fisico proprio
Adiada. E uma opcao valida quando isolamento fisico, retencao, throughput,
backup, restauracao ou governanca de acesso exigirem separacao real. Para a POC
atual, o custo e a complexidade seriam maiores que o beneficio observavel.

### 4. Auditoria assincrona via Kafka desde o inicio
Rejeitada nesta etapa. Kafka e adequado quando houver produtores reais,
necessidade de desacoplamento, tolerancia a indisponibilidade da API de
auditoria ou alto volume. Criar topicos, consumers, worker, DLQ e redrive antes
do primeiro fluxo integrado aumentaria superficie operacional sem uso real.

## Motivos para nao criar Kafka ou Worker agora
- Ainda nao existe integracao entre AuditService e os demais bounded contexts.
- Nao ha contrato de evento de auditoria funcional acordado entre produtores e
  consumidor.
- Nao ha requisito atual de captura assincrona, backlog, DLQ ou redrive.
- A API HTTP ja permite validar o contrato canonico, seguranca e idempotencia.
- Criar worker agora misturaria evolucao arquitetural com infraestrutura sem
  beneficio observavel nesta etapa.

## Criterios para evoluir para integracao
Avaliar nova ADR antes de conectar o `AuditService` a outros fluxos quando
houver pelo menos um dos sinais abaixo:

- um bounded context precisar registrar eventos funcionais reais no AuditService;
- a chamada HTTP sincrona criar acoplamento temporal inaceitavel;
- o volume de auditoria exigir buffering, backpressure ou processamento
  dedicado;
- houver requisito de entrega duravel com retry, DLQ, redrive ou replay;
- a taxonomia de `sourceService` e `operationType` exigir catalogo formal;
- compliance, retencao ou seguranca exigirem banco fisico proprio ou controles
  operacionais separados.

Qualquer integracao futura deve preservar correlacao, idempotencia, autorizacao
por merchant quando aplicavel, contrato versionado e fronteiras entre bounded
contexts.

## Documentacao relacionada
- [AuditService API](../development/audit-api.md)
- [Arquitetura do AuditService](../architecture/audit-service.md)
- [OpenAPI audit.v1](../openapi/audit.v1.json)

## Evolucao posterior

Em etapa posterior, o `AuditService.Worker` passou a consumir
`AuditRecordRequested.v1` via Kafka, sem criar producers em Ledger, Balance ou
Transfer. A estrategia assincrona e a idempotencia por `eventId` ficam
registradas na [ADR-0099](./0099-audit-async-integration-strategy.md).
