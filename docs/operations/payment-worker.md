# Operacao do PaymentService.Worker

O `PaymentService.Worker` processa a Inbox persistida de webhooks Stripe e
materializa no `LedgerService.Api` o credito financeiro de pagamentos
confirmados externamente. Ele nao expoe HTTP, nao chama controllers, nao grava
schema do Ledger e nao usa Kafka proprio.

Fluxo atual:

```text
Webhook
-> payment.inbox_messages
-> PaymentService.Worker
-> state machine do Payment
-> Processed / Ignored / RetryScheduled / DeadLetter
-> Payment Succeeded
-> Ledger CREDIT
-> Payment Completed
```

## Claim e lease

O claim e feito em lote com `FOR UPDATE SKIP LOCKED` no PostgreSQL. A mensagem
reclamada recebe:

- `status = Processing`;
- `attempt_count = attempt_count + 1`;
- `processing_started_at_utc`;
- `lock_owner`;
- `locked_until_utc`.

Se o processo morrer durante o processamento, outro Worker pode reclamar a
mensagem quando `locked_until_utc <= now`. O lease evita roubo prematuro; a
idempotencia da state machine protege reprocessamento apos crash.

## Retry e DeadLetter

Falhas transitorias usam retry persistido:

```text
delay = min(BaseRetryDelay * 2^(attempt - 1), MaxRetryDelay)
```

Payment ausente e tratado como transitorio ate `MaxRetryCount`, para cobrir race
entre criacao local e webhook. Payload invalido, providerPaymentId ausente e
referencia de provider incoerente sao falhas definitivas e vao para
`DeadLetter` sem retry inutil.

`LastError` guarda apenas resumo seguro, sem payload bruto, secrets ou stack
trace completa.

## Eventos

Eventos processados:

| Stripe | Comportamento |
| --- | --- |
| `payment_intent.processing` | tenta mover Payment para `Processing`. |
| `payment_intent.succeeded` | tenta mover Payment para `Succeeded`. |
| `payment_intent.payment_failed` | tenta mover Payment para `Failed`. |
| `payment_intent.canceled` | tenta mover Payment para `Cancelled`. |

Transicoes idempotentes finalizam como `Processed`. Regressao esperada, como
`Processing` apos `Succeeded`, tambem finaliza como `Processed`, com metrica
`payment_inbox_regressive_event_total`.

## Observabilidade

Meter: `PaymentService.InboxWorker`.

Metricas principais:

- `payment_inbox_claim_total`;
- `payment_inbox_process_total`;
- `payment_inbox_process_failure_total`;
- `payment_inbox_retry_scheduled_total`;
- `payment_inbox_deadletter_total`;
- `payment_inbox_regressive_event_total`;
- `payment_inbox_idempotent_transition_total`;
- `payment_inbox_recovered_lease_total`;
- `payment_inbox_processing_duration`;
- `payment_inbox_backlog`.

Labels usam baixa cardinalidade: `provider`, `outcome` e `error_category`.
IDs de Payment, merchant, provider event, provider payment e correlation id nao
sao labels.

Traces usam `ActivitySource` `PaymentService.InboxWorker` com spans
`payment.inbox.poll` e `payment.inbox.process`.

## Integracao com Ledger

O processor de Ledger busca Payments elegiveis:

- `Status` em `Succeeded` ou `LedgerPending`;
- `ledger_entry_id` ausente;
- retry vencido ou sem retry futuro;
- lease ausente ou expirado;
- estado operacional nao terminal.

O claim local atualiza `status = LedgerPending`,
`ledger_integration_status = Processing`, incrementa
`ledger_integration_attempt_count` e grava `ledger_lock_owner`/
`ledger_locked_until_utc`. Depois do commit local, o Worker chama
`POST /api/v1/lancamentos` no Ledger usando a porta `ILedgerEntryGateway`.

Campos persistidos em `payment.payments` para recovery:

- `ledger_integration_status`;
- `ledger_integration_attempt_count`;
- `ledger_next_retry_at_utc`;
- `ledger_last_error`;
- `ledger_processing_started_at_utc`;
- `ledger_locked_until_utc`;
- `ledger_lock_owner`;
- `ledger_correlation_id`;
- `ledger_entry_id`.

Timeout, rede, `408`, `429`, `5xx` e circuito aberto sao tratados como
transitorios ou resultado desconhecido. O Worker agenda retry persistido e
reutiliza a mesma `Idempotency-Key` deterministica por Payment. `400`, `401`,
`403`, `404`, `409` e `422` sao falhas definitivas e nao entram em retry cego.

Meter adicional: `PaymentService.LedgerWorker`.

Metricas principais:

- `payment_ledger_claim_total`;
- `payment_ledger_request_total`;
- `payment_ledger_success_total`;
- `payment_ledger_failure_total`;
- `payment_ledger_retry_scheduled_total`;
- `payment_ledger_deadletter_total`;
- `payment_ledger_processing_duration`.

Labels usam baixa cardinalidade: `operation` e `error_category`.

## Troubleshooting

Backlog elegivel:

```sql
SELECT status, count(*)
FROM payment.inbox_messages
WHERE status IN ('Pending', 'Processing', 'RetryScheduled', 'DeadLetter')
GROUP BY status
ORDER BY status;
```

Mensagens presas em Processing com lease expirado:

```sql
SELECT id, provider_event_id, event_type, attempt_count, locked_until_utc, last_error
FROM payment.inbox_messages
WHERE status = 'Processing'
  AND locked_until_utc <= now()
ORDER BY received_at_utc
LIMIT 20;
```

DeadLetters recentes:

```sql
SELECT id, provider_event_id, event_type, attempt_count, last_error, updated_at
FROM payment.inbox_messages
WHERE status = 'DeadLetter'
ORDER BY updated_at DESC
LIMIT 20;
```

Nao ha endpoint de redrive nesta etapa. Investigacao e redrive administrativo
ficam para etapa futura explicita.
