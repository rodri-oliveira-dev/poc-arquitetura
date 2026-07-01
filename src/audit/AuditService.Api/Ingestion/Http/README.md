# HTTP ingestion futura

Esta pasta reserva o namespace do adapter HTTP interno de ingestao do
`AuditService`.

Nao ha endpoint interno novo nesta etapa. O endpoint publico atual continua em
`Controllers/AuditRecordsController` e segue usando diretamente o caso de uso
`CreateAuditRecord`.

Quando houver decisao de integracao entre bounded contexts, um adapter HTTP
interno pode desserializar `AuditRecordEnvelope`, validar o envelope e delegar a
`IAuditRecordIngestionService`, sem acoplar o `AuditService` a tipos de
Ledger, Balance ou Transfer.
