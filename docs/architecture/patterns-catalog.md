# Catalogo de padroes arquiteturais e de design

## Objetivo

Este catalogo consolida os padroes arquiteturais, de design e de integracao que aparecem na implementacao real da POC. A leitura e orientada por problema: cada padrao explica qual risco ou dor do repositorio ele reduz, onde aparece, como foi implementado, quais beneficios trouxe e quais custos introduziu.

O documento nao e um inventario teorico. Um item so aparece como implementado quando ha evidencia em codigo, testes, infraestrutura ou configuracao. ADRs e documentos sao usados como contexto de decisao, mas nao bastam sozinhos para classificar um padrao como implementado.

## Como ler este catalogo

Use a tabela de visao geral como indice rapido. Depois consulte a categoria do padrao para entender o fluxo real, as evidencias e os documentos relacionados.

Status usados:

- **Implementado**: existe no runtime atual, com codigo/configuracao/testes coerentes.
- **Parcialmente implementado**: existe em parte, ou esta restrito a alguns contexts/cenarios.
- **Opcional**: existe, mas so entra quando configurado explicitamente.
- **Legado**: continua suportado por compatibilidade ou historico.
- **Planejado**: registrado em ADR/spec/roadmap, sem runtime completo atual.
- **Nao identificado como padrao explicito**: ha mecanismos parecidos, mas nao ha evidencia suficiente para tratar como padrao central do projeto.

## Criterios de classificacao

Foram cruzadas evidencias em `src/`, `tests/`, `infra/`, `docs/adrs/`, `docs/architecture/`, `docs/development/` e `docs/operations/`. Quando uma ADR diz "futuro", mas o codigo atual ja implementa a decisao, o status do catalogo segue o codigo e aponta a necessidade de manter o indice de ADRs coerente.

## Visao geral

| Categoria | Padrao | Status | Aplicacao principal | Problema resolvido |
| --- | --- | --- | --- | --- |
| Arquitetural | Clean Architecture | Implementado | Ledger, Balance, Transfer, Payment, Identity e Audit | Isola regras de negocio de HTTP, EF Core, Kafka, Stripe, Keycloak e Compose |
| Arquitetural | Domain-Driven Design | Implementado | Aggregates financeiros, identidade, pagamento, auditoria | Evita regras financeiras e estados complexos espalhados em controllers e persistencia |
| Arquitetural | Bounded Context | Implementado | Ledger, Balance, Transfer, Payment, Identity, Audit | Separa modelos, linguagem, persistencia e ownership de dominios distintos |
| Arquitetural | Ports and Adapters | Implementado | Mensageria, Stripe, Ledger HTTP, Keycloak, e-mail | Impede que Application/Domain dependam diretamente de providers externos |
| Arquitetural | CQRS pragmatico distribuido | Implementado | Ledger -> Kafka/PubSub -> Balance | Separa escrita transacional de consulta de saldos projetados |
| Arquitetural | Layered Architecture | Implementado | APIs, Workers e composition roots | Organiza entrada HTTP/background e dependencias por camada |
| Arquitetural | Composition Root e DI | Implementado | `*.Api` e `*.Worker` | Controla quais adapters e hosted services existem em cada processo |
| Dominio/aplicacao | Aggregate Root | Implementado | `LedgerEntry`, `TransferenciaSaga`, `Payment`, `User`, `FunctionalAuditRecord` | Protege invariantes e transicoes por raiz controladora |
| Dominio/aplicacao | Value Object | Implementado | `Money`, `Currency`, ids tipados, `Email`, `MerchantId` | Reduz primitive obsession e centraliza validacoes semanticas |
| Dominio/aplicacao | State Machine | Implementado | Payment, Refund, TransferenciaSaga, Inbox, Outbox, estornos | Impede transicoes invalidas em processos assincronos |
| Dominio/aplicacao | Domain Events | Implementado | IdentityService | Desacopla cadastro de usuario de efeitos pos-commit, como e-mail |
| Dominio/aplicacao | Policy Object | Implementado | Ledger reversal | Concentra decisao de estorno em objeto nomeado |
| Dominio/aplicacao | Command/Query + Mediator | Implementado | APIs e workers dos contexts | Separa casos de uso e evita controllers com orquestracao |
| Dominio/aplicacao | Factory | Implementado | Eventos, DbContext design-time, Kafka clients, Resend | Centraliza montagem de objetos tecnicos ou mensagens versionadas |
| Dominio/aplicacao | Strategy | Implementado | Retry/backoff, provider Kafka/PubSub, Stripe/Fake, Mailpit/Resend | Permite trocar comportamento/provider por configuracao e DI |
| Integracao | Outbox Pattern | Implementado | LedgerService e TransferService | Evita commit no banco sem evento recuperavel para publicar |
| Integracao | Inbox Pattern | Implementado | PaymentService webhooks Stripe | Separa recepcao HTTP de processamento duravel e idempotente |
| Integracao | Idempotent Consumer | Implementado | BalanceService, AuditService.Worker | Evita aplicar o mesmo evento mais de uma vez em entrega at-least-once |
| Integracao | Idempotency Key | Implementado | Ledger, Transfer, Payment, Identity, Audit | Torna retries HTTP seguros apos timeout ou resposta perdida |
| Integracao | Saga orquestrada | Implementado | TransferService | Coordena debito, credito e compensacao sem transacao distribuida |
| Integracao | Compensating Transaction | Implementado | Transfer, Ledger estorno, Payment refund, Identity Keycloak | Compensa efeitos ja realizados quando etapa posterior falha |
| Integracao | Eventual Consistency | Implementado | Ledger -> Balance; Payment -> Ledger -> Balance | Permite separar ownership e escala sem transacao distribuida |
| Integracao | Event-Driven Architecture | Parcialmente implementado | Fluxos financeiros e eventos de Saga | Usa eventos onde ha consumo real, mantendo HTTP onde faz sentido |
| Integracao | Versionamento de eventos | Implementado | `LedgerEntryCreated.v1` e `.v2` | Evolui contrato sem quebrar consumidores antigos |
| Integracao | Dead Letter Queue | Implementado | Outbox Ledger, Balance DLQ, Transfer DLQ, Payment Inbox, Audit DLQ | Isola poison messages e falhas definitivas sem retry infinito |
| Integracao | Replay e Projection Rebuild | Implementado | Balance e runbooks operacionais | Recupera ou compara projecoes sem declarar Event Sourcing |
| Resiliencia | Retry e retry persistido | Implementado | HTTP resiliente, Outbox, Inbox, Saga | Recupera falhas transitorias de chamadas e processamento |
| Resiliencia | Exponential Backoff | Implementado | Outbox, Inbox, Saga e docs de DLQ | Evita pressionar continuamente dependencias indisponiveis |
| Resiliencia | Circuit Breaker | Implementado | Clientes HTTP resilientes | Interrompe chamadas repetidamente destinadas a falhar |
| Resiliencia | Timeout | Implementado | JWKS, Keycloak, Ledger, Stripe e workers | Evita chamadas indefinidas e protege threads/conexoes |
| Resiliencia | Bulkhead explicito | Nao identificado como padrao explicito | Nao ha implementacao dedicada | Evita superestimar separacao de workers como Bulkhead |
| Resiliencia | Resultado desconhecido | Implementado | Payment -> Ledger, Transfer -> Ledger, Stripe | Combina idempotencia, retry e consulta de estado para reduzir duplicidade |
| Persistencia | Repository | Implementado | Repositories por agregado/projecao | Encapsula acesso a dados sem Generic Repository artificial |
| Persistencia | Unit of Work | Implementado | DbContexts de Ledger, Balance, Transfer, Payment | Coordena alteracoes locais em transacao unica |
| Persistencia | Database per Service logico | Implementado | PostgreSQL unico com schemas por servico | Preserva ownership de dados na POC sem multiplas instancias fisicas |
| Persistencia | Pessimistic Lock / Claim / Lease | Implementado | Outbox, Inbox, Saga, estornos, Balance | Evita lost update, duplo processamento e trabalho preso apos morte do worker |
| Persistencia | Optimistic Concurrency | Nao identificado como padrao explicito | Nao ha token/versionamento central | Evita documentar conflito otimista onde ha locks/constraints |
| Persistencia | Unique Constraint como ultima defesa | Implementado | Idempotencia, Inbox, eventos processados, chaves externas | Bloqueia duplicidade mesmo com concorrencia simultanea |
| Seguranca | JWT/OIDC com JWKS | Implementado | APIs de negocio com Keycloak | Valida tokens localmente sem introspeccao remota por request |
| Seguranca | Identity Provider | Implementado | Keycloak + IdentityService | Separa emissor de tokens de cadastro/vinculo local de usuarios |
| Seguranca | Policy-Based Authorization | Implementado | Scopes e merchant authorization | Mitiga acesso indevido entre merchants e BOLA |
| Seguranca | Rate Limiting e Security Headers | Implementado | ApiDefaults e Nginx local | Limita abuso e reduz riscos comuns na borda HTTP |
| Seguranca | Secret management local | Parcialmente implementado | `.env.local`, user-secrets, placeholders | Evita credenciais reais no repo, com lacunas historicas em placeholders |
| Observabilidade | Correlation ID | Implementado | HTTP, Outbox, Kafka, Nginx | Correlaciona logs e mensagens de uma mesma operacao |
| Observabilidade | Distributed Tracing | Implementado | HTTP -> Outbox -> Kafka -> consumidores | Reconstrui caminho distribuido com contexto W3C |
| Observabilidade | Metrics | Implementado | System.Diagnostics.Metrics, OpenTelemetry, Prometheus/Grafana | Observa volume, latencia, falha, retry, backlog e circuit breaker |
| Observabilidade | Centralized Logging | Implementado | Loki e Grafana Alloy opcionais | Pesquisa logs de containers em ponto central |
| Observabilidade | Health, Liveness e Readiness | Implementado | APIs e stack local | Diferencia processo vivo de processo pronto para trafego |
| Observabilidade | Runbooks | Implementado | `docs/operations` | Transforma recuperacao operacional em procedimento repetivel |
| Infraestrutura | Reverse Proxy / Edge Gateway local | Opcional | Nginx local | Fornece borda HTTPS, roteamento local e headers padronizados |
| Infraestrutura | Load Balancer local | Opcional | Nginx `least_conn` para LedgerService.Api | Demonstra escala horizontal local do Ledger |
| Infraestrutura | API Gateway completo | Nao identificado como padrao explicito | Nginx local nao agrega contratos nem quotas avancadas | Evita chamar reverse proxy de gateway completo |
| Infraestrutura | Workers como processos separados | Implementado | Ledger, Balance, Transfer, Payment, Audit | Permite ciclo de vida, escala e falha independentes das APIs |
| Infraestrutura | Containerization local | Implementado | Docker Compose | Padroniza runtime local sem declarar desenho produtivo final |

## Padroes arquiteturais

### Clean Architecture

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

Sem separacao de camadas, regras de lancamento, pagamento, transferencia, cadastro e auditoria ficariam presas a ASP.NET Core, EF Core, Kafka, Stripe ou Keycloak.

#### Onde foi aplicado

Ledger, Balance, Transfer, Payment, Identity e Audit usam projetos `Api`, `Application`, `Domain` e `Infrastructure`; os contexts assincronos tambem usam `Worker`.

#### Como funciona neste repositorio

`Api` recebe HTTP, auth, Swagger e composition root. `Application` orquestra casos de uso, comandos, queries, idempotencia e transacao. `Domain` guarda aggregates, value objects e invariantes. `Infrastructure` implementa EF Core, repositories e adapters. `Worker` registra hosted services e adapters de mensageria/processamento sem superficie HTTP.

#### Beneficios obtidos

- Reduz acoplamento com frameworks e providers.
- Permite testar Application/Domain sem broker real.
- Mantem APIs e workers com composition roots diferentes.

#### Trade-offs e limitacoes

- Aumenta quantidade de projetos e abstracoes.
- Ha inconsistencias historicas: alguns repositories ficam no Domain, outros em Application; Outbox do Ledger ainda mistura preocupacao tecnica com fluxo de aplicacao.

#### Evidencias

- [`docs/architecture/boundaries.md`](boundaries.md)
- [`src/ledger/LedgerService.Application/LedgerService.Application.csproj`](../../src/ledger/LedgerService.Application/LedgerService.Application.csproj)
- [`src/payment/PaymentService.Domain/Payments/Payment.cs`](../../src/payment/PaymentService.Domain/Payments/Payment.cs)
- [`tests/Architecture.Tests/ArchitectureDependencyTests.cs`](../../tests/Architecture.Tests/ArchitectureDependencyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0002](../adrs/0002-clean-architecture-ddd-por-servico.md)
- [ADR-0034](../adrs/0034-boundaries-arquiteturais-e-estrutura-de-camadas.md)

