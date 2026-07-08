# Specification SDD: PaymentService integrado a Stripe - tasks futuras

## Visao geral

As tarefas abaixo sao incrementais e devem ser executadas em prompts/PRs
separados. Cada etapa deve preservar o comportamento existente de Ledger,
Balance, Transfer, Identity e Audit. Sempre que uma etapa alterar contrato HTTP,
OpenAPI deve ser gerado pelos scripts oficiais e versionado em `docs/openapi`.

## 1. Estrutura e dominio do PaymentService

Objetivo: criar a estrutura minima do bounded context e modelar `Payment`.

Escopo:

- Criar projetos `PaymentService.Api`, `Application`, `Domain`,
  `Infrastructure` e, se necessario nesta fase, `Worker` vazio/sem loop.
- Criar tests em `tests/payment`.
- Modelar `Payment`, value objects e state machine inicial.
- Criar testes unitarios de invariantes e transicoes.
- Atualizar solution agregadora e contextual futura.

Fora de escopo:

- Stripe real.
- Endpoints funcionais completos.
- Migrations e tabelas, salvo se a fatia explicitamente incluir persistencia.
- Kafka e Ledger.

Dependencias:

- ADR-0101 e `state-machine.md`.

Criterios de aceite:

- Build compila.
- Architecture tests preservam dependencias por camada.
- Domain nao referencia Infrastructure, Api, Worker, Stripe ou Kafka.
- State machine cobre duplicidade, regressao e estados finais.

Testes obrigatorios:

- Unitarios de `Payment`.
- Architecture tests de camadas.

Documentacao afetada:

- `README.md`, `docs/README.md`, `docs/architecture/*` e LikeC4 quando a
  estrutura existir em runtime.
- ADR se a decisao mudar.

## 2. Stripe adapter e criacao de pagamento

Objetivo: implementar `POST /api/v1/payments` com ACL e provider fake/Stripe.

Escopo:

- Definir porta `IPaymentGateway`.
- Implementar fake provider para testes e desenvolvimento.
- Implementar adapter Stripe somente atras da porta, sem vazamento de SDK.
- Implementar idempotencia de `POST /payments`.
- Implementar persistencia do Payment se ainda nao existir.
- Configurar resiliencia HTTP para provider.

Fora de escopo:

- Webhook.
- Ledger.
- Refund.
- Kafka.

Dependencias:

- Task 1.
- Decisao sobre retorno de `clientSecret`.

Criterios de aceite:

- Replay com mesma `Idempotency-Key` nao cria nova intencao externa.
- Payload diferente com mesma key retorna `409`.
- Erros transitorios e definitivos do provider sao classificados.
- Secrets nao sao versionados.

Testes obrigatorios:

- Unitarios de Application.
- Adapter fake.
- Testes de endpoint.
- Testes de resiliencia com handler fake ou WireMock.Net/equivalente.

Documentacao afetada:

- `docs/development/payment-api.md` quando endpoint existir.
- `docs/openapi/payment.v1.json`.
- Configuracao local.

## 3. Webhook e Inbox

Objetivo: receber eventos Stripe com seguranca e persistir Inbox deduplicada.

Escopo:

- Implementar `POST /api/v1/webhooks/stripe`.
- Capturar raw body.
- Validar `Stripe-Signature`, timestamp e secret.
- Persistir Inbox com unique `(provider, provider_event_id)`.
- Tratar duplicidade simultanea como sucesso idempotente.
- Criar migration nova para schema/tabela de Inbox.

Fora de escopo:

- Processar evento financeiro no controller.
- Chamar Ledger.
- Refund.

Dependencias:

- Task 2 ou persistencia minima do Payment.
- Decisao sobre retencao de payload bruto.

Criterios de aceite:

- Assinatura invalida retorna erro e nao persiste Inbox.
- Payload adulterado falha.
- Evento duplicado retorna `2xx` e nao cria segunda linha.
- Inbox indisponivel retorna `5xx`.

Testes obrigatorios:

- Endpoint webhook com assinatura valida/invalida.
- Teste de raw body adulterado.
- Testcontainers PostgreSQL para unique constraint.
- Concorrencia de inserts duplicados.

Documentacao afetada:

- `docs/development/payment-api.md`.
- `docs/openapi/payment.v1.json`.
- Runbook operacional de webhooks se criado.

## 4. Inbox Worker e state machine

Objetivo: processar Inbox de forma assincrona e aplicar eventos externos no
aggregate `Payment`.

Escopo:

- Implementar `PaymentService.Worker`.
- Claim com lease/ownership e retry persistido.
- Mapear eventos Stripe aceitos para comandos internos.
- Aplicar state machine.
- Marcar Inbox como processed, retry ou dead-letter logico.
- Metricas e logs de processamento.

Fora de escopo:

- Chamada ao Ledger, se a task for mantida isolada.
- Refund completo.
- Kafka interno.

Dependencias:

- Task 3.

