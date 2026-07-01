# Kafka ingestion futura

Esta pasta reserva o namespace do adapter Kafka futuro do `AuditService`.

Nao ha consumer, worker, topico, subscription, DLQ ou publicacao ativa nesta
etapa. Nenhum servico financeiro publica eventos de auditoria agora.

Se uma ADR futura aprovar ingestao assincrona, o adapter Kafka devera traduzir
uma mensagem versionada para `AuditRecordEnvelope` e delegar a
`IAuditRecordIngestionService`, preservando correlacao, idempotencia e
agnosticismo em relacao aos dominios chamadores.