### Domain-Driven Design

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

O dominio financeiro possui estados e invariantes que nao podem ficar como `if`s dispersos em controllers, handlers e mappings EF Core.

#### Onde foi aplicado

Aggregates e value objects aparecem nos contexts Ledger, Balance, Transfer, Payment, Identity e Audit.

#### Como funciona neste repositorio

Cada bounded context usa linguagem propria. `LedgerEntry` representa fato financeiro; `DailyBalance` e projecao; `TransferenciaSaga` coordena uma transferencia; `Payment` modela estado do pagamento externo e refunds; `User` representa cadastro local vinculado ao Keycloak; `FunctionalAuditRecord` representa auditoria canonica.

#### Beneficios obtidos

- Centraliza invariantes nos objetos de dominio.
- Evita contaminacao entre linguagens de pagamento, ledger, saldo e auditoria.
- Facilita testes de transicoes de estado.

#### Trade-offs e limitacoes

- Exige disciplina para nao criar "DDD cerimonial".
- Nem todos os contexts tem a mesma maturidade de modelo.

#### Evidencias

- [`src/transfer/TransferService.Domain/Sagas/TransferenciaSaga.cs`](../../src/transfer/TransferService.Domain/Sagas/TransferenciaSaga.cs)
- [`src/payment/PaymentService.Domain/Payments/Payment.cs`](../../src/payment/PaymentService.Domain/Payments/Payment.cs)
- [`src/identity/IdentityService.Domain/Users/User.cs`](../../src/identity/IdentityService.Domain/Users/User.cs)
- [`tests/transfer/TransferService.UnitTests/Domain/Sagas/TransferenciaSagaTests.cs`](../../tests/transfer/TransferService.UnitTests/Domain/Sagas/TransferenciaSagaTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0002](../adrs/0002-clean-architecture-ddd-por-servico.md)
- [Boundaries arquiteturais](boundaries.md)

### Bounded Context

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

Um unico modelo para lancamento, saldo, transferencia, pagamento externo, identidade e auditoria misturaria responsabilidades, schemas e linguagem de negocio.

#### Onde foi aplicado

`LedgerService`, `BalanceService`, `TransferService`, `PaymentService`, `IdentityService` e `AuditService`.

#### Como funciona neste repositorio

Cada context tem pasta em `src/<contexto>`, testes em `tests/<contexto>` e, no runtime local, schema PostgreSQL proprio. Ledger e dono do fato financeiro; Balance e dono da projecao; Transfer orquestra Saga; Payment coordena provider externo e materializacao no Ledger; Identity cadastra usuarios e vinculo local; Audit registra trilha funcional canonica.

#### Beneficios obtidos

- Preserva ownership de dados e linguagem.
- Reduz acoplamento entre contexts.
- Permite que cada contexto tenha API/Worker e validacoes proprias.

#### Trade-offs e limitacoes

- Aumenta custo de navegacao e operacao local.
- O `AGENTS.md` ainda nao lista `payment` entre contextos atuais, embora o codigo e a documentacao o tratem como context real.

#### Evidencias

- [`src/payment`](../../src/payment)
- [`src/audit`](../../src/audit)
- [`infra/postgres/init/001-create-schemas-users-permissions.sql`](../../infra/postgres/init/001-create-schemas-users-permissions.sql)
- [`docs/architecture/model.c4`](model.c4)

#### ADRs e documentacao relacionados

- [ADR-0001](../adrs/0001-separar-ledger-e-balance-com-projecao.md)
- [ADR-0089](../adrs/0089-bounded-context-identity-service.md)
- [ADR-0097](../adrs/0097-functional-audit-service.md)
- [ADR-0101](../adrs/0101-payment-service-bounded-context.md)

### Hexagonal Architecture / Ports and Adapters

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

Sem portas, Application e Domain dependeriam de Stripe, Kafka, Pub/Sub, Keycloak, HTTP, SMTP/Resend e EF Core.

#### Onde foi aplicado

Mensageria Ledger/Balance, Stripe/Fake provider, Ledger HTTP no Payment, Keycloak Admin API, e-mail Identity e DLQ.

#### Como funciona neste repositorio

Portas como `IPaymentGateway`, `ILedgerEntryGateway`, `IIdentityProviderUserService`, `IEmailSender`, `IOutboxMessagePublisher` e `IDeadLetterPublisher` ficam em Application/Worker boundaries; adapters concretos ficam em Infrastructure ou Worker composition roots.

#### Beneficios obtidos

- Testes substituem adapters externos por fakes.
- Providers podem ser trocados por DI/configuracao.
- Detalhes de transporte ficam fora de Domain/Application.

#### Trade-offs e limitacoes

- Ha custo de mapeamento entre modelos internos e externos.
- Algumas interfaces existem para boundaries reais; criar interfaces sem variacao continua sendo evitado.

#### Evidencias

- [`src/payment/PaymentService.Application/Abstractions/Gateway/IPaymentGateway.cs`](../../src/payment/PaymentService.Application/Abstractions/Gateway/IPaymentGateway.cs)
- [`src/payment/PaymentService.Infrastructure/Gateway/StripePaymentGateway.cs`](../../src/payment/PaymentService.Infrastructure/Gateway/StripePaymentGateway.cs)
- [`src/ledger/LedgerService.Worker/Messaging/Abstractions/IOutboxMessagePublisher.cs`](../../src/ledger/LedgerService.Worker/Messaging/Abstractions/IOutboxMessagePublisher.cs)
- [`src/balance/BalanceService.Worker/Messaging/Abstractions/IDeadLetterPublisher.cs`](../../src/balance/BalanceService.Worker/Messaging/Abstractions/IDeadLetterPublisher.cs)

#### ADRs e documentacao relacionados

- [ADR-0075](../adrs/0075-mensageria-ports-adapters-kafka-provider.md)
- [ADR-0102](../adrs/0102-stripe-anti-corruption-layer.md)

### CQRS pragmatico distribuido

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

Registrar fatos financeiros e consultar saldos possuem requisitos diferentes. Se o mesmo modelo atendesse escrita e leitura, o Ledger tenderia a acumular responsabilidade de projecao.

#### Onde foi aplicado

Fluxo Ledger -> evento financeiro -> Balance.

#### Como funciona neste repositorio

`LedgerService.Api` grava lancamentos e Outbox; `LedgerService.Worker` publica `LedgerEntryCreated.v2`; `BalanceService.Worker` consome e atualiza `daily_balances`/`processed_events`; `BalanceService.Api` consulta a projecao.

#### Beneficios obtidos

- Separa fonte de verdade de projecao.
- Permite idempotencia e rebuild da projecao.
- Mantem Balance sem comandos financeiros.

#### Trade-offs e limitacoes

- A leitura e eventualmente consistente.
- Nao e CQRS completo em todos os bounded contexts; e uma variante pragmatica distribuida do fluxo financeiro.

#### Evidencias

