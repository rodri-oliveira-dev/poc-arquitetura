# ADR-0102: Anti-Corruption Layer para integracao com Stripe

## Status
Aceito

Nota: este ADR foi criado inicialmente como proposta durante a fase de
specification. Apos a implementacao do fluxo PaymentService + Stripe + Inbox +
Ledger no PR #66, a decisao foi considerada aceita.

## Data
2026-07-08

## Contexto
Stripe possui tipos, eventos, erros e semantica propria. O repositorio preserva
fronteiras entre `Domain`, `Application` e `Infrastructure` e evita detalhes de
transporte ou fornecedor externo nas camadas internas.

Sem uma Anti-Corruption Layer, conceitos como `PaymentIntent`, `Charge`, `Event`
e `Refund` poderiam vazar para o dominio de pagamentos ou para contratos entre
contexts.

## Decisao
Integrar Stripe por uma Anti-Corruption Layer na `Infrastructure`, atras de uma
porta da `Application` como `IPaymentGateway`.

A porta deve receber e retornar modelos internos, por exemplo
`CreateExternalPaymentRequest`, `CreateExternalPaymentResult` e falhas
normalizadas. Tipos do SDK Stripe nao podem atravessar a fronteira da
Infrastructure.

O adapter deve tratar:

- idempotencia externa;
- timeouts e cancellation;
- erros transitorios, definitivos e de resultado desconhecido;
- observabilidade sem secrets ou payload sensivel;
- mapeamento de status externo para estado interno;
- suporte futuro a refund sem impor complexidade no MVP.

## Consequencias

### Beneficios
- Protege o dominio de pagamentos de mudancas do SDK/API Stripe.
- Permite provider fake em testes e k6.
- Facilita suporte futuro a outro payment provider sem criar framework generico
  prematuro.

### Custos e limitacoes
- Exige modelos internos e mapeamentos explicitos.
- Pode duplicar nomes parecidos com a Stripe, mas com semantica local.
- O adapter precisa ser bem testado para nao esconder diferencas relevantes do
  provider.

## Alternativas consideradas

### 1. Usar tipos Stripe diretamente na Application
Rejeitada. Criaria acoplamento indevido e dificultaria testes sem conta externa.

### 2. Criar biblioteca compartilhada generica de pagamentos
Rejeitada nesta etapa. Ha apenas um provider planejado; uma abstracao mais
ampla seria prematura.

### 3. Implementar chamadas Stripe no controller
Rejeitada. Controller deve permanecer fino e sem regra de resiliencia ou
traducao de dominio.

## Fora do escopo
- Escolher pacote concreto da Stripe.
- Implementar adapter.
- Criar configuracao real ou secrets.

## Documentacao relacionada
- [Spec Payment Stripe - design](../specs/payment-stripe/design.md)
- [ADR-0015](./0015-api-resilience-timeouts-retries-circuit-breaker.md)
- [ADR-0020](./0020-padronizar-configuracao-segura-de-secrets-e-ambientes.md)
