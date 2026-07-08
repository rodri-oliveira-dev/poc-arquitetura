# ADR-0105: Ordenacao, deduplicacao e regressao de eventos externos de pagamento

## Status
Proposto

## Data
2026-07-08

## Contexto
Eventos externos de um provider de pagamentos podem chegar duplicados,
atrasados ou fora de ordem. O sistema nao controla a entrega da Stripe e nao
pode assumir ordenacao perfeita. Ao mesmo tempo, eventos duplicados nao podem
gerar lancamento financeiro duplicado nem regredir estado interno.

## Decisao
O `PaymentService` deve tratar eventos externos com tres mecanismos
complementares:

1. deduplicacao de entrada por Inbox unique `(provider, provider_event_id)`;
2. state machine monotona do aggregate `Payment`, proibindo regressao de estados
   finais ou mais avancados;
3. idempotencia de efeito financeiro por `ledgerEntryId` e `Idempotency-Key`
   deterministica no `LedgerService`.

Eventos intermediarios atrasados, como `Processing` depois de `Succeeded`, devem
ser marcados como processados/ignorados, com log e metrica. Contradicoes apos
estado final, como `Failed` depois de `Completed`, nao devem alterar estado nem
criar efeito financeiro; devem virar evidencia operacional ou dead-letter
logico conforme classificacao.

`Succeeded` representa sucesso no provider. `Completed` representa sucesso do
efeito financeiro aceito/criado no Ledger.

## Consequencias

### Beneficios
- Evita duplicidade financeira em retries, replay e eventos fora de ordem.
- Deixa explicita a diferenca entre estado externo e efeito financeiro interno.
- Permite replay seguro de Inbox.
- Reduz necessidade de ordenacao global.

### Custos e limitacoes
- Eventos contraditorios exigem operacao/reconciliacao.
- Algumas informacoes intermediarias podem ser ignoradas depois de estado mais
  avancado.
- A reconciliacao futura com Stripe precisa respeitar a mesma state machine.

## Alternativas consideradas

### 1. Aplicar sempre o ultimo evento recebido
Rejeitada. Evento atrasado poderia regredir `Succeeded` para `Processing` ou
`Completed` para `Failed`.

### 2. Exigir ordenacao perfeita por fila
Rejeitada. A fonte externa e HTTP/webhook; a protecao deve estar no modelo e na
persistencia, nao em uma garantia que o sistema nao controla.

### 3. Tratar todos eventos fora de ordem como DLQ
Rejeitada. Muitos eventos atrasados sao esperados e podem ser ignorados com
seguranca; DLQ deve ficar para payload invalido, estado impossivel ou acao
operacional necessaria.

## Fora do escopo
- Implementar reconciliacao Stripe.
- Definir todos os tipos Stripe aceitos.
- Criar topicos Kafka do PaymentService.

## Documentacao relacionada
- [Spec Payment Stripe - state machine](../specs/payment-stripe/state-machine.md)
- [Spec Payment Stripe - design](../specs/payment-stripe/design.md)
- [Runbook de recuperacao de eventos](../operations/event-recovery-runbook.md)
