# AuditService

`AuditService` e o bounded context de auditoria funcional por operacao.

O servico expõe o primeiro contrato funcional:

- `POST /api/v1/audit-records`: cria registro canonico e agnostico de auditoria funcional.

O endpoint exige `Idempotency-Key` em formato UUID. Repeticoes com a mesma chave e o mesmo
payload retornam o mesmo identificador; repeticoes com payload diferente retornam `409 Conflict`.
`X-Correlation-Id` e opcional; se ausente, o servico usa `correlationId` do body ou o valor gerado
pelo middleware padrao.

Metadata e aceita como objeto JSON simples de pares string/string, com limite de tamanho, sem gravar
payload bruto de request.

Estrutura em Clean Architecture:

- `AuditService.Api`
- `AuditService.Application`
- `AuditService.Domain`
- `AuditService.Infrastructure`

O servico nao consome Kafka, nao possui worker e ainda nao integra com `LedgerService`,
`BalanceService` ou `TransferService`.
