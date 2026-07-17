# Conclusao da padronizacao temporal com TimeProvider - Design

## Decisoes

1. `TimeProvider` e injetado diretamente; nenhuma abstracao propria sera criada.
2. Aggregates recebem instantes como parametro obrigatorio.
3. Application/Worker/Infrastructure capturam `GetUtcNow()` no ponto de orquestracao.
4. Timestamps externos vindos da Stripe sao preservados; apenas ausencia de `created` usa fallback interno controlado.
5. Duracoes de telemetria continuam monotonicas e fora da regra de negocio.

## Mudancas por area

### IdentityService

`User.Register` passa a exigir `occurredAt` UTC. `CreateUserCommandHandler`
obtem `timeProvider.GetUtcNow().UtcDateTime` e fornece ao aggregate. O timeout
de compensacao usa timer criado pelo `TimeProvider`.

### PaymentService

`FakePaymentGateway` recebe `TimeProvider` para delay simulado e `CreatedAt` de
refund. `StripePaymentGateway` recebe `TimeProvider` para fallback quando a
Stripe omite `created`; quando a Stripe fornece `created`, o timestamp externo e
preservado. `PaymentLedgerWorkerService` usa delay controlavel.

### AuditService

`FunctionalAuditRecord.Create` exige `createdAt`; o handler captura o instante
via `TimeProvider`. DLQ do worker usa `TimeProvider`.

### BalanceService e LedgerService

Retries/backoffs de consumers e polling de estorno usam `Task.Delay` com
`TimeProvider`. DLQ de Balance usa timestamp controlavel.

## Usos mantidos

- `Stopwatch.GetTimestamp/GetElapsedTime` em metricas de processamento e HTTP.
- `TimeProvider.System.GetTimestamp` nos gateways de pagamento para duracao
  monotonica.
- `services.AddSingleton(TimeProvider.System)` nas composition roots.
- `Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken)` no consumer Pub/Sub como
  mecanismo tecnico de cancelamento, sem timestamp funcional.
