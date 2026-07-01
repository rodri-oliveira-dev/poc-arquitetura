# ADR-0099: Estrategia de integracao assincrona do AuditService

## Status
Proposto

## Data
2026-07-01

## Contexto
O `AuditService` ja existe em `src/audit`, com testes em `tests/audit`,
bounded context proprio, schema `audit`, contrato HTTP canonico e pontos
internos de extensao para ingestao futura.

Ele ainda nao esta integrado com `LedgerService`, `BalanceService` ou
`TransferService`. Esses bounded contexts executam fluxos financeiros principais
como criacao de lancamentos, estornos, reprocessamentos, projecoes de saldo e
transferencias. A captura de auditoria funcional e importante, mas nao deve
introduzir acoplamento temporal, dependencia de disponibilidade ou risco de
bloqueio nesses fluxos.

A decisao precisava definir a estrategia futura de integracao sem criar
producer, consumer, worker, topico, evento real ou alteracao nos servicos de
origem nesta etapa.

## Decisao
Quando a integracao automatica do `AuditService` for implementada, o padrao sera
assincrono por Outbox transacional local no servico de origem e Kafka como
transporte entre bounded contexts.

Fluxo alvo:

```text
Servico de origem
  -> operacao de negocio
  -> Outbox transacional local
  -> Worker/publicador do servico de origem
  -> Kafka
  -> AuditService.Worker
  -> schema audit
```

Os servicos de origem devem gravar a intencao de auditoria na propria transacao
da operacao de negocio, junto com seus dados locais e sua Outbox. Um worker ou
publicador do proprio servico de origem publicara eventos canonicos de auditoria
no Kafka. O `AuditService.Worker` consumira esses eventos, validara o envelope
canonico, aplicara idempotencia e persistira registros funcionais no schema
`audit` usando o caso de uso de criacao existente.

Esta ADR nao implementa a integracao. Portanto, nesta etapa:

- `LedgerService` nao publica eventos de auditoria;
- `BalanceService` nao publica eventos de auditoria;
- `TransferService` nao publica eventos de auditoria;
- nenhum producer Kafka de auditoria foi criado;
- nenhum consumer Kafka de auditoria foi criado;
- nenhum `AuditService.Worker` foi criado;
- nenhum contrato HTTP existente foi alterado;
- nenhum contrato de evento foi versionado como ativo.

## Motivos da decisao
- Auditoria funcional nao deve bloquear o fluxo financeiro principal.
- Falha ou indisponibilidade do `AuditService` nao deve impedir criacao de
  lancamento, estorno, reprocessamento, projecao ou transferencia.
- Outbox preserva consistencia entre a operacao de origem e a intencao de
  auditoria, sem depender de chamada remota dentro da transacao de negocio.
- Kafka desacopla disponibilidade e ritmo de processamento entre produtores e
  consumidor.
- O `AuditService` mantem contrato canonico e agnostico ao chamador, sem
  depender de tipos internos de Ledger, Balance ou Transfer.
- Retry, backoff, DLQ, redrive e observabilidade sao mais naturais no fluxo
  assincrono do que em chamadas HTTP sincronas feitas no caminho critico.
- HTTP sincrono pode ser avaliado apenas para cenarios internos especificos,
  administrativos ou de baixa criticidade, mas nao como padrao inicial de
  integracao entre os fluxos financeiros e auditoria.

## Diretrizes para eventos canonicos futuros
Os servicos de origem devem publicar eventos canonicos de auditoria, nao eventos
de dominio internos crus. O contrato deve representar a operacao auditavel em
linguagem funcional estavel, com campos como:

- identificador de operacao auditavel;
- servico de origem;
- tipo de operacao;
- entidade auditada;
- merchant quando aplicavel;
- actor ou identidade tecnica declarada;
- status funcional;
- reason funcional;
- metadata minimizada e sem segredos;
- `occurredAt`;
- correlation id, causation id e idempotency key.

O contrato nao deve exigir que o `AuditService` conheca aggregates, commands,
handlers, entidades EF, eventos de dominio ou tipos especificos dos servicos de
origem.

## Retry, DLQ e idempotencia
Os publicadores dos servicos de origem devem seguir a politica de Outbox ja
adotada no repositorio: publicacao at-least-once, tentativas com backoff,
marcacao de falha e caminho operacional para reprocessamento quando aplicavel.

O consumo pelo `AuditService.Worker` tambem deve assumir entrega at-least-once.
Por isso, a idempotencia e obrigatoria no contrato canonico. A chave de
idempotencia deve ser estavel para a operacao auditavel e permitir que retries,
redrives e replays nao dupliquem registros funcionais.

