# ADR-0104: Integracao PaymentService -> LedgerService para efeito financeiro

## Status
Proposto

## Data
2026-07-08

## Contexto
Um pagamento externo confirmado precisa gerar efeito financeiro interno. No
repositorio, o `LedgerService` e o unico dono dos lancamentos financeiros e o
`BalanceService` e atualizado por eventos do Ledger.

O `PaymentService` nao deve gravar tabelas do Ledger nem publicar eventos que o
Balance consuma como saldo. Tambem precisa lidar com timeout e resposta perdida
sem criar lancamento duplicado.

## Decisao
Quando um Payment for confirmado pelo provider, o `PaymentService.Worker` deve
chamar `LedgerService.Api` em `POST /api/v1/lancamentos` para criar um
`CREDIT`, usando:

- token service-to-service com `ledger.write`;
- `merchantId` do Payment;
- `externalReference` deterministica, como `payment:{paymentId}`;
- `X-Correlation-Id` preservado;
- `Idempotency-Key` UUID deterministica por `paymentId` e operacao
  `ledger-credit`.

O Payment deve armazenar a referencia do lancamento criado. Reprocessamentos,
retries e eventos duplicados nao devem chamar o Ledger novamente quando
`ledgerEntryId` ja existir. Se a resposta HTTP do Ledger for perdida apos o
commit, o retry com a mesma key deve acionar o replay idempotente do Ledger.

## Consequencias

### Beneficios
- Preserva ownership do Ledger.
- Usa idempotencia existente do contrato HTTP do Ledger.
- Mantem Balance derivado apenas de `LedgerEntryCreated`.
- Permite retry seguro em resultado desconhecido.

### Custos e limitacoes
- Cria dependencia operacional do PaymentService.Worker em LedgerService.Api.
- Exige token service-to-service e resiliencia HTTP.
- Enquanto Ledger estiver indisponivel, pagamentos confirmados ficam pendentes
  de contabilizacao interna.

## Alternativas consideradas

### 1. PaymentService gravar direto no banco do Ledger
Rejeitada. Viola fronteira de bounded context e invariantes do Ledger.

### 2. PaymentService publicar evento para Balance
Rejeitada. Balance nao deve usar eventos de pagamento como fonte de saldo.

### 3. Stripe webhook chamar Ledger diretamente
Rejeitada. Stripe nao conhece dominio interno nem autorizacao do sistema; o
webhook deve entrar na Inbox do PaymentService.

## Fora do escopo
- Implementar client Ledger.
- Alterar contrato atual do Ledger.
- Criar novos eventos financeiros.

## Documentacao relacionada
- [Spec Payment Stripe - design](../specs/payment-stripe/design.md)
- [Spec Payment Stripe - fluxos](../specs/payment-stripe/integration-flows.md)
- [LedgerService API](../development/ledger-api.md)
- [ADR-0001](./0001-separar-ledger-e-balance-com-projecao.md)
- [ADR-0087](./0087-saga-orquestrada-transfer-service-kafka.md)
