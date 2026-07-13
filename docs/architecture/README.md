# Documentacao de Arquitetura

## Objetivo

Esta pasta explica a arquitetura atual da POC e mantem o modelo LikeC4 usado
para navegar pelos bounded contexts, containers, componentes e fluxos
distribuidos.

A documentacao aqui deve responder perguntas arquiteturais reais:

- quais sistemas existem e como se relacionam;
- quais APIs, workers, bancos, brokers e provedores externos compoem o runtime;
- onde ficam as fronteiras entre Ledger, Balance, Transfer, Payment, Identity e
  Audit;
- como lancamentos, saldos, pagamentos, refunds, webhooks, Outbox, Inbox, Kafka
  e autenticacao se conectam;
- como o caminho Kafka padrao se diferencia do caminho Pub/Sub explicito e
  legado ainda executavel para Ledger/Balance;
- quais partes fazem parte do runtime documentado e quais fluxos ainda sao
  opcionais ou futuros.

Ela nao substitui ADRs, specs, runbooks nem contratos OpenAPI/eventos. O papel
do LikeC4 e mostrar a arquitetura resultante dessas decisoes.

Como separar as leituras:

- Diagramas C4 mostram estrutura, relacionamentos, fluxos e deployment local.
- Boundaries explicam responsabilidades e dependencias permitidas entre
  `Api`, `Application`, `Domain`, `Infrastructure` e `Worker`.
- [Catalogo de padroes](patterns-catalog.md) explica solucoes reutilizaveis,
  problemas resolvidos, evidencias, status e trade-offs.
- ADRs registram decisoes, alternativas e consequencias historicas.
- Specs detalham requisitos e fluxos de uma iniciativa.
- Runbooks explicam operacao, recuperacao e troubleshooting.

## Como esta documentacao esta organizada

Arquivos principais:

- `model.c4`: elementos do modelo, relacionamentos e boundaries logicos.
- `views.c4`: diagramas navegaveis gerados a partir do modelo.
- `deployment.c4`: deployment local via Docker Compose para a aba
  `Deployments` do LikeC4.
- `boundaries.md`: regras de fronteira entre `Api`, `Application`, `Domain`,
  `Infrastructure` e `Worker`.
- `patterns-catalog.md`: catalogo de padroes arquiteturais, de design,
  integracao, resiliencia, seguranca, observabilidade e infraestrutura
  confirmados por evidencias do repositorio.
- `payment-service.md`: leitura arquitetural do PaymentService.
- `audit-service.md`: leitura arquitetural do AuditService.
- `decisions.md`: avaliacao critica e riscos arquiteturais.
- `production-readiness.md`: baseline de evolucao produtiva futura.

## O que e C4 Model

C4 e uma forma de ler arquitetura em niveis:

- **System Context**: quem usa o sistema e quais sistemas externos existem.
- **Container**: quais executaveis, bancos, brokers e provedores compoem o
  sistema.
- **Component**: quais blocos internos relevantes existem dentro de um container
  ou bounded context.
- **Dynamic / Flow**: como uma operacao acontece ao longo do tempo.
- **Deployment / Runtime**: onde os elementos rodam em um ambiente concreto.

Neste repositorio, "container" no C4 nao significa necessariamente Docker
container. Pode ser uma API, Worker, schema logico de banco, topico Kafka ou
sistema externo.

## O que e LikeC4

LikeC4 e a ferramenta que transforma os arquivos `.c4` em uma documentacao
visual navegavel. O modelo fica em texto versionado, e as views sao geradas a
partir desse modelo.

Na pratica:

- `model.c4` define os elementos e relacoes uma vez;
- `views.c4` escolhe recortes do modelo para responder perguntas especificas;
- `deployment.c4` mapeia elementos logicos para o ambiente local;
- `npm run architecture:build` gera o site estatico em `dist/architecture`.

## Como ler os diagramas

Use esta ordem de leitura para onboarding:

1. `systemLandscape`: ecossistema, atores, bounded contexts e externos.
2. `containers`: APIs, workers, schemas, Kafka e provedores externos.
3. Fluxos principais: lancamento, pagamento, refund, transferencia e auditoria.
4. Component view do servico que sera alterado.
5. `localDeployment`: mapeamento para o Compose local documentado.
6. Markdown complementar, ADRs, specs e runbooks quando precisar de detalhe.

