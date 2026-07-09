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
- endpoints `POST /api/v1/payments` e `GET /api/v1/payments/{paymentId}`;
- scopes `payment.write` e `payment.read`;
- autorizacao por `merchant_id`;
- worker estrutural sem `BackgroundService` funcional.

## Nao implementado nesta etapa

- SDK ou adapter Stripe;
- criacao real de PaymentIntent;
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
  persistencia e a porta futura `IPaymentGateway`.
- `PaymentService.Infrastructure` implementa EF Core, migrations e repositories
  do schema `payment`.
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

Nao ha foreign keys para schemas de outros bounded contexts.
