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
2. Container view do bounded context escolhido: `ledgerContainers`,
   `balanceContainers`, `transferContainers`, `paymentContainers`,
   `identityServiceContainers` ou `auditContainers`.
3. `businessContainers`: executaveis, bancos logicos e relacoes funcionais dos
   servicos de negocio.
4. `integrationContainers`: Kafka, provedores externos, identidade, e-mail e
   chamadas HTTP service-to-service.
5. Fluxos principais: lancamento, transferencia, compensacao, pagamento, refund
   e auditoria.
6. Component view da API ou Worker do servico que sera alterado.
7. `platformContainers` e `localDeployment`: runtime local, plataforma,
   observabilidade e mapeamento para Compose.
8. `containers`: referencia expandida quando precisar ver tudo junto.
9. Markdown complementar, ADRs, specs e runbooks quando precisar de detalhe.

Evite ler todas as views em sequencia como se fossem capitulos. Cada view deve
ser usada como resposta a uma pergunta.

Na UI gerada do LikeC4, use o clique como drill-down conceitual: clique em um
bounded context no `systemLandscape` para abrir sua view de containers; clique
em uma API ou Worker para abrir a component view correspondente; use as dynamic
views quando a pergunta for sobre a ordem de uma operacao no tempo. Esse caminho
mantem os niveis C4 separados e evita que um unico diagrama misture ecossistema,
processos, classes e fluxo.

## Jornadas arquiteturais

### Jornada rapida

Para entender o projeto em cerca de 10 minutos, leia `systemLandscape`,
`businessContainers`, `ledgerContainers`, `paymentContainers`,
`ledgerBalanceProjectionFlow`,
`transferSagaSuccessFlow` e `paymentLedgerMaterializationFlow`. Essa jornada
mostra o ecossistema, os servicos de negocio e os tres fluxos que mais explicam
a POC atual.

### Jornada para iniciantes

Comece por esta pagina, leia as secoes "O que e C4 Model", "O que e LikeC4" e
"Niveis usados neste repositorio", depois abra `systemLandscape`,
`businessContainers`, a container view de um bounded context e um fluxo dynamic
por vez. Se um termo aparecer estranho, use `boundaries.md`,
`patterns-catalog.md`, `docs/development/kafka-outbox.md` e
`docs/events/README.md` antes de ir para component views.

### Jornada por bounded context

Escolha o servico e siga a view de containers antes da view de componentes:
Ledger usa `ledgerContainers`, `ledgerApiComponents` e
`ledgerWorkerComponents`; Balance usa `balanceContainers`,
`balanceApiComponents` e `balanceWorkerComponents`; Transfer usa
`transferContainers`, `transferApiComponents` e `transferWorkerComponents`;
Payment usa `paymentContainers`, `paymentApiComponents` e
`paymentWorkerComponents`; Identity usa `identityServiceContainers`,
`identityServiceComponents` e `identityComponents`; Audit usa
`auditContainers`, `auditApiComponents` e `auditWorkerComponents`.

### Jornada por fluxo

Para seguir uma operacao entre servicos, use as dynamic views: lancamento e
saldo em `ledgerBalanceProjectionFlow`, cadastro em
`identityRegistrationFlow`, transferencia em `transferSagaSuccessFlow` ou
`transferSagaCompensationFlow`, pagamento em `paymentCreateFlow`,
`paymentWebhookInboxFlow`, `paymentLedgerMaterializationFlow` e
`paymentRefundFlow`, auditoria em `auditKafkaIngestionFlow` e modo legado em
`pubSubLegacyProjectionFlow`.

### Jornada operacional

Para runtime e falhas, leia `platformContainers`, `localDeployment`,
`kafkaFlow`, `observabilityFlow`, `docs/operations/dlq-strategy.md`,
`docs/operations/replay-strategy.md`, `docs/operations/payment-worker.md` e
`docs/operations/audit-worker.md`. Essa jornada separa o ambiente local atual
de baselines futuros descritos em `production-readiness.md`.

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

Mostra componentes internos relevantes de um container ou bounded context,
respeitando camadas: `Api`, `Application`, `Domain`, `Infrastructure` e
`Worker`. Quando `Application`, `Domain` ou `Infrastructure` sao assemblies
compartilhados por API e Worker, a view deve deixar claro que eles nao pertencem
ao processo HTTP nem sao implantados como servicos separados. Nao deve
substituir o codigo nem listar classes sem decisao arquitetural envolvida.

As component views seguem duas granularidades:

- **Estrutural**: usada principalmente para APIs. Mostra superficie HTTP,
  camadas/assemblies, persistencia e externos relevantes. Evita listar classes
  concretas como `DbContext`, templates e adapters quando isso nao responder a
  pergunta da view.
