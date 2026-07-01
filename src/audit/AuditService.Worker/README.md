# AuditService.Worker

Estrutura inicial do worker do bounded context AuditService.

Nesta etapa o processo apenas valida a composition root, registra Application/Infrastructure, options e observabilidade opcional. O servico hospedado atual e um placeholder seguro: nao consome Kafka, nao abre conexao com topico e nao executa loop de processamento.

O consumer real de `AuditRecordRequested.v1` deve ser adicionado em uma etapa futura, mantendo os detalhes de transporte em `Worker`/`Infrastructure` e sem referencias a Ledger, Balance ou Transfer.