Evite ler todas as views em sequencia como se fossem capitulos. Cada view deve
ser usada como resposta a uma pergunta.

## Niveis usados neste repositorio

### System Context

Mostra atores, bounded contexts, sistemas externos, broker e observabilidade em
alto nivel. Nao deve mostrar controllers, handlers, repositories, DbContext,
tabelas ou migrations.

### Container

Mostra APIs, Workers, schemas PostgreSQL, Kafka e provedores externos. Schemas
como `ledger`, `balance`, `transfer`, `payment`, `identity` e `audit`
representam isolamento logico por bounded context. No ambiente local, eles usam
um PostgreSQL compartilhado quando as migrations correspondentes sao aplicadas.

### Component

Mostra componentes internos relevantes de um container, respeitando camadas:
`Api`, `Application`, `Domain`, `Infrastructure` e `Worker`. Nao deve substituir
o codigo nem listar classes sem decisao arquitetural envolvida.

### Dynamic / Flow

Mostra a sequencia de uma operacao. Use para entender tempo, ownership,
idempotencia, Inbox, Outbox, retry, DLQ e chamadas entre contexts.

### Operational / Runtime

Mostra runtime local, mensageria, observabilidade e deployment. Use para
diagnostico operacional e para distinguir Compose local de arquitetura logica.

## Diagramas disponiveis

| Diagrama | Nivel | Quando consultar | O que responde |
| --- | --- | --- | --- |
| `systemLandscape` | System Context | Inicio da leitura | Quais sistemas, contexts e externos existem no ecossistema |
| `containers` | Container | Entender APIs, Workers, schemas, brokers e provedores | Quais executaveis e recursos logicos compoem a POC |
| `ledgerBalanceProjectionFlow` | Dynamic / Flow | Entender lancamento financeiro e saldo | Como Ledger grava o fato, publica Outbox no Kafka e Balance projeta saldo |
| `identityRegistrationFlow` | Dynamic / Flow | Entender cadastro de usuario | Como IdentityService cria usuario no Keycloak, persiste vinculo local e envia e-mail |
| `kafkaFlow` | Operational / Runtime | Entender mensageria assincrona padrao | Onde Kafka e usado por Ledger, Balance, Transfer e auditoria opcional |
| `pubSubLegacyProjectionFlow` | Operational / Runtime | Diagnosticar o modo Pub/Sub legado | Como Ledger publica e Balance consome via Pub/Sub quando `Messaging:Provider=PubSub` |
| `observabilityFlow` | Operational / Observability | Entender telemetria local | Como APIs, Workers, Collector, Jaeger, Prometheus, Loki, Alloy, Alertmanager e Grafana se conectam |
| `localDeployment` | Deployment / Runtime | Entender Docker Compose local | Quais servicos do Compose atual existem e a que elementos logicos correspondem |
| `ledgerApiComponents` | Component | Revisar LedgerService.Api | Como HTTP, Application, Domain, Infrastructure e schema ledger se separam |
| `ledgerWorkerComponents` | Component | Revisar LedgerService.Worker | Como Outbox Kafka, estornos e reprocessamento ficam no Worker |
| `balanceApiComponents` | Component | Revisar BalanceService.Api | Como a API consulta a projecao sem criar fatos financeiros |
| `balanceWorkerComponents` | Component | Revisar BalanceService.Worker | Como eventos do Ledger viram saldos e DLQ |
| `identityServiceComponents` | Component | Revisar IdentityService.Api | Como cadastro, Keycloak Admin API, Domain Event Dispatcher e e-mail se conectam |
| `identityComponents` | Component | Revisar Keycloak local | Quais partes do IdP local importado importam para autenticacao |
| `paymentApiComponents` | Component | Revisar PaymentService.Api | Como controllers, ACL Stripe/fake, Application, Domain, Infrastructure e Inbox se separam |
| `paymentWorkerComponents` | Component | Revisar PaymentService.Worker | Como Inbox, state machine e materializacao no Ledger rodam fora da API |
| `paymentCreateFlow` | Dynamic / Flow | Entender criacao de Payment | Como cliente, PaymentService.Api, provider externo e schema payment interagem |
| `paymentWebhookInboxFlow` | Dynamic / Flow | Entender webhooks Stripe | Como Stripe entra pela API, e persistido na Inbox e processado pelo Worker |
| `paymentLedgerMaterializationFlow` | Dynamic / Flow | Entender Payment -> Ledger -> Balance | Como pagamento confirmado vira lancamento Ledger e saldo projetado |
| `paymentRefundFlow` | Dynamic / Flow | Entender refund total | Como refund Stripe vira estorno Ledger e evento compensatorio para Balance |
| `auditApiComponents` | Component | Revisar AuditService.Api | Como o contrato HTTP canonico de auditoria persiste registros no schema audit |
| `auditWorkerComponents` | Component | Revisar AuditService.Worker | Como o consumer Kafka opcional processa AuditRecordRequested.v1 |
| `auditKafkaIngestionFlow` | Dynamic / Flow | Entender auditoria assincrona opcional | Como o Worker consome auditoria quando houver producer futuro, sem declarar producers atuais |

