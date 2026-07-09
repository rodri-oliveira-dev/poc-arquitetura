# PaymentService API

O `PaymentService.Api` expoe a fatia de criacao de pagamentos externos do
bounded context de pagamentos. Nesta etapa, a API cria `Payment` localmente no
schema `payment`, chama a porta `IPaymentGateway` para criar uma intencao
externa no provider configurado (`Fake` ou `Stripe`) e persiste a referencia do
provider. Webhook, Inbox, Ledger, Balance, refund e Kafka continuam fora do
escopo.

## Autenticacao e autorizacao

- Audience esperada: `payment-api`.
- Claim de escopo: `scope`.
- Claim de merchant: `merchant_id`.

| Endpoint | Scope | Regra de merchant |
| --- | --- | --- |
| `POST /api/v1/payments` | `payment.write` | `merchantId` do body deve estar em `merchant_id`. |
| `GET /api/v1/payments/{paymentId}` | `payment.read` | merchant do Payment persistido deve estar em `merchant_id`. |

## `POST /api/v1/payments`

Cria um pagamento interno e solicita a criacao da intencao externa no provider
configurado. A resposta sincrona confirma apenas a criacao da intencao externa;
ela nao confirma efeito financeiro final nem contabilizacao no Ledger.

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

Exemplo de resposta:

```json
{
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "status": "RequiresAction",
  "merchantId": "m1",
  "amount": 100.00,
  "currency": "BRL",
  "provider": "Stripe",
  "providerPaymentId": "pi_...",
  "providerStatus": "requires_payment_method",
  "clientSecret": "pi_..._secret_...",
  "externalReference": "pedido-123",
  "statusUrl": "/api/v1/payments/00000000-0000-0000-0000-000000000000"
}
```

O MVP aceita somente `BRL`. A idempotencia e interna ao endpoint: mesma chave e
mesmo payload retorna replay equivalente; mesma chave e payload diferente retorna
`409 Conflict`.

A idempotencia externa usada na Stripe e deterministica por Payment:

```text
payment:{paymentId:N}:stripe:create-payment-intent
```

Em retry ou timeout de resultado desconhecido, o mesmo `paymentId` produz a
mesma chave externa. `clientSecret` pode ser retornado no `POST`, mas nao e
persistido na resposta idempotente nem aparece no `GET`.

## `GET /api/v1/payments/{paymentId}`

Retorna o estado interno persistido do Payment.

O `GET` e local: ele nao chama Stripe e nao retorna `clientSecret`.

## Provider fake

O provider fake e o default local seguro:

```json
{
  "PaymentGateway": {
    "Provider": "Fake",
    "Fake": {
      "Scenario": "Success"
    }
  }
}
```

Cenarios suportados: `Success`, `RequiresAction`, `Processing`,
`DefinitiveFailure`, `Timeout`, `RateLimit` e `TransientFailure`.

## Provider Stripe

Para usar Stripe Sandbox, selecione o provider e injete a API key fora do
repositorio:

```powershell
dotnet user-secrets set "PaymentGateway:Provider" "Stripe" --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
dotnet user-secrets set "PaymentGateway:Stripe:ApiKey" "<STRIPE_SECRET_KEY>" --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
```

Ou por variaveis de ambiente:

```powershell
$env:PaymentGateway__Provider = "Stripe"
$env:PaymentGateway__Stripe__ApiKey = "<STRIPE_SECRET_KEY>"
```

`PaymentGateway:Stripe:ApiBaseUrl` existe apenas para override controlado em
testes/smoke. A suite automatizada nao depende da Stripe real.

## Limitacoes atuais

- Nao ha webhook Stripe.
- Nao ha validacao de assinatura nem raw body handling.
- Nao ha Inbox Pattern.
- Nao ha Worker funcional de Payment.
- Nao ha integracao com Ledger ou Balance.
- Nao ha refund.
- Nao ha Kafka no PaymentService.

Contrato versionado: [`docs/openapi/payment.v1.json`](../openapi/payment.v1.json).
