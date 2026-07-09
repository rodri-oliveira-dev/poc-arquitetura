# PaymentService API

O `PaymentService.Api` expoe a fatia de criacao de pagamentos externos do
bounded context de pagamentos. A API cria `Payment` localmente no schema
`payment`, chama a porta `IPaymentGateway` para criar uma intencao externa no
provider configurado (`Fake` ou `Stripe`) e recebe webhooks Stripe assinados,
persistindo o evento validado em uma Inbox duravel antes de responder sucesso ao
provider.

## Autenticacao e autorizacao

- Audience esperada: `payment-api`.
- Claim de escopo: `scope`.
- Claim de merchant: `merchant_id`.

| Endpoint | Scope | Regra de merchant |
| --- | --- | --- |
| `POST /api/v1/payments` | `payment.write` | `merchantId` do body deve estar em `merchant_id`. |
| `GET /api/v1/payments/{paymentId}` | `payment.read` | merchant do Payment persistido deve estar em `merchant_id`. |
| `POST /api/v1/webhooks/stripe` | nao usa JWT | protegido por `Stripe-Signature` com o signing secret do endpoint. |

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

## `POST /api/v1/webhooks/stripe`

Recebe eventos Stripe e persiste a entrada na Inbox. Este endpoint e publico no
sentido de nao exigir JWT/OIDC, porque a Stripe nao envia token Keycloak; a
autenticidade vem da assinatura criptografica do header `Stripe-Signature`.

Headers:

- `Stripe-Signature`: obrigatorio;
- `Content-Type: application/json`;
- `X-Correlation-Id`: opcional; se ausente, a API gera um id proprio da
  entrega.

Fluxo implementado:

```text
Stripe
-> PaymentService.Api webhook
-> raw body + Stripe-Signature + signing secret
-> validacao HMAC/timestamp
-> extracao minima de id/type/data.object.id/metadata.payment_id
-> payment.inbox_messages
-> 200 OK
```

Eventos MVP persistidos como `Pending`:

- `payment_intent.processing`;
- `payment_intent.succeeded`;
- `payment_intent.payment_failed`;
- `payment_intent.canceled`.

Eventos conhecidos fora do MVP, como `charge.*`, `checkout.*`, `customer.*`,
`invoice.*`, `payment_method.*` e `setup_intent.*`, sao persistidos como
`Ignored`. Eventos desconhecidos tambem sao persistidos como `Ignored`, sem
gerar retry infinito na Stripe. Essa classificacao preserva rastreabilidade sem
marcar evento valido como `Processed` ou `DeadLetter` antes do Worker existir.

Respostas:

| Status | Quando |
| --- | --- |
| `200 OK` | Assinatura valida e evento inserido, ignorado conscientemente ou duplicado reconhecido. |
| `400 Bad Request` | Header ausente/malformado ou JSON invalido apos assinatura valida. |
| `401 Unauthorized` | Assinatura invalida, secret incorreto ou timestamp fora da tolerancia. |
| `413 Payload Too Large` | Body acima de `ApiLimits:MaxRequestBodySizeBytes`. |
| `429 Too Many Requests` | Rate limit compartilhado excedido. |
| `503 Service Unavailable` | Signing secret ausente ou persistencia indisponivel. |
| `500 Internal Server Error` | Falha inesperada. |

Duplicidade usa a constraint unica `(provider, provider_event_id)`. Reentregas
com o mesmo `event.id` retornam `200 OK` e nao criam segunda linha. Falha de
persistencia nao retorna sucesso, permitindo retry do provider.

O payload persistido e o texto bruto validado. Ele nao e logado integralmente;
a tabela guarda `payload_sha256` para diagnostico sem expor o corpo completo em
logs, metricas ou traces.

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
dotnet user-secrets set "PaymentGateway:Stripe:WebhookSigningSecret" "<STRIPE_WEBHOOK_SIGNING_SECRET>" --project ./src/payment/PaymentService.Api/PaymentService.Api.csproj
```

Ou por variaveis de ambiente:

```powershell
$env:PaymentGateway__Provider = "Stripe"
$env:PaymentGateway__Stripe__ApiKey = "<STRIPE_SECRET_KEY>"
$env:PaymentGateway__Stripe__WebhookSigningSecret = "<STRIPE_WEBHOOK_SIGNING_SECRET>"
```

`PaymentGateway:Stripe:ApiBaseUrl` existe apenas para override controlado em
testes/smoke. A suite automatizada nao depende da Stripe real.

`PaymentGateway:Stripe:WebhookSignatureTolerance` usa `00:05:00` por padrao.
Nao use tolerancia zero, pois isso removeria a protecao temporal contra replay.

### Stripe CLI local

O Stripe CLI e opcional para desenvolvimento manual. A porta HTTP direta atual
do `PaymentService.Api` e `5234`, conforme `launchSettings.json`:

```bash
stripe listen --forward-to localhost:5234/api/v1/webhooks/stripe
```

O comando mostra um signing secret temporario (`whsec_...`). Configure esse
valor via user-secrets ou variavel de ambiente; nao versione o segredo.

Exemplo para disparar evento de teste:

```bash
stripe trigger payment_intent.succeeded
```

## Limitacoes atuais

- Nao ha Worker funcional de Payment.
- Nao ha processamento assincrono da Inbox.
- Nao ha alteracao de state machine a partir do webhook.
- Nao ha integracao com Ledger ou Balance.
- Nao ha refund.
- Nao ha Kafka no PaymentService.

Contrato versionado: [`docs/openapi/payment.v1.json`](../openapi/payment.v1.json).
