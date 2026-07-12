# Documentacao arquitetural

Esta pasta registra a leitura arquitetural atual da POC e o modelo LikeC4 usado para visualizar o sistema.

Arquivos principais:

- `model.c4`: modelo estrutural do ecossistema, containers e componentes reais, incluindo IdentityService, PaymentService, Keycloak, PostgreSQL por schemas, Kafka default, Pub/Sub explicito/legado, Mailpit, Resend e observabilidade.
- `audit-service.md`: papel arquitetural do AuditService como bounded context de auditoria funcional isolado, com schema `audit`, contrato canonico, ausencia de integracao inicial e estrategia futura por Outbox + Kafka.
- `payment-service.md`: papel arquitetural do PaymentService, schema `payment`, ACL fake/Stripe, webhook, Inbox, Worker e integracao idempotente com Ledger.
- `deployment.c4`: modelo de deployment local que associa servicos do `compose.yaml` e overlays locais aos elementos logicos com `instanceOf`, alimentando a aba `Deployments` do LikeC4.
- `views.c4`: views LikeC4 para contexto, containers, fluxo de cadastro no IdentityService, fluxo Kafka, Pub/Sub explicito/legado, observabilidade local e componentes por processo.
- `boundaries.md`: regras de fronteira entre camadas, responsabilidades e anti-patterns.
- `decisions.md`: avaliacao critica, riscos e roadmap pragmatico de evolucao.
- `production-readiness.md`: baseline recomendado para uma evolucao futura em GCP mais proxima de producao, sem declarar prontidao produtiva nem implementar infraestrutura nova.
- [`../README.md`](../README.md): indice geral da documentacao.

Classificacao atual: arquitetura hibrida, com predominancia de Clean Architecture/DDD nos bounded contexts principais. `IdentityService` isola cadastro de usuarios, `MerchantId`, vinculo local com Keycloak e envio de e-mail de boas-vindas; `LedgerService` escreve fatos financeiros e Outbox; `BalanceService` mantem projecao de leitura; `TransferService` orquestra Saga com Worker e Outbox Kafka; `PaymentService` registra pagamentos externos, recebe webhooks Stripe assinados, processa Inbox e materializa credito/estorno via Ledger; `AuditService` registra auditoria funcional por contrato HTTP canonico, ainda sem integracao com os demais dominios.

## Leitura rapida

- O sistema e uma POC de microservicos .NET para identidade, ledger, saldos, transferencias e auditoria funcional.
- Clientes obtem tokens no Keycloak e chamam APIs HTTP protegidas por JWT, audience, scopes e autorizacao por merchant quando aplicavel.
- O PostgreSQL local e unico, mas os servicos usam schemas e roles separados: `identity`, `ledger`, `balance`, `transfer` e `audit`.
- Kafka e o provider padrao dos fluxos principais de mensageria. Pub/Sub continua explicito/legado para Ledger/Balance quando configurado.
- O `IdentityService.Api` cria usuarios no Keycloak, persiste o vinculo local no schema `identity`, despacha domain events depois do commit e envia e-mail por Mailpit no local ou Resend em ambiente real configurado.
- O `AuditService.Api` cria e consulta registros de auditoria funcional no schema `audit`, com `Idempotency-Key`, scopes `audit.*` e contrato agnostico ao chamador, sem worker ou Kafka nesta etapa; a integracao futura proposta usa Outbox + Kafka fora do caminho critico financeiro.

## Estado atual e evolucao futura

Estado atual:

- `IdentityService.Api` e API HTTP sincrona; nao ha Worker de identidade, Outbox de e-mail, fila de e-mail ou DLQ de identidade implementados.
- O e-mail de boas-vindas e side effect intra-processo apos commit local. Falha de e-mail e logada e nao invalida o cadastro.
- Mailpit e ferramenta local; Resend e provider externo real selecionado por configuracao e secret.
- `AuditService.Api` e API HTTP sincrona e isolada; nao ha integracao inicial com Ledger, Balance ou Transfer, nao ha worker de auditoria e nao ha consumo Kafka.

Evolucao futura documentada:

- ADR-0095 registra a possibilidade de evoluir e-mail do IdentityService para Outbox, mensageria, retry, DLQ e worker dedicado. Isso nao esta implementado.
- ADR-0097 registra os criterios para evoluir o AuditService para integracao futura, incluindo quando avaliar Kafka, worker, catalogo de operacoes ou banco fisico proprio.
- ADR-0099 registra Outbox transacional local + Kafka como estrategia futura para integrar auditoria funcional aos bounded contexts financeiros, sem implementacao ativa nesta etapa.

## ADRs relacionadas

- [ADR-0074: Keycloak como identidade principal e Auth.Api legado](../adrs/0074-keycloak-como-identidade-principal.md)
- [ADR-0089: Novo bounded context IdentityService](../adrs/0089-bounded-context-identity-service.md)
- [ADR-0090: Cadastro de usuarios no IdentityService](../adrs/0090-cadastro-usuarios-identity-service.md)
- [ADR-0091: Domain Event Dispatcher no IdentityService](../adrs/0091-domain-event-dispatcher-identity-service.md)
- [ADR-0092: Envio de e-mail no IdentityService](../adrs/0092-envio-email-identity-service.md)
- [ADR-0093: Resend como provider de e-mail do IdentityService](../adrs/0093-resend-email-provider-identity-service.md)
- [ADR-0094: Mailpit local para e-mails do IdentityService](../adrs/0094-mailpit-local-identity-service.md)
- [ADR-0095: Evolucao futura do envio de e-mails do IdentityService](../adrs/0095-evolucao-futura-email-identity-service.md)
- [ADR-0097: Bounded context de auditoria funcional](../adrs/0097-functional-audit-service.md)
- [ADR-0099: Estrategia de integracao assincrona do AuditService](../adrs/0099-audit-async-integration-strategy.md)
- [ADR-0100: Organizacao de solutions por contexto e agregadora](../adrs/0100-organizacao-solutions-contexto-agregadora.md)

## Visualizacao

O site LikeC4 e publicado no GitHub Pages pelo workflow `architecture-pages`:

<https://rodri-oliveira-dev.github.io/poc-arquitetura/>

Para gerar localmente:

```bash
npm ci
npm run architecture:build
```

Detalhes operacionais: [`docs/development/github-pages.md`](../development/github-pages.md).
