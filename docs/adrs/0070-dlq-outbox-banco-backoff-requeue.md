# ADR-0070: DLQ em banco no Outbox com backoff exponencial

## Status
Aceito

## Data
2026-05-22

## Contexto

O Ledger usa Outbox para publicar eventos financeiros no Kafka. A implementacao ja possuia retry, lock concorrente e requeue administrativo de mensagens finais, mas a nomenclatura `Failed`/`Sent` nao deixava claro o papel de DLQ em banco nem havia endpoint de inspecao paginada.

Falhas transientes de Kafka devem ser retentadas com atraso crescente. Falhas que excedem o limite configurado devem parar de ser processadas automaticamente, preservar diagnostico e permitir requeue manual apos correcao da causa raiz.

## Decisao

Padronizar o Outbox do Ledger com os estados `Pending`, `Processing`, `Processed` e `DeadLetter`.

O publisher passa a:

- selecionar apenas mensagens `Pending` elegiveis por `next_retry_at`, alem de recuperar locks `Processing` expirados;
- incrementar `retry_count` em falhas de publicacao;
- calcular `next_retry_at` por `ExponentialBackoffRetryStrategy`, com jitter isolado em Application;
- mover a mensagem para `DeadLetter` ao atingir `Outbox:Publisher:MaxAttempts`;
- registrar log critico ao mover para DLQ, preservando contexto W3C disponivel na linha da Outbox.

A API administrativa passa a expor:

- `GET /api/v1/outbox/dead-letters`;
- `POST /api/v1/outbox/dead-letters/{id}/requeue`.

Ambos exigem o scope `outbox.admin`. O requeue atua somente sobre mensagens `DeadLetter`, volta o status para `Pending`, zera `retry_count`, limpa `last_error` e registra auditoria do ultimo requeue.

## Consequencias

- A DLQ do Outbox fica visivel e reprocessavel por contrato HTTP administrativo.
- A estrategia de backoff fica testavel fora do Worker e adequada a mutation testing.
- Operacao manual fica mais segura por id, evitando requeue amplo por filtros.
- A mudanca exige migration para renomear `attempts` para `retry_count`, `next_attempt_at` para `next_retry_at` e converter os status historicos `Sent`/`Failed`.

## Alternativas consideradas

1. **Manter `Failed` como nome de DLQ**
   - Rejeitado porque a especificacao operacional pede `DeadLetter` e a nova API de inspecao deve expor esse conceito diretamente.

2. **Publicar DLQ em topico Kafka tambem no producer**
   - Rejeitado nesta etapa. A falha esta antes da publicacao confiavel; persistir a DLQ no banco evita depender do mesmo Kafka indisponivel.

3. **Requeue em lote por filtros**
   - Rejeitado para o novo endpoint para reduzir risco operacional. Reprocessamento manual deve ser pontual por id.
