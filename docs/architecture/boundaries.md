# Boundaries arquiteturais

## Classificacao encontrada

A solucao atual e uma arquitetura hibrida:

- Clean Architecture/DDD em LedgerService e BalanceService, com projetos `Api`, `Application`, `Domain` e `Infrastructure`.
- Clean Architecture/DDD em IdentityService, com projetos `Api`, `Application`, `Domain` e `Infrastructure` em `src/identity`.
- Clean Architecture/DDD em TransferService, com projetos `Api`, `Application`, `Domain`, `Infrastructure` e `Worker`, Saga orquestrada, persistencia, Outbox Kafka e endpoints HTTP.
- Clean Architecture/DDD em PaymentService, com projetos `Api`, `Application`, `Domain`, `Infrastructure` e `Worker`, Inbox para webhooks Stripe, ACL de provider e integracao idempotente com Ledger via HTTP.
- Clean Architecture/DDD em AuditService, com projetos `Api`, `Application`, `Domain`, `Infrastructure` e `Worker`, API HTTP canonica e consumer Kafka sem producers reais nos demais contexts.
- Elementos hexagonais onde existem contratos de persistencia e implementacoes em Infrastructure.
- Layered architecture na entrega HTTP, porque controllers, auth e composicao ficam concentrados nos projetos `*.Api`, com defaults tecnicos comuns em `Shared/ApiDefaults`.
- Workers dedicados (`LedgerService.Worker` e `BalanceService.Worker`) para processamento assincrono continuo, sem superficie HTTP.
- CQRS pragmatico entre servicos: Ledger escreve e publica eventos; Balance consome e mantem uma projecao de leitura.
- Keycloak e o provedor principal de autenticacao/JWT da stack local.
- IdentityService e o bounded context de cadastro e vinculo local de usuarios.
- PaymentService e o bounded context de pagamentos externos; ele nao grava Ledger DB, Balance DB nem publica evento financeiro direto no Kafka.
- AuditService e o bounded context de auditoria funcional; ele nao deve conhecer tipos internos de Ledger, Balance, Transfer ou Payment.

## Boundaries recomendados

### API

Deve conter:

- contratos HTTP, controllers ou endpoints;
- binders e mappers de entrada/saida HTTP;
- autenticacao, autorizacao, scopes, merchant authorization e policies;
- configuracoes HTTP especificas do servico, Swagger especifico, health e readiness;
- composicao do processo via DI.

Nao deve conter:

- regra de negocio de lancamento ou consolidacao;
- acesso direto a repositories de negocio para executar caso de uso;
- regra de retry/outbox/DLQ que pertenca ao pipeline de mensageria;
- registro de `HostedService` de worker, consumer Kafka ou publisher Outbox;
- detalhes de schema relacional alem de checks operacionais inevitaveis.

Observacao real: os endpoints `/ready` acessam `DbContext` diretamente no `Program.cs`. Isso e aceitavel para readiness operacional desta POC, mas deve permanecer limitado a dependencias necessarias para trafego HTTP.

### Shared/ApiDefaults

Deve conter apenas defaults HTTP tecnicos comuns entre APIs de negocio: ProblemDetails, registro de exception handler, correlation id, security headers, limite de body, forwarded headers, CORS, rate limit, versionamento e defaults Swagger.

Nao deve conter regra de negocio, merchant authorization, scopes especificos, handlers de excecao dependentes do dominio ou readiness dependente de `DbContext`.

### Worker

Deve conter:

- composition root explicito do processo de background;
- registro de `BackgroundService`/`IHostedService`;
- configuracao de mensageria, providers concretos, Outbox, DLQ, polling, retry, locks e consumers;
- observabilidade do processo com `ServiceName` proprio.

Nao deve conter:

- controllers, Swagger, CORS, rate limit ou superficie HTTP;
- validacao de JWT/JWKS de requests HTTP;
- regras de negocio duplicadas fora da Application/Domain.

Observacao real: `LedgerService.Worker` registra Outbox, estorno e reprocessamento; `BalanceService.Worker` registra o consumer Kafka de eventos do Ledger e DLQ. APIs e workers compartilham Application/Infrastructure, mas cada processo decide explicitamente quais adapters e HostedServices registra. A composition root deve manter entradas explicitas de mensageria e concentrar detalhes de Kafka nos workers.

