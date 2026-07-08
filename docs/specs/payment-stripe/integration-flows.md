# Specification SDD: PaymentService integrado a Stripe - fluxos

## Criacao de pagamento

```mermaid
sequenceDiagram
    participant Client
    participant PaymentApi as PaymentService.Api
    participant PaymentApp as PaymentService.Application
    participant Gateway as IPaymentGateway
    participant Stripe
    participant Db as payment schema

    Client->>PaymentApi: POST /api/v1/payments<br/>JWT + Idempotency-Key + X-Correlation-Id
    PaymentApi->>PaymentApi: valida JWT, scope payment.write e merchant_id
    PaymentApi->>PaymentApp: CreatePaymentCommand
    PaymentApp->>Db: reserva idempotencia e cria Payment Pending
    PaymentApp->>Gateway: CreatePaymentIntent(request interno)
    Gateway->>Stripe: cria PaymentIntent com idempotencia externa
    Stripe-->>Gateway: status externo traduzido
    Gateway-->>PaymentApp: CreateExternalPaymentResult
    PaymentApp->>Db: salva externalPaymentReference/providerStatus
    PaymentApp-->>PaymentApi: resultado aceito
    PaymentApi-->>Client: 202 Accepted + statusUrl
```

Decisoes:

- A resposta nao promete efeito financeiro.
- `Idempotency-Key` do cliente controla replay de `POST /payments`.
- A key externa enviada a Stripe deve ser deterministica e derivada do
  `paymentId` ou da idempotencia interna.

## Webhook

```mermaid
sequenceDiagram
    participant Stripe
    participant PaymentApi as PaymentService.Api
    participant Verifier as StripeSignatureVerifier
    participant Inbox as payment.inbox_messages

    Stripe->>PaymentApi: POST /api/v1/webhooks/stripe<br/>Stripe-Signature + raw body
    PaymentApi->>Verifier: valida assinatura, timestamp e raw body
    Verifier-->>PaymentApi: valido
    PaymentApi->>Inbox: INSERT provider/eventId/payload
    alt evento novo
        Inbox-->>PaymentApi: persisted
    else duplicado
        Inbox-->>PaymentApi: unique violation tratada como duplicate
    end
    PaymentApi-->>Stripe: 200 OK ou 202 Accepted
```

Decisoes:

- JWT nao se aplica ao webhook.
- Assinatura invalida nao persiste Inbox.
- Sucesso ao provider ocorre apos persistencia, nao apos processamento completo.

## Deduplicacao

```mermaid
sequenceDiagram
    participant Stripe
    participant Api1 as PaymentService.Api #1
    participant Api2 as PaymentService.Api #2
    participant Inbox as payment.inbox_messages

    Stripe->>Api1: evt_123
    Stripe->>Api2: evt_123 retry simultaneo
    Api1->>Inbox: INSERT (Stripe, evt_123)
    Api2->>Inbox: INSERT (Stripe, evt_123)
    Inbox-->>Api1: success
    Inbox-->>Api2: unique constraint violation
    Api2->>Api2: classifica duplicate
    Api1-->>Stripe: 2xx
    Api2-->>Stripe: 2xx
```

Resultado: apenas uma linha fica elegivel para o Worker. Mesmo que a duplicidade
passe por falha futura, a state machine e o Ledger idempotente evitam segundo
credito.

## Processamento da Inbox

```mermaid
sequenceDiagram
    participant Worker as PaymentService.Worker
    participant Inbox as payment.inbox_messages
    participant App as PaymentService.Application
    participant Payment as Payment aggregate
    participant Db as payment schema

    Worker->>Inbox: claim lote Pending/RetryScheduled com lease
    Inbox-->>Worker: mensagem reclamada
    Worker->>App: ProcessProviderEventCommand
    App->>Payment: aplica evento traduzido
    Payment-->>App: nova transicao ou ignored
    App->>Db: persiste Payment e resultado da Inbox
    alt transicao exige Ledger
        App-->>Worker: ledger entry required
    else sem efeito financeiro
        App-->>Worker: processed
    end
```

Regras:

- Claim deve ser atomico.
- Lease expirado permite retry por outra instancia.
- Eventos regressivos conhecidos podem ser `Processed/Ignored`.
- Poison messages vao para dead-letter logico.

