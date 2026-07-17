# Kafka ingestion

Esta pasta concentra pontos de extensao de infraestrutura para ingestao Kafka
do `AuditService`.

O consumer ativo fica no projeto `AuditService.Worker`, que consome
`AuditRecordRequested.v1` do topico `audit.record.requested` quando o worker e
o consumer estao habilitados por configuracao. A persistencia continua no
schema `audit`.

Nenhum servico financeiro publica eventos reais de auditoria nesta etapa. O
contrato e o consumer existem para validar a fatia de ingestao assincrona sem
acoplar Ledger, Balance, Transfer ou Payment ao modelo interno do Audit.
