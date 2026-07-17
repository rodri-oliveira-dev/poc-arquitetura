# Conclusao da padronizacao temporal com TimeProvider - Report

## Usos migrados

| Arquivo | Antes | Depois | Justificativa |
| --- | --- | --- | --- |
| `src/identity/IdentityService.Domain/Users/User.cs` | `occurredAt ?? DateTime.UtcNow` no aggregate. | `occurredAt` obrigatorio e UTC. | Aggregate nao consulta relogio real; evento de dominio deterministico. |
| `src/identity/IdentityService.Application/Users/Commands/CreateUserCommandHandler.cs` | Cadastro chamava `User.Register` sem instante; compensacao usava `CancelAfter`. | Handler passa `TimeProvider.GetUtcNow().UtcDateTime`; timeout agendado por timer do `TimeProvider`. | Application controla o instante e timeout fica testavel. |
| `src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs` | Timeout de compensacao usava `CancelAfter`. | Timer criado via `TimeProvider`. | Timeout tecnico controlavel sem nova abstracao. |
| `src/payment/PaymentService.Infrastructure/Gateway/FakePaymentGateway.cs` | Delay real e refund com `DateTimeOffset.UtcNow`. | Delay e `CreatedAt` usam `TimeProvider`. | Provider fake vira deterministico em testes. |
| `src/payment/PaymentService.Infrastructure/Gateway/StripePaymentGateway.cs` | Fallback de refund `created` usava `DateTimeOffset.UtcNow`. | Preserva `created` externo; fallback usa `TimeProvider`. | Diferencia timestamp externo de fallback interno. |
| `src/payment/PaymentService.Worker/HostedServices/PaymentLedgerWorkerService.cs` | Polling delay sem `TimeProvider`. | `Task.Delay(interval, TimeProvider, token)`. | Delay de worker pode ser controlado. |
| `src/audit/AuditService.Domain/FunctionalAuditing/FunctionalAuditRecord.cs` | `createdAt ?? DateTimeOffset.UtcNow` no Domain. | `createdAt` obrigatorio. | Persistencia funcional nao depende do relogio real no Domain. |
| `src/audit/AuditService.Application/FunctionalAuditing/CreateAuditRecord/CreateAuditRecordCommandHandler.cs` | Criava auditoria sem `createdAt`. | Captura `TimeProvider.GetUtcNow()`. | Application define timestamp de criacao. |
| `src/audit/AuditService.Worker/Messaging/Kafka/*` | DLQ e retries usavam relogio/delay real. | DLQ e retries usam `TimeProvider`. | Dead letter e backoff deterministicos. |
| `src/balance/BalanceService.Worker/Messaging/*` | DLQ e retries usavam relogio/delay real. | DLQ e retries usam `TimeProvider`. | Mensageria testavel e timestamp funcional controlado. |
| `src/ledger/LedgerService.Worker/*` | Polling/retry de worker usava delay real. | `Task.Delay` com `TimeProvider`. | Backoff/polling controlaveis. |

## Usos mantidos

| Arquivo | Motivo tecnico | Razao para nao injetar TimeProvider |
| --- | --- | --- |
| `src/Shared/HttpResilienceDefaults/HttpResilienceMetricsHandler.cs` | `Stopwatch` mede duracao monotonica de HTTP. | Nao gera timestamp funcional nem influencia regra. |
| `src/payment/PaymentService.Infrastructure/Gateway/*PaymentGateway.cs` | `TimeProvider.System.GetTimestamp()` mede duracao monotonica de chamadas externas. | A duracao e telemetria tecnica; o timestamp funcional usa `TimeProvider` injetado. |
| `src/*/Worker/**` com `Stopwatch` | Metricas de processamento. | Nao define estado, evento, expiracao ou resposta. |
| `DependencyInjection.cs` e composition roots | Registro de `TimeProvider.System`. | Este e o ponto esperado de composicao. |
| `src/balance/BalanceService.Worker/Messaging/PubSub/Consumers/LedgerEventsPubSubConsumer.cs` | `Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken)` aguarda cancelamento tecnico. | Nao representa backoff, regra ou timestamp funcional. |

## Testes

Adicionados/alterados:

- `IdentityService.UnitTests.Domain.Users.UserTests`: evento com instante fixo e rejeicao de `DateTimeKind.Local`.
- `IdentityService.UnitTests.Application.Users.Commands.CreateUserCommandHandlerTests`: handler injeta relogio fixo e evento recebe instante exato.
- `PaymentService.UnitTests.Infrastructure.Gateway.FakePaymentGatewayTests`: refund fake usa `CreatedAt` controlado.
- `PaymentService.IntegrationTests.Infrastructure.Gateway.StripePaymentGatewayTests`: preserva `created` da Stripe e usa fallback controlado.
- `AuditService.*Tests`: auditoria funcional usa `createdAt` explicito.
- Testes de workers Balance, Ledger e Audit ajustados para novos construtores com `TimeProvider`.

Comandos executados:

- `dotnet build ./IdentityService.slnx --configuration Release`
- `dotnet test ./IdentityService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
- `dotnet build ./PaymentService.slnx --configuration Release`
- `dotnet test ./PaymentService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
- `dotnet build ./AuditService.slnx --configuration Release`
- `dotnet test ./AuditService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
- `dotnet build ./LedgerService.slnx --configuration Release`
- `dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
- `dotnet build ./BalanceService.slnx --configuration Release`
- `dotnet test ./BalanceService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`

Resultado: todos passaram. Ledger teve 2 testes Pub/Sub ignorados por condicao do ambiente/emulador.

## Riscos residuais

- Testes auxiliares de JWT e alguns fixtures de integracao ainda usam `UtcNow` como conveniencia de teste, fora de producao.
- Migrations/snapshots historicos nao foram alterados.
- Medicoes por `Stopwatch` continuam dependentes do relogio monotonico do processo, intencionalmente.
- O consumer Pub/Sub de Balance mantem `Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken)` como sentinela tecnica.
