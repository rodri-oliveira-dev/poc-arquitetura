# Spec SDD: AuditRecordRequested.v1

## Objetivo

Definir o contrato canonico `AuditRecordRequested.v1` para uma integracao
assincrona futura em que servicos de origem solicitem ao `AuditService` o
registro de auditoria funcional.

Esta spec cria contrato, schema e exemplos. Ela nao implementa publicacao,
consumo, worker, topico, DLQ ou alteracao em servicos de origem.

## Semantica

`AuditRecordRequested.v1` representa uma solicitacao de registro de auditoria
funcional. O evento indica que um produtor quer que o `AuditService` registre a
trilha funcional de uma operacao. Ele nao representa que o registro ja foi
persistido no schema `audit`.

O contrato e canonico: produtores devem traduzir sua linguagem interna para os
campos funcionais do evento antes de publicar. O `AuditService` nao deve
conhecer aggregates, commands, handlers, entidades EF, topicos internos ou
payloads crus dos bounded contexts produtores.

## Classificacao da mudanca

A recomendacao foi classificada como necessaria para o requisito explicito de
formalizar o contrato futuro. A implementacao fica limitada a documentacao,
JSON Schema, exemplos e indices, porque ainda nao ha primeiro fluxo produtor
nem criterio operacional para criar worker, topico ou consumidores.

## Arquivos canonicos

- Schema: [`../../../contracts/events/audit-record-requested.v1.schema.json`](../../../contracts/events/audit-record-requested.v1.schema.json)
- Exemplo valido: [`../../../contracts/events/examples/audit-record-requested.v1.valid.json`](../../../contracts/events/examples/audit-record-requested.v1.valid.json)
- Exemplo invalido: [`../../../contracts/events/examples/audit-record-requested.v1.invalid.json`](../../../contracts/events/examples/audit-record-requested.v1.invalid.json)
- Documento do evento: [`../../events/audit-record-requested-v1.md`](../../events/audit-record-requested-v1.md)

## Campos obrigatorios

| Campo | Regra |
| --- | --- |
| `eventId` | UUID obrigatorio da publicacao logica. |
| `eventType` | Constante `AuditRecordRequested.v1`. |
| `schemaVersion` | Constante numerica `1`. |
| `occurredAt` | Date-time obrigatorio. |
| `sourceService` | String obrigatoria, ate 100 caracteres, sem enum fechado. |
| `operationId` | UUID obrigatorio da operacao auditavel. |
| `operationType` | String obrigatoria, ate 150 caracteres, sem enum fechado. |
| `status` | Um dos status funcionais suportados. |

## Campos opcionais

| Campo | Regra |
| --- | --- |
| `correlationId` | UUID quando informado. |
| `idempotencyKey` | UUID quando informado; recomendado para produtores reais. |
| `entityType` | String ate 150 caracteres ou null. |
| `entityId` | String ate 150 caracteres ou null. |
| `merchantId` | String ate 100 caracteres ou null. |
| `actor` | Object ou null, com `type` em `User`, `Client` ou `System`. |
| `reason` | String ate 1000 caracteres ou null. |
| `metadata` | Object ou null, pares string/string, maximo operacional de 4096 bytes serializados. |

## Metadata e dados sensiveis

`metadata` existe para pequenos atributos funcionais que ajudem a interpretar a
trilha. Ela nao deve carregar payload bruto de request/response, PAN, senha,
token, segredo, documento completo ou qualquer dado sensivel sem decisao
explicita.

O schema limita a estrutura a pares string/string e ate 50 propriedades. O
limite operacional de 4096 bytes serializados deve ser validado pelo consumidor
quando o `AuditService.Worker` for implementado.

## Compatibilidade

Mudancas compativeis:

- documentacao mais precisa;
- exemplos adicionais;
- novos campos opcionais ignoraveis por consumidores antigos;
- novas recomendacoes de nomes para `sourceService` ou `operationType`, sem
  transformar esses campos em enum.

Mudancas incompativeis:

- remover, renomear ou mudar tipo de campo;
- mudar semantica de solicitacao para fato persistido;
- adicionar campo obrigatorio;
- alterar regra de idempotencia;
- exigir que o `AuditService` conheca tipos internos dos produtores;
- permitir payload bruto sensivel.

## Exemplo valido

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

## Validacao esperada

A validacao de contrato deve usar o validador existente:

```bash
npm run events:validate
```

Nao ha validacao .NET obrigatoria nesta etapa, porque nenhum codigo em
`src/audit` ou `tests/audit` foi alterado.

## Fora de escopo confirmado

- Criar producer em `LedgerService`, `BalanceService` ou `TransferService`.
- Criar consumer no `AuditService`.
- Criar `AuditService.Worker`.
- Criar topico Kafka, DLQ ou redrive.
- Alterar contratos HTTP ou OpenAPI.
- Executar build/test da solution inteira.

## Proximos passos para worker futuro

1. Escolher o primeiro fluxo produtor e seu caso de uso auditavel.
2. Criar ADR/fatia tecnica com topico Kafka, message key, headers obrigatorios,
   retry, DLQ, redrive, observabilidade e rollout.
3. Implementar gravacao na Outbox transacional local do servico produtor.
4. Implementar publicador Kafka do produtor, se ainda nao existir no fluxo.
5. Criar `AuditService.Worker` para consumir `AuditRecordRequested.v1`, validar
   schema, aplicar idempotencia e delegar ao caso de uso existente de criacao.