## Principais bounded contexts

### LedgerService

Dono do fato financeiro. A API recebe comandos HTTP e grava lancamentos,
idempotencia, estornos, reprocessamentos e Outbox no schema `ledger`. O Worker
publica a Outbox, processa estornos e reprocessamentos. Ledger nao e projecao de
saldo.

### BalanceService

Dono da projecao de saldo. O Worker consome eventos financeiros do Ledger e
atualiza `daily_balances`/`processed_events` no schema `balance`. A API consulta
a projecao. Balance nao cria fatos financeiros.

### TransferService

Dono da Saga de transferencia entre merchants. A API registra/consulta Sagas, o
Worker chama LedgerService.Api para debito/credito/compensacao e publica eventos
de Saga no Kafka. Transfer nao e PaymentService e nao usa Stripe.

### PaymentService

Dono do ciclo de vida de pagamentos externos. A API cria Payment/Refund,
integra com provider fake/Stripe por ACL e recebe webhooks assinados. A Inbox e
processada pelo Worker, que chama LedgerService.Api para materializar credito ou
estorno. Payment nao grava Ledger DB, nao grava Balance DB e nao publica evento
financeiro direto no Kafka.

No ambiente local padrao, `payment-service` e `payment-worker` sobem pelo
`compose.yaml` apos as migrations do schema `payment` serem aplicadas pelos
scripts `scripts/local/start-stack.*`.

### IdentityService e Keycloak

Keycloak e o IdP local e emissor de JWT. IdentityService cadastra usuarios,
cria vinculo local, gera `MerchantId` e envia e-mail de boas-vindas. Identity
nao emite tokens e nao assume regra financeira.

### AuditService

Dono da trilha funcional de auditoria. A API HTTP canonica cria e consulta
registros no schema `audit`. O Worker Kafka opcional consome
`AuditRecordRequested.v1` quando habilitado, mas Ledger, Balance, Transfer e
Payment ainda nao publicam eventos reais de auditoria.

## Fluxos principais

### Lancamento financeiro e saldo

Veja `ledgerBalanceProjectionFlow`. A fonte de verdade e Ledger; Balance apenas
projeta eventos do Ledger recebidos pelo Kafka, que e o provider padrao e
recomendado para onboarding.

O modo Pub/Sub continua documentado em `pubSubLegacyProjectionFlow` porque ainda
e executavel por `compose.pubsub.yaml` e `Messaging:Provider=PubSub` nos
workers de Ledger/Balance. Essa view fica isolada para nao poluir os diagramas
principais e deve ser lida como caminho explicito/legado, nao como default.

### Transferencia

Veja `containers` e `kafkaFlow`. TransferService orquestra a Saga e chama
LedgerService.Api. Os eventos de Saga sao Kafka-only e nao alimentam saldo.

### Pagamento externo com Stripe

Veja `paymentCreateFlow`, `paymentWebhookInboxFlow` e
`paymentLedgerMaterializationFlow`. Stripe nunca chama Worker, banco, Kafka ou
Balance diretamente.

### Refund

Veja `paymentRefundFlow`. Refund total passa por PaymentService, Stripe,
webhook/Inbox, Worker, LedgerService.Api, Outbox do Ledger, Kafka e Balance.

### Autenticacao

Veja `systemLandscape`, `containers`, `identityRegistrationFlow` e
`identityComponents`. Keycloak e o emissor; APIs validam JWT/JWKS.

### Observabilidade

Veja `observabilityFlow`. A stack e local e inclui OpenTelemetry Collector,
Jaeger, Prometheus, Loki, Grafana Alloy, Alertmanager e Grafana.

