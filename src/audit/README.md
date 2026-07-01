# AuditService

`AuditService` e o bounded context de auditoria funcional por operacao.

O servico expoe os contratos funcionais:

- `POST /api/v1/audit-records`: cria registro canonico e agnostico de auditoria funcional.
- `GET /api/v1/audit-records/{id}`: consulta registro por id.
- `GET /api/v1/audit-records/operations/{operationId}`: consulta a trilha funcional de uma operacao.
- `GET /api/v1/audit-records`: pesquisa registros por filtros com paginacao.

O endpoint exige `Idempotency-Key` em formato UUID. Repeticoes com a mesma chave e o mesmo
payload retornam o mesmo identificador; repeticoes com payload diferente retornam `409 Conflict`.
`X-Correlation-Id` e opcional; se ausente, o servico usa `correlationId` do body ou o valor gerado
pelo middleware padrao.

Metadata e aceita como objeto JSON simples de pares string/string, com limite de tamanho, sem gravar
payload bruto de request.

Consultas por `operationId` retornam `200 OK` com lista possivelmente vazia, ordenada por
`occurredAt` ascendente para facilitar a reconstrucao cronologica da trilha funcional da operacao.

A pesquisa por filtros aceita `merchantId`, `sourceService`, `operationType`, `status`, `entityType`,
`entityId`, `from`, `to`, `page` e `pageSize`. Como protecao contra buscas amplas, `from` e `to` sao
obrigatorios, o intervalo maximo e de 31 dias, `page` inicia em `1`, `pageSize` usa default `50` e
limite maximo `100`. A ordenacao padrao da pesquisa e `occurredAt` descendente.

Estrutura em Clean Architecture:

- `AuditService.Api`
- `AuditService.Application`
- `AuditService.Domain`
- `AuditService.Infrastructure`

O servico nao consome Kafka, nao possui worker e ainda nao integra com `LedgerService`,
`BalanceService` ou `TransferService`.