- **Tecnica de pipeline**: usada principalmente para Workers. Mostra
  composition root, HostedServices, processors, ports, adapters, topicos e
  persistencia envolvidos no fluxo operacional. Nao repete camadas genericas
  quando o objetivo da view e explicar o pipeline.

Use componentes internos com estes papeis:

- **Camadas arquiteturais**: `API Layer`, `Application`, `Domain` e
  `Infrastructure` aparecem em views estruturais.
- **Casos de uso**: aparecem como handlers/services de Application quando a
  decisao arquitetural ou o fluxo exigir essa precisao.
- **Ports**: interfaces como `IOutboxMessagePublisher` aparecem quando ajudam a
  separar regra/orquestracao de adapter de infraestrutura.
- **Adapters**: clients HTTP, producers/consumers Kafka, Pub/Sub ou e-mail
  aparecem em views tecnicas de integracao/pipeline.
- **Processors**: componentes que traduzem, validam ou orquestram mensagens
  aparecem em views tecnicas ou dynamic views.
- **HostedServices**: aparecem em Workers quando representam polling, consumer,
  publisher, Inbox, Outbox ou processamento de Saga.
- **Composition roots**: aparecem em APIs/Workers quando a separacao de DI e
  processo ajuda a evitar confusao de ownership.
- **Externos e persistencia**: aparecem como containers externos ao escopo da
  component view, nao como classes internas.

### Dynamic / Flow

Mostra a sequencia de uma operacao. Use para entender tempo, ownership,
idempotencia, Inbox, Outbox, retry, DLQ e chamadas entre contexts.

### Operational / Runtime

Mostra runtime local, mensageria, observabilidade e deployment. Use para
diagnostico operacional e para distinguir Compose local de arquitetura logica.

## Convencao visual

Os diagramas usam uma taxonomia pequena de tags em `model.c4` e um
`styleGroup architectureTheme` em `views.c4`, aplicado a todas as views. A
legenda visual usa as notations do LikeC4 e pode ser consultada pelo botao de
ajuda/legenda da UI gerada.

| Tag | Significado | Forma/cor principal |
| --- | --- | --- |
| `api` | API HTTP interna | Browser, indigo |
| `worker` | Processo sem HTTP / Worker Service | Component, amber |
| `database` | Persistencia ou schema logico | Cylinder, green |
| `broker` | Broker de mensageria | Queue, red |
| `topic` | Topico, DLQ ou subscription | Queue, secondary |
| `external` | Ator ou sistema externo | Muted, borda solida |
| `identity-provider` | IdP/JWKS/OIDC | Component, secondary |
| `observability` | Telemetria, logs, metricas, dashboards e alertas | Component, gray |
| `optional` | Elemento habilitado por configuracao, profile ou overlay | Borda dotted, opacidade reduzida |
| `future` | Elemento modelado, mas ainda nao integrado ao fluxo principal | Muted, borda dotted, opacidade reduzida |
| `legacy` | Alternativa legada ainda executavel explicitamente | Borda dashed, opacidade reduzida |

Cores indicam familias, mas nao sao a unica fonte de significado: APIs,
Workers, bancos, brokers/topicos e IdP usam formas diferentes; estados
`optional`, `future` e `legacy` usam borda/opacidade alem da cor. Quando um
elemento acumula tags, o estilo mais especifico de estado pode reduzir a
opacidade ou neutralizar a cor para evitar apresentar futuro/opcional como
fluxo principal.

Elementos atuais do runtime padrao devem aparecer sem `future` e sem `legacy`.
Kafka e o broker padrao dos fluxos principais. Pub/Sub fica marcado como
`legacy` e `optional`. O caminho Kafka de auditoria fica marcado como
`optional`/`future` enquanto nao houver producers reais em Ledger, Balance,
Transfer ou Payment.

Limitacao da versao atual: o projeto usa LikeC4 `1.58.0`. As notations de view
existem e foram usadas como legenda visual, mas o proprio LikeC4 ainda trata
esse recurso como experimental, e notations de relacionamentos permanecem em
progresso. Por isso, a convencao tambem fica documentada aqui e as relacoes
continuam explicadas por titulos, tecnologias e descricoes das views.

## Diagramas disponiveis

