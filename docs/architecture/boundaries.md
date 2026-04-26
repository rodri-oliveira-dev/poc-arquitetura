# Boundaries arquiteturais

## Classificacao encontrada

A solucao atual e uma arquitetura hibrida:

- Clean Architecture/DDD em LedgerService e BalanceService, com projetos `Api`, `Application`, `Domain` e `Infrastructure`.
- Elementos hexagonais onde existem contratos de persistencia e implementacoes em Infrastructure.
- Layered architecture na entrega HTTP, porque controllers, middlewares, swagger, auth e composicao ficam concentrados nos projetos `*.Api`.
- CQRS pragmatico entre servicos: Ledger escreve e publica eventos; Balance consome e mantem uma projecao de leitura.
- Auth.Api e deliberadamente mais simples, em projeto unico, coerente com uma API de autenticacao de POC.

## Boundaries recomendados

### API

Deve conter:

- contratos HTTP, controllers ou endpoints;
- binders e mappers de entrada/saida HTTP;
- autenticacao, autorizacao, scopes, merchant authorization e policies;
- middlewares, ProblemDetails, Swagger, rate limit, CORS, health e readiness;
- composicao do processo via DI.

Nao deve conter:

- regra de negocio de lancamento ou consolidacao;
- acesso direto a repositories de negocio para executar caso de uso;
- regra de retry/outbox/DLQ que pertenca ao pipeline de mensageria;
- detalhes de schema relacional alem de checks operacionais inevitaveis.

Observacao real: os endpoints `/ready` acessam `DbContext` e Kafka options diretamente no `Program.cs`. Isso e aceitavel para readiness operacional desta POC, mas deve permanecer limitado a checks operacionais.

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
- detalhes de transporte Kafka como topic, partition, offset e commit.

Observacoes reais:

- LedgerService.Application hoje serializa o payload `LedgerEntryCreatedV1` e cria `OutboxMessage`. Isso e pragmatico para uma POC, mas mistura caso de uso com formato de integracao. Se o contrato Kafka crescer, vale mover a montagem serializada para uma porta/outbox mapper.
- BalanceService.Application usa `ILogger` e `ActivitySource`, o que e aceitavel para observabilidade leve, mas deve evitar que tags tecnicas de transporte contaminem regras de dominio.

### Domain

Deve conter:

- entidades, invariantes e comportamento do dominio;
- tipos de dominio;
- excecoes de dominio;
- agregados e regras que independem de ASP.NET, EF Core e Kafka.

Nao deve conter:

- EF Core attributes/configurations;
- Kafka headers, nomes de topico ou DTOs de transporte;
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
- Kafka producer/consumer, DLQ, headers, retry, commit e clients;
- hosted services e configuracoes tecnicas;
- implementacoes de portas da Application/Domain.

Nao deve conter:

- regra de negocio de lancamento ou consolidacao;
- decisao de autorizacao de usuario;
- contrato HTTP;
- invariantes que deveriam estar nas entidades.

Observacao real: BalanceService.Infrastructure faz a traducao do contrato Kafka para o evento de dominio/aplicacao e decide DLQ. Isso faz sentido, porque e fronteira de transporte.

## Servico por servico

### LedgerService

Camadas atuais fazem sentido para o objetivo da POC. O servico tem transacao, idempotencia, entidade com invariantes, persistencia relacional e Outbox. Separar `Application`, `Domain` e `Infrastructure` agrega valor real.

Pontos de atencao:

- `CreateLancamentoService` faz bastante coisa: hash de idempotencia, parse de input, criacao de entidade, response DTO, evento e outbox. Ainda e aceitavel, mas e o primeiro ponto a decompor se o caso de uso crescer.
- Evento `LedgerEntryCreatedV1` fica em Application, enquanto o consumidor tem outro contrato em Infrastructure. Isso evita referencia cruzada entre servicos, mas exige documentacao e testes de contrato.
- Uso de `DateTime.Now` aparece em dominio/aplicacao/outbox. Para regras temporais e testes mais fortes, um clock explicito seria melhor, como ja existe no BalanceService.

### BalanceService

Camadas tambem fazem sentido, porque o servico possui leitura HTTP, consumidor Kafka, idempotencia por evento, projecao e DLQ.

Pontos de atencao:

- MediatR agrega valor moderado: ajuda a separar queries/comandos, mas e mais framework do que o LedgerService usa. Para a POC e aceitavel; se houver poucos casos de uso, pode ser overhead.
- `IDailyBalanceService` e `IPeriodBalanceService` parecem abstracoes de baixo ganho enquanto houver uma unica implementacao simples. Elas podem ser mantidas se forem usadas em testes e para legibilidade, mas nao devem virar padrao automatico.
- A ausencia de currency no evento obriga default `BRL` no handler. Isso e uma fragilidade de contrato, nao uma regra de dominio consolidada.

### Auth.Api

Projeto unico e a escolha correta neste momento. Criar `Auth.Application`, `Auth.Domain` e `Auth.Infrastructure` agora seria overengineering.

Pontos de atencao:

- A regra de login da POC vive no endpoint. Isso e aceitavel enquanto for autenticacao local temporaria e documentada.
- Se Auth.Api evoluir para usuarios reais, refresh tokens, revogacao, persistencia e federacao OIDC, ai sim boundaries mais fortes seriam necessarios.
- ADR existente ja propoe migracao para Keycloak, que provavelmente e melhor do que transformar a POC de Auth em um identity provider caseiro.

## Anti-patterns encontrados ou proximos

- Padrao por reflexo: criar interfaces para toda classe sem variacao real.
- Service anemico que apenas encaminha para repository.
- Domain recebendo contratos Kafka, claims ou nomes de topicos.
- Application conhecendo detalhes de transporte ou headers Kafka.
- Controllers chamando DbContext/repository para executar regra de negocio.
- Duplicar a mesma politica de arquitetura com variacoes injustificadas entre Ledger e Balance.
- Criar camadas adicionais em Auth.Api antes de haver complexidade real.

## Arquitetura recomendada

A arquitetura ideal para este projeto deve ser minimalista e pragmatica, com robustez seletiva:

- manter quatro camadas para LedgerService e BalanceService;
- manter Auth.Api em projeto unico enquanto for POC;
- reforcar boundaries onde ha risco real: contratos de eventos, tempo/clock, outbox e idempotencia;
- evitar novas camadas genericas, shared kernel prematuro ou frameworks adicionais sem dor concreta;
- documentar contratos entre servicos antes de refatorar estrutura.