### Application

Deve conter:

- casos de uso e handlers;
- validacao de input de caso de uso;
- orquestracao de transacao;
- idempotencia de aplicacao;
- criacao de eventos de aplicacao/outbox;
- modelos de entrada e saida internos.

Nao deve conter:

- controllers, attributes HTTP ou contratos OpenAPI;
- `DbContext`, SQL, Kafka client ou configuracao de infraestrutura;
- autorizacao baseada em `ClaimsPrincipal`;
- detalhes de transporte como topic, partition, offset ou commit.

Observacoes reais:

- LedgerService.Application hoje serializa o payload `LedgerEntryCreatedV2` e cria `OutboxMessage`. Isso e pragmatico para uma POC, mas mistura caso de uso com formato de integracao. Se o contrato Kafka crescer, vale mover a montagem serializada para uma porta/outbox mapper.
- BalanceService.Application usa `ILogger` e `ActivitySource`, o que e aceitavel para observabilidade leve, mas deve evitar que tags tecnicas de transporte contaminem regras de dominio.

### Domain

Deve conter:

- entidades, invariantes e comportamento do dominio;
- tipos de dominio;
- excecoes de dominio;
- agregados e regras que independem de ASP.NET, EF Core e Kafka.

Nao deve conter:

- EF Core attributes/configurations;
- headers Kafka, nomes de topic/topico ou DTOs de transporte;
- claims, scopes, JWT, JWKS;
- clock do sistema, salvo por abstracao recebida de fora.

Observacoes reais:

- LedgerEntry e DailyBalance possuem invariantes reais; nao sao apenas DTOs.
- OutboxMessage no Domain do Ledger e uma escolha discutivel: ela representa mecanismo tecnico de integracao. Hoje esta acoplada ao caso de uso e ao banco, mas pode ser mantida enquanto a POC tratar Outbox como parte da consistencia do agregado operacional. Em produto, consideraria mover o estado de outbox para Application/Infrastructure ou isolar melhor como integration event store.
- Repositories no Domain do Ledger e em Application do Balance mostram inconsistencia de boundary entre servicos. Nao quebra a solucao, mas aumenta custo cognitivo.

### Infrastructure

Deve conter:

- EF Core, DbContext, migrations e configurations;
- repositories concretos;
- configuracoes e implementacoes tecnicas compartilhadas pelos processos;
- implementacoes de portas da Application/Domain.
- adapters concretos de mensageria quando forem compartilhados entre processos.

Nao deve conter:

- regra de negocio de lancamento ou consolidacao;
- decisao de autorizacao de usuario;
- contrato HTTP;
- invariantes que deveriam estar nas entidades.
- HostedServices e adapters Kafka/DLQ exclusivos de workers.

Observacao real: `BalanceService.Infrastructure` concentra persistencia e repositorios. A traducao do transporte Kafka, o consumer e a DLQ pertencem ao `BalanceService.Worker`, porque sao adapters tecnicos exclusivos do processamento em background. Portas como `IOutboxMessagePublisher`, `ReceivedMessage`, `IDeadLetterPublisher` e `TransportMessageContext` formam o boundary neutro; offset, partition e commit permanecem nos adapters Kafka e nao devem contaminar processors neutros.

## Servico por servico

### LedgerService

Camadas atuais fazem sentido para o objetivo da POC. O servico tem transacao, idempotencia, entidade com invariantes, persistencia relacional e Outbox. Separar `Application`, `Domain` e `Infrastructure` agrega valor real.

Operacionalmente, `LedgerService.Api` recebe HTTP e grava Outbox; `LedgerService.Worker` publica a Outbox no Kafka por padrao, processa estornos e consome solicitacoes de reprocessamento. O caminho Pub/Sub permanece disponivel apenas quando `Messaging:Provider=PubSub` e deve ser tratado como legado/explicito. O consumer de reprocessamento traduz `ConsumeResult` para `ReceivedMessage` antes de chamar o processor neutro. Durante rollout, API antiga e Worker novo nao devem executar os mesmos HostedServices simultaneamente.

Pontos de atencao:

