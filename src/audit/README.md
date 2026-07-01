# AuditService

`AuditService` e o bounded context de auditoria funcional por operacao.

Esta etapa cria apenas a estrutura inicial do servico em Clean Architecture:

- `AuditService.Api`
- `AuditService.Application`
- `AuditService.Domain`
- `AuditService.Infrastructure`

O servico ainda nao possui dominio implementado, persistencia, migrations, endpoints funcionais de auditoria, consumo Kafka ou integracao com `LedgerService`, `BalanceService` ou `TransferService`.
