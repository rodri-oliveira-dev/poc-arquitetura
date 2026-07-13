# AuditService

O `AuditService` e o bounded context de auditoria funcional da POC. Ele registra
trilhas canonicas de operacoes de negocio sem assumir quem e o servico chamador.
Nesta etapa, o contexto existe isolado em `src/audit` e `tests/audit`, com
dominio, Application, Infrastructure, API HTTP, Worker, seguranca, persistencia,
testes e contrato OpenAPI proprios.

## Papel do bounded context

O contexto responde pela captura e consulta de eventos funcionais auditaveis:
operacao, correlacao, origem, tipo de operacao, entidade, merchant, actor,
status, reason, metadata e timestamps.

Ele nao substitui logs tecnicos, tracing distribuido, metricas ou Outbox dos
servicos financeiros. O objetivo e manter uma trilha funcional consultavel e
estavel para operacoes de negocio, com contrato HTTP canonico e idempotente.

No LikeC4, `AuditService.Application`, `AuditService.Domain` e
`AuditService.Infrastructure` sao modelados no nivel do bounded context, e nao
como filhos de `AuditService.Api`. A API HTTP e o Worker Kafka opcional sao
composition roots separados que referenciam esses assemblies compartilhados; o
Worker nao depende do container da API.

## Persistencia e schema audit

A decisao atual usa o PostgreSQL local compartilhado da POC com schema separado
`audit`. O `AuditDbContext` define `modelBuilder.HasDefaultSchema("audit")` e
a tabela principal e `audit.functional_audit_records`.

Esse desenho segue o padrao pragmatico ja usado em outros bounded contexts da
POC: um database local compartilhado para reduzir custo operacional do
laboratorio, mas schemas, migrations e responsabilidades separados por contexto.
O schema `audit` preserva isolamento logico, evita misturar tabelas de auditoria
com `ledger`, `balance`, `transfer` ou `identity`, e deixa aberta a possibilidade
de migrar para banco fisico proprio quando houver necessidade real.

## Relacao com Ledger, Balance e Transfer

Nao existe integracao nesta primeira etapa:

- `LedgerService` nao chama o `AuditService`;
- `BalanceService` nao chama o `AuditService`;
- `TransferService` nao chama o `AuditService`;
- o `AuditService.Worker` possui consumer Kafka opcional de
  `AuditRecordRequested.v1`, com retry controlado e DLQ de aplicacao;
- nenhum outro bounded context publica eventos reais de auditoria;
- nenhum evento financeiro foi alterado para carregar auditoria.

Essa separacao evita acoplamento prematuro e permite validar o contrato
funcional de auditoria antes de conectar fluxos de outros dominios.

## Estrategia futura de integracao assincrona

A estrategia proposta para integracao automatica futura esta registrada na
[ADR-0099](../adrs/0099-audit-async-integration-strategy.md). O consumer do
`AuditService.Worker` ja existe, mas ainda nao ha produtores reais. Quando
houver chamador real, a integracao deve seguir Outbox transacional local no
servico de origem e Kafka como transporte para o `AuditService.Worker`:

```text
Servico de origem
  -> operacao de negocio
  -> Outbox transacional local
  -> Worker/publicador do servico de origem
  -> Kafka
  -> AuditService.Worker
  -> schema audit
```

Essa direcao evita que falhas ou latencia do `AuditService` bloqueiem fluxos
financeiros principais. Ela tambem preserva o contrato canonico de auditoria,
com retry, DLQ, redrive e idempotencia tratados no fluxo assincrono.

Nesta etapa, apenas o consumidor do `AuditService.Worker` foi implementado e
endurecido. Producers e Outbox nos servicos de origem continuam fora de escopo.
Redrive automatico da DLQ tambem continua fora de escopo.

## Pontos de extensao para ingestao futura

O `AuditService.Application` contem contratos canonicos internos em
`FunctionalAuditing/Ingestion` para preparar adapters futuros sem ativar
integracao:

- `AuditRecordEnvelope`: envelope versionado da requisicao de auditoria;
- `AuditRecordPayload`: dados funcionais auditaveis;
- `AuditActor`: actor declarado no contrato canonico;
- `AuditMetadata`: metadados tecnicos do envelope, como correlacao;
- `IAuditRecordValidator`: valida apenas o formato minimo do envelope;
- `IAuditRecordMapper`: traduz o envelope para `CreateAuditRecordCommand`;
- `IAuditRecordSerializer`: serializa/desserializa o envelope;
- `IAuditRecordIngestionService`: valida, mapeia e delega ao caso de uso
  existente `CreateAuditRecord`.

Essas abstracoes nao duplicam a regra de criacao. A criacao, idempotencia,
persistencia e regras do registro continuam concentradas no caso de uso
`CreateAuditRecord` e no dominio de auditoria funcional.

As pastas `src/audit/AuditService.Api/Ingestion/Http` e
`src/audit/AuditService.Infrastructure/Ingestion/Kafka` continuam como pontos
documentados para adapters futuros. O consumer Kafka ativo fica em
`src/audit/AuditService.Worker/Messaging/Kafka`. Nao ha endpoint interno novo,
producer real ou publicacao ativa em outro bounded context.

O evento canonico `AuditRecordRequested.v1` solicita a criacao de uma trilha
funcional. Exemplo conceitual, sem produtor real atual:

