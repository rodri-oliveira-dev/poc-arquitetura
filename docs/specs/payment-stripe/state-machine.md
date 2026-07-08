# Specification SDD: PaymentService integrado a Stripe - state machine

## Principios

- A state machine representa o estado interno do pagamento no
  `PaymentService`, nao o objeto `PaymentIntent` da Stripe.
- Estados finais nao podem regredir por evento atrasado.
- Eventos duplicados devem ser idempotentes.
- Eventos fora de ordem devem ser ignorados quando representam regressao ou
  mantidos como evidencia operacional quando indicam lacuna.
- Sucesso no provider e sucesso financeiro end-to-end sao estados diferentes.
- Refund futuro nao deve forcar complexidade no MVP de pagamento confirmado.

## Estados recomendados para o MVP

| Estado | Final | Significado |
| --- | --- | --- |
| `Pending` | Nao | Payment registrado localmente; intencao externa criada ou aguardando confirmacao inicial. |
| `RequiresAction` | Nao | Provider exige acao adicional do cliente. |
| `Processing` | Nao | Provider aceitou/processa a operacao, ainda sem confirmacao final. |
| `Succeeded` | Nao | Provider confirmou sucesso; efeito financeiro ainda pode estar pendente. |
| `LedgerPending` | Nao | Credito no Ledger precisa ser criado ou esta em retry. |
| `Completed` | Sim | Ledger aceitou/criou o lancamento financeiro. |
| `Failed` | Sim | Provider falhou de forma definitiva antes de efeito financeiro. |
| `Cancelled` | Sim | Payment cancelado antes de confirmacao financeira. |

Estados avaliados e nao incluidos no MVP:

- `Created`: redundante com `Pending` se a criacao local e imediata.
- `RefundPending`, `PartiallyRefunded`, `Refunded`: reservados para refund.
- `Rejected`: pode ser categoria de falha de validacao antes de criar Payment,
  sem precisar virar estado persistido no aggregate inicial.

## Ranking de progresso

Para evitar regressao por evento atrasado:

```text
Pending < RequiresAction < Processing < Succeeded < LedgerPending < Completed
```

Estados finais `Failed` e `Cancelled` encerram o fluxo antes do efeito
financeiro. Depois de `Succeeded`, `Failed` externo tardio nao deve apagar o
sucesso sem reconciliacao explicita; deve virar evento ignorado/operacional,
pois pode ser atraso, ruido ou evento de tentativa anterior.

## Eventos causadores

| Evento interno ou externo traduzido | Origem | Transicao permitida |
| --- | --- | --- |
| `PaymentRequested` | `POST /payments` | novo -> `Pending` |
| `ProviderRequiresAction` | Stripe adapter/webhook | `Pending`/`Processing` -> `RequiresAction` |
| `ProviderProcessing` | Stripe webhook | `Pending`/`RequiresAction` -> `Processing` |
| `ProviderSucceeded` | Stripe webhook | `Pending`/`RequiresAction`/`Processing` -> `Succeeded` |
| `LedgerEntryRequested` | Worker | `Succeeded` -> `LedgerPending` |
| `LedgerEntryCreated` | Ledger HTTP replay/sucesso | `Succeeded`/`LedgerPending` -> `Completed` |
| `ProviderFailed` | Stripe webhook | `Pending`/`RequiresAction`/`Processing` -> `Failed` |
| `ProviderCancelled` | Stripe webhook | `Pending`/`RequiresAction`/`Processing` -> `Cancelled` |
| `RefundRequested` | Futuro endpoint/comando | `Completed` -> futuro `RefundPending` |

## Transicoes permitidas

| De | Para | Regra |
| --- | --- | --- |
| novo | `Pending` | Criacao local idempotente. |
| `Pending` | `RequiresAction` | Provider pede acao do cliente. |
| `Pending` | `Processing` | Provider iniciou processamento. |
| `Pending` | `Succeeded` | Webhook de sucesso pode chegar sem intermediarios. |
| `Pending` | `Failed` | Falha definitiva antes de sucesso. |
| `Pending` | `Cancelled` | Cancelamento antes de sucesso. |
| `RequiresAction` | `Processing` | Cliente concluiu etapa externa. |
| `RequiresAction` | `Succeeded` | Provider confirmou sucesso. |
| `RequiresAction` | `Failed` | Acao falhou/expirou. |
| `RequiresAction` | `Cancelled` | Cancelamento antes de sucesso. |
| `Processing` | `Succeeded` | Confirmacao do provider. |
| `Processing` | `Failed` | Falha definitiva antes de sucesso. |
| `Processing` | `Cancelled` | Cancelamento antes de sucesso. |
| `Succeeded` | `LedgerPending` | Worker vai criar credito no Ledger. |
| `Succeeded` | `Completed` | Ledger respondeu sucesso antes de persistir estado intermediario. |
| `LedgerPending` | `Completed` | Ledger criou/reproduziu lancamento. |

