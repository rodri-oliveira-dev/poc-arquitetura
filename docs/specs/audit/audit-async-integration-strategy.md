# Estrategia futura de integracao assincrona do AuditService

## Objetivo
Orientar a implementacao futura da integracao automatica do `AuditService` com
`LedgerService`, `BalanceService` e `TransferService`, preservando baixo
acoplamento e mantendo a auditoria fora do caminho critico financeiro.

Esta spec orienta a integracao entre bounded contexts. O consumer do
`AuditService.Worker` ja existe, mas nao ha producer real nos servicos de
origem.

## Fluxo alvo

```text
Servico de origem
  -> operacao de negocio
  -> Outbox transacional local
  -> Worker/publicador do servico de origem
  -> Kafka
  -> AuditService.Worker
  -> schema audit
```

## Regras de desenho
- O servico de origem grava a intencao de auditoria na mesma transacao da
  operacao de negocio.
- A publicacao para Kafka acontece fora da request ou do handler principal,
  usando Outbox do proprio servico de origem.
- O evento publicado deve ser canonico para auditoria, nao um evento de dominio
  interno cru.
- O `AuditService.Worker` consome o evento canonico, valida o payload, aplica
  idempotencia e delega ao caso de uso existente de criacao de registro.
- Falha do `AuditService`, do worker ou do Kafka nao deve reverter a operacao
  financeira ja confirmada.

## Contrato futuro esperado
O contrato canonico futuro foi formalizado como
[`AuditRecordRequested.v1`](audit-record-requested-v1.md), com JSON Schema e
exemplos versionados em [`contracts/events`](../../../contracts/events/README.md).

Esse contrato inclui, no minimo:

- `eventId`, `eventType` e `schemaVersion`;
- correlation id quando disponivel;
- source service;
- operation type;
- operation id;
- entity type e entity id;
- merchant id quando aplicavel;
- actor;
- status funcional;
- reason funcional;
- metadata minimizada, sem segredos;
- occurred at.

O contrato e o consumer do AuditService estao implementados, mas ainda nao ha
produtor, DLQ ou integracao ativa com Ledger, Balance ou Transfer.

## Operacao
- Entrega deve ser tratada como at-least-once.
- Idempotencia deve impedir duplicidade em retry, replay e redrive.
- Falhas transientes usam retry com backoff.
- Falhas permanentes de contrato ou payload seguem para DLQ.
- Redrive deve validar schema e preservar correlation id, causation id e
  idempotency key.

## Fora de escopo desta spec
- Criar producer Kafka nos servicos de origem.
- Alterar Ledger, Balance ou Transfer.
- Alterar contratos HTTP existentes.
- Criar DLQ ou redrive de auditoria.

## Referencias
- [ADR-0099](../../adrs/0099-audit-async-integration-strategy.md)
- [Arquitetura do AuditService](../../architecture/audit-service.md)