```json
{
  "contractName": "AuditRecordRequested",
  "contractVersion": 1,
  "idempotencyKey": "11111111-1111-1111-1111-111111111111",
  "metadata": {
    "correlationId": "22222222-2222-2222-2222-222222222222",
    "causationId": "request-123",
    "attributes": {
      "adapter": "future-kafka"
    }
  },
  "payload": {
    "operationId": "33333333-3333-3333-3333-333333333333",
    "sourceService": "AnyCaller",
    "operationType": "FunctionalAuditRecorded",
    "entityType": "Payment",
    "entityId": "pay_123",
    "merchantId": "mrc_123",
    "actor": {
      "type": "Client",
      "subject": null,
      "clientId": "any-caller-api"
    },
    "status": "Succeeded",
    "reason": "Functional operation recorded by caller.",
    "metadata": {
      "channel": "api"
    },
    "occurredAt": "2026-07-01T10:15:30Z"
  }
}
```

Nenhum servico publica esse evento hoje. Qualquer ativacao deve ser definida em
ADR propria, incluindo topico, headers, idempotencia, retry, DLQ, seguranca,
observabilidade e criterios de rollout.

## Contrato canonico

O contrato HTTP canonico esta documentado em
[`docs/development/audit-api.md`](../development/audit-api.md) e versionado em
[`docs/openapi/audit.v1.json`](../openapi/audit.v1.json).

Endpoints:

- `POST /api/v1/audit-records`;
- `GET /api/v1/audit-records/{id}`;
- `GET /api/v1/audit-records/operations/{operationId}`;
- `GET /api/v1/audit-records`.

O contrato e agnostico ao chamador. Ele descreve a operacao auditada por campos
canonicos, nao por tipos internos de Ledger, Balance, Transfer ou Identity.

## SourceService e OperationType como strings

`sourceService` e `operationType` sao strings obrigatorias validadas por
presenca e tamanho, nao enums fechados.

Essa escolha e intencional porque o `AuditService` deve aceitar chamadores
atuais e futuros sem recompilar seu dominio a cada nova operacao auditavel.
Enums centralizados criariam acoplamento inverso: o contexto de auditoria teria
que conhecer previamente todos os bounded contexts e casos de uso consumidores.

A governanca fica no contrato e na documentacao: nomes devem ser estaveis,
descritivos, sem dados sensiveis e tratados como parte do vocabulario funcional
do chamador. Se a taxonomia crescer ou virar requisito de compliance, a evolucao
natural e criar catalogo ou registry de operacoes, sem mudar a premissa de que
o dominio de auditoria nao conhece regras internas dos chamadores.

## Politica de metadata

`metadata` e um objeto JSON simples de pares string/string, limitado a 4096
bytes serializados. Ele serve para atributos funcionais pequenos que ajudam a
consultar ou interpretar a trilha.

Nao deve ser usado para persistir payload bruto de request/response, PAN, senha,
token, segredo, documento completo ou qualquer dado sensivel sem decisao
explicita. Quando um atributo for necessario, prefira valores minimizados,
mascarados ou identificadores opacos.

## Segurança e autorizacao

Os endpoints de auditoria exigem JWT Bearer validado por issuer, audience e JWKS
configurados.

Scopes:

- `audit.write`: cria registros;
- `audit.read`: consulta registros dos merchants presentes na claim
  `merchant_id`;
- `audit.admin`: consulta registros sem restricao de merchant ou filtra por
  diferentes merchants.

Na criacao, quando o token contem `sub` ou `client_id`, o actor persistido e
derivado das claims e prevalece sobre o body. Esse comportamento reduz o risco
de um chamador forjar identidade funcional na request.

## Idempotencia

`POST /api/v1/audit-records` exige `Idempotency-Key` UUID. O schema `audit`
mantem indice unico para a chave, e a Application compara um hash canonico do
payload logico.

Retries com mesma chave e mesmo payload retornam o mesmo identificador. Reuso
da chave com payload diferente retorna `409 Conflict`.

Para Kafka, a idempotencia usa `eventId` do `AuditRecordRequested.v1`, persistido
em `source_event_id` com indice unico. Essa chave e separada do
`Idempotency-Key` HTTP para preservar as semanticas de transporte.

## Limitacoes conhecidas

- O contexto ainda nao esta conectado a nenhum fluxo de Ledger, Balance ou
  Transfer.
- Ha consumer Kafka endurecido no `AuditService.Worker`, mas nao ha producer
  real nos demais bounded contexts.
- Nao ha Outbox de auditoria nos servicos de origem nem redrive automatico da
  DLQ de auditoria.
- Os contratos de ingestao em `Application` sao pontos internos de extensao e
  ainda nao representam contrato de mensageria ativo entre servicos.
- Nao ha catalogo central versionado de `sourceService` e `operationType`.
- A retencao, particionamento, arquivamento e politicas de expurgo de registros
  ainda nao foram definidos.
- A documentacao descreve o comportamento atual; qualquer mudanca de contrato
  HTTP deve regenerar `docs/openapi/audit.v1.json`.

## Proximas evolucoes planejadas

- Definir criterios objetivos para conectar fluxos de outros bounded contexts.
- Implementar producers por Outbox + Kafka apenas quando houver primeiro fluxo
  produtor definido, contrato versionado e plano de retry/DLQ.
- Registrar catalogo leve de operacoes auditaveis quando houver o primeiro
  chamador real.
- Avaliar retencao, expurgo e mascaramento conforme requisitos de auditoria.
- Avaliar Outbox, Kafka ou worker apenas se houver necessidade de captura
  assincrona, resiliencia entre servicos ou desacoplamento operacional real.
- Atualizar LikeC4 e ADRs quando a primeira integracao for desenhada.