- `CreateLancamentoService` orquestra o caso de uso e preserva a transacao unica. Hash, verificacao e replay idempotente ficam em `CreateLancamentoIdempotencyService`; a criacao de `LedgerEntryCreatedV2` fica em `LedgerEntryCreatedEventFactory`; a montagem e escrita da mensagem ficam em `LedgerEntryCreatedOutboxWriter`.
- Evento `LedgerEntryCreatedV2` fica em Application, enquanto o consumidor tem outro contrato no `BalanceService.Worker`. Isso evita referencia cruzada entre servicos, mas exige documentacao e testes de contrato.
- Uso de `DateTime.Now` aparece em dominio/aplicacao/outbox. Para regras temporais e testes mais fortes, um clock explicito seria melhor, como ja existe no BalanceService.

### BalanceService

Camadas tambem fazem sentido, porque o servico possui leitura HTTP, consumidor Kafka, idempotencia por evento, projecao e DLQ.

Operacionalmente, `BalanceService.Api` atende consultas HTTP sobre a projecao; `BalanceService.Worker` consome `LedgerEntryCreated.v2` pelo Kafka por padrao, mantem leitura de `LedgerEntryCreated.v1` como legado, aplica idempotencia, atualiza `daily_balances`/`processed_events` e envia mensagens invalidas para DLQ Kafka. Quando `Messaging:Provider=PubSub`, o worker usa a subscription Pub/Sub e a DLQ de aplicacao Pub/Sub ainda suportadas, sem levar ack/nack, topic ou subscription para Application/Domain.

Pontos de atencao:

- MediatR agrega valor moderado: ajuda a separar queries/comandos, mas e mais framework do que o LedgerService usa. Para a POC e aceitavel; se houver poucos casos de uso, pode ser overhead.
- As consultas diaria e por periodo ficam diretamente em handlers MediatR. As interfaces e services intermediarios foram removidos porque apenas encaminhavam chamadas sem representar boundary ou variacao real.
- A ausencia de currency no evento obriga default `BRL` no handler. Isso e uma fragilidade de contrato, nao uma regra de dominio consolidada.

### IdentityService

O `IdentityService` e o bounded context atual para cadastro de usuarios e emissao de `MerchantId`. Ele nao emite tokens e nao substitui o Keycloak como IdP. A API recebe `POST /api/v1/users`, exige token com audience `identity-api` e scope `identity.write`, chama o Keycloak Admin API para criar o usuario e definir senha, gera `MerchantId`, persiste o vinculo local no schema `identity` e retorna os identificadores do cadastro.

Boundaries atuais:

- `IdentityService.Api` contem Minimal APIs, contratos HTTP, Swagger, JWT/JWKS, policies, rate limit, health/readiness e composition root.
- `IdentityService.Application` contem `CreateUserCommandHandler` e portas como `IIdentityProviderUserService`, `IMerchantIdGenerator`, `IUserRepository`, `IEmailSender` e `IEmailTemplateRenderer`.
- `IdentityService.Domain` contem o aggregate `User`, value objects e `UserRegisteredDomainEvent`.
- `IdentityService.Infrastructure` contem EF Core, `IdentityDbContext`, repositories, `KeycloakAdminClient`, Domain Event Dispatcher, handlers de e-mail, template HTML e adapters Mailpit/Resend.

Pontos de atencao:

- A senha e enviada ao Keycloak e nao deve ser persistida localmente.
- Se qualquer operacao apos criar o usuario no Keycloak e antes da confirmacao local falha, o handler tenta compensar removendo o usuario recem-criado no provider. Essa compensacao e best effort.
- Domain events sao despachados depois do commit do EF Core. Falhas nos handlers sao logadas e nao revertem o cadastro.
- O envio de e-mail atual e side effect intra-processo, sem Outbox, retry duravel, DLQ ou worker dedicado. A evolucao futura esta registrada na ADR-0095, mas nao esta implementada.
- Mailpit e somente local/teste controlado; Resend e provider real configuravel via secret.

### TransferService

O `TransferService` existe como bounded context de transferencias entre merchants conforme ADR-0087. A estrutura atual contem aggregate de Saga, portas de persistencia/idempotencia/Outbox em Application, `TransferServiceDbContext`, mappings EF Core, migration do schema `transfer`, repository concreto, idempotencia persistida, Outbox transacional, API HTTP de solicitacao/status, Worker de Saga, client HTTP do Ledger e publisher Kafka da Outbox.