Criterios de aceite:

- Multiplos workers nao processam a mesma mensagem simultaneamente.
- Worker recupera item apos lease expirado.
- Evento regressivo conhecido e ignorado sem DLQ.
- Poison message vai para dead-letter logico apos limite.

Testes obrigatorios:

- Unitarios da state machine.
- Integration tests com PostgreSQL para claim concorrente.
- Worker tests com fakes.

Documentacao afetada:

- `docs/operations/payment-worker.md` se houver runbook.
- `docs/observability.md` para metricas novas.

## 5. Integracao com Ledger

Objetivo: criar o efeito financeiro no Ledger apos sucesso confirmado do
provider.

Escopo:

- Implementar porta/client para `LedgerService.Api`.
- Obter token service-to-service com `ledger.write`.
- Gerar `Idempotency-Key` deterministica por `paymentId` e operacao.
- Chamar `POST /api/v1/lancamentos`.
- Persistir `ledgerEntryId` e estado `Completed`.
- Tratar timeout desconhecido e retry.
- Configurar resiliencia `HttpResilience:Clients:Ledger`.

Fora de escopo:

- Balance direto.
- Eventos Payment para outros consumidores.
- Refund.

Dependencias:

- Task 4.
- Contrato Ledger existente.

Criterios de aceite:

- Um pagamento confirmado gera no maximo um credito no Ledger.
- Retry apos resposta perdida usa mesma key.
- `409` por payload diferente para mesma key para processamento automatico.
- Payment fica pendente em indisponibilidade prolongada do Ledger.

Testes obrigatorios:

- Unitarios de geracao de key UUID deterministica.
- Worker tests para timeout/retry.
- Integration/smoke com Ledger fake ou stack local controlada.

Documentacao afetada:

- `docs/development/payment-api.md`.
- `docs/observability.md`.
- Runbook de reconciliacao Payment-Ledger.

## 6. Refund e estorno

Objetivo: evoluir refund externo e reflexo financeiro interno.

Escopo:

- Modelar solicitacao de refund.
- Chamar provider com idempotencia externa.
- Receber webhook de refund.
- Solicitar estorno/lancamento compensatorio ao Ledger.
- Suportar refund parcial.

Fora de escopo:

- Rebuild de Balance.
- Ajuste manual de saldo.

Dependencias:

- Tasks 1 a 5.
- Decisao de contrato HTTP de refund.

Criterios de aceite:

- Refund parcial nao excede valor capturado.
- Refund externo confirmado nao altera Balance diretamente.
- Estorno interno usa idempotency key deterministica.

Testes obrigatorios:

- State machine de refund.
- Webhook refund duplicado/fora de ordem.
- Integracao com Ledger fake.

Documentacao afetada:

- Nova spec/ADR se a decisao de refund alterar modelo.
- OpenAPI.

## 7. Testes de integracao e smoke

Objetivo: validar o fluxo ponta a ponta controlado sem depender da Stripe real.

Escopo:

- Testcontainers PostgreSQL.
- Provider fake local.
- Webhook fake assinado.
- Worker + Ledger fake ou stack local.
- Smoke: request -> provider fake -> webhook -> Inbox -> Worker -> Ledger ->
  Kafka -> Balance.

Fora de escopo:

- Carga contra Stripe Sandbox.
- Teste produtivo.

Dependencias:

- Tasks 2 a 5.

Criterios de aceite:

- Smoke falha se Payment nao chega a `Completed`.
- DLQ nao cresce no fluxo feliz.
- Balance reflete evento do Ledger de forma eventual.

Testes obrigatorios:

- Integration tests.
- k6 smoke local opcional com fake provider.

Documentacao afetada:

- `loadtests/k6/README.md`.
- `docs/development/local-development.md`.

## 8. Hardening, observabilidade e revisao final

Objetivo: fechar riscos operacionais antes de tratar o fluxo como baseline da
POC.

Escopo:

- Metricas customizadas.
- Logs sem dados sensiveis.
- Tracing e correlation linkage.
- Runbooks de Inbox DLQ, replay e reconciliacao.
- Retencao/limpeza de Inbox.
- Validacoes OpenAPI, eventos se existirem, build e testes proporcionais.

Fora de escopo:

- Infra produtiva.
- WAF ou secrets manager real, salvo tarefa GCP separada.

Dependencias:

- Tasks anteriores.

Criterios de aceite:

- Operador consegue diagnosticar backlog, poison message e Payment-Ledger
  pendente.
- Metricas nao possuem labels de alta cardinalidade.
- Documentacao e ADRs estao alinhadas ao codigo.

Testes obrigatorios:

- Testes de metricas/logs quando aplicavel.
- Build/test da solution ou subconjunto justificado.
- Lint OpenAPI se contrato HTTP mudou.

Documentacao afetada:

- `docs/observability.md`.
- `docs/operations/*`.
- `docs/roadmap.md` e `docs/maturity.md` se a maturidade mudar.
