# PaymentService API

O `PaymentService.Api` expoe a fatia de criacao de pagamentos externos do
bounded context de pagamentos. A API cria `Payment` localmente no schema
`payment`, chama a porta `IPaymentGateway` para criar uma intencao externa no
provider configurado (`Fake` ou `Stripe`) e recebe webhooks Stripe assinados,
persistindo o evento validado em uma Inbox duravel antes de responder sucesso ao
provider. Depois que o provider confirma sucesso, o `PaymentService.Worker`
materializa o efeito financeiro chamando o contrato publico do `LedgerService.Api`.

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
gerar retry infinito na Stripe. Essa classificacao preserva rastreabilidade e
impede que o Worker tente interpretar eventos fora do MVP.

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

`PaymentGateway:Stripe:ApiKey` recebe a API key de teste (`sk_test_...`) usada
para chamadas da API Stripe. `PaymentGateway:Stripe:WebhookSigningSecret`
recebe o signing secret do endpoint (`whsec_...`) usado exclusivamente para
validar webhooks. Nao misture os dois valores.

`PaymentGateway:Stripe:ApiBaseUrl` existe apenas para override controlado em
testes/smoke. A suite automatizada nao depende da Stripe real.

`PaymentGateway:Stripe:WebhookSignatureTolerance` usa `00:05:00` por padrao.
Nao use tolerancia zero, pois isso removeria a protecao temporal contra replay.

### Stripe CLI local

O Stripe CLI e opcional para desenvolvimento manual e nao faz parte do build,
do CI ou dos testes automatizados. A porta HTTP direta atual do
`PaymentService.Api` e `5234`, conforme `launchSettings.json`:

```bash
stripe listen --forward-to http://localhost:5234/api/v1/webhooks/stripe
```

O comando mostra um signing secret temporario (`whsec_...`). Configure esse
valor via user-secrets ou variavel de ambiente; nao versione o segredo.

Exemplo para disparar evento de teste:

```bash
stripe trigger payment_intent.succeeded
```

Eventos sinteticos validam entrega HTTP, assinatura, raw body, Inbox e
deduplicacao, mas nao provam associacao com Payment local nem integracao com
Ledger. Para o fluxo completo, crie um Payment real pelo `PaymentService`,
confirme o PaymentIntent correspondente no sandbox e receba o webhook
correlacionado.

O runbook completo fica em
[Validacao local de webhooks Stripe com Stripe CLI](stripe-cli-webhooks.md).

## Worker de Inbox

O processamento assincrono roda no `PaymentService.Worker`, nao no endpoint de
webhook. O webhook termina quando a assinatura foi validada e a Inbox foi
persistida; a state machine do Payment e aplicada posteriormente pelo Worker.

Configuracao versionada em `src/payment/PaymentService.Worker/appsettings.json`:

```json
{
  "PaymentService": {
    "InboxWorker": {
      "PollingInterval": "00:00:02",
      "BatchSize": 20,
      "MaxRetryCount": 5,
      "BaseRetryDelay": "00:00:05",
      "MaxRetryDelay": "00:05:00",
      "ProcessingLeaseTimeout": "00:01:00"
    }
  }
}
```

Execute localmente:

```powershell
dotnet run --project ./src/payment/PaymentService.Worker/PaymentService.Worker.csproj
```

O Worker processa somente:

- `Pending`;
- `RetryScheduled` com retry vencido;
- `Processing` com lease expirado.

Ele marca eventos suportados como `Processed` quando a state machine foi
aplicada ou quando a transicao era idempotente/regressiva esperada. Falhas
transitorias viram `RetryScheduled` com backoff exponencial persistido. Falhas
definitivas e tentativas esgotadas viram `DeadLetter`.

## Integracao Payment -> Ledger

Pagamentos em `Succeeded` e sem `ledgerEntryId` sao processados por um processor
dedicado no `PaymentService.Worker`. A chamada ao Ledger nao ocorre dentro da
transacao que processa o webhook.

Fluxo implementado:

```text
Payment Succeeded
-> claim de integracao Ledger
-> POST /api/v1/lancamentos no LedgerService.Api
-> CREDIT com amount positivo
-> ledgerEntryId persistido
-> Payment Completed
```

A Application depende da porta `ILedgerEntryGateway`; o adapter concreto
`LedgerHttpGateway` fica em `PaymentService.Infrastructure`.

Request enviado ao Ledger:

- `merchantId`: merchant do Payment;
- `type`: `CREDIT`;
- `amount`: valor positivo do Payment;
- `description`: `Payment captured`;
- `externalReference`: `payment:{paymentId}`;
- `Idempotency-Key`: UUID deterministico derivado de
  `payment:{paymentId:N}:ledger-credit`;
- `X-Correlation-Id`: correlation id preservado do webhook quando disponivel.

O Worker usa client credentials para obter token service-to-service com escopo
minimo `ledger.write`. O token e mantido em cache ate a janela de refresh
configurada por `PaymentService:Ledger:Auth:RefreshSkew`.

Falhas transitórias (`timeout`, `408`, `429`, `5xx`, rede e circuito aberto)
mantem o Payment em `LedgerPending` e agendam retry persistido com backoff. O
mesmo Payment sempre reutiliza a mesma `Idempotency-Key`; por isso um timeout
de resultado desconhecido pode ser reexecutado com segurança pelo replay
idempotente do Ledger. Falhas definitivas (`400`, `401`, `403`, `404`, `409`,
`422`) param o processamento automatico e ficam registradas no estado
operacional da integracao sem transformar o Payment em `Failed`.

## Limitacoes atuais

- Nao ha integracao direta com Balance.
- Nao ha refund.
- Nao ha Kafka no PaymentService.

Contrato versionado: [`docs/openapi/payment.v1.json`](../openapi/payment.v1.json).