Falhas transientes devem ser tentadas novamente. Falhas permanentes de contrato,
payload invalido, schema incompativel ou violacao de politica devem ir para DLQ
com metadados suficientes para diagnostico, redrive controlado ou descarte
auditavel. A DLQ nao deve virar fonte primaria de consulta funcional.

## Alternativas consideradas

### 1. Auditoria dentro de cada servico
Rejeitada para a trilha funcional canonica. Manter auditoria em Ledger, Balance
e Transfer reduziria um servico, mas espalharia contratos, consultas, politicas
de metadata, autorizacao e retencao. Tambem dificultaria reconstruir uma trilha
transversal por operacao ou merchant.

### 2. AuditService chamado por HTTP sincrono
Rejeitada como padrao inicial de integracao dos fluxos financeiros. HTTP
sincrono e simples para casos pontuais, mas cria acoplamento temporal: timeout,
latencia, indisponibilidade ou erro do `AuditService` passam a afetar o caminho
critico da operacao financeira ou exigem logica compensatoria em cada chamador.

### 3. AuditService consumindo eventos de dominio especificos de Ledger/Transfer
Rejeitada. Consumir eventos internos ou muito especificos dos bounded contexts
criaria acoplamento semantico e evolutivo. Mudancas em regras ou nomes internos
dos produtores poderiam quebrar auditoria, e o `AuditService` passaria a
conhecer detalhes que pertencem aos dominios de origem.

### 4. AuditService com contrato canonico assincrono via Kafka
Escolhida como estrategia futura. Preserva baixo acoplamento, permite retry,
DLQ, redrive e backpressure, e mantem a auditoria fora do caminho critico dos
fluxos financeiros.

### 5. Banco compartilhado sem servico de auditoria
Rejeitada. Um banco ou tabela compartilhada entre servicos reduziria componentes,
mas violaria fronteiras entre bounded contexts, espalharia regras de escrita e
autorizacao, e aumentaria risco de acoplamento por schema.

### 6. AuditService com banco fisico separado desde o inicio
Adiada. Banco fisico separado pode fazer sentido quando houver requisito real de
isolamento, throughput, backup, retencao, residencia, acesso operacional ou
compliance. Para a POC atual, o schema `audit` no PostgreSQL compartilhado
continua suficiente ate que esses requisitos aparecam.

## Consequencias positivas
- Fluxos financeiros continuam independentes da disponibilidade do
  `AuditService`.
- A intencao de auditoria fica duravel junto com a operacao de origem.
- Kafka permite desacoplar ritmo de escrita financeira e persistencia de
  auditoria.
- O contrato canonico evita dependencia direta do `AuditService` em tipos dos
  bounded contexts financeiros.
- Retry, DLQ, redrive e replay podem ser tratados com os padroes assincronos ja
  usados no repositorio.
- A estrategia permite evoluir incrementalmente, conectando um fluxo por vez.

## Consequencias negativas
- A auditoria deixa de ser imediatamente consistente com a operacao de origem; a
  consistencia passa a ser eventual.
- Sera necessario criar e operar topico Kafka, publicadores, worker, DLQ,
  metricas, logs e runbooks quando a integracao for implementada.
- O contrato canonico precisara de governanca de versionamento e compatibilidade.
- Consultas de auditoria poderao nao refletir eventos ainda pendentes na Outbox
  ou no Kafka.
- Replays e redrives exigirao disciplina para preservar idempotencia e evitar
  registros duplicados.

## Criterios para implementar em etapa futura
A implementacao deve ser precedida por uma fatia pequena e uma revisao de
contrato que defina:

- primeiro fluxo produtor e seu caso de uso auditavel;
- nome do evento canonico, versao e JSON Schema;
- topico Kafka, headers obrigatorios e chave de particionamento;
- idempotency key e semantica de deduplicacao;
- politica de retry, DLQ, redrive e descarte;
- observabilidade minima, incluindo correlation id e metricas de backlog/falha;
- estrategia de rollout e compatibilidade;
- atualizacao de docs de eventos, arquitetura e runbooks.

## Documentacao relacionada
- [ADR-0003](./0003-integracao-assincrona-kafka-com-outbox.md)
- [ADR-0070](./0070-dlq-outbox-banco-backoff-requeue.md)
- [ADR-0088](./0088-kafka-default-ledger-balance-workers.md)
- [ADR-0097](./0097-functional-audit-service.md)
- [ADR-0098](./0098-audit-service-ingestao-futura.md)
- [Arquitetura do AuditService](../architecture/audit-service.md)
- [Spec de integracao assincrona futura do AuditService](../specs/audit/audit-async-integration-strategy.md)
