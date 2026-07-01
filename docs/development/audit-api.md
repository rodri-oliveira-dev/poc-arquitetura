# AuditService API

Este documento descreve o contrato HTTP atual do `AuditService`, bounded
context de auditoria funcional agnostico ao servico chamador. O contrato
versionado fica em [`docs/openapi/audit.v1.json`](../openapi/audit.v1.json).

O `AuditService` registra trilhas funcionais canonicas por operacao, com
idempotencia, correlacao, filtros seguros e autorizacao por scope e
`merchant_id`. Nesta etapa, ele nao esta integrado a `LedgerService`,
`BalanceService` ou `TransferService`, nao possui worker e nao consome Kafka.

## Headers

| Header | Obrigatorio | Uso |
| --- | --- | --- |
| `Authorization` | sim | Token JWT Bearer validado por issuer, audience e JWKS configurados. |
| `Content-Type` | sim para `POST` | `application/json`. |
| `Idempotency-Key` | sim para `POST` | Chave UUID para retry seguro de criacao de registro. |
| `X-Correlation-Id` | nao | Correlation id UUID. Se ausente, a API usa `correlationId` do body ou o valor gerado pelo middleware. |

## Scopes

| Scope | Uso |
| --- | --- |
| `audit.write` | Permite criar registros em `POST /api/v1/audit-records`. |
| `audit.read` | Permite consultar registros restritos aos merchants autorizados no token. |
| `audit.admin` | Permite consultar registros sem restricao de merchant ou filtrar por diferentes merchants. |

Consultas aceitam `audit.read` ou `audit.admin`. Tokens com `audit.read`
precisam ter claim `merchant_id` compativel com o registro ou filtro solicitado.

## POST /api/v1/audit-records

Cria um registro canonico de auditoria funcional. O endpoint exige token Bearer
com scope `audit.write` e header `Idempotency-Key` em formato UUID.

Exemplo:

```bash
curl -i -X POST "http://localhost:5235/api/v1/audit-records" \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" \
  -H "X-Correlation-Id: 22222222-2222-2222-2222-222222222222" \
  -d '{
    "operationId": "33333333-3333-3333-3333-333333333333",
    "correlationId": "22222222-2222-2222-2222-222222222222",
    "sourceService": "AnyCaller",
    "operationType": "PaymentCaptured",
    "entityType": "Payment",
    "entityId": "pay_123",
    "merchantId": "mrc_123",
    "actor": {
      "type": "Client",
      "subject": null,
      "clientId": "any-caller-api"
    },
    "status": "Succeeded",
    "reason": "Payment capture confirmed by caller.",
    "metadata": {
      "channel": "api",
      "riskLevel": "low"
    },
    "occurredAt": "2026-07-01T10:15:30Z"
  }'
```

Resposta de sucesso:

```http
HTTP/1.1 201 Created
Location: /api/v1/audit-records/44444444-4444-4444-4444-444444444444
Content-Type: application/json
```

```json
{
  "id": "44444444-4444-4444-4444-444444444444"
}
```

Campos principais:

| Campo | Regra |
| --- | --- |
| `operationId` | UUID obrigatorio que agrupa a trilha funcional de uma operacao. |
| `correlationId` | UUID opcional para correlacao tecnica/funcional. |
| `sourceService` | String obrigatoria, ate 100 caracteres, validada por tamanho e nao por enum fechado. |
| `operationType` | String obrigatoria, ate 150 caracteres, validada por tamanho e nao por enum fechado. |
| `entityType` | String opcional, ate 150 caracteres. |
| `entityId` | String opcional, ate 150 caracteres. |
| `merchantId` | String opcional, ate 100 caracteres. |
| `actor.type` | Opcional. Quando informado, aceita `User`, `Client` ou `System`. |
| `status` | Obrigatorio. Aceita `Received`, `Succeeded`, `Failed`, `Rejected` ou `Replayed`. |
| `reason` | Opcional, ate 1000 caracteres. |
| `metadata` | Objeto JSON simples de pares string/string, ate 4096 bytes serializados. |
| `occurredAt` | Data/hora obrigatoria do acontecimento auditado. |

Se o token trouxer `sub` ou `client_id`, o actor persistido e derivado dessas
claims e tem precedencia sobre `actor` enviado no body. Isso evita confiar
cegamente na identidade declarada pelo chamador.

## GET /api/v1/audit-records/{id}

Consulta um registro por id. Exige `audit.read` ou `audit.admin`.

```bash
curl -i "http://localhost:5235/api/v1/audit-records/44444444-4444-4444-4444-444444444444" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

Resposta:

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "operationId": "33333333-3333-3333-3333-333333333333",
  "correlationId": "22222222-2222-2222-2222-222222222222",
  "sourceService": "AnyCaller",
  "operationType": "PaymentCaptured",
  "entityType": "Payment",
  "entityId": "pay_123",
  "merchantId": "mrc_123",
  "actor": {
    "type": "Client",
    "subject": null,
    "clientId": "any-caller-api"
  },
  "status": "Succeeded",
  "reason": "Payment capture confirmed by caller.",
  "metadata": {
    "channel": "api",
    "riskLevel": "low"
  },
  "occurredAt": "2026-07-01T10:15:30+00:00",
  "createdAt": "2026-07-01T10:15:31+00:00"
}
```

Com `audit.read`, o usuario so acessa registros cujo `merchantId` esteja nas
claims `merchant_id` do token. `audit.admin` pode acessar qualquer merchant.

