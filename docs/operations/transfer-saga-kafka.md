# Runbook DLQ e replay da Saga do TransferService

Este runbook cobre a operacao local do fluxo de Saga orquestrada do `TransferService` com Kafka. O fluxo nao usa Pub/Sub.

## Escopo

- API grava `TransferenciaSaga` e Outbox no schema `transfer`.
- Worker processa Saga pendente, chama `LedgerService.Api` para debito, credito e compensacao.
- Worker publica Outbox no Kafka com `transferenciaId` como message key.
- DLQ de aplicacao: `transfer.transferencia.dlq`.

## Diagnostico rapido

1. Verificar containers:

```bash
docker compose --env-file .env.local -f compose.yaml ps
```

2. Verificar logs do Worker com correlation id ou transferencia:

```bash
docker compose --env-file .env.local -f compose.yaml logs transfer-worker
```

3. Listar topicos Kafka:

```bash
docker compose --env-file .env.local -f compose.yaml exec kafka \
  /opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka:9092 --list
```

4. Inspecionar DLQ:

```bash
docker compose --env-file .env.local -f compose.yaml exec kafka \
  /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server kafka:9092 \
  --topic transfer.transferencia.dlq \
  --from-beginning \
  --timeout-ms 10000 \
  --property print.key=true \
  --property print.headers=true
```

## Decisao operacional

| Sintoma | Acao |
| --- | --- |
| Outbox `Pending` com erro temporario de Kafka | Corrigir broker/rede/configuracao e deixar o Worker tentar novamente. |
| Outbox `DeadLetter` por payload invalido | Nao republicar diretamente. Corrigir bug/contrato, registrar decisao e criar evento corretivo se necessario. |
| Outbox `DeadLetter` por erro definitivo do producer | Validar se o erro ainda existe. Se era configuracao local, corrigir e reprocessar com cuidado. |
| Saga `Pending` ou estado intermediario com `NextRetryAt` futuro | Aguardar retry ou ajustar manualmente apenas em ambiente local descartavel. |
| Saga `Completed` | Nao reprocessar. A idempotencia do Ledger evita duplicidade, mas o estado final nao deve ser reaberto sem decisao explicita. |
| Saga `CompensationRequested` | Confirmar o estorno no Ledger antes de qualquer acao manual adicional. |

## Replay local da Outbox

Em ambiente local descartavel, para reenfileirar uma mensagem que foi para DLQ por erro operacional ja corrigido:

1. Identifique `outbox_messages.id`, `aggregate_id`, `event_type`, `status` e `last_error`.
2. Confirme que o payload ainda representa o estado atual da Saga.
3. Atualize a mensagem para `Pending`, limpando lock e retry:

```sql
UPDATE transfer.outbox_messages
SET status = 'Pending',
    retry_count = 0,
    next_retry_at = NULL,
    locked_until = NULL,
    lock_owner = NULL,
    last_error = NULL,
    updated_at = now()
WHERE id = '<outbox-id>'
  AND status = 'DeadLetter';
```

4. Reinicie ou aguarde o `transfer-worker`.
5. Verifique que a mensagem foi marcada como `Published` somente apos confirmacao do producer.

Nao use esse SQL como procedimento produtivo. Um ambiente compartilhado deve ter ferramenta auditada de redrive, permissao separada e trilha de aprovacao.

## Replay da Saga

O retry da Saga e controlado pelo proprio estado persistido, nao por retry HTTP cego. Para reprocessamento manual local:

- nunca altere Sagas `Completed`;
- para falhas antes do debito, prefira corrigir a causa e deixar `NextRetryAt` expirar;
- para falhas apos debito, verifique `debitLancamentoId` e `compensationEstornoId` antes de mexer no estado;
- qualquer alteracao manual deve preservar as idempotency keys UUID deterministicas derivadas de `transferenciaId` e da etapa logica `debit`, `credit` ou `compensate-debit`.

## Evidencias esperadas

- Logs do Worker com `TransferenciaId` e `CorrelationId`.
- Outbox publicada com `status = Published`.
- Mensagens Kafka no topico correto e key igual ao `transferenciaId`.
- Em falha compensavel, evento `TransferenciaCompensacaoSolicitada.v1` no topico `transfer.transferencia.compensacao-solicitada`.
