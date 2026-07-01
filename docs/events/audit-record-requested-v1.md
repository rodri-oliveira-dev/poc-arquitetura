# AuditRecordRequested.v1

## Identificacao

| Item | Valor |
| --- | --- |
| Nome do evento | `AuditRecordRequested` |
| Versao | `v1` |
| `event_type` | `AuditRecordRequested.v1` |
| Produtor | Nenhum produtor atual |
| Consumidores | Nenhum consumidor atual |
| Natureza | Solicitacao canonica futura para auditoria funcional |
| JSON Schema versionado | [`../../contracts/events/audit-record-requested.v1.schema.json`](../../contracts/events/audit-record-requested.v1.schema.json) |
| Exemplos versionados | [`valido`](../../contracts/events/examples/audit-record-requested.v1.valid.json), [`invalido`](../../contracts/events/examples/audit-record-requested.v1.invalid.json) |

Este contrato representa uma solicitacao para que o `AuditService` registre uma
auditoria funcional. Ele nao afirma que o registro ja foi persistido.

O contrato e canonico e agnostico ao chamador. Ele deve descrever a operacao
auditavel em linguagem funcional estavel, sem vazar aggregates, commands,
entidades EF, payloads HTTP crus ou detalhes internos de `LedgerService`,
`BalanceService`, `TransferService` ou outros bounded contexts.

## Estado atual

Este evento foi documentado e versionado, mas nao esta ativo em runtime:

- nenhum servico publica `AuditRecordRequested.v1`;
- nenhum worker consome `AuditRecordRequested.v1`;
- nenhum topico Kafka foi criado por esta mudanca;
- nenhum producer, consumer ou DLQ foi implementado por esta mudanca.

A integracao futura deve seguir a estrategia de Outbox transacional local no
servico de origem e Kafka para o `AuditService.Worker`, conforme ADR-0099.

## Payload logico

| Campo | Obrigatorio | Tipo de dado | Semantica |
| --- | --- | --- | --- |
| `eventId` | Sim | string UUID | Identificador unico da publicacao logica do evento de auditoria. |
| `eventType` | Sim | string | Deve ser `AuditRecordRequested.v1`. |
| `schemaVersion` | Sim | integer | Deve ser `1`. |
| `occurredAt` | Sim | string date-time | Instante UTC em que a operacao auditavel ocorreu no servico de origem. |
| `sourceService` | Sim | string ate 100 caracteres | Servico que solicita auditoria. Nao e enum fechado. |
| `operationId` | Sim | string UUID | Identificador estavel da operacao funcional auditada. |
| `correlationId` | Nao | string UUID ou null | Correlacao logica do fluxo, quando disponivel. |
| `idempotencyKey` | Nao | string UUID ou null | Chave estavel para deduplicacao em retries, redrives e replays. |
| `operationType` | Sim | string ate 150 caracteres | Tipo funcional da operacao auditada. Nao e enum fechado. |
| `entityType` | Nao | string ate 150 caracteres ou null | Tipo funcional da entidade auditada, quando aplicavel. |
| `entityId` | Nao | string ate 150 caracteres ou null | Identificador funcional da entidade auditada, quando aplicavel. |
| `merchantId` | Nao | string ate 100 caracteres ou null | Merchant associado a operacao, quando aplicavel. |
| `actor` | Nao | object ou null | Identidade funcional declarada pelo produtor. |
| `status` | Sim | string | Status funcional da operacao auditada. |
| `reason` | Nao | string ate 1000 caracteres ou null | Motivo funcional resumido, quando aplicavel. |
| `metadata` | Nao | object ou null | Pares funcionais string/string minimizados, sem payload bruto sensivel. |

`metadata` deve caber em ate 4096 bytes quando serializado, alinhado ao limite
do `AuditService`. O schema restringe o objeto a no maximo 50 pares string/string
para manter a intencao de metadata pequena; o limite exato em bytes deve ser
validado pelo consumidor quando a integracao for implementada.

## Status e actor

Valores atuais de `status`:

- `Received`
- `Succeeded`
- `Failed`
- `Rejected`
- `Replayed`

Valores atuais de `actor.type`:

- `User`
- `Client`
- `System`

`sourceService` e `operationType` nao devem virar enums fechados no contrato,
pois o `AuditService` nao deve conhecer previamente todos os chamadores e
operacoes auditaveis.

## Exemplo de payload valido

```json
{
  "eventId": "00000000-0000-0000-0000-000000000001",
  "eventType": "AuditRecordRequested.v1",
  "schemaVersion": 1,
  "occurredAt": "2026-07-01T10:30:00Z",
  "sourceService": "LedgerService",
  "operationId": "00000000-0000-0000-0000-000000000002",
  "correlationId": "00000000-0000-0000-0000-000000000003",
  "idempotencyKey": "00000000-0000-0000-0000-000000000004",
  "operationType": "LancamentoCriado",
  "entityType": "Lancamento",
  "entityId": "lan_123",
  "merchantId": "m1",
  "actor": {
    "type": "Client",
    "subject": "poc-automation",
    "clientId": "poc-automation"
  },
  "status": "Succeeded",
  "reason": null,
  "metadata": {
    "amount": "100.00",
    "currency": "BRL"
  }
}
```

## Exemplo de payload invalido

```json
{
  "eventId": "00000000-0000-0000-0000-000000000001",
  "eventType": "AuditRecordRequested.v1",
  "schemaVersion": 2,
  "occurredAt": "2026-07-01T10:30:00Z",
  "sourceService": "LedgerService",
  "correlationId": "not-a-uuid",
  "operationType": "LancamentoCriado",
  "status": "Unknown"
}
```

Motivos: `schemaVersion` deve ser `1`, `operationId` esta ausente,
`correlationId` nao e UUID e `status` nao e suportado.

## Idempotencia

`idempotencyKey` e opcional no schema para permitir evolucao controlada, mas
produtores reais devem informa-la quando a integracao for implementada. A chave
deve ser estavel para a operacao auditavel e permitir que retries, redrives e
replays nao dupliquem registros funcionais.

O `eventId` identifica a publicacao logica; a deduplicacao funcional deve usar
`idempotencyKey` quando disponivel.

## Transporte futuro

O JSON Schema valida somente o payload logico. Headers Kafka, topic, partition,
offset, message key, consumer group, DLQ e metadados de redrive ficam fora deste
schema.

Quando a integracao for implementada, a ADR/fatia tecnica deve definir:

- topico Kafka;
- message key;
- headers obrigatorios;
- politica de retry e DLQ;
- validacao de schema no `AuditService.Worker`;
- observabilidade minima;
- estrategia de rollout e compatibilidade.

## Compatibilidade

Mudancas compativeis em `v1`:

- melhorar descricao, exemplos ou documentacao sem mudar semantica;
- adicionar campo opcional que consumidores antigos ignorem com seguranca;
- ampliar documentacao de valores recomendados para `sourceService` ou
  `operationType` sem fechar enum.

Mudancas que exigem nova versao ou rollout coordenado:

- remover, renomear ou tornar obrigatorio um campo opcional;
- mudar tipo ou semantica de campo;
- trocar a regra de idempotencia;
- incluir payload bruto sensivel;
- fechar `sourceService` ou `operationType` como enum central do AuditService;
- fazer o evento representar auditoria ja persistida em vez de solicitacao.
