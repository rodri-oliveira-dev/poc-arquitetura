# ADR-0101: Bounded context PaymentService para pagamentos externos

## Status
Proposto

## Data
2026-07-08

## Contexto
O repositorio possui `LedgerService` como dono dos lancamentos financeiros,
`BalanceService` como projecao eventual desses lancamentos, `TransferService`
como orquestrador de Sagas e `AuditService`/`IdentityService` como bounded
contexts separados.

O novo estudo de pagamentos externos com Stripe precisa representar o ciclo de
vida de um pagamento processado fora do sistema, sem mover regras de lancamento
para fora do Ledger e sem fazer o Balance reagir diretamente a eventos de
pagamento.

## Decisao
Criar, em etapa futura, um bounded context chamado `PaymentService`, com
estrutura alinhada aos servicos recentes:

- `PaymentService.Api`;
- `PaymentService.Application`;
- `PaymentService.Domain`;
- `PaymentService.Infrastructure`;
- `PaymentService.Worker`.

O `PaymentService` sera a fonte de verdade do estado interno do pagamento. O
`LedgerService` continua sendo a fonte de verdade do efeito financeiro. O
`BalanceService` continua sendo projecao assincrona dos eventos financeiros do
Ledger.

O primeiro escopo funcional deve priorizar pagamento confirmado. Refund,
reconciliacao completa e publicacao de eventos de integracao do PaymentService
ficam para etapas posteriores.

## Consequencias

### Beneficios
- Isola a linguagem de pagamentos externos em contexto proprio.
- Evita contaminar Ledger com detalhes de Stripe.
- Preserva Balance como leitura/projecao.
- Permite evoluir state machine, Inbox, retry e reconciliacao sem mexer em
  contexts financeiros existentes.

### Custos e limitacoes
- Adiciona mais um contexto, schemas, testes e operacao local em etapa futura.
- Exige client service-to-service para Ledger.
- Exige governanca de secrets e webhooks.

### Riscos
- Overengineering se o contexto nascer com eventos, topicos ou shared libraries
  antes de consumidores reais.
- Ambiguidade entre `Succeeded` no provider e `Completed` financeiro se a
  state machine nao for respeitada.

## Alternativas consideradas

### 1. Implementar Stripe dentro do LedgerService
Rejeitada. O Ledger deve continuar dono dos lancamentos, nao do processo externo
de pagamento, webhooks, provider status ou reconciliacao Stripe.

### 2. PaymentService atualizar BalanceService diretamente
Rejeitada. Balance continua derivado dos eventos financeiros do Ledger.

### 3. Integrar Stripe diretamente ao cliente e apenas receber Ledger command
Rejeitada para este estudo. Perderia a oportunidade de modelar webhooks,
deduplicacao, Inbox, reconciliacao e consistencia eventual no backend.

## Fora do escopo
- Criar projetos, migrations, endpoints ou compose nesta ADR.
- Implementar Stripe, Inbox ou Worker.
- Alterar Ledger, Balance ou Transfer.

## Documentacao relacionada
- [Spec Payment Stripe - requisitos](../specs/payment-stripe/requirements.md)
- [Spec Payment Stripe - design](../specs/payment-stripe/design.md)
- [ADR-0001](./0001-separar-ledger-e-balance-com-projecao.md)
- [ADR-0002](./0002-clean-architecture-ddd-por-servico.md)
- [ADR-0100](./0100-organizacao-solutions-contexto-agregadora.md)
