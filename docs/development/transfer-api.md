# TransferService API

Este documento resume os contratos HTTP iniciais do `TransferService.Api`.

## Solicitar transferencia

`POST /api/v1/transferencias`

Registra uma saga de transferencia entre merchants com status inicial `Pending` e grava o evento `TransferenciaSolicitada.v1` no Outbox na mesma transacao. O processamento financeiro ocorre de forma assincrona por worker.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `transfer.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID. Repetir a mesma chave com o mesmo payload retorna a mesma resposta; repetir com payload diferente retorna `409 Conflict`;
- `X-Correlation-Id`: opcional. Se ausente, a API gera e devolve no response.

Request body:

```json
{
  "sourceMerchantId": "m1",
  "destinationMerchantId": "m2",
  "amount": 100.00,
  "description": "Transferencia entre carteiras",
  "externalReference": "pedido-123"
}
```

Regras de validacao:

| Campo | Regra |
| --- | --- |
| `sourceMerchantId` | Obrigatorio, ate 100 caracteres e autorizado na claim `merchant_id`. |
| `destinationMerchantId` | Obrigatorio, ate 100 caracteres e diferente do merchant de origem. |
| `amount` | Obrigatorio, maior que zero, ate 18 digitos e 2 casas decimais. |
| `description` | Opcional, ate 500 caracteres. |
| `externalReference` | Opcional, ate 200 caracteres. |
| `Idempotency-Key` | Obrigatorio e UUID valido. |

Resposta de sucesso:

```http
HTTP/1.1 202 Accepted
Location: /api/v1/transferencias/00000000-0000-0000-0000-000000000000
```

```json
{
  "transferenciaId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "sourceMerchantId": "m1",
  "destinationMerchantId": "m2",
  "amount": 100.00,
  "createdAt": "2026-06-17T10:30:00Z",
  "statusUrl": "/api/v1/transferencias/00000000-0000-0000-0000-000000000000"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `202 Accepted` | Solicitacao aceita e persistida como `Pending`, incluindo replay idempotente com mesma chave e mesmo payload. |
| `400 Bad Request` | Payload, JSON ou headers invalidos. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant de origem. |
| `409 Conflict` | Chave de idempotencia reutilizada com payload diferente. |
| `413 Payload Too Large` | Body acima de `ApiLimits:MaxRequestBodySizeBytes`. |
| `422 Unprocessable Entity` | Violacao de regra de dominio. |
| `429 Too Many Requests` | Rate limit excedido. |

## Consultar status de transferencia

`GET /api/v1/transferencias/{transferenciaId}`

Consulta o estado atual da saga registrada pelo POST. A consulta usa Mediator na camada Application e retorna contrato HTTP proprio da API, sem expor entidade de dominio ou persistencia.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `transfer.read`.

Resposta:

```json
{
  "transferenciaId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "sourceMerchantId": "m1",
  "destinationMerchantId": "m2",
  "amount": 100.00,
  "createdAt": "2026-06-17T10:30:00Z",
  "updatedAt": "2026-06-17T10:30:00Z"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `200 OK` | Saga encontrada e token autorizado para o merchant de origem ou destino. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para os merchants da transferencia. |
| `404 Not Found` | Saga inexistente ou rota com `transferenciaId` fora do formato UUID. |
| `429 Too Many Requests` | Rate limit excedido. |

Estados modelados inicialmente:

| Status | Significado |
| --- | --- |
| `Pending` | Saga registrada, ainda nao processada. |
| `Processing` | Worker iniciou o processamento. |
| `DebitCreating` | Criacao do debito em andamento. |
| `DebitCreated` | Debito criado. |
| `CreditCreating` | Criacao do credito em andamento. |
| `Completed` | Transferencia concluida. |
| `CompensationRequested` | Compensacao solicitada apos falha posterior ao debito. |
| `Compensated` | Transferencia compensada. |
| `Failed` | Falha tecnica ou inesperada. |
| `Rejected` | Transferencia rejeitada antes de debito efetivo. |