- [`docs/architecture/views.c4`](views.c4)
- [`src/ledger/LedgerService.Application/Lancamentos/Commands/CreateLancamentoCommandHandler.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Commands/CreateLancamentoCommandHandler.cs)
- [`src/balance/BalanceService.Application/Balances/Commands/ApplyLedgerEntryCreatedHandler.cs`](../../src/balance/BalanceService.Application/Balances/Commands/ApplyLedgerEntryCreatedHandler.cs)
- [`tests/balance/BalanceService.Worker.Tests/Messaging/Contracts/LedgerEntryCreatedConsumerContractTests.cs`](../../tests/balance/BalanceService.Worker.Tests/Messaging/Contracts/LedgerEntryCreatedConsumerContractTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0001](../adrs/0001-separar-ledger-e-balance-com-projecao.md)
- [Contratos logicos de eventos](../events/README.md)

### Layered Architecture

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

APIs e workers precisam de pontos claros para contratos HTTP, autorizacao, workers, persistencia e regras de negocio.

#### Onde foi aplicado

Entrega HTTP dos projetos `*.Api`, composition roots dos `*.Worker` e defaults compartilhados em `src/Shared`.

#### Como funciona neste repositorio

Controllers/endpoints fazem bind/map e chamam MediatR/casos de uso; Program.cs registra auth, Swagger, rate limit, health/readiness e DI; workers registram BackgroundServices, mensageria, DLQ e clients externos.

#### Beneficios obtidos

- Facilita ler onde uma responsabilidade deve ficar.
- Evita que controller processe regra de negocio.

#### Trade-offs e limitacoes

- Readiness acessa DbContext diretamente no `Program.cs`; e aceitavel como check operacional, mas deve permanecer restrito.

#### Evidencias

- [`src/ledger/LedgerService.Api/Controllers/LancamentosController.cs`](../../src/ledger/LedgerService.Api/Controllers/LancamentosController.cs)
- [`src/payment/PaymentService.Api/Controllers/StripeWebhooksController.cs`](../../src/payment/PaymentService.Api/Controllers/StripeWebhooksController.cs)
- [`src/Shared/ApiDefaults`](../../src/Shared/ApiDefaults)
- [`docs/architecture/boundaries.md`](boundaries.md)

#### ADRs e documentacao relacionados

- [ADR-0016](../adrs/0016-contrato-http-explicito-swagger-e-controllers-magros.md)
- [ADR-0034](../adrs/0034-boundaries-arquiteturais-e-estrutura-de-camadas.md)

### Composition Root e Dependency Injection

**Categoria:** Arquitetural  
**Status:** Implementado

#### Problema resolvido

APIs nao devem hospedar consumers e workers por acidente; workers nao devem expor Swagger, controllers ou CORS.

#### Onde foi aplicado

`Program.cs` e extensoes de DI de APIs e Workers.

#### Como funciona neste repositorio

Cada executavel registra somente os adapters necessarios. Ledger/Balance selecionam Kafka por default e Pub/Sub somente quando `Messaging:Provider=PubSub`. Transfer usa Kafka-only; Payment Worker registra Inbox e materializacao Ledger; Audit Worker registra consumer opcional.

#### Beneficios obtidos

- Evita hosted services duplicados na API.
- Permite escalar e diagnosticar processos separadamente.
- Torna a selecao de providers explicita.

#### Trade-offs e limitacoes

- Requer testes de composition para impedir regressao.

#### Evidencias

- [`src/ledger/LedgerService.Worker/Extensions/WorkerCompositionExtensions.cs`](../../src/ledger/LedgerService.Worker/Extensions/WorkerCompositionExtensions.cs)
- [`src/balance/BalanceService.Worker/Extensions/WorkerCompositionExtensions.cs`](../../src/balance/BalanceService.Worker/Extensions/WorkerCompositionExtensions.cs)
- [`tests/ledger/LedgerService.Worker.Tests/Composition/ProcessCompositionPolicyTests.cs`](../../tests/ledger/LedgerService.Worker.Tests/Composition/ProcessCompositionPolicyTests.cs)
- [`tests/balance/BalanceService.Worker.Tests/Composition/ProcessCompositionPolicyTests.cs`](../../tests/balance/BalanceService.Worker.Tests/Composition/ProcessCompositionPolicyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0064](../adrs/0064-ledger-worker-processo-dedicado.md)
- [ADR-0067](../adrs/0067-separacao-workers-processos-api.md)
- [ADR-0088](../adrs/0088-kafka-default-ledger-balance-workers.md)

## Padroes de dominio e aplicacao

### Aggregate Root

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Estados financeiros e de identidade precisam ser alterados por pontos controlados para impedir combinacoes invalidas.

#### Onde foi aplicado

`LedgerEntry`, `DailyBalance`, `TransferenciaSaga`, `Payment`, `User` e `FunctionalAuditRecord`.

#### Como funciona neste repositorio

Os aggregates encapsulam transicoes como criar lancamento compensatorio, aplicar evento de saldo, marcar etapas da Saga, processar Payment/Refund, registrar usuario e validar auditoria funcional.

#### Beneficios obtidos

- Invariantes ficam perto do estado.
- Testes de dominio cobrem transicoes sem infraestrutura.

#### Trade-offs e limitacoes

- Alguns estados tecnicos, como Outbox, ainda vivem proximos ao dominio em Ledger por decisao pragmatica da POC.

#### Evidencias

- [`src/ledger/LedgerService.Domain/Entities/LedgerEntry.cs`](../../src/ledger/LedgerService.Domain/Entities/LedgerEntry.cs)
- [`src/payment/PaymentService.Domain/Payments/Payment.cs`](../../src/payment/PaymentService.Domain/Payments/Payment.cs)
- [`src/transfer/TransferService.Domain/Sagas/TransferenciaSaga.cs`](../../src/transfer/TransferService.Domain/Sagas/TransferenciaSaga.cs)
- [`tests/payment/PaymentService.UnitTests`](../../tests/payment/PaymentService.UnitTests)

#### ADRs e documentacao relacionados

- [ADR-0002](../adrs/0002-clean-architecture-ddd-por-servico.md)
- [Arquitetura do PaymentService](payment-service.md)

### Value Object

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Strings e decimals soltos facilitam valores invalidos, moedas ambiguas e ids trocados entre contexts.

#### Onde foi aplicado

Money/currency e ids tipados em Payment/Transfer/Identity/Audit, alem de value objects de e-mail, username e merchant.

#### Como funciona neste repositorio

Value objects validam formato, escala, moeda, nulidade e identidade logo na criacao, antes de chegar ao banco ou ao provider externo.

#### Beneficios obtidos

- Reduz primitive obsession.
- Documenta semantica por tipo.
- Impede algumas classes de erro em tempo de compilacao.

#### Trade-offs e limitacoes

- Exige mapeamento EF Core e conversao em contratos HTTP.

#### Evidencias

- [`src/payment/PaymentService.Domain/Payments/Money.cs`](../../src/payment/PaymentService.Domain/Payments/Money.cs)
- [`src/identity/IdentityService.Domain/Users/Email.cs`](../../src/identity/IdentityService.Domain/Users/Email.cs)
- [`src/transfer/TransferService.Domain/Sagas/TransferAmount.cs`](../../src/transfer/TransferService.Domain/Sagas/TransferAmount.cs)
- [`src/audit/AuditService.Domain/FunctionalAuditing/FunctionalAuditRecord.cs`](../../src/audit/AuditService.Domain/FunctionalAuditing/FunctionalAuditRecord.cs)

#### ADRs e documentacao relacionados

- [ADR-0002](../adrs/0002-clean-architecture-ddd-por-servico.md)

### State Machine

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Processos assincronos podem receber mensagens duplicadas, atrasadas ou falhar no meio. Sem estados monotonicamente controlados, seria facil regredir Payment, reabrir Saga concluida ou processar Inbox indefinidamente.

#### Onde foi aplicado

`Payment`, `PaymentRefund`, `TransferenciaSaga`, Inbox do Payment, Outbox do Ledger/Transfer, estornos e reprocessamentos.

#### Como funciona neste repositorio

Metodos `Mark*`, `Register*`, `Claim*` e enums de status controlam transicoes. Workers processam apenas estados elegiveis e persistem `NextRetryAt`, `LockedUntil` ou status final conforme o caso.

#### Beneficios obtidos

- Evita regressao e estados impossiveis.
- Ajuda retry, DLQ e operacao.
- Diferencia sucesso externo de sucesso financeiro interno.

#### Trade-offs e limitacoes

- Aumenta a quantidade de estados que precisam ser documentados e testados.

#### Evidencias

- [`src/payment/PaymentService.Domain/Payments/PaymentStatus.cs`](../../src/payment/PaymentService.Domain/Payments/PaymentStatus.cs)
- [`src/payment/PaymentService.Domain/Payments/RefundStatus.cs`](../../src/payment/PaymentService.Domain/Payments/RefundStatus.cs)
- [`src/transfer/TransferService.Domain/Sagas/TransferenciaSagaStatus.cs`](../../src/transfer/TransferService.Domain/Sagas/TransferenciaSagaStatus.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs)

#### ADRs e documentacao relacionados

- [ADR-0105](../adrs/0105-payment-provider-event-ordering-deduplication.md)
- [Spec state machine Payment](../specs/payment-stripe/state-machine.md)

### Domain Events

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

O cadastro de usuario precisa disparar efeitos secundarios, como e-mail de boas-vindas, sem colocar esse detalhe dentro do aggregate `User`.

#### Onde foi aplicado

IdentityService, com `UserRegisteredDomainEvent`.

#### Como funciona neste repositorio

`User.Create` adiciona o domain event. `IdentityDbContext` salva o usuario e, apos o commit local, chama o dispatcher. Handlers registram log e enviam e-mail por `IEmailSender`.

#### Beneficios obtidos

- Desacopla cadastro de side effects.
- Mantem e-mail atras de porta configuravel.

#### Trade-offs e limitacoes

- O dispatch atual e pos-commit intra-processo, sem Outbox, retry duravel ou DLQ; falhas sao logadas e nao desfazem cadastro.

#### Evidencias

- [`src/identity/IdentityService.Domain/Users/UserRegisteredDomainEvent.cs`](../../src/identity/IdentityService.Domain/Users/UserRegisteredDomainEvent.cs)
- [`src/identity/IdentityService.Infrastructure/Persistence/IdentityDbContext.cs`](../../src/identity/IdentityService.Infrastructure/Persistence/IdentityDbContext.cs)
- [`src/identity/IdentityService.Infrastructure/DomainEvents/DomainEventDispatcher.cs`](../../src/identity/IdentityService.Infrastructure/DomainEvents/DomainEventDispatcher.cs)
- [`src/identity/IdentityService.Infrastructure/DomainEvents/SendWelcomeEmailOnUserRegisteredDomainEventHandler.cs`](../../src/identity/IdentityService.Infrastructure/DomainEvents/SendWelcomeEmailOnUserRegisteredDomainEventHandler.cs)

#### ADRs e documentacao relacionados

- [ADR-0091](../adrs/0091-domain-event-dispatcher-identity-service.md)
- [ADR-0092](../adrs/0092-envio-email-identity-service.md)
- [ADR-0095](../adrs/0095-evolucao-futura-email-identity-service.md)

### Policy Object

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Decisoes de estorno poderiam ficar duplicadas entre solicitacao e processamento.

#### Onde foi aplicado

LedgerService, na politica de reversao/estorno.

#### Como funciona neste repositorio

`LedgerReversalPolicy` concentra verificacoes para solicitar e concluir estorno, e os handlers chamam a politica antes de transicionar estado ou criar lancamento compensatorio.

#### Beneficios obtidos

- Nomeia a regra de negocio.
- Reduz condicionais dispersas.

#### Trade-offs e limitacoes

- A politica precisa continuar livre de infraestrutura e autorizacao HTTP.

#### Evidencias

- [`src/ledger/LedgerService.Domain/Policies/LedgerReversalPolicy.cs`](../../src/ledger/LedgerService.Domain/Policies/LedgerReversalPolicy.cs)
- [`src/ledger/LedgerService.Application/Lancamentos/Commands/SolicitarEstornoLancamentoHandler.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Commands/SolicitarEstornoLancamentoHandler.cs)
- [`src/ledger/LedgerService.Application/Lancamentos/Commands/ProcessarEstornoLancamentoHandler.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Commands/ProcessarEstornoLancamentoHandler.cs)
- [`tests/ledger/LedgerService.UnitTests/Domain/Policies/LedgerReversalPolicyTests.cs`](../../tests/ledger/LedgerService.UnitTests/Domain/Policies/LedgerReversalPolicyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0049](../adrs/0049-solicitacao-assincrona-estorno-lancamento-mediator.md)
- [ADR-0050](../adrs/0050-processamento-assincrono-estornos-ledger.md)

### Command/Query e Mediator

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Controllers e workers precisavam chamar casos de uso sem carregar orquestracao de negocio, validacao e transacao dentro da borda HTTP/background.

#### Onde foi aplicado

Ledger, Balance, Transfer, Payment e Audit usam comandos/queries MediatR em graus diferentes.

#### Como funciona neste repositorio

Requests HTTP e processors de mensagem montam commands/queries. Handlers executam validacao de caso de uso, idempotencia, transacao e acesso por portas.

#### Beneficios obtidos

- Casos de uso ficam nomeados.
- Controllers permanecem finos.
- Workers reutilizam handlers sem depender de HTTP.

#### Trade-offs e limitacoes

- MediatR e um framework adicional; em casos simples pode ser overhead.

#### Evidencias

- [`src/ledger/LedgerService.Application/Lancamentos/Commands/CreateLancamentoCommand.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Commands/CreateLancamentoCommand.cs)
- [`src/balance/BalanceService.Application/Balances/Queries/GetDailyBalanceQuery.cs`](../../src/balance/BalanceService.Application/Balances/Queries/GetDailyBalanceQuery.cs)
- [`src/payment/PaymentService.Application/Payments/Commands/CreatePaymentCommandHandler.cs`](../../src/payment/PaymentService.Application/Payments/Commands/CreatePaymentCommandHandler.cs)
- [`src/audit/AuditService.Application/FunctionalAuditing/CreateAuditRecord/CreateAuditRecordCommandHandler.cs`](../../src/audit/AuditService.Application/FunctionalAuditing/CreateAuditRecord/CreateAuditRecordCommandHandler.cs)

#### ADRs e documentacao relacionados

- [ADR-0040](../adrs/0040-padronizacao-commands-queries-validacao-entrada-apis.md)
- [ADR-0049](../adrs/0049-solicitacao-assincrona-estorno-lancamento-mediator.md)

### Factory

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Eventos versionados, DbContexts design-time e clients Kafka/Resend exigem montagem repetivel e padronizada.

#### Onde foi aplicado

Factories de eventos Ledger/Transfer, factories de DbContext, factories Kafka e Resend client factory.

#### Como funciona neste repositorio

Factories constroem payloads versionados com headers/keys coerentes, criam DbContexts para migrations/design-time ou encapsulam criacao/configuracao de clients externos. Nem toda classe `Factory` e tratada como GoF Factory Method; o status aqui e uso pragmatico de fabricas.

#### Beneficios obtidos

- Reduz duplicacao de montagem.
- Centraliza convencoes de contrato.

#### Trade-offs e limitacoes

- Pode virar abstracao desnecessaria se nao houver regra de montagem real.

#### Evidencias

- [`src/ledger/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedEventFactory.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedEventFactory.cs)
- [`src/transfer/TransferService.Application/Transferencias/Events/TransferenciaSagaEventFactory.cs`](../../src/transfer/TransferService.Application/Transferencias/Events/TransferenciaSagaEventFactory.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/PaymentDbContextFactory.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/PaymentDbContextFactory.cs)
- [`src/identity/IdentityService.Infrastructure/Email/ResendClientFactory.cs`](../../src/identity/IdentityService.Infrastructure/Email/ResendClientFactory.cs)

#### ADRs e documentacao relacionados

- [Contratos logicos de eventos](../events/README.md)
- [ADR-0084](../adrs/0084-ledger-entry-created-v2-currency-explicita.md)

### Strategy

**Categoria:** Dominio e aplicacao  
**Status:** Implementado

#### Problema resolvido

Retry, mensageria, provider de pagamento e provider de e-mail variam por ambiente/cenario sem mudar os casos de uso consumidores.

#### Onde foi aplicado

`IRetryStrategy`, Kafka/PubSub, Stripe/Fake payment gateway e Mailpit/Resend.

#### Como funciona neste repositorio

DI registra implementacoes diferentes conforme configuracao. Ledger/Balance trocam provider de mensageria por `Messaging:Provider`; Payment troca gateway por `PaymentGateway:Provider`; Identity troca e-mail por `Email:Provider`.

#### Beneficios obtidos

- Mantem Application estavel.
- Permite testar local com fakes.
- Preserva provider legado Pub/Sub sem misturar semanticas com Kafka.

#### Trade-offs e limitacoes

- Providers diferentes possuem semanticas diferentes; a porta nao deve fingir que Pub/Sub tem partition/offset ou que Stripe e igual ao fake.

#### Evidencias

- [`src/ledger/LedgerService.Application/Outbox/Retry/IRetryStrategy.cs`](../../src/ledger/LedgerService.Application/Outbox/Retry/IRetryStrategy.cs)
- [`src/ledger/LedgerService.Application/Outbox/Retry/ExponentialBackoffRetryStrategy.cs`](../../src/ledger/LedgerService.Application/Outbox/Retry/ExponentialBackoffRetryStrategy.cs)
- [`src/payment/PaymentService.Infrastructure/DependencyInjection.cs`](../../src/payment/PaymentService.Infrastructure/DependencyInjection.cs)
- [`src/identity/IdentityService.Infrastructure/DependencyInjection.cs`](../../src/identity/IdentityService.Infrastructure/DependencyInjection.cs)

#### ADRs e documentacao relacionados

- [ADR-0088](../adrs/0088-kafka-default-ledger-balance-workers.md)
- [ADR-0093](../adrs/0093-resend-email-provider-identity-service.md)
- [ADR-0094](../adrs/0094-mailpit-local-identity-service.md)

## Padroes de integracao e sistemas distribuidos

### Outbox Pattern

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Sem Outbox, o Ledger poderia confirmar um lancamento no PostgreSQL e falhar antes de publicar o evento para o Balance. O saldo ficaria desatualizado sem mensagem recuperavel.

#### Onde foi aplicado

LedgerService para eventos financeiros/operacionais e TransferService para eventos da Saga.

#### Como funciona neste repositorio

Na transacao local, o caso de uso grava estado e Outbox. Um Worker reclama mensagens elegiveis, publica no provider configurado e marca como processada. Falhas temporarias usam retry/backoff; falhas definitivas podem ir para DeadLetter.

#### Beneficios obtidos

- Evita perda silenciosa de eventos apos commit.
- Permite entrega at-least-once.
- Suporta requeue operacional de Outbox.

#### Trade-offs e limitacoes

- Consumidores precisam ser idempotentes.
- Ha atraso entre commit e publicacao.
- Outbox nao garante exatamente-uma-vez ponta a ponta.

#### Evidencias

- [`src/ledger/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedOutboxWriter.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Services/LedgerEntryCreatedOutboxWriter.cs)
- [`src/ledger/LedgerService.Worker/Outbox/OutboxPublisherService.cs`](../../src/ledger/LedgerService.Worker/Outbox/OutboxPublisherService.cs)
- [`src/transfer/TransferService.Worker/Outbox/TransferenciaOutboxPublisherService.cs`](../../src/transfer/TransferService.Worker/Outbox/TransferenciaOutboxPublisherService.cs)
- [`tests/ledger/LedgerService.IntegrationTests/Outbox/OutboxPublisherWorkerTests.cs`](../../tests/ledger/LedgerService.IntegrationTests/Outbox/OutboxPublisherWorkerTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0003](../adrs/0003-integracao-assincrona-kafka-com-outbox.md)
- [ADR-0070](../adrs/0070-dlq-outbox-banco-backoff-requeue.md)
- [Kafka, Outbox e DLQ](../development/kafka-outbox.md)

