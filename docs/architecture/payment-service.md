# PaymentService

O `PaymentService` e o bounded context inicial para pagamentos externos. Ele
representa o ciclo de vida interno de um pagamento no sistema, preservando a
diferenca entre sucesso confirmado pelo provider (`Succeeded`) e efeito
financeiro aceito/criado pelo Ledger (`Completed`).

## Implementado nesta etapa

- projetos `PaymentService.Api`, `Application`, `Domain`, `Infrastructure` e
  `Worker`;
- aggregate `Payment`, value objects e state machine inicial;
- persistencia EF Core/PostgreSQL no schema `payment`;
- tabela `payment.payments`;
- tabela `payment.idempotency_records` para idempotencia do endpoint interno;
- tabela `payment.inbox_messages` para entrada duravel de webhooks Stripe;
- Anti-Corruption Layer `IPaymentGateway` na Application;
- provider fake configuravel para desenvolvimento e testes;
- adapter HTTP Stripe em `Infrastructure` para criar PaymentIntent com
  idempotencia externa deterministica;
- timeout, retry e circuit breaker via `PocArquitetura.HttpResilienceDefaults`;
- observabilidade da chamada externa por span `payment.provider.create` e
  metricas `payment_provider_*`;
- observabilidade do webhook por spans `stripe webhook signature validation` e
  `payment inbox persist`, metricas `payment_webhook_*` e
  `payment_inbox_pending_total`;
- endpoints `POST /api/v1/payments`, `GET /api/v1/payments/{paymentId}` e
  `POST /api/v1/webhooks/stripe`;
- scopes `payment.write` e `payment.read`;
- autorizacao por `merchant_id`;
- validacao de webhook Stripe por raw body, `Stripe-Signature`, signing secret e
  tolerancia temporal;
- worker estrutural sem `BackgroundService` funcional.

## Nao implementado nesta etapa

- processamento assincrono da Inbox;
- transicao de Payment acionada por webhook;
- Kafka;
- Outbox;
- integracao com LedgerService;
- integracao com BalanceService;
- refund.

## Fronteiras

- `PaymentService.Domain` contem o aggregate e as invariantes, sem EF Core,
  HTTP, Kafka, Stripe ou referencias a outros bounded contexts.
- `PaymentService.Application` coordena criacao e consulta, define portas de
  persistencia e a porta `IPaymentGateway` com modelos internos da ACL.
- `PaymentService.Infrastructure` implementa EF Core, migrations, repositories
  do schema `payment`, provider fake e adapter Stripe. Tipos e contratos
  externos da Stripe nao atravessam a Infrastructure.
- `PaymentService.Api` compoe autenticacao, autorizacao, ProblemDetails,
  Swagger/OpenAPI, health/readiness, controllers HTTP e validacao tecnica do
  webhook Stripe.
- `PaymentService.Worker` existe como composition root futura, mas nao executa
  processamento nesta etapa.

## Persistencia

Schema: `payment`.

Tabelas:

- `payments`: estado interno do aggregate, merchant, amount, currency, provider,
  status, referencias externa/provider/Ledger e timestamps;
- `idempotency_records`: replay seguro do `POST /api/v1/payments`.
- `inbox_messages`: eventos Stripe recebidos com `provider`, `provider_event_id`,
  `event_type`, payload bruto validado, `payload_sha256`, status, categoria,
  timestamps, campos futuros de processamento/lease e metadados de correlacao.

Indices:

- `ux_payment_payments_provider_external_reference`: unico por
  `provider + external_payment_reference` quando a referencia externa existe.
- `ux_payment_inbox_provider_event`: unico por `provider + provider_event_id`;
- `idx_payment_inbox_status_next_retry`: prepara claim futuro do Worker sem
  implementar polling nesta etapa;
- `idx_payment_inbox_received_at`: suporte a diagnostico e retencao futura.

Nao ha foreign keys para schemas de outros bounded contexts.

## Webhook e Inbox

O endpoint `POST /api/v1/webhooks/stripe` nao usa JWT. A autenticidade e
validada com HMAC SHA-256 no raw body recebido, header `Stripe-Signature` e
`PaymentGateway:Stripe:WebhookSigningSecret`. Assinatura invalida, header
ausente, timestamp fora da tolerancia ou payload invalido nao sao persistidos.

Eventos suportados no MVP entram como `Pending`. Eventos conhecidos fora do MVP
e eventos desconhecidos entram como `Ignored` para evitar retry infinito do
provider. Duplicidade concorrente e resolvida pelo PostgreSQL via unique
constraint; o segundo request retorna sucesso idempotente sem expor erro de
constraint.
