# ADR-0103: Inbox Pattern para webhooks externos da Stripe

## Status
Proposto

## Data
2026-07-08

## Contexto
Webhooks Stripe podem ser reenviados, chegar simultaneamente, atrasados ou fora
de ordem. O endpoint de webhook nao deve executar processamento financeiro
complexo nem depender da disponibilidade do Ledger.

O repositorio ja usa Outbox, retry, DLQ, idempotencia e workers para fluxos
assincronos internos. Para eventos externos recebidos por HTTP, a entrada
confiavel adequada e uma Inbox persistida antes do processamento.

## Decisao
Adotar Inbox Pattern para webhooks Stripe no `PaymentService`.

O endpoint `POST /api/v1/webhooks/stripe` deve:

1. ler o raw body;
2. validar a assinatura Stripe;
3. persistir o evento na Inbox com unique `(provider, provider_event_id)`;
4. responder `2xx` quando a assinatura for valida e a persistencia tiver sido
   concluida ou a duplicidade reconhecida.

O `PaymentService.Worker` processara a Inbox de forma assincrona, com claim
concorrente, lease, retry persistido, backoff, dead-letter logico e
observabilidade.

## Consequencias

### Beneficios
- Evita perda de eventos validos apos recebimento.
- Deduplica webhooks simultaneos por constraint do banco.
- Permite responder rapidamente a Stripe.
- Isola poison messages sem travar endpoint HTTP.
- Permite replay operacional sem duplicar efeito financeiro.

### Custos e limitacoes
- Exige tabela, migration, worker e operacao de backlog em etapa futura.
- O processamento passa a ser eventualmente consistente.
- Payload bruto exige politica de retencao e cuidados com dados sensiveis.

## Alternativas consideradas

### 1. Processar webhook completamente no controller
Rejeitada. Aumenta timeout, acopla Stripe a Ledger no request e torna retries
do provider mais perigosos.

### 2. Publicar webhook direto em Kafka sem persistir
Rejeitada para esta etapa. A entrada externa deve ser confirmada localmente
antes de depender de broker; Kafka pode aparecer depois para eventos internos
com consumidor real.

### 3. Ignorar duplicidade apenas na state machine
Rejeitada como unica protecao. State machine e Ledger idempotente continuam como
defesas adicionais, mas a Inbox deve deduplicar na entrada.

## Fora do escopo
- Criar tabela Inbox nesta ADR.
- Implementar worker.
- Definir retencao produtiva final.

## Documentacao relacionada
- [Spec Payment Stripe - design](../specs/payment-stripe/design.md)
- [Spec Payment Stripe - fluxos](../specs/payment-stripe/integration-flows.md)
- [ADR-0070](./0070-dlq-outbox-banco-backoff-requeue.md)