### Inbox Pattern

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Webhooks Stripe podem chegar duplicados, simultaneos, atrasados ou fora de ordem. Processar o efeito financeiro dentro do request tornaria timeout e retry do provider perigosos.

#### Onde foi aplicado

PaymentService, no endpoint de webhook Stripe e Worker de Inbox.

#### Como funciona neste repositorio

O endpoint valida assinatura/raw body e persiste o evento em Inbox com unique `(provider, provider_event_id)`. O Worker faz claim, aplica state machine do Payment/Refund, agenda retry persistido ou DeadLetter logico.

#### Beneficios obtidos

- Responde rapidamente ao provider apos persistencia.
- Deduplica entrada externa no banco.
- Permite recuperacao por backlog/retry.

#### Trade-offs e limitacoes

- O efeito financeiro passa a ser eventual.
- Payload bruto exige cuidado de retencao e seguranca.

#### Evidencias

- [`src/payment/PaymentService.Api/Controllers/StripeWebhooksController.cs`](../../src/payment/PaymentService.Api/Controllers/StripeWebhooksController.cs)
- [`src/payment/PaymentService.Application/Payments/Webhooks/ReceiveStripeWebhookCommandHandler.cs`](../../src/payment/PaymentService.Application/Payments/Webhooks/ReceiveStripeWebhookCommandHandler.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs)
- [`src/payment/PaymentService.Worker/HostedServices/PaymentInboxWorkerService.cs`](../../src/payment/PaymentService.Worker/HostedServices/PaymentInboxWorkerService.cs)

#### ADRs e documentacao relacionados

- [ADR-0103](../adrs/0103-inbox-pattern-webhooks-stripe.md)
- [Operacao do PaymentService.Worker](../operations/payment-worker.md)

### Idempotent Consumer

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Kafka, Pub/Sub e replay podem entregar o mesmo evento mais de uma vez. Sem deduplicacao, saldo e auditoria poderiam ser aplicados duplicadamente.

#### Onde foi aplicado

BalanceService com `ProcessedEvent`; AuditService.Worker com `source_event_id`.

#### Como funciona neste repositorio

Balance registra `EventId` em tabela com indice unico e atualiza saldo dentro da mesma transacao. Audit usa `source_event_id`/idempotency key para evitar duplicidade de registros funcionais.

#### Beneficios obtidos

- Compatibiliza consumidores com entrega at-least-once.
- Torna replay seguro quando o event id e preservado.

#### Trade-offs e limitacoes

- Depende de identificadores estaveis e constraints do banco.

#### Evidencias

- [`src/balance/BalanceService.Domain/ProcessedEvents/ProcessedEvent.cs`](../../src/balance/BalanceService.Domain/ProcessedEvents/ProcessedEvent.cs)
- [`src/balance/BalanceService.Infrastructure/Persistence/Configurations/ProcessedEventConfiguration.cs`](../../src/balance/BalanceService.Infrastructure/Persistence/Configurations/ProcessedEventConfiguration.cs)
- [`src/audit/AuditService.Infrastructure/Persistence/Repositories/FunctionalAuditRecordRepository.cs`](../../src/audit/AuditService.Infrastructure/Persistence/Repositories/FunctionalAuditRecordRepository.cs)
- [`tests/balance/BalanceService.IntegrationTests/Workers/ApplyLedgerEntryCreatedConcurrencyTests.cs`](../../tests/balance/BalanceService.IntegrationTests/Workers/ApplyLedgerEntryCreatedConcurrencyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0003](../adrs/0003-integracao-assincrona-kafka-com-outbox.md)
- [ADR-0099](../adrs/0099-audit-async-integration-strategy.md)

### Idempotency Key

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Timeout, perda de resposta e retries concorrentes podem repetir comandos HTTP. Sem chave de idempotencia, o sistema poderia criar lancamentos, usuarios, pagamentos, refunds ou auditorias duplicadas.

#### Onde foi aplicado

Ledger, Transfer, Payment, Identity, Audit e chamadas internas Payment/Transfer -> Ledger.

#### Como funciona neste repositorio

Mesma chave + mesmo payload retorna replay seguro; mesma chave + payload diferente retorna conflito. Banco protege a chave por constraints unicas. Workers usam chaves deterministicas por etapa para chamadas HTTP ao Ledger.

#### Beneficios obtidos

- Permite retry seguro.
- Reduz risco de duplicidade por concorrencia.
- Ajuda a lidar com resultado desconhecido.

#### Trade-offs e limitacoes

- Exige armazenar hash/resposta e expirar registros.
- Reuso indevido de chave precisa ser diagnosticado como conflito real.

#### Evidencias

- [`src/ledger/LedgerService.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs`](../../src/ledger/LedgerService.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentIdempotencyService.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentIdempotencyService.cs)
- [`src/identity/IdentityService.Infrastructure/Persistence/Repositories/IdempotencyRepository.cs`](../../src/identity/IdentityService.Infrastructure/Persistence/Repositories/IdempotencyRepository.cs)
- [`docs/development/payment-api.md`](../development/payment-api.md)

#### ADRs e documentacao relacionados

- [ADR-0096](../adrs/0096-idempotencia-cadastro-usuarios-identity-service.md)
- [ADR-0104](../adrs/0104-payment-ledger-integration.md)

### Saga orquestrada

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Transferir valor entre merchants exige debito, credito e compensacao sem transacao distribuida entre processos.

#### Onde foi aplicado

TransferService.

#### Como funciona neste repositorio

`TransferService.Api` registra a Saga. `TransferService.Worker` reclama Sagas pendentes, chama `LedgerService.Api` para debito/credito/compensacao com idempotency keys deterministicas, persiste estado da Saga e publica eventos da Saga por Outbox/Kafka.

#### Beneficios obtidos

- Centraliza decisao de coordenacao.
- Preserva Ledger como dono dos lancamentos.
- Mantem Balance como projecao eventual.

#### Trade-offs e limitacoes

- O orquestrador vira ponto central de decisao.
- Compensacao nao e rollback ACID distribuido.
- Eventos da Saga nao devem ser usados pelo Balance para saldo.

#### Evidencias

- [`src/transfer/TransferService.Worker/Sagas/TransferenciaSagaProcessorService.cs`](../../src/transfer/TransferService.Worker/Sagas/TransferenciaSagaProcessorService.cs)
- [`src/transfer/TransferService.Domain/Sagas/TransferenciaSaga.cs`](../../src/transfer/TransferService.Domain/Sagas/TransferenciaSaga.cs)
- [`tests/transfer/TransferService.IntegrationTests/Sagas/TransferenciaSagaKafkaFlowTests.cs`](../../tests/transfer/TransferService.IntegrationTests/Sagas/TransferenciaSagaKafkaFlowTests.cs)
- [`docs/operations/transfer-saga-kafka.md`](../operations/transfer-saga-kafka.md)

#### ADRs e documentacao relacionados

- [ADR-0087](../adrs/0087-saga-orquestrada-transfer-service-kafka.md)
- [TransferService API](../development/transfer-api.md)

### Compensating Transaction

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Quando uma etapa posterior falha, o sistema precisa desfazer semanticamente o efeito ja aceito, sem prometer rollback distribuido.

#### Onde foi aplicado

Ledger estorno, Transfer compensacao de debito, Payment refund/estorno no Ledger e Identity remocao best effort no Keycloak.

#### Como funciona neste repositorio

Ledger cria lancamento compensatorio. Transfer solicita estorno quando credito falha apos debito. Payment solicita estorno do Ledger para refund confirmado. Identity tenta remover usuario do Keycloak quando a persistencia local falha apos criar usuario externo.

#### Beneficios obtidos

- Torna falhas posteriores recuperaveis semanticamente.
- Mantem cada context dono de sua regra.

#### Trade-offs e limitacoes

- Compensacao pode falhar e exigir operacao.
- Nao restaura atomicamente todos os efeitos externos.

#### Evidencias