## Integracao com Ledger

```mermaid
sequenceDiagram
    participant Worker as PaymentService.Worker
    participant App as PaymentService.Application
    participant Ledger as LedgerService.Api
    participant LedgerDb as ledger schema
    participant LedgerWorker as LedgerService.Worker
    participant Kafka
    participant BalanceWorker as BalanceService.Worker

    Worker->>App: Payment Succeeded sem ledgerEntryId
    App-->>Worker: comando Ledger CREDIT + key deterministica
    Worker->>Ledger: POST /api/v1/lancamentos<br/>ledger.write + Idempotency-Key + X-Correlation-Id
    Ledger->>LedgerDb: grava lancamento + Outbox
    Ledger-->>Worker: 201 Created
    Worker->>App: registrar ledgerEntryId
    LedgerWorker->>LedgerDb: publica Outbox
    LedgerWorker->>Kafka: LedgerEntryCreated.v2
    BalanceWorker->>Kafka: consume
    BalanceWorker->>BalanceWorker: aplica idempotente na projecao
```

Boundaries:

- PaymentService usa HTTP publico/autenticado do Ledger.
- PaymentService nao grava no schema `ledger`.
- BalanceService recebe apenas eventos do Ledger.

## Falha e retry

```mermaid
sequenceDiagram
    participant Worker
    participant Inbox
    participant App
    participant Ledger

    Worker->>Inbox: claim evt success
    Worker->>App: aplica ProviderSucceeded
    Worker->>Ledger: POST /lancamentos com key K
    Ledger--xWorker: timeout/5xx
    Worker->>Inbox: agenda retry com backoff
    Note over Worker,Inbox: Payment fica Succeeded ou LedgerPending
    Worker->>Inbox: novo claim apos NextRetryAt
    Worker->>Ledger: POST /lancamentos com mesma key K
    Ledger-->>Worker: 201 replay ou criado
    Worker->>App: salva ledgerEntryId e Completed
    Worker->>Inbox: Processed
```

Regras:

- Retry de `POST` ao Ledger so e permitido porque a key e deterministica.
- 4xx definitivos nao entram em retry infinito.
- 429 respeita backoff e possivel `Retry-After`.

## Timeout desconhecido no Ledger

```mermaid
sequenceDiagram
    participant Worker as PaymentService.Worker
    participant Ledger as LedgerService.Api
    participant LedgerDb as ledger schema

    Worker->>Ledger: POST /api/v1/lancamentos<br/>Idempotency-Key K
    Ledger->>LedgerDb: commit lancamento + idempotency record + Outbox
    Ledger--xWorker: resposta perdida
    Worker->>Worker: classifica UnknownResult/transitorio
    Worker->>Ledger: retry POST com mesma key K e mesmo payload
    Ledger-->>Worker: replay idempotente 201 Created
    Worker->>Worker: salva ledgerEntryId
```

Garantia: se o Ledger persistiu a primeira chamada, o retry com mesma key e
mesmo payload nao cria segundo lancamento. Se o payload mudou, o `409 Conflict`
indica bug ou corrupcao de determinismo e deve parar o fluxo automatico.

## Futuro refund

```mermaid
sequenceDiagram
    participant Client
    participant PaymentApi as PaymentService.Api
    participant App as PaymentService.Application
    participant Gateway as IPaymentGateway
    participant Stripe
    participant Inbox
    participant Worker as PaymentService.Worker
    participant Ledger as LedgerService.Api

    Client->>PaymentApi: POST /api/v1/payments/{id}/refunds
    PaymentApi->>App: RequestRefundCommand
    App->>Gateway: CreateRefundAsync
    Gateway->>Stripe: refund com idempotencia externa
    Stripe-->>Gateway: refund pending/succeeded
    PaymentApi-->>Client: 202 Accepted
    Stripe->>PaymentApi: webhook refund.updated/succeeded
    PaymentApi->>Inbox: persiste evento
    Worker->>App: processa refund confirmado
    Worker->>Ledger: solicita estorno/lancamento compensatorio
```

Fora de escopo agora:

- Endpoint real de refund.
- Tabelas de refund.
- Eventos de refund.
- Estorno automatico no Ledger.
