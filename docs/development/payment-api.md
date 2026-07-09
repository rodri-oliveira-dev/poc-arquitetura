# PaymentService API

O `PaymentService.Api` expoe a fatia inicial do bounded context de pagamentos
externos. Nesta etapa, a API cria e consulta `Payment` localmente no schema
`payment`, sem chamar Stripe, sem webhook, sem Inbox e sem integrar com Ledger.

## Autenticacao e autorizacao

- Audience esperada: `payment-api`.
- Claim de escopo: `scope`.
- Claim de merchant: `merchant_id`.

| Endpoint | Scope | Regra de merchant |
| --- | --- | --- |
| `POST /api/v1/payments` | `payment.write` | `merchantId` do body deve estar em `merchant_id`. |
| `GET /api/v1/payments/{paymentId}` | `payment.read` | merchant do Payment persistido deve estar em `merchant_id`. |

## `POST /api/v1/payments`

Registra um pagamento interno com estado inicial `Pending`.

Headers:

- `Authorization: Bearer <token>`;
- `Idempotency-Key`: obrigatorio, UUID;
- `X-Correlation-Id`: opcional.

Request:

```json
{
  "merchantId": "m1",
  "amount": 100.00,
  "currency": "BRL",
  "description": "Pagamento de pedido",
  "externalReference": "pedido-123"
}
```

Resposta de sucesso:

- `202 Accepted`;
- `Location: /api/v1/payments/{paymentId}`.

O MVP aceita somente `BRL`. A idempotencia e interna ao endpoint: mesma chave e
mesmo payload retorna replay equivalente; mesma chave e payload diferente retorna
`409 Conflict`.

## `GET /api/v1/payments/{paymentId}`

Retorna o estado interno persistido do Payment.

Nesta etapa, o endpoint de criacao retorna `Pending`. As demais transicoes da
state machine existem no dominio para as etapas futuras de provider, Inbox e
Ledger.

Contrato versionado: [`docs/openapi/payment.v1.json`](../openapi/payment.v1.json).