- [`src/ledger/LedgerService.Application/Lancamentos/Commands/ProcessarEstornoLancamentoHandler.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Commands/ProcessarEstornoLancamentoHandler.cs)
- [`src/transfer/TransferService.Worker/Sagas/TransferenciaSagaProcessorService.cs`](../../src/transfer/TransferService.Worker/Sagas/TransferenciaSagaProcessorService.cs)
- [`src/payment/PaymentService.Application/Payments/Ledger/PaymentLedgerProcessor.cs`](../../src/payment/PaymentService.Application/Payments/Ledger/PaymentLedgerProcessor.cs)
- [`src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs`](../../src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs)

#### ADRs e documentacao relacionados

- [ADR-0050](../adrs/0050-processamento-assincrono-estornos-ledger.md)
- [ADR-0087](../adrs/0087-saga-orquestrada-transfer-service-kafka.md)
- [ADR-0104](../adrs/0104-payment-ledger-integration.md)

### Eventual Consistency

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Saldo, pagamento e transferencia nao devem depender de transacao distribuida entre API, banco, broker e consumidores.

#### Onde foi aplicado

Ledger -> Balance; Payment -> Ledger -> Balance; Transfer -> Ledger -> Balance por eventos financeiros do Ledger.

#### Como funciona neste repositorio

O estado de escrita e confirmado primeiro no context dono. Workers publicam ou processam mensagens depois. Balance reflete o saldo quando consome eventos do Ledger; Payment pode estar `Completed` antes de o Balance projetar o efeito.

#### Beneficios obtidos

- Reduz acoplamento temporal.
- Permite escalar APIs e workers separadamente.
- Evita transacao distribuida.

#### Trade-offs e limitacoes

- Existe janela de inconsistencia entre comando aceito e leitura projetada.
- Observabilidade e runbooks sao obrigatorios para diagnosticar atraso.

#### Evidencias

- [`docs/architecture/views.c4`](views.c4)
- [`src/payment/PaymentService.Application/Payments/Ledger/PaymentLedgerProcessor.cs`](../../src/payment/PaymentService.Application/Payments/Ledger/PaymentLedgerProcessor.cs)
- [`src/balance/BalanceService.Worker/Messaging/Processors/LedgerEntryCreatedMessageProcessor.cs`](../../src/balance/BalanceService.Worker/Messaging/Processors/LedgerEntryCreatedMessageProcessor.cs)
- [`docs/development/payment-api.md`](../development/payment-api.md)

#### ADRs e documentacao relacionados

- [ADR-0001](../adrs/0001-separar-ledger-e-balance-com-projecao.md)
- [ADR-0104](../adrs/0104-payment-ledger-integration.md)

### Event-Driven Architecture

**Categoria:** Integracao distribuida  
**Status:** Parcialmente implementado

#### Problema resolvido

Alguns fluxos precisam desacoplar produtor e consumidor no tempo, mas nem toda comunicacao do repositorio deve virar evento.

#### Onde foi aplicado

Eventos financeiros Ledger/Balance, eventos de Saga do Transfer e consumer Kafka opcional de Audit. Identity usa Domain Events intra-processo.

#### Como funciona neste repositorio

O fluxo financeiro principal e orientado a eventos. Transfer publica eventos de Saga para rastreabilidade/diagnostico. Payment usa HTTP sincrono para provider e Ledger, e Inbox para entrada externa; nao publica evento financeiro direto para Balance.

#### Beneficios obtidos

- Usa assincronia onde ha consumidor real ou valor operacional.
- Evita coreografia acidental em Transfer.

#### Trade-offs e limitacoes

- A arquitetura e hibrida; HTTP sincrono continua correto para comandos entre contexts quando ha ownership claro.

#### Evidencias

- [`docs/events/README.md`](../events/README.md)
- [`src/ledger/LedgerService.Worker/Messaging/Kafka/Producers/KafkaOutboxMessagePublisher.cs`](../../src/ledger/LedgerService.Worker/Messaging/Kafka/Producers/KafkaOutboxMessagePublisher.cs)
- [`src/transfer/TransferService.Worker/Messaging/Kafka/KafkaTransferenciaOutboxPublisher.cs`](../../src/transfer/TransferService.Worker/Messaging/Kafka/KafkaTransferenciaOutboxPublisher.cs)
- [`src/audit/AuditService.Worker/Messaging/Kafka/AuditRecordRequestedConsumer.cs`](../../src/audit/AuditService.Worker/Messaging/Kafka/AuditRecordRequestedConsumer.cs)

#### ADRs e documentacao relacionados

- [ADR-0003](../adrs/0003-integracao-assincrona-kafka-com-outbox.md)
- [ADR-0088](../adrs/0088-kafka-default-ledger-balance-workers.md)
- [ADR-0099](../adrs/0099-audit-async-integration-strategy.md)

### Versionamento de eventos

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Consumidores antigos nao podem quebrar imediatamente quando o contrato de evento evolui, como aconteceu ao adicionar `currency`.

#### Onde foi aplicado

`LedgerEntryCreated.v1` legado e `LedgerEntryCreated.v2` atual.

#### Como funciona neste repositorio

Schemas JSON versionados documentam payloads. Balance aceita v1 como legado e v2 como atual; v1 tem limitacao conhecida de moeda default `BRL`.

#### Beneficios obtidos

- Permite evolucao incremental de contrato.
- Mantem compatibilidade com mensagens antigas.

#### Trade-offs e limitacoes

- Consumidor precisa manter tolerant reader.
- Versoes antigas aumentam codigo de compatibilidade.

#### Evidencias

- [`src/ledger/LedgerService.Application/Lancamentos/Events/LedgerEntryCreatedV1.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Events/LedgerEntryCreatedV1.cs)
- [`src/ledger/LedgerService.Application/Lancamentos/Events/LedgerEntryCreatedV2.cs`](../../src/ledger/LedgerService.Application/Lancamentos/Events/LedgerEntryCreatedV2.cs)
- [`contracts/events/ledger-entry-created.v2.schema.json`](../../contracts/events/ledger-entry-created.v2.schema.json)
- [`tests/ledger/LedgerService.UnitTests/Application/Lancamentos/Contracts/LedgerEntryCreatedProducerContractTests.cs`](../../tests/ledger/LedgerService.UnitTests/Application/Lancamentos/Contracts/LedgerEntryCreatedProducerContractTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0076](../adrs/0076-formalizar-contrato-ledger-entry-created-v1.md)
- [ADR-0084](../adrs/0084-ledger-entry-created-v2-currency-explicita.md)
- [Politica de versionamento de contratos](../development/event-contract-versioning.md)

### Dead Letter Queue

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Mensagens invalidas ou falhas definitivas nao devem causar retry infinito nem sumir sem evidencia operacional.

#### Onde foi aplicado

Outbox DeadLetter no Ledger, DLQ Kafka/PubSub do Balance, DLQ da Saga Transfer, DeadLetter logico da Inbox Payment e DLQ do Audit Worker.

#### Como funciona neste repositorio

Cada fluxo classifica falhas recuperaveis e definitivas. Recuperaveis usam retry/backoff; definitivas viram status `DeadLetter` no banco ou publicacao em topico DLQ de aplicacao.

#### Beneficios obtidos

- Evita bloquear filas indefinidamente.
- Preserva payload/causa para diagnostico.
- Permite requeue/redrive com decisao explicita.

#### Trade-offs e limitacoes

- DLQ nao corrige contrato ruim.
- Reprocessar exige validar causa raiz e idempotencia.

#### Evidencias

- [`src/ledger/LedgerService.Application/Abstractions/Messaging/OutboxStatus.cs`](../../src/ledger/LedgerService.Application/Abstractions/Messaging/OutboxStatus.cs)
- [`src/balance/BalanceService.Worker/Messaging/Kafka/DeadLetter/KafkaDeadLetterPublisher.cs`](../../src/balance/BalanceService.Worker/Messaging/Kafka/DeadLetter/KafkaDeadLetterPublisher.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs)
- [`src/audit/AuditService.Worker/Messaging/Kafka/AuditKafkaDeadLetterPublisher.cs`](../../src/audit/AuditService.Worker/Messaging/Kafka/AuditKafkaDeadLetterPublisher.cs)

#### ADRs e documentacao relacionados

- [ADR-0017](../adrs/0017-implementar-dlq-versionamento-eventos-readiness-operacional.md)
- [ADR-0070](../adrs/0070-dlq-outbox-banco-backoff-requeue.md)
- [Estrategia operacional de DLQ](../operations/dlq-strategy.md)

### Replay e Projection Rebuild

**Categoria:** Integracao distribuida  
**Status:** Implementado

#### Problema resolvido

Projecoes podem divergir ou mensagens podem precisar de reprocessamento apos correcao. O projeto precisa recuperar leitura sem transformar Outbox em Event Store.

#### Onde foi aplicado

Balance replay/rebuild e runbooks de recuperacao.

#### Como funciona neste repositorio

Handlers de replay filtram eventos elegiveis, aplicam validacao/idempotencia e podem reconstruir uma projecao parcial ou gerar relatorio de divergencia. A fonte atual e Outbox/eventos persistidos, mas isso nao caracteriza Event Sourcing porque o estado de dominio nao e reconstruido exclusivamente de um log de eventos.

#### Beneficios obtidos

- Ajuda corrigir ou comparar projecoes.
- Mantem idempotencia em reprocessamento.

#### Trade-offs e limitacoes

- Operacao deve ser manual/cuidadosa.
- Nao substitui contrato de eventos nem auditoria completa.

#### Evidencias

- [`src/balance/BalanceService.Application/Balances/Replay/PartialProjectionRebuildHandler.cs`](../../src/balance/BalanceService.Application/Balances/Replay/PartialProjectionRebuildHandler.cs)
- [`src/balance/BalanceService.Application/Balances/Replay/ProjectionRebuildDivergenceReportHandler.cs`](../../src/balance/BalanceService.Application/Balances/Replay/ProjectionRebuildDivergenceReportHandler.cs)
- [`docs/operations/projection-rebuild.md`](../operations/projection-rebuild.md)
- [`docs/operations/replay-strategy.md`](../operations/replay-strategy.md)

#### ADRs e documentacao relacionados

- [ADR-0052](../adrs/0052-processamento-assincrono-reprocessamento-lancamentos-ledger.md)
- [Runbook de recuperacao de eventos](../operations/event-recovery-runbook.md)

## Padroes de resiliencia

### Retry

**Categoria:** Resiliencia  
**Status:** Implementado

#### Problema resolvido

Falhas transitorias de HTTP, broker ou banco nao devem exigir intervencao manual imediata.

#### Onde foi aplicado

Clientes HTTP resilientes, Outbox, Inbox Payment, Saga Transfer e DLQ/replay.

#### Como funciona neste repositorio

Ha retry HTTP curto para dependencias como JWKS/Keycloak/Ledger. Processamento assincrono usa retry persistido com `NextRetryAt`, contador de tentativa e DeadLetter quando excede o limite.

#### Beneficios obtidos

- Reduz falhas por indisponibilidade breve.
- Evita perder trabalho assumido por worker.

#### Trade-offs e limitacoes

- Retry sem idempotencia pode duplicar efeitos; por isso e combinado com idempotency keys, constraints e consumidores idempotentes.

#### Evidencias

- [`src/Shared/HttpResilience`](../../src/Shared/HttpResilience)
- [`src/ledger/LedgerService.Worker/Outbox/OutboxPublisherService.cs`](../../src/ledger/LedgerService.Worker/Outbox/OutboxPublisherService.cs)
- [`src/payment/PaymentService.Worker/HostedServices/PaymentInboxWorkerService.cs`](../../src/payment/PaymentService.Worker/HostedServices/PaymentInboxWorkerService.cs)
- [`docs/operations/payment-worker.md`](../operations/payment-worker.md)

#### ADRs e documentacao relacionados

- [ADR-0015](../adrs/0015-api-resilience-timeouts-retries-circuit-breaker.md)
- [Estrategia operacional de replay seguro](../operations/replay-strategy.md)

### Exponential Backoff

**Categoria:** Resiliencia  
**Status:** Implementado

#### Problema resolvido

Quando uma dependencia fica indisponivel, retry continuo aumenta pressao e dificulta recuperacao.

#### Onde foi aplicado

Outbox do Ledger, Inbox Payment, Transfer Saga/Outbox e estrategias de DLQ.

#### Como funciona neste repositorio

`ExponentialBackoffRetryStrategy` calcula proxima tentativa com jitter. Workers persistem `NextRetryAt` ou usam politicas equivalentes para reagendar unidades de trabalho.

#### Beneficios obtidos

- Reduz thundering herd local.
- Da tempo para dependencia recuperar.

#### Trade-offs e limitacoes

- Aumenta latencia de recuperacao em falhas prolongadas.

#### Evidencias

- [`src/ledger/LedgerService.Application/Outbox/Retry/ExponentialBackoffRetryStrategy.cs`](../../src/ledger/LedgerService.Application/Outbox/Retry/ExponentialBackoffRetryStrategy.cs)
- [`tests/ledger/LedgerService.Worker.Tests/Outbox/OutboxRetryStrategyTests.cs`](../../tests/ledger/LedgerService.Worker.Tests/Outbox/OutboxRetryStrategyTests.cs)
- [`src/payment/PaymentService.Worker/HostedServices/PaymentInboxWorkerService.cs`](../../src/payment/PaymentService.Worker/HostedServices/PaymentInboxWorkerService.cs)
- [`docs/operations/dlq-strategy.md`](../operations/dlq-strategy.md)

#### ADRs e documentacao relacionados

- [ADR-0070](../adrs/0070-dlq-outbox-banco-backoff-requeue.md)

### Circuit Breaker

**Categoria:** Resiliencia  
**Status:** Implementado

#### Problema resolvido

Chamadas repetidas para dependencia em falha desperdicam recursos e atrasam recuperacao.

#### Onde foi aplicado

Pipeline HTTP resiliente compartilhado para JWKS, Keycloak e Ledger.

#### Como funciona neste repositorio

O pipeline usa `Microsoft.Extensions.Http.Resilience` e registra metricas/logs para estados Closed, Open e Half-Open. Quando aberto, chamadas sao rejeitadas rapidamente ate a janela de recuperacao.

#### Beneficios obtidos

- Falha rapido durante indisponibilidade.
- Expoe metricas de transicao para observabilidade.

#### Trade-offs e limitacoes

- Configuracao ruim pode abrir circuito cedo demais ou tarde demais.

#### Evidencias

- [`src/Shared/HttpResilience`](../../src/Shared/HttpResilience)
- [`tests/transfer/TransferService.Worker.Tests/Http/HttpClientCircuitBreakerPolicyTests.cs`](../../tests/transfer/TransferService.Worker.Tests/Http/HttpClientCircuitBreakerPolicyTests.cs)
- [`docs/development/authentication.md`](../development/authentication.md)

#### ADRs e documentacao relacionados

- [ADR-0015](../adrs/0015-api-resilience-timeouts-retries-circuit-breaker.md)

### Timeout

**Categoria:** Resiliencia  
**Status:** Implementado

#### Problema resolvido

Chamadas externas indefinidas seguram threads/conexoes e criam resultado ambiguo para o chamador.

#### Onde foi aplicado

JWKS, Keycloak Admin API, Ledger HTTP, Stripe/Fake gateway e workers.

#### Como funciona neste repositorio

Timeouts aparecem como attempt timeout e total request timeout no pipeline resiliente; adapters tambem convertem timeouts para erros classificados de dominio/aplicacao.

#### Beneficios obtidos

- Protege recursos do processo.
- Permite retry controlado e classificacao de resultado desconhecido.

#### Trade-offs e limitacoes

- Timeout nao prova que a dependencia nao executou a operacao.

