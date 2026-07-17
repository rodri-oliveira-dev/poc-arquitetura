# HTTP ingestion

Esta pasta reserva o namespace para uma possivel ingestao HTTP interna do
`AuditService`.

Nao ha endpoint interno novo nesta etapa. O endpoint publico atual continua em
`Controllers/AuditRecordsController` e segue usando diretamente o caso de uso
`CreateAuditRecord`.

Se houver decisao futura de integracao HTTP entre bounded contexts, um adapter
interno pode validar um envelope canonico e delegar ao caso de uso de criacao,
sem acoplar o `AuditService` a tipos de Ledger, Balance, Transfer ou Payment.
