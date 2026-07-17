# Conclusao da padronizacao temporal com TimeProvider - Requirements

## Contexto

Tempo e dependencia externa. Regras de dominio, eventos, persistencia funcional,
expiracoes, leases, retries, backoff e respostas precisam receber instantes
controlaveis para serem testados sem depender do relogio real do processo.

## Inventario

Busca obrigatoria executada em `src/**/*.cs`:

```text
DateTime.Now
DateTime.UtcNow
DateTimeOffset.Now
DateTimeOffset.UtcNow
TimeProvider.System
Task.Delay
PeriodicTimer
CancellationTokenSource.CancelAfter
Stopwatch
Environment.TickCount
SystemClock
IClock
new SystemClock
```

Classificacao encontrada:

| Categoria | Usos |
| --- | --- |
| Regra de dominio | `IdentityService.Domain.Users.User.Register` tinha fallback `DateTime.UtcNow`; `AuditService.Domain.FunctionalAuditing.FunctionalAuditRecord.Create` tinha fallback `DateTimeOffset.UtcNow`. |
| Timestamp funcional | `FakePaymentGateway` criava refund com `DateTimeOffset.UtcNow`; `StripePaymentGateway` usava `DateTimeOffset.UtcNow` como fallback quando `created` vinha ausente; DLQs de Balance/Audit usavam `DateTimeOffset.UtcNow`. |
| Persistencia | Idempotencia, Inbox, Outbox, leases e retries ja recebiam instantes de handlers/repositorios com `TimeProvider` em Ledger, Payment, Identity, Transfer e Balance. |
| Integracao externa | Stripe preserva `created` externo quando fornecido; fallback interno agora vem de `TimeProvider`. |
| Expiracao, retry, lease ou timeout | Workers e idempotencia usam `TimeProvider`; delays de retry/backoff migrados quando relevantes. |
| Telemetria e duracao monotonica | `Stopwatch` e `TimeProvider.System.GetTimestamp()` permanecem apenas para duracao de chamadas/processamento. |
| Infraestrutura do framework | Registros `services.AddSingleton(TimeProvider.System)` nas composition roots; `Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken)` em consumer Pub/Sub como tarefa sentinela de cancelamento. |
| Migration historica | Migrations/snapshots EF foram inventariados, mas nao alterados. |
| Teste | Testes foram ajustados para instantes fixos nos pontos afetados; alguns factories JWT e fixtures ainda usam fallback de teste. |
| Uso justificadamente mantido | Medicoes monotonicas e registros de DI com `TimeProvider.System`; delays ja parametrizados por `TimeProvider`. |

## Requisitos

- `TimeProvider` e a abstracao temporal principal.
- Composition roots registram `TimeProvider.System`.
- Componentes que dependem de tempo recebem `TimeProvider` por DI.
- Aggregates nao consultam relogio real.
- Application captura o instante e passa explicitamente ao Domain.
- Eventos de dominio mantem timestamp explicito.
- UTC deve ser consistente.
- Timestamps externos sao preservados; fallback interno deve ser explicito e controlavel.
- Delays de retry/backoff testaveis usam APIs com `TimeProvider`.
- Usos monotonicos tecnicos podem ficar com `Stopwatch` ou `TimeProvider.System.GetTimestamp()` quando nao influenciam regras.
- Migrations historicas nao devem ser alteradas.

## Fora de escopo

- Trocar todos os `DateTime` por `DateTimeOffset`.
- Alterar contratos HTTP/OpenAPI.
- Criar abstracao propria como `IClock`.
- Refatorar regras nao relacionadas.