#### Evidencias

- [`src/Shared/HttpResilience`](../../src/Shared/HttpResilience)
- [`src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs`](../../src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs)
- [`src/payment/PaymentService.Infrastructure/Gateway/StripePaymentGateway.cs`](../../src/payment/PaymentService.Infrastructure/Gateway/StripePaymentGateway.cs)
- [`docs/development/authentication.md`](../development/authentication.md)

#### ADRs e documentacao relacionados

- [ADR-0015](../adrs/0015-api-resilience-timeouts-retries-circuit-breaker.md)

### Bulkhead

**Categoria:** Resiliencia  
**Status:** Nao identificado como padrao explicito

#### Problema resolvido

Bulkhead isolaria pools de recursos para evitar que uma dependencia consuma toda a capacidade de outra.

#### Onde foi aplicado

Nao foi identificada implementacao explicita de bulkhead com isolamento de pool, fila ou limite por dependencia.

#### Como funciona neste repositorio

APIs e workers sao processos separados, o que melhora isolamento operacional, mas isso e documentado como separacao de processos, nao como Bulkhead Pattern formal.

#### Beneficios obtidos

- Evita superestimar a arquitetura.

#### Trade-offs e limitacoes

- Falhas de dependencia ainda podem exigir limites especificos se surgirem gargalos reais.

#### Evidencias

- [`docs/architecture/boundaries.md`](boundaries.md)
- [`docs/architecture/production-readiness.md`](production-readiness.md)

#### ADRs e documentacao relacionados

- [ADR-0067](../adrs/0067-separacao-workers-processos-api.md)

### Resultado desconhecido

**Categoria:** Resiliencia distribuida  
**Status:** Implementado

#### Problema resolvido

Uma dependencia pode executar a operacao, mas a resposta se perder por timeout, rede ou circuito. O cliente nao sabe se deve repetir.

#### Onde foi aplicado

Payment -> Ledger, Transfer -> Ledger, Identity -> Keycloak e Stripe gateway.

#### Como funciona neste repositorio

Chamadas com efeito usam idempotency key deterministica e estado persistido. O retry com a mesma chave tende a retornar replay seguro ou conflito; workers tambem consultam estado local antes de repetir efeito externo.

#### Beneficios obtidos

- Reduz duplicidade em timeouts.
- Permite recuperacao automatica com retry persistido.

#### Trade-offs e limitacoes

- Ainda exige observabilidade e reconciliacao quando a dependencia externa nao oferece replay confiavel.

#### Evidencias

- [`src/payment/PaymentService.Application/Payments/Ledger/PaymentLedgerProcessor.cs`](../../src/payment/PaymentService.Application/Payments/Ledger/PaymentLedgerProcessor.cs)
- [`src/transfer/TransferService.Worker/Sagas/TransferenciaSagaProcessorService.cs`](../../src/transfer/TransferService.Worker/Sagas/TransferenciaSagaProcessorService.cs)
- [`src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs`](../../src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs)
- [`docs/development/payment-api.md`](../development/payment-api.md)

#### ADRs e documentacao relacionados

- [ADR-0104](../adrs/0104-payment-ledger-integration.md)
- [ADR-0096](../adrs/0096-idempotencia-cadastro-usuarios-identity-service.md)

## Padroes de persistencia e concorrencia

### Repository

**Categoria:** Persistencia  
**Status:** Implementado

#### Problema resolvido

Application nao deve depender diretamente de EF Core para buscar e persistir aggregates/projecoes.

#### Onde foi aplicado

Repositories especificos por context: Ledger, Balance, Transfer, Payment, Identity e Audit.

#### Como funciona neste repositorio

Interfaces de persistencia representam necessidades reais de caso de uso. Implementacoes EF Core ficam em Infrastructure. Nao ha Generic Repository central como padrao.

#### Beneficios obtidos

- Encapsula consultas e comandos de persistencia.
- Facilita testes com fakes/mocks.

#### Trade-offs e limitacoes

- Interfaces variam entre contexts; isso e aceito quando reflete fronteiras diferentes.

#### Evidencias

- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentRepository.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentRepository.cs)
- [`src/transfer/TransferService.Infrastructure/Persistence/Repositories/TransferenciaSagaRepository.cs`](../../src/transfer/TransferService.Infrastructure/Persistence/Repositories/TransferenciaSagaRepository.cs)
- [`src/identity/IdentityService.Infrastructure/Persistence/Repositories/UserRepository.cs`](../../src/identity/IdentityService.Infrastructure/Persistence/Repositories/UserRepository.cs)
- [`src/audit/AuditService.Infrastructure/Persistence/Repositories/FunctionalAuditRecordRepository.cs`](../../src/audit/AuditService.Infrastructure/Persistence/Repositories/FunctionalAuditRecordRepository.cs)

#### ADRs e documentacao relacionados

- [ADR-0002](../adrs/0002-clean-architecture-ddd-por-servico.md)

### Unit of Work

**Categoria:** Persistencia  
**Status:** Implementado

#### Problema resolvido

Algumas alteracoes locais precisam ser confirmadas na mesma transacao: aggregate, idempotencia e Outbox, ou saldo e evento processado.

#### Onde foi aplicado

DbContexts de Ledger, Balance, Transfer e Payment.

#### Como funciona neste repositorio

DbContext implementa `IUnitOfWork`, expondo `BeginTransactionAsync` e `SaveChangesAsync` para handlers coordenarem commit local. Nao representa transacao distribuida.

#### Beneficios obtidos

- Mantem consistencia local.
- Permite Outbox/Inbox/idempotencia atomicas no banco do context.

#### Trade-offs e limitacoes

- A consistencia entre contexts continua eventual.

#### Evidencias

- [`src/ledger/LedgerService.Infrastructure/Persistence/AppDbContext.cs`](../../src/ledger/LedgerService.Infrastructure/Persistence/AppDbContext.cs)
- [`src/balance/BalanceService.Infrastructure/Persistence/BalanceDbContext.cs`](../../src/balance/BalanceService.Infrastructure/Persistence/BalanceDbContext.cs)
- [`src/transfer/TransferService.Infrastructure/Persistence/TransferServiceDbContext.cs`](../../src/transfer/TransferService.Infrastructure/Persistence/TransferServiceDbContext.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/PaymentDbContext.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/PaymentDbContext.cs)

#### ADRs e documentacao relacionados

- [ADR-0007](../adrs/0007-banco-por-microservico-postgres-efcore.md)
- [ADR-0081](../adrs/0081-postgres-local-unico-com-schemas-por-servico.md)

### Database per Service logico

**Categoria:** Persistencia  
**Status:** Implementado

#### Problema resolvido

A POC precisa preservar ownership de dados por servico sem o custo operacional de varias instancias PostgreSQL locais.

#### Onde foi aplicado

PostgreSQL unico local com schemas e usuarios por servico.

#### Como funciona neste repositorio

O container PostgreSQL local usa database `appdb`, schemas `ledger`, `balance`, `transfer`, `payment`, `identity` e `audit`, com usuarios de runtime e migration separados.

#### Beneficios obtidos

- Mantem isolamento logico e grants por servico.
- Simplifica ambiente local.

#### Trade-offs e limitacoes

- Nao equivale a isolamento fisico de producao.
- Falha do PostgreSQL local afeta todos os contexts na POC.

#### Evidencias

- [`infra/postgres/init/001-create-schemas-users-permissions.sql`](../../infra/postgres/init/001-create-schemas-users-permissions.sql)
- [`docs/development/local-development.md`](../development/local-development.md)
- [`docs/architecture/model.c4`](model.c4)

#### ADRs e documentacao relacionados

- [ADR-0007](../adrs/0007-banco-por-microservico-postgres-efcore.md)
- [ADR-0081](../adrs/0081-postgres-local-unico-com-schemas-por-servico.md)

### Pessimistic Lock, Claim e Lease

**Categoria:** Persistencia e concorrencia  
**Status:** Implementado

#### Problema resolvido

Multiplos workers podem tentar processar a mesma mensagem, Saga, Inbox ou saldo ao mesmo tempo.

#### Onde foi aplicado

Outbox Ledger/Transfer, Payment Inbox/Ledger materialization, Saga Transfer, estornos Ledger e Balance.

#### Como funciona neste repositorio

Claims usam `FOR UPDATE SKIP LOCKED`, status `Processing`, `LockedUntil` e `lockOwner`. Leases permitem recuperar trabalho quando o worker morre apos assumir a unidade. Balance usa lock transacional por chave para evitar lost update em `daily_balances`.

#### Beneficios obtidos

- Evita duplo processamento concorrente.
- Permite paralelismo seguro.
- Recupera unidade travada apos expirar lease.

#### Trade-offs e limitacoes

- Locks aumentam acoplamento com PostgreSQL.
- Leases exigem clocks e timeouts coerentes.

#### Evidencias

- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentInboxRepository.cs)
- [`src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentRepository.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Repositories/PaymentRepository.cs)
- [`src/ledger/LedgerService.Infrastructure/Persistence/Repositories/OutboxMessageRepository.cs`](../../src/ledger/LedgerService.Infrastructure/Persistence/Repositories/OutboxMessageRepository.cs)
- [`tests/balance/BalanceService.IntegrationTests/Workers/ApplyLedgerEntryCreatedConcurrencyTests.cs`](../../tests/balance/BalanceService.IntegrationTests/Workers/ApplyLedgerEntryCreatedConcurrencyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0053](../adrs/0053-lock-transacional-por-chave-no-balance.md)
- [ADR-0054](../adrs/0054-controle-concorrencia-estornos-ledger.md)
- [Operacao do PaymentService.Worker](../operations/payment-worker.md)

### Optimistic Concurrency

**Categoria:** Persistencia e concorrencia  
**Status:** Nao identificado como padrao explicito

#### Problema resolvido

Controle otimista resolveria conflitos por versao/token quando duas gravacoes tentam alterar a mesma linha.

#### Onde foi aplicado

Nao foi identificado uso central de token de concorrencia, `rowversion`, `xmin` ou tratamento explicito de conflito otimista.

#### Como funciona neste repositorio

O repositorio prefere locks pessimistas, claims, leases e unique constraints.

#### Beneficios obtidos

- A classificacao evita chamar constraints ou locks de optimistic concurrency.

#### Trade-offs e limitacoes

- Se surgirem edicoes concorrentes de aggregates long-lived, pode ser necessario introduzir versao explicita.

#### Evidencias

- [`docs/architecture/boundaries.md`](boundaries.md)
- [`tests/balance/BalanceService.IntegrationTests/Workers/ApplyLedgerEntryCreatedConcurrencyTests.cs`](../../tests/balance/BalanceService.IntegrationTests/Workers/ApplyLedgerEntryCreatedConcurrencyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0053](../adrs/0053-lock-transacional-por-chave-no-balance.md)

### Unique Constraint como ultima defesa

**Categoria:** Persistencia e concorrencia  
**Status:** Implementado

#### Problema resolvido

Duas requisicoes concorrentes podem passar por validacoes em memoria antes de uma delas persistir. O banco precisa ser a ultima linha de defesa contra duplicidade.

#### Onde foi aplicado

Idempotency keys, external references, Inbox provider event id, processed event id, e-mail/Keycloak user id e source_event_id de auditoria.

#### Como funciona neste repositorio

Mappings EF Core configuram indices unicos; repositories capturam `PostgresErrorCodes.UniqueViolation` e traduzem para conflito/idempotencia.

#### Beneficios obtidos

- Fecha janela de corrida.
- Mantem consistencia mesmo com multiplas instancias.

#### Trade-offs e limitacoes

- Erros precisam ser traduzidos para resposta/acao adequada.

#### Evidencias

- [`src/payment/PaymentService.Infrastructure/Persistence/Migrations/PaymentDbContextModelSnapshot.cs`](../../src/payment/PaymentService.Infrastructure/Persistence/Migrations/PaymentDbContextModelSnapshot.cs)
- [`src/ledger/LedgerService.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs`](../../src/ledger/LedgerService.Infrastructure/Persistence/Configurations/IdempotencyRecordConfiguration.cs)
- [`src/balance/BalanceService.Infrastructure/Persistence/Configurations/ProcessedEventConfiguration.cs`](../../src/balance/BalanceService.Infrastructure/Persistence/Configurations/ProcessedEventConfiguration.cs)
- [`tests/ledger/LedgerService.IntegrationTests/Api/Lancamentos/CreateLancamentoPostgresTests.cs`](../../tests/ledger/LedgerService.IntegrationTests/Api/Lancamentos/CreateLancamentoPostgresTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0054](../adrs/0054-controle-concorrencia-estornos-ledger.md)
- [ADR-0096](../adrs/0096-idempotencia-cadastro-usuarios-identity-service.md)

## Padroes de seguranca

### JWT/OIDC com JWKS

**Categoria:** Seguranca  
**Status:** Implementado

#### Problema resolvido

As APIs precisam validar tokens sem chamar introspeccao remota a cada request.

#### Onde foi aplicado

APIs de Ledger, Balance, Transfer, Payment, Identity e Audit com Keycloak local.

#### Como funciona neste repositorio

Keycloak emite JWT RS256 e publica JWKS. APIs configuram issuer, audience, JWKS URL e policies de scope/merchant. O fetch de JWKS usa cliente HTTP resiliente.

#### Beneficios obtidos

- Reduz dependencia runtime por request.
- Mantem IdP padronizado localmente.

#### Trade-offs e limitacoes

- Ambiente local aceita HTTP/JWKS sem HTTPS; fora de local, transporte seguro e configuracao correta sao obrigatorios.

#### Evidencias

- [`infra/keycloak/realm-poc.json`](../../infra/keycloak/realm-poc.json)
- [`src/ledger/LedgerService.Api/Extensions/JwtAuthServiceCollectionExtensions.cs`](../../src/ledger/LedgerService.Api/Extensions/JwtAuthServiceCollectionExtensions.cs)
- [`src/Shared/ApiDefaults/Security`](../../src/Shared/ApiDefaults/Security)
- [`docs/development/authentication.md`](../development/authentication.md)

#### ADRs e documentacao relacionados

- [ADR-0004](../adrs/0004-autenticacao-jwt-rs256-via-jwks.md)
- [ADR-0074](../adrs/0074-keycloak-como-identidade-principal.md)

### Identity Provider

**Categoria:** Seguranca  
**Status:** Implementado

#### Problema resolvido

Cadastro local de usuario nao deve ser confundido com emissao de token.

#### Onde foi aplicado

Keycloak como IdP; IdentityService como bounded context de cadastro/vinculo local.

#### Como funciona neste repositorio

Keycloak emite tokens e publica JWKS. IdentityService cria usuario no Keycloak, gera `MerchantId`, persiste vinculo local e envia e-mail, mas nao emite tokens.

#### Beneficios obtidos

- Separa identidade tecnica de cadastro do dominio.
- Permite evoluir cadastro sem reimplementar OIDC.

#### Trade-offs e limitacoes

- Falhas entre Keycloak e banco local exigem compensacao best effort.

#### Evidencias

- [`src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs`](../../src/identity/IdentityService.Infrastructure/IdentityProvider/KeycloakAdminClient.cs)
- [`src/identity/IdentityService.Api/Endpoints/UserEndpoints.cs`](../../src/identity/IdentityService.Api/Endpoints/UserEndpoints.cs)
- [`docs/architecture/model.c4`](model.c4)
- [`README.md`](../../README.md)

#### ADRs e documentacao relacionados

- [ADR-0074](../adrs/0074-keycloak-como-identidade-principal.md)
- [ADR-0089](../adrs/0089-bounded-context-identity-service.md)
- [ADR-0090](../adrs/0090-cadastro-usuarios-identity-service.md)

### Policy-Based Authorization

**Categoria:** Seguranca  
**Status:** Implementado

#### Problema resolvido

Endpoints financeiros e de auditoria precisam impedir acesso a operacoes ou dados de outro merchant, mitigando BOLA.

#### Onde foi aplicado

Scopes por API e autorizacao por `merchant_id` em Ledger, Balance, Transfer, Payment, Identity e Audit.

#### Como funciona neste repositorio

Policies exigem scopes como `ledger.write`, `payment.refund` ou `audit.write`. Services/extensoes verificam se o `merchant_id` do token autoriza o merchant do body/query.

#### Beneficios obtidos

- Define autorizacao declarativa por endpoint.
- Separa autenticacao de autorizacao de negocio.

#### Trade-offs e limitacoes

- Scopes e audiences precisam permanecer sincronizados com Keycloak/OpenAPI.

#### Evidencias

- [`src/ledger/LedgerService.Api/Security/MerchantAuthorizationService.cs`](../../src/ledger/LedgerService.Api/Security/MerchantAuthorizationService.cs)
- [`src/payment/PaymentService.Api/Security/ScopePolicies.cs`](../../src/payment/PaymentService.Api/Security/ScopePolicies.cs)
- [`src/audit/AuditService.Api/Security/AuditClaimsPrincipalExtensions.cs`](../../src/audit/AuditService.Api/Security/AuditClaimsPrincipalExtensions.cs)
- [`docs/development/authentication.md`](../development/authentication.md)

#### ADRs e documentacao relacionados

- [ADR-0023](../adrs/0023-autorizacao-por-merchant.md)
- [ADR-0029](../adrs/0029-limites-operacionais-de-api.md)

### Rate Limiting e Security Headers

**Categoria:** Seguranca  
**Status:** Implementado

#### Problema resolvido

Endpoints expostos precisam de limites basicos contra abuso e headers que reduzam riscos comuns de borda HTTP.

#### Onde foi aplicado

Defaults compartilhados das APIs e Nginx local.

#### Como funciona neste repositorio

`ApiDefaults` registra rate limit e headers padrao para APIs. Nginx local adiciona/filtra headers, reduz fingerprinting e aplica limites defensivos na borda opcional.

#### Beneficios obtidos

- Politica tecnica comum nas APIs.
- Exercita comportamento de borda local.

#### Trade-offs e limitacoes

- Nao substitui WAF, quota por consumidor ou protecao distribuida produtiva.

#### Evidencias

- [`src/Shared/ApiDefaults`](../../src/Shared/ApiDefaults)
- [`infra/nginx/security-headers.conf`](../../infra/nginx/security-headers.conf)
- [`infra/nginx/nginx.conf`](../../infra/nginx/nginx.conf)
- [`docs/troubleshooting.md`](../troubleshooting.md)

#### ADRs e documentacao relacionados

- [ADR-0029](../adrs/0029-limites-operacionais-de-api.md)
- [ADR-0071](../adrs/0071-borda-local-nginx-https.md)

### Secret Management local

**Categoria:** Seguranca  
**Status:** Parcialmente implementado

#### Problema resolvido

Credenciais reais nao devem ser versionadas, mas a stack local precisa de valores para PostgreSQL, Keycloak, Stripe, Resend e workers.

#### Onde foi aplicado

`.env.local`, `.env.local.example`, `dotnet user-secrets`, placeholders e docs de desenvolvimento local.

#### Como funciona neste repositorio

`.env.local` e ignorado e gerado por scripts locais. Documentacao orienta user-secrets para providers reais. Arquivos versionados usam placeholders como `<KEYCLOAK_CLIENT_SECRET>`.

#### Beneficios obtidos

- Reduz risco de segredo real no Git.
- Mantem onboarding local.

#### Trade-offs e limitacoes

- ADR-0085 permanece historicamente proposta/parcial; nem todos os services possuem `appsettings.Local.example.json`, e ha placeholders com formato de segredo em varios appsettings versionados.

#### Evidencias

- [`.env.local.example`](../../.env.local.example)
- [`.gitignore`](../../.gitignore)
- [`docs/development/local-development.md`](../development/local-development.md)
- [`docs/development/stripe-cli-webhooks.md`](../development/stripe-cli-webhooks.md)

#### ADRs e documentacao relacionados

- [ADR-0085](../adrs/0085-separacao-configuracoes-locais-sensiveis-arquivos-versionados.md)
- [Baseline produtivo](production-readiness.md)

## Padroes de observabilidade e operacao

### Correlation ID

**Categoria:** Observabilidade  
**Status:** Implementado

#### Problema resolvido

Uma operacao atravessa API, banco, Outbox, Kafka, Worker e outras APIs. Sem um identificador comum, diagnostico depende de adivinhacao por horario.

#### Onde foi aplicado

APIs, Nginx, Outbox, Kafka headers, Payment/Transfer/Ledger e respostas HTTP.

#### Como funciona neste repositorio

Middleware normaliza `X-Correlation-Id`; publishers propagam header `correlation_id`; clients internos preservam o valor nas chamadas HTTP.

#### Beneficios obtidos

- Conecta logs, traces, responses, mensagens e registros.
- Facilita runbooks.

#### Trade-offs e limitacoes

- Correlation ID nao substitui TraceId; ambos tem usos complementares.

#### Evidencias

- [`src/Shared/ApiDefaults/Middlewares/CorrelationIdMiddleware.cs`](../../src/Shared/ApiDefaults/Middlewares/CorrelationIdMiddleware.cs)
- [`src/ledger/LedgerService.Worker/Messaging/Kafka/Producers/KafkaOutboxMessagePublisher.cs`](../../src/ledger/LedgerService.Worker/Messaging/Kafka/Producers/KafkaOutboxMessagePublisher.cs)
- [`src/transfer/TransferService.Worker/Ledger/LedgerServiceClient.cs`](../../src/transfer/TransferService.Worker/Ledger/LedgerServiceClient.cs)
- [`infra/nginx/nginx.conf`](../../infra/nginx/nginx.conf)

#### ADRs e documentacao relacionados

- [ADR-0005](../adrs/0005-observabilidade-correlationid-otel.md)
- [ADR-0058](../adrs/0058-propagacao-w3c-outbox-kafka.md)

### Distributed Tracing

**Categoria:** Observabilidade  
**Status:** Implementado

#### Problema resolvido

Fluxos distribuidos precisam ser reconstruidos de ponta a ponta, inclusive quando passam por Outbox e broker.

#### Onde foi aplicado

HTTP, Outbox, Kafka, Balance consumer, workers e stack local OTEL.

#### Como funciona neste repositorio

Contexto W3C e persistido/propagado em Outbox e headers Kafka. OpenTelemetry exporta traces para Collector/Jaeger quando habilitado.

#### Beneficios obtidos

- Mostra caminho temporal da operacao.
- Ajuda diferenciar falha em API, worker, broker ou consumidor.

#### Trade-offs e limitacoes

- Observabilidade completa e opcional no compose; precisa ser habilitada.

#### Evidencias

- [`src/ledger/LedgerService.Worker/Messaging/Kafka/Tracing/KafkaTraceContext.cs`](../../src/ledger/LedgerService.Worker/Messaging/Kafka/Tracing/KafkaTraceContext.cs)
- [`src/ledger/LedgerService.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs`](../../src/ledger/LedgerService.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs)
- [`compose.observability.yaml`](../../compose.observability.yaml)
- [`docs/observability.md`](../observability.md)

#### ADRs e documentacao relacionados

- [ADR-0058](../adrs/0058-propagacao-w3c-outbox-kafka.md)
- [ADR-0060](../adrs/0060-opentelemetry-collector-local.md)

### Metrics

**Categoria:** Observabilidade  
**Status:** Implementado

#### Problema resolvido

Sem metricas, backlog, retry, DLQ, latencia e circuit breaker so aparecem quando alguem le logs manualmente.

#### Onde foi aplicado

Outbox, Payment Inbox/Ledger workers, Balance consumers, HTTP resilience, Prometheus e Grafana.

#### Como funciona neste repositorio

Codigo usa `System.Diagnostics.Metrics`; OpenTelemetry exporta para Collector; Prometheus coleta metricas e Grafana provisiona dashboards locais.

#### Beneficios obtidos

- Observa volume e falhas sem inspecionar banco toda hora.
- Ajuda detectar backlog e instabilidade.

#### Trade-offs e limitacoes

- Labels devem evitar alta cardinalidade.

#### Evidencias

- [`src/ledger/LedgerService.Infrastructure/Observability/OutboxMetrics.cs`](../../src/ledger/LedgerService.Infrastructure/Observability/OutboxMetrics.cs)
- [`src/payment/PaymentService.Worker/Observability/PaymentInboxWorkerMetrics.cs`](../../src/payment/PaymentService.Worker/Observability/PaymentInboxWorkerMetrics.cs)
- [`observability/grafana/dashboards`](../../observability/grafana/dashboards)
- [`docs/observability.md`](../observability.md)

#### ADRs e documentacao relacionados

- [ADR-0059](../adrs/0059-metricas-customizadas-system-diagnostics.md)
- [ADR-0061](../adrs/0061-prometheus-grafana-metricas-tecnicas-locais.md)

### Centralized Logging

**Categoria:** Observabilidade  
**Status:** Implementado

#### Problema resolvido

Logs espalhados em containers dificultam diagnosticar fluxos distribuidos.

#### Onde foi aplicado

Loki e Grafana Alloy no overlay de observabilidade.

#### Como funciona neste repositorio

Alloy descobre containers do Compose e envia logs com labels estaveis ao Loki. Grafana permite pesquisar logs e navegar para traces quando ha TraceId.

#### Beneficios obtidos

- Busca centralizada por servico, correlation id e janela.
- Integra logs com dashboards.

#### Trade-offs e limitacoes

- E stack local/opcional, nao solucao produtiva final.

#### Evidencias

- [`compose.observability.yaml`](../../compose.observability.yaml)
- [`observability/alloy/config.alloy`](../../observability/alloy/config.alloy)
- [`docs/observability.md`](../observability.md)
- [`docs/troubleshooting.md`](../troubleshooting.md)

#### ADRs e documentacao relacionados

- [ADR-0063](../adrs/0063-loki-alloy-logs-centralizados-locais.md)

### Health, Liveness e Readiness

**Categoria:** Observabilidade e operacao  
**Status:** Implementado

#### Problema resolvido

Processo vivo nao significa pronto para receber trafego. APIs precisam expor checks simples de vida e prontidao.

#### Onde foi aplicado

APIs e Compose local.

#### Como funciona neste repositorio

`/health` indica vida do processo; `/ready` valida dependencias essenciais para trafego HTTP, como conexao com banco. Compose usa healthchecks para orquestrar subida local.

#### Beneficios obtidos

- Ajuda scripts locais e troubleshooting.
- Separa liveness de dependencia obrigatoria.

#### Trade-offs e limitacoes

- Readiness com DbContext no `Program.cs` deve permanecer simples.

#### Evidencias

- [`src/ledger/LedgerService.Api/Program.cs`](../../src/ledger/LedgerService.Api/Program.cs)
- [`src/payment/PaymentService.Api/Program.cs`](../../src/payment/PaymentService.Api/Program.cs)
- [`src/Shared/ApiDefaults/Health`](../../src/Shared/ApiDefaults/Health)
- [`docs/troubleshooting.md`](../troubleshooting.md)

#### ADRs e documentacao relacionados

- [ADR-0017](../adrs/0017-implementar-dlq-versionamento-eventos-readiness-operacional.md)
- [ADR-0021](../adrs/0021-padronizar-exposicao-operacional-swagger-cors-health.md)

### Runbook

**Categoria:** Observabilidade e operacao  
**Status:** Implementado

#### Problema resolvido

Operacao de DLQ, replay, Pub/Sub, Payment Worker e Saga nao deve depender de conhecimento tacito.

#### Onde foi aplicado

`docs/operations`.

#### Como funciona neste repositorio

Runbooks diferenciam retry, requeue, replay, redrive, discard, DLQ tecnica, DLQ de aplicacao, Outbox e rebuild de projecao.

#### Beneficios obtidos

- Torna recuperacao repetivel.
- Evita acoes perigosas como republicar payload invalido sem corrigir causa.

#### Trade-offs e limitacoes

- Runbook fica obsoleto se codigo/runtime mudarem sem documentacao.

#### Evidencias

- [`docs/operations/event-recovery-runbook.md`](../operations/event-recovery-runbook.md)
- [`docs/operations/dlq-strategy.md`](../operations/dlq-strategy.md)
- [`docs/operations/payment-worker.md`](../operations/payment-worker.md)
- [`docs/operations/transfer-saga-kafka.md`](../operations/transfer-saga-kafka.md)

#### ADRs e documentacao relacionados

- [ADR-0033](../adrs/0033-governanca-documentacao-operacional.md)

## Padroes de infraestrutura

### Reverse Proxy / Edge Gateway local

**Categoria:** Infraestrutura  
**Status:** Opcional

#### Problema resolvido

Desenvolvimento local precisa de uma borda HTTPS padronizada para Swaggers, headers, roteamento e portal sem substituir as portas HTTP diretas.

#### Onde foi aplicado

Nginx local opcional.

#### Como funciona neste repositorio

Overlay Nginx serve portal local, roteia subdominios `.localhost` para APIs, propaga `X-Forwarded-*` e `X-Correlation-Id`, aplica headers e limites defensivos.

#### Beneficios obtidos

- Exercita borda local semelhante a ambiente real.
- Centraliza acesso HTTPS local.

#### Trade-offs e limitacoes

- Nao e desenho produtivo final de gateway/WAF.
- Exige certificados locais.

#### Evidencias

- [`infra/nginx/nginx.conf`](../../infra/nginx/nginx.conf)
- [`infra/nginx/README.md`](../../infra/nginx/README.md)
- [`compose.nginx.yaml`](../../compose.nginx.yaml)
- [`docs/troubleshooting.md`](../troubleshooting.md)

#### ADRs e documentacao relacionados

- [ADR-0071](../adrs/0071-borda-local-nginx-https.md)

### Load Balancer local

**Categoria:** Infraestrutura  
**Status:** Opcional

#### Problema resolvido

A POC precisa demonstrar horizontal scaling local do `LedgerService.Api` sem introduzir plataforma gerenciada.

#### Onde foi aplicado

Nginx balanceando duas instancias do LedgerService.Api.

#### Como funciona neste repositorio

O upstream `ledger_api` usa `least_conn` para `ledger-service-1` e `ledger-service-2`. O balanceamento se aplica ao host `ledger.localhost:7443`.

#### Beneficios obtidos

- Demonstra multiplas instancias atras da borda.
- Ajuda testar idempotencia e correlation id via proxy.

#### Trade-offs e limitacoes

- E local e limitado ao Ledger; nao representa load balancer produtivo global.

#### Evidencias

- [`infra/nginx/nginx.conf`](../../infra/nginx/nginx.conf)
- [`compose.nginx.yaml`](../../compose.nginx.yaml)
- [`docs/troubleshooting.md`](../troubleshooting.md)

#### ADRs e documentacao relacionados

- [ADR-0072](../adrs/0072-load-balance-local-ledger-nginx.md)

### API Gateway completo

**Categoria:** Infraestrutura  
**Status:** Nao identificado como padrao explicito

#### Problema resolvido

Um API Gateway completo agregaria contratos, transformaria payloads, aplicaria quotas por consumidor e politicas avancadas.

#### Onde foi aplicado

Nao ha implementacao completa. O Nginx atual deve ser chamado de Reverse Proxy / Edge Gateway local e Load Balancer local.

#### Como funciona neste repositorio

Nginx roteia, aplica HTTPS/headers/limites e balanceia Ledger; nao agrega contratos, nao transforma payloads de negocio e nao implementa politicas avancadas por consumidor.

#### Beneficios obtidos

- Terminologia precisa evita superestimar a arquitetura.

#### Trade-offs e limitacoes

- Se a POC precisar de gateway completo, sera nova decisao arquitetural.

#### Evidencias

- [`infra/nginx/nginx.conf`](../../infra/nginx/nginx.conf)
- [`docs/architecture/production-readiness.md`](production-readiness.md)

#### ADRs e documentacao relacionados

- [ADR-0071](../adrs/0071-borda-local-nginx-https.md)
- [ADR-0072](../adrs/0072-load-balance-local-ledger-nginx.md)

### Workers como processos separados

**Categoria:** Infraestrutura  
**Status:** Implementado

#### Problema resolvido

Processamento continuo nao deve competir com HTTP no mesmo processo nem subir por acidente dentro da API.

#### Onde foi aplicado

LedgerService.Worker, BalanceService.Worker, TransferService.Worker, PaymentService.Worker e AuditService.Worker.

#### Como funciona neste repositorio

Cada Worker tem `Program.cs` proprio, registra hosted services e adapters necessarios. APIs permanecem com superficie HTTP.

#### Beneficios obtidos

- Permite escala, falha e deploy independentes.
- Torna composition root explicito por processo.

#### Trade-offs e limitacoes

- Aumenta numero de containers e health checks locais.

#### Evidencias

- [`src/payment/PaymentService.Worker/Program.cs`](../../src/payment/PaymentService.Worker/Program.cs)
- [`src/transfer/TransferService.Worker/Program.cs`](../../src/transfer/TransferService.Worker/Program.cs)
- [`compose.yaml`](../../compose.yaml)
- [`tests/ledger/LedgerService.Worker.Tests/Composition/ProcessCompositionPolicyTests.cs`](../../tests/ledger/LedgerService.Worker.Tests/Composition/ProcessCompositionPolicyTests.cs)

#### ADRs e documentacao relacionados

- [ADR-0065](../adrs/0065-workers-dedicados-no-compose-local.md)
- [ADR-0067](../adrs/0067-separacao-workers-processos-api.md)

### Containerization local

**Categoria:** Infraestrutura  
**Status:** Implementado

#### Problema resolvido

A POC precisa subir dependencias, APIs, workers, Keycloak, Kafka, PostgreSQL e observabilidade de forma reproduzivel localmente.

#### Onde foi aplicado

Docker Compose e Dockerfiles dos servicos.

#### Como funciona neste repositorio

Scripts em `scripts/local` geram `.env.local`, aplicam migrations e sobem compose base ou overlays opcionais de Pub/Sub, observabilidade e Nginx.

#### Beneficios obtidos

- Onboarding local reproduzivel.
- Permite validar fluxos ponta a ponta.

#### Trade-offs e limitacoes

- E padrao de implantacao local, nao design pattern de dominio nem blueprint de producao.

#### Evidencias

- [`compose.yaml`](../../compose.yaml)
- [`compose.observability.yaml`](../../compose.observability.yaml)
- [`compose.nginx.yaml`](../../compose.nginx.yaml)
- [`docs/development/local-development.md`](../development/local-development.md)

#### ADRs e documentacao relacionados

- [ADR-0055](../adrs/0055-runtime-docker-compatible-testcontainers.md)
- [ADR-0065](../adrs/0065-workers-dedicados-no-compose-local.md)

## Padroes nao identificados ou nao implementados explicitamente

| Padrao | Classificacao | Observacao |
| --- | --- | --- |
| Decorator | Nao identificado como padrao explicito | Pode haver `DelegatingHandler`/pipelines que se aproximam, mas nao e padrao central documentado por evidencia. |
| Chain of Responsibility | Nao identificado como padrao explicito | Middlewares ASP.NET formam pipeline, mas o repositorio nao trata isso como padrao de design proprio. |
| Event Sourcing | Nao implementado | Outbox, replay e rebuild de projecao nao significam reconstruir aggregates exclusivamente a partir de event store. |
| Service Mesh | Nao implementado | Nao ha sidecars, mTLS mesh, traffic policy ou control plane. |
| API Gateway completo | Nao implementado | Nginx e reverse proxy/edge local e load balancer, nao gateway completo. |
| Bulkhead explicito | Nao identificado como padrao explicito | Separar workers melhora isolamento, mas nao ha bulkheads por pool/fila/dependencia. |
| CQRS completo em todos os contexts | Nao implementado | O projeto usa CQRS pragmatico principalmente no fluxo Ledger/Balance e comandos/queries em Application. |
| Optimistic Concurrency central | Nao identificado como padrao explicito | Concorrencia atual usa locks, claims, leases e constraints unicas. |

## Relacao com ADRs e documentacao

Este catalogo le ADRs como registro historico de decisoes e documentos operacionais como evidencia de runtime/operacao. Os principais alinhamentos sao:

- ADR-0001, ADR-0002 e ADR-0003 explicam a base Ledger/Balance, Clean Architecture/DDD e Outbox.
- ADR-0075, ADR-0077, ADR-0078 e ADR-0088 explicam a evolucao Kafka/PubSub, com Kafka como default atual e Pub/Sub explicito/legado.
- ADR-0087 registra a Saga orquestrada; o arquivo individual ja esta como `Aceito` e o codigo confirma runtime implementado.
- ADR-0101 a ADR-0105 registram PaymentService, ACL Stripe, Inbox, integracao Ledger e ordenacao/deduplicacao; os arquivos individuais ja estao como `Aceito` e o codigo confirma runtime implementado.
- ADR-0085 continua tratada como proposta/parcial no catalogo de secrets, porque ha progresso real com `.env.local` e placeholders, mas a politica ainda nao aparece uniforme em todos os servicos.
- Documentos de operacao de replay/DLQ/projection rebuild sao evidencias de recuperacao operacional, mas nao transformam o repositorio em Event Sourcing.

## Como manter este catalogo atualizado

- Atualize este catalogo quando um novo padrao virar runtime real, nao apenas quando uma ADR for escrita.
- Para cada novo item, inclua problema resolvido, local de aplicacao, fluxo real, beneficios, trade-offs, evidencias e ADRs/documentos relacionados.
- Se uma decisao sair de `Proposto` para implementada, alinhe tambem o arquivo ADR e o indice `docs/adrs/README.md`.
- Nao liste toda classe relacionada; escolha de duas a cinco evidencias representativas.
- Preserve terminologia precisa: Nginx e Reverse Proxy / Edge Gateway local e Load Balancer local; replay/rebuild de projecao nao e Event Sourcing.