## Transicoes ignoradas

| Estado atual | Evento recebido | Tratamento |
| --- | --- | --- |
| `Succeeded` | `ProviderProcessing` | Ignorar regressao; registrar metrica/log. |
| `Succeeded` | `ProviderRequiresAction` | Ignorar regressao. |
| `LedgerPending` | `ProviderProcessing` | Ignorar regressao. |
| `Completed` | `ProviderProcessing` | Ignorar regressao. |
| `Completed` | `ProviderSucceeded` duplicado | Sucesso idempotente; nao chamar Ledger se `ledgerEntryId` existe. |
| `Failed` | `ProviderFailed` duplicado | Sucesso idempotente. |
| `Cancelled` | `ProviderCancelled` duplicado | Sucesso idempotente. |

Exemplo obrigatorio: se o pagamento ja esta `Succeeded` e chega evento atrasado
indicando `Processing`, o sistema nao altera estado, marca o evento da Inbox
como processado/ignorado e registra log/metrica de regressao ignorada.

## Transicoes invalidas

| De | Para/evento | Motivo |
| --- | --- | --- |
| `Completed` | `Failed` | Efeito financeiro ja aceito pelo Ledger; precisa de refund/estorno, nao regressao. |
| `Completed` | `Cancelled` | Cancelamento tardio nao desfaz lancamento. |
| `Failed` | `Succeeded` | Contradicao apos final definitivo; exige reconciliacao manual ou regra especifica futura. |
| `Cancelled` | `Succeeded` | Contradicao apos cancelamento definitivo; exige reconciliacao. |
| `LedgerPending` | `Failed` | Provider ja havia confirmado; falha tardia nao deve remover pendencia financeira. |
| `Completed` | `LedgerPending` | Regressao interna proibida. |

Transicao invalida nao deve gerar nova chamada ao Ledger. O evento pode ir para
`DeadLetter` logico se indicar corrupcao, incompatibilidade de contrato ou
estado impossivel que exija acao operacional. Para eventos Stripe conhecidos
mas atrasados, prefira `Processed/Ignored` para nao criar backlog artificial.

## Duplicidade

Camadas de defesa:

1. Inbox unique `(provider, providerEventId)`.
2. State machine idempotente por `Payment`.
3. Campo `ledgerEntryId`/`ledgerCreditRequestedAt`.
4. `Idempotency-Key` deterministica no `LedgerService.Api`.
5. Idempotencia propria do Balance por eventos do Ledger.

Resultado esperado: replay de Inbox ou webhook duplicado pode reexecutar a
decisao, mas nao cria segundo credito financeiro.

## Eventos atrasados e fora de ordem

| Situacao | Decisao |
| --- | --- |
| Evento intermediario chega depois de sucesso | Ignorar regressao. |
| Sucesso chega antes de processing | Aceitar sucesso e avancar. |
| Falha chega depois de sucesso | Nao regredir; registrar como contradicao tardia. |
| Cancelamento chega depois de Completed | Nao alterar; refund/estorno futuro, se aplicavel. |
| Evento sem `paymentId` interno em metadata | Tentar localizar por referencia externa; se impossivel, dead-letter/reconciliacao. |

## Estado financeiro

O aggregate deve diferenciar:

- `providerStatus`: estado externo traduzido.
- `paymentStatus`: state machine interna.
- `ledgerEntryId`: referencia ao lancamento criado.
- `ledgerStatus`: `NotRequired`, `Pending`, `Created`, `FailedTransient`,
  `FailedDefinitive` ou equivalente interno.

`Succeeded` nao significa que o Balance ja foi atualizado. O Balance e eventual
e depende de `LedgerEntryCreated.v2` publicado pelo Ledger.

## Refund futuro

Estados futuros a considerar apenas quando refund entrar no escopo:

| Estado futuro | Significado |
| --- | --- |
| `RefundPending` | Refund solicitado no PaymentService ou provider. |
| `PartiallyRefunded` | Parte do valor foi reembolsada externamente e refletida no Ledger. |
| `Refunded` | Valor total foi reembolsado externamente e refletido no Ledger. |
| `RefundFailed` | Refund falhou apos estado intermediario. |

Regras futuras:

- Refund externo confirmado nao atualiza Balance diretamente.
- Estorno interno deve ser solicitado ao Ledger com idempotency key por refund.
- Refund parcial nao pode exceder valor capturado menos refunds anteriores.
- Refund confirmado externamente, mas ainda sem Ledger, deve ficar pendente de
  estorno interno de forma semelhante a `LedgerPending`.