| Diagrama | Nivel | Quando consultar | O que responde |
| --- | --- | --- | --- |
| `systemLandscape` | System Context | Inicio da leitura | Quais sistemas, contexts e externos existem no ecossistema |
| `businessContainers` | Container | Primeira leitura detalhada dos servicos | Quais executaveis, schemas logicos e relacoes funcionais existem por bounded context |
| `identityServiceContainers` | Container | Aprofundar IdentityService a partir do contexto | Quais containers, dados e externos sustentam cadastro, Keycloak e e-mail |
| `ledgerContainers` | Container | Aprofundar LedgerService a partir do contexto | Quais containers, dados e topicos sustentam o fato financeiro e Outbox |
| `balanceContainers` | Container | Aprofundar BalanceService a partir do contexto | Quais containers, dados e topicos sustentam a projecao de saldo |
| `transferContainers` | Container | Aprofundar TransferService a partir do contexto | Quais containers, dados, chamadas Ledger e topicos sustentam a Saga |
| `paymentContainers` | Container | Aprofundar PaymentService a partir do contexto | Quais containers, dados e externos sustentam pagamentos, webhooks e materializacao Ledger |
| `auditContainers` | Container | Aprofundar AuditService a partir do contexto | Quais containers, dados e topicos sustentam auditoria HTTP/Kafka |
| `integrationContainers` | Container | Entender integracoes entre servicos e externos | Como Kafka, topicos, Keycloak, Stripe, e-mail e chamadas HTTP service-to-service conectam os servicos |
| `platformContainers` | Container / Runtime | Entender plataforma local sem entrar no deployment fisico | Como Nginx, Keycloak, PostgreSQL compartilhado por schemas, Kafka e observabilidade sustentam o runtime local |
| `containers` | Container | Referencia expandida, nao primeira leitura | Qual e o mapa completo quando APIs, Workers, schemas, broker, externos e observabilidade precisam aparecer juntos |
| `ledgerBalanceProjectionFlow` | Dynamic / Flow | Entender lancamento financeiro e saldo | Como Ledger grava o fato, publica Outbox no Kafka e Balance projeta saldo |
| `identityRegistrationFlow` | Dynamic / Flow | Entender cadastro de usuario | Como IdentityService cria usuario no Keycloak, persiste vinculo local e envia e-mail |
| `kafkaFlow` | Operational / Runtime | Entender mensageria assincrona padrao | Onde Kafka e usado por Ledger, Balance, Transfer e auditoria |
| `pubSubLegacyProjectionFlow` | Operational / Runtime | Diagnosticar o modo Pub/Sub legado | Como Ledger publica e Balance consome via Pub/Sub quando `Messaging:Provider=PubSub` |
| `observabilityFlow` | Operational / Observability | Entender telemetria local | Como APIs, Workers, Collector, Jaeger, Prometheus, Loki, Alloy, Alertmanager e Grafana se conectam |
| `localDeployment` | Deployment / Runtime | Entender Docker Compose local | Quais servicos do Compose atual existem e a que elementos logicos correspondem |
| `ledgerApiComponents` | Component | Revisar LedgerService.Api | Como HTTP, Application, Domain, Infrastructure e schema ledger se separam |
| `ledgerWorkerComponents` | Component / Pipeline tecnico | Revisar LedgerService.Worker | Como Outbox Kafka, estornos, reprocessamento, processors, ports, adapters, topicos e persistencia ficam no Worker |
| `balanceApiComponents` | Component | Revisar BalanceService.Api | Como a API consulta a projecao sem criar fatos financeiros |
| `balanceWorkerComponents` | Component / Pipeline tecnico | Revisar BalanceService.Worker | Como consumer, mapper, processor, porta de DLQ, topicos e schema balance projetam eventos do Ledger |
| `transferApiComponents` | Component | Revisar TransferService.Api | Como HTTP, Application, Domain, Infrastructure, idempotencia, Outbox, schema transfer e Keycloak se conectam sem regra de Saga no controller |
| `transferWorkerComponents` | Component | Revisar TransferService.Worker | Como o Worker reclama Sagas, chama Ledger com client credentials, aplica retry/backoff/compensacao e publica Outbox Kafka/DLQ |
| `transferSagaSuccessFlow` | Dynamic / Flow | Entender transferencia concluida | Como Transfer registra a Saga, cria debito/credito no Ledger, publica eventos de Saga Kafka e permite consulta de status |
| `transferSagaCompensationFlow` | Dynamic / Flow | Entender falha compensavel | Como falha no credito apos debito gera solicitacao de estorno no Ledger, evento de compensacao solicitada e possivel falha definitiva |
| `identityServiceComponents` | Component / Estrutural | Revisar IdentityService.Api | Como superficie HTTP, camadas, persistencia, Keycloak e e-mail se conectam |
| `identityComponents` | Component | Revisar Keycloak local | Quais partes do IdP local importado importam para autenticacao |
| `paymentApiComponents` | Component / Estrutural | Revisar PaymentService.Api | Como controllers usam Application/Domain/Infrastructure compartilhadas sem transformar essas bibliotecas em parte exclusiva da API |
| `paymentWorkerComponents` | Component / Pipeline tecnico | Revisar PaymentService.Worker | Como Inbox, mapper de provider, materializacao no Ledger, processor, gateway HTTP e schema payment rodam fora da API |
| `paymentCreateFlow` | Dynamic / Flow | Entender criacao de Payment | Como cliente, PaymentService.Api, provider externo e schema payment interagem |
| `paymentWebhookInboxFlow` | Dynamic / Flow | Entender webhooks Stripe | Como Stripe entra pela API, e persistido na Inbox e processado pelo Worker |
| `paymentLedgerMaterializationFlow` | Dynamic / Flow | Entender Payment -> Ledger -> Balance | Como pagamento confirmado vira lancamento Ledger e saldo projetado |
| `paymentRefundFlow` | Dynamic / Flow | Entender refund total | Como refund Stripe vira estorno Ledger e evento compensatorio para Balance |
| `auditApiComponents` | Component | Revisar AuditService.Api | Como o contrato HTTP canonico usa Application/Domain/Infrastructure compartilhadas para persistir registros no schema audit |
| `auditWorkerComponents` | Component / Pipeline tecnico | Revisar AuditService.Worker | Como o consumer Kafka processa AuditRecordRequested.v1 sem depender do container da API |
| `auditKafkaIngestionFlow` | Dynamic / Flow | Entender auditoria assincrona | Como o Worker consome auditoria no Compose padrao enquanto os demais dominios ainda nao publicam producers reais |

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

