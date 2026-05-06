# LedgerService API

Este documento resume os contratos HTTP de escrita do `LedgerService.Api`.

## Criar lancamento

`POST /api/v1/lancamentos`

Cria um lancamento `CREDIT` ou `DEBIT`, persiste o lancamento e grava um evento `LedgerEntryCreated.v1` no Outbox na mesma transacao.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

## Solicitar estorno de lancamento

`POST /api/v1/lancamentos/{lancamentoId}/estornos`

Solicita o estorno de um lancamento existente. O endpoint apenas registra a intencao de estorno com status inicial `Pending`, persiste a solicitacao e grava o evento `LancamentoEstornoSolicitado.v1` no Outbox. O processamento financeiro efetivo nao acontece no request HTTP; ele fica para o fluxo assincrono de worker/background publishing ja usado pelo Ledger.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID. Repetir a mesma chave com o mesmo payload retorna o mesmo resultado; repetir com payload diferente retorna `409 Conflict`;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

Request body:

```json
{
  "motivo": "Erro operacional no lancamento original"
}
```

Resposta de sucesso:

```http
HTTP/1.1 202 Accepted
Location: /api/v1/lancamentos/estornos/00000000-0000-0000-0000-000000000000
```

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Pending",
  "statusUrl": "/api/v1/lancamentos/estornos/00000000-0000-0000-0000-000000000000"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `202 Accepted` | Solicitacao aceita e persistida como `Pending`. |
| `400 Bad Request` | Payload ou headers invalidos. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant do lancamento original. |
| `404 Not Found` | Lancamento original inexistente. |
| `409 Conflict` | Chave de idempotencia reutilizada com payload diferente ou solicitacao ativa duplicada. |
| `413 Payload Too Large` | Body acima de `ApiLimits:MaxRequestBodySizeBytes`. |
| `429 Too Many Requests` | Rate limit excedido. |

## Consultar status de estorno

`GET /api/v1/lancamentos/estornos/{estornoId}`

Consulta o estado atual de uma solicitacao de estorno criada por `POST /api/v1/lancamentos/{lancamentoId}/estornos`. A consulta usa Mediator na camada Application e retorna somente campos publicos do contrato HTTP, sem expor entidade de dominio, tabela ou detalhes de persistencia.

Parametros de rota:

| Parametro | Descricao |
| --- | --- |
| `estornoId` | Identificador da solicitacao de estorno retornado no `202 Accepted` do endpoint de criacao. Deve ser um UUID valido. |

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.read`.

Exemplo de request:

```http
GET /api/v1/lancamentos/estornos/00000000-0000-0000-0000-000000000000
Authorization: Bearer <token>
```

Resposta `Pending`:

```http
HTTP/1.1 200 OK
```

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Pending",
  "motivo": "Erro operacional no lancamento original",
  "solicitadoEm": "2026-05-06T10:30:00"
}
```

Resposta `Completed`:

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Completed",
  "motivo": "Erro operacional no lancamento original",
  "solicitadoEm": "2026-05-06T10:30:00"
}
```

Resposta `Failed`:

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Failed",
  "motivo": "Erro operacional no lancamento original",
  "solicitadoEm": "2026-05-06T10:30:00"
}
```

Estados possiveis:

| Status | Significado |
| --- | --- |
| `Pending` | Solicitacao registrada e ainda nao processada. |
| `Processing` | Processamento iniciado por fluxo assincrono futuro. |
| `Completed` | Estorno concluido por fluxo assincrono futuro. |
| `Failed` | Processamento falhou por fluxo assincrono futuro. |
| `Rejected` | Solicitacao rejeitada por regra de processamento futuro. |

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `200 OK` | Solicitacao encontrada e autorizada para o merchant do token. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant do estorno. |
| `404 Not Found` | Solicitacao inexistente ou rota com `estornoId` fora do formato UUID. |
| `429 Too Many Requests` | Rate limit excedido. |

Como o processamento financeiro e assincrono e ainda nao ha consumidor de estorno implementado, a API registra a intencao e inicialmente retorna `Pending`. Estados posteriores refletem a evolucao futura do processamento e devem ser interpretados com consistencia eventual: uma consulta logo apos o `POST` pode retornar `Pending` ate que um worker processe a solicitacao.

## Outbox e processamento assincrono

A solicitacao de estorno grava uma linha em `estornos_lancamentos` e outra em `outbox_messages` na mesma transacao. O evento usa:

- `EventType`: `LancamentoEstornoSolicitado.v1`;
- topico Kafka mapeado: `ledger.lancamento.estorno.solicitado`;
- `AggregateType`: `LancamentoEstorno`;
- `AggregateId`: identificador da solicitacao de estorno.

Nesta etapa nao existe consumidor de estorno implementado. O contrato fica registrado para evolucao do processamento assincrono sem acoplar a API ao Kafka diretamente.