## Como gerar os diagramas localmente

Instale dependencias Node e gere o site estatico:

```bash
npm ci
npm run architecture:build
```

O build grava a saida em `dist/architecture`. A publicacao no GitHub Pages e
feita pelo workflow `architecture-pages`:

<https://rodri-oliveira-dev.github.io/poc-arquitetura/>

Detalhes operacionais ficam em
[`docs/development/github-pages.md`](../development/github-pages.md).

## Como adicionar ou alterar diagramas

Antes de criar uma view nova, escreva a pergunta que ela responde. So crie um
novo diagrama se os diagramas existentes nao responderem essa pergunta.

Bons motivos:

- novo bounded context;
- novo fluxo distribuido;
- nova integracao externa critica;
- nova decisao de runtime/deployment;
- mudanca relevante em mensageria, seguranca, banco ou observabilidade.

Maus motivos:

- duplicar uma view existente;
- mostrar uma classe isolada;
- documentar detalhe que pertence ao codigo;
- embelezar a documentacao;
- criar variacao sem pergunta nova.

Ao alterar diagramas, confira tambem:

- ADRs relacionadas;
- estrutura real em `src/<contexto>` e `tests/<contexto>`;
- Compose, scripts e docs operacionais quando a mudanca afetar runtime;
- contratos OpenAPI/eventos quando a mudanca afetar API ou mensageria.

## Criterios para remover diagramas

Remova ou consolide diagramas quando eles:

- ficam obsoletos;
- duplicam outro diagrama;
- contradizem ADRs, specs, codigo ou Compose;
- misturam niveis demais;
- nao tem pergunta clara;
- representam futuro como se fosse runtime atual;
- confundem mais do que ajudam.

Ao remover uma view, ajuste referencias no README, docs relacionados e valide o
build LikeC4.

## Relacao com ADRs

Use esta regra:

- ADRs registram decisoes.
- LikeC4 mostra a arquitetura resultante dessas decisoes.
- Specs detalham requisitos, fluxos e criterios de aceite.
- Runbooks explicam operacao e troubleshooting.
- README orienta navegacao.

Nao reescreva ADR historica para parecer documentacao atual. Quando uma decisao
evoluir, crie nova ADR ou registre a evolucao na ADR existente conforme o padrao
do repositorio.

ADRs mais relevantes para estes diagramas:

- [ADR-0001: Ledger e Balance com projecao assincrona](../adrs/0001-separar-ledger-e-balance-com-projecao.md)
- [ADR-0002: Clean Architecture + DDD por microservico](../adrs/0002-clean-architecture-ddd-por-servico.md)
- [ADR-0003: Kafka com Outbox](../adrs/0003-integracao-assincrona-kafka-com-outbox.md)
- [ADR-0074: Keycloak como identidade principal](../adrs/0074-keycloak-como-identidade-principal.md)
- [ADR-0081: PostgreSQL local unico com schemas por servico](../adrs/0081-postgres-local-unico-com-schemas-por-servico.md)
- [ADR-0087: TransferService com Saga orquestrada Kafka](../adrs/0087-saga-orquestrada-transfer-service-kafka.md)
- [ADR-0088: Kafka como default dos workers principais](../adrs/0088-kafka-default-ledger-balance-workers.md)
- [ADR-0089: IdentityService](../adrs/0089-bounded-context-identity-service.md)
- [ADR-0097: AuditService](../adrs/0097-functional-audit-service.md)
- [ADR-0099: Integracao assincrona futura do AuditService](../adrs/0099-audit-async-integration-strategy.md)
- [ADR-0100: Solutions por contexto](../adrs/0100-organizacao-solutions-contexto-agregadora.md)
- [ADR-0101 a ADR-0105: PaymentService e Stripe](../adrs/0101-payment-service-bounded-context.md)

## Troubleshooting

Se `npm run architecture:build` falhar:

- confirme que executou `npm ci`;
- verifique erros de referencia a elementos ou views removidas;
- procure elementos sem correspondencia real em `src/`, `tests/`, Compose ou
  documentacao operacional;
- valide se uma relacao pertence ao nivel correto: contexto, container,
  componente, fluxo ou deployment;
- rode `git diff --check` para capturar whitespace antes do commit.

Se uma view parecer grande demais, provavelmente esta misturando perguntas.
Prefira uma view dinamica curta para fluxo e uma component view separada para
detalhes internos.