No modelo LikeC4, `PaymentService.Application`, `PaymentService.Domain` e
`PaymentService.Infrastructure` aparecem no nivel do bounded context para evitar
ownership visual enganoso: API e Worker sao processos separados que referenciam
essas bibliotecas, nao ha dependencia do Worker no container HTTP.

No ambiente local padrao, `payment-service` e `payment-worker` sobem pelo
`compose.yaml` apos as migrations do schema `payment` serem aplicadas pelos
scripts `scripts/local/start-stack.*`.

### IdentityService e Keycloak

Keycloak e o IdP local e emissor de JWT. IdentityService cadastra usuarios,
cria vinculo local, gera `MerchantId` e envia e-mail de boas-vindas. Identity
nao emite tokens e nao assume regra financeira.

### AuditService

Dono da trilha funcional de auditoria. A API HTTP canonica cria e consulta
registros no schema `audit`. O Worker Kafka consome `AuditRecordRequested.v1`
no Compose padrao, mas Ledger, Balance, Transfer e Payment ainda nao publicam
eventos reais de auditoria.

No modelo LikeC4, `AuditService.Application`, `AuditService.Domain` e
`AuditService.Infrastructure` tambem aparecem no nivel do bounded context. Isso
representa os assemblies compartilhados por `AuditService.Api` e
`AuditService.Worker`, sem sugerir que o Worker dependa da API.

No ambiente local padrao, `audit-service` e `audit-worker` sobem pelo
`compose.yaml` apos as migrations do schema `audit` serem aplicadas pelos
scripts `scripts/local/start-stack.*`. No modo Pub/Sub legado, o
`audit-worker` fica desativado porque o overlay desliga Kafka.

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

Veja `transferSagaSuccessFlow`, `transferSagaCompensationFlow`,
`transferApiComponents`, `transferWorkerComponents` e `kafkaFlow`.
TransferService orquestra a Saga, chama LedgerService.Api por HTTP e publica
eventos de Saga Kafka-only. Esses eventos nao sao fatos financeiros do Ledger e
nao alimentam saldo diretamente.

### Pagamento externo com Stripe

Veja `paymentCreateFlow`, `paymentWebhookInboxFlow` e
`paymentLedgerMaterializationFlow`. Stripe nunca chama Worker, banco, Kafka ou
Balance diretamente.

### Refund

Veja `paymentRefundFlow`. Refund total passa por PaymentService, Stripe,
webhook/Inbox, Worker, LedgerService.Api, Outbox do Ledger, Kafka e Balance.

### Autenticacao

Veja `systemLandscape`, `integrationContainers`, `identityRegistrationFlow` e
`identityComponents`. Keycloak e o emissor; APIs validam JWT/JWKS.

### Observabilidade

Veja `platformContainers` para contexto local e `observabilityFlow` para o fluxo
tecnico de telemetria. A stack e local e inclui OpenTelemetry Collector, Jaeger,
Prometheus, Loki, Grafana Alloy, Alertmanager e Grafana.

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