## GET /api/v1/audit-records/operations/{operationId}

Consulta a trilha funcional de uma operacao. A resposta e uma lista
possivelmente vazia, ordenada por `occurredAt` ascendente.

```bash
curl -i "http://localhost:5235/api/v1/audit-records/operations/33333333-3333-3333-3333-333333333333" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

Resposta:

```json
[
  {
    "id": "44444444-4444-4444-4444-444444444444",
    "operationId": "33333333-3333-3333-3333-333333333333",
    "correlationId": "22222222-2222-2222-2222-222222222222",
    "sourceService": "AnyCaller",
    "operationType": "PaymentCaptured",
    "entityType": "Payment",
    "entityId": "pay_123",
    "merchantId": "mrc_123",
    "actor": {
      "type": "Client",
      "subject": null,
      "clientId": "any-caller-api"
    },
    "status": "Succeeded",
    "reason": "Payment capture confirmed by caller.",
    "metadata": {
      "channel": "api",
      "riskLevel": "low"
    },
    "occurredAt": "2026-07-01T10:15:30+00:00",
    "createdAt": "2026-07-01T10:15:31+00:00"
  }
]
```

Com `audit.read`, registros de merchants nao autorizados sao omitidos da lista.
Com `audit.admin`, a lista nao e filtrada por merchant.

## GET /api/v1/audit-records

Pesquisa registros por filtros e paginacao segura. Exige `from` e `to`; o
intervalo maximo e de 31 dias.

Filtros aceitos:

| Query string | Regra |
| --- | --- |
| `merchantId` | Opcional. Para `audit.read`, precisa estar autorizado no token. |
| `sourceService` | Opcional, ate 100 caracteres. |
| `operationType` | Opcional, ate 150 caracteres. |
| `status` | Opcional, ate 50 caracteres. |
| `entityType` | Opcional, ate 150 caracteres. |
| `entityId` | Opcional, ate 150 caracteres. |
| `from` | Obrigatorio. Inicio do intervalo. |
| `to` | Obrigatorio. Fim do intervalo, maior ou igual a `from`. |
| `page` | Opcional, inicia em `1`, default `1`. |
| `pageSize` | Opcional, de `1` a `100`, default `50`. |

Exemplo:

```bash
curl -i "http://localhost:5235/api/v1/audit-records?merchantId=mrc_123&sourceService=AnyCaller&from=2026-07-01T00:00:00Z&to=2026-07-01T23:59:59Z&page=1&pageSize=50" \
  -H "Authorization: Bearer <ACCESS_TOKEN>"
```

Resposta:

```json
{
  "items": [
    {
      "id": "44444444-4444-4444-4444-444444444444",
      "operationId": "33333333-3333-3333-3333-333333333333",
      "correlationId": "22222222-2222-2222-2222-222222222222",
      "sourceService": "AnyCaller",
      "operationType": "PaymentCaptured",
      "entityType": "Payment",
      "entityId": "pay_123",
      "merchantId": "mrc_123",
      "actor": {
        "type": "Client",
        "subject": null,
        "clientId": "any-caller-api"
      },
      "status": "Succeeded",
      "reason": "Payment capture confirmed by caller.",
      "metadata": {
        "channel": "api",
        "riskLevel": "low"
      },
      "occurredAt": "2026-07-01T10:15:30+00:00",
      "createdAt": "2026-07-01T10:15:31+00:00"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalItems": 1,
  "totalPages": 1
}
```

A ordenacao padrao da pesquisa e `occurredAt` descendente.

## Idempotencia

`POST /api/v1/audit-records` exige `Idempotency-Key` em formato UUID.

- primeira chamada valida cria o registro e retorna `201 Created`;
- retry com a mesma chave e o mesmo payload logico retorna `201 Created` com o
  mesmo identificador;
- retry com a mesma chave e payload logico diferente retorna `409 Conflict`;
- a chave e unica no schema `audit` do PostgreSQL.

O payload logico usado na comparacao inclui identificadores, source, operation,
entidade, merchant, actor, status, reason, metadata canonica e `occurredAt`.

## Status codes

| Status | Quando ocorre |
| --- | --- |
| `200 OK` | Consultas concluidas com sucesso. |
| `201 Created` | Registro criado ou replay idempotente de criacao ja concluida. |
| `400 Bad Request` | Payload, header, rota ou filtro invalido. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Token sem scope exigido ou sem acesso ao merchant solicitado. |
| `404 Not Found` | Registro por id nao encontrado. |
| `409 Conflict` | `Idempotency-Key` reutilizada com payload diferente. |
| `422 Unprocessable Entity` | Violacao de regra de dominio. |
| `429 Too Many Requests` | Limite de requisicoes excedido quando a politica estiver ativa. |
| `500 Internal Server Error` | Erro inesperado. |

## Limitacoes atuais

- O `AuditService` e agnostico ao servico chamador; `sourceService` e
  `operationType` identificam a origem e a operacao sem acoplar o contrato a um
  bounded context especifico.
- Nao ha integracao inicial com `LedgerService`, `BalanceService` ou
  `TransferService`.
- Nao ha worker, consumo Kafka, Outbox de auditoria ou DLQ de auditoria nesta
  etapa.
- `metadata` deve conter apenas atributos funcionais pequenos e nao deve
  carregar payload bruto, dados sensiveis ou segredos.
