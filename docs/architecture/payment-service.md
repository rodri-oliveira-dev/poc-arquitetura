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
- Anti-Corruption Layer `IPaymentGateway` na Application;
- provider fake configuravel para desenvolvimento e testes;
- adapter HTTP Stripe em `Infrastructure` para criar PaymentIntent com
  idempotencia externa deterministica;
- timeout, retry e circuit breaker via `PocArquitetura.HttpResilienceDefaults`;
- observabilidade da chamada externa por span `payment.provider.create` e
  metricas `payment_provider_*`;
- endpoints `POST /api/v1/payments` e `GET /api/v1/payments/{paymentId}`;
- scopes `payment.write` e `payment.read`;
- autorizacao por `merchant_id`;
- worker estrutural sem `BackgroundService` funcional.

## Nao implementado nesta etapa

- webhook Stripe;
- raw body handling e validacao de assinatura;
- Inbox Pattern;
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
  Swagger/OpenAPI, health/readiness e controllers HTTP.
- `PaymentService.Worker` existe como composition root futura, mas nao executa
  processamento nesta etapa.

## Persistencia

Schema: `payment`.

Tabelas:

- `payments`: estado interno do aggregate, merchant, amount, currency, provider,
  status, referencias externa/provider/Ledger e timestamps;
- `idempotency_records`: replay seguro do `POST /api/v1/payments`.

Indices:

- `ux_payment_payments_provider_external_reference`: unico por
  `provider + external_payment_reference` quando a referencia externa existe.

Nao ha foreign keys para schemas de outros bounded contexts.