A publicacao continua fora da Application: a Application grava o evento logico pela porta de Outbox, enquanto Infrastructure mapeia `event_type`, `topic`, headers e `message_key`; o Worker publica no Kafka e envia payload invalido ou erro definitivo para DLQ de aplicacao.

Pontos de atencao:

- `TransferService.Domain` nao deve referenciar nenhum projeto interno.
- `TransferService.Application` pode depender apenas do dominio local.
- `TransferService.Infrastructure` pode depender de Application e Domain, concentrando EF Core, PostgreSQL, Outbox, idempotencia persistida e mapeamento Kafka.
- `TransferService.Api` expoe HTTP e nao registra HostedServices de Worker.
- `TransferService.Worker` registra HostedServices, client HTTP do Ledger e publisher Kafka, sem controllers, Swagger ou CORS.
- TransferService usa Kafka no fluxo definido pela ADR-0087.

### PaymentService

O `PaymentService` existe como bounded context de pagamentos externos. A API
recebe criacao/consulta/refund, integra com provider fake/Stripe por porta
`IPaymentGateway`, persiste Inbox de webhooks Stripe no schema `payment` e
mantem a state machine do aggregate `Payment`. O Worker processa a Inbox e
materializa Payments/Refunds no Ledger por `ILedgerEntryGateway` e
`LedgerHttpGateway`.

Pontos de atencao:

- `PaymentService.Domain` nao deve conhecer Stripe, Kafka, Ledger HTTP,
  Balance ou EF Core.
- `PaymentService.Application` pode orquestrar estado de pagamento, Inbox e
  portas internas, mas nao deve publicar evento financeiro direto.
- `PaymentService.Infrastructure` concentra EF Core, provider fake/Stripe,
  client HTTP do Ledger e token client-credentials.
- `PaymentService.Api` recebe webhook Stripe e valida assinatura/raw body, mas
  nao processa efeito financeiro no request.
- `PaymentService.Worker` nao expoe HTTP e nao chama Balance diretamente.

### AuditService

O `AuditService` existe como bounded context de auditoria funcional. A API HTTP
canonica cria e consulta registros no schema `audit`. O Worker Kafka
consome `AuditRecordRequested.v1` quando habilitado e aplica idempotencia por
`eventId`/`source_event_id`.

Pontos de atencao:

- nenhum bounded context financeiro publica auditoria automaticamente nesta
  etapa;
- o contrato canonico de auditoria deve permanecer agnostico ao chamador;
- detalhes Kafka do consumer e da DLQ pertencem ao Worker;
- `sourceService` e `operationType` sao vocabulario funcional governado por
  contrato/documentacao, nao enums que acoplam o dominio de auditoria aos
  dominios chamadores.

## Anti-patterns encontrados ou proximos

- Padrao por reflexo: criar interfaces para toda classe sem variacao real.
- Service anemico que apenas encaminha para repository.
- Domain recebendo contratos Kafka, claims ou nomes de topicos.
- Application conhecendo detalhes de transporte ou headers Kafka.
- Controllers chamando DbContext/repository para executar regra de negocio.
- Duplicar a mesma politica de arquitetura com variacoes injustificadas entre Ledger e Balance.

## Arquitetura recomendada

A arquitetura ideal para este projeto deve ser minimalista e pragmatica, com robustez seletiva:

- manter quatro camadas para LedgerService e BalanceService;
- manter o TransferService como bounded context separado, com Saga e Outbox Kafka alinhados ao broker padrao;
- manter o PaymentService como bounded context separado para pagamentos externos, sem Balance direto e sem Kafka financeiro proprio;
- manter o AuditService como bounded context separado para trilha funcional, com integracao automatica apenas quando houver producer real e decisao explicita;
- manter o IdentityService como bounded context separado para usuarios, MerchantId, vinculo local com Keycloak e e-mail de boas-vindas;
- manter APIs e workers como processos separados, com composition root e `ServiceName` explicitos por processo;
- reforcar boundaries onde ha risco real: contratos de eventos, tempo/clock, outbox e idempotencia;
- evitar novas camadas genericas, shared kernel prematuro ou frameworks adicionais sem dor concreta;
- documentar contratos entre servicos antes de refatorar estrutura.
