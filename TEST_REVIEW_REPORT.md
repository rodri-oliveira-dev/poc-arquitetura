# Revisao tecnica da suite de testes

Data da revisao: 2026-06-03

## Resumo executivo

A suite atual tem boa cobertura comportamental para os fluxos criticos da POC: lancamentos, estornos, reprocessamentos, consolidacao de saldos, seguranca por escopo/merchant, Outbox, workers, DLQ, observabilidade e regras arquiteturais. Foram encontrados 9 projetos de teste na `LedgerService.slnx`, com cerca de 409 testes xUnit (`Fact`/`Theory`).

Nao foram encontrados sinais sistemicos de testes sem assert, asserts sempre verdadeiros ou testes que apenas executam codigo sem verificar resultado. A maior oportunidade de melhoria esta na organizacao e calibragem da piramide: parte dos testes unitarios usa muitos mocks rigidos e verifica detalhes de orquestracao interna, alguns testes de composition parecem existir mais por cobertura do que por risco real, e alguns cenarios HTTP repetem validacoes ja cobertas em unitarios, embora varios deles protejam riscos distintos de contrato, autorizacao, pipeline e persistencia.

Recomendacao geral: manter a suite, mas consolidar gradualmente os pontos de baixo valor, separar melhor testes de infraestrutura/composition dos unitarios de regra, e preservar os testes de integracao PostgreSQL que cobrem transacoes, constraints, locks, idempotencia e concorrencia.

## Projetos de teste encontrados

| Projeto | Testes aproximados | Papel principal |
| --- | ---: | --- |
| `Architecture.Tests` | 6 | Regras estruturais e dependencias entre camadas |
| `Auth.UnitTests` | 16 | Auth legado: JWT, chaves, Swagger, middleware |
| `Auth.IntegrationTests` | 11 | Contratos HTTP do Auth legado |
| `LedgerService.UnitTests` | 119 | Domain, Application, Api helpers, Infrastructure InMemory, politicas |
| `LedgerService.IntegrationTests` | 69 | HTTP, seguranca, Outbox, PostgreSQL, concorrencia, Pub/Sub emulator opcional |
| `LedgerService.Worker.Tests` | 50 | Workers, consumers, producers, mappers, configuracao, observabilidade |
| `BalanceService.UnitTests` | 52 | Domain, Application, Api helpers, Infrastructure InMemory |
| `BalanceService.IntegrationTests` | 27 | HTTP, seguranca, PostgreSQL e concorrencia de saldos |
| `BalanceService.Worker.Tests` | 59 | Workers, consumers, DLQ, mappers, configuracao, observabilidade |

## Classificacao por camada da piramide

| Camada | Projetos/classes principais | Avaliacao |
| --- | --- | --- |
| Unitarios de dominio | `DailyBalanceDomainTests`, `ProcessedEventTests`, `LedgerEntryAmountRulesTests`, `OutboxMessageClaimTests` | Bons candidatos para base da piramide; validam regras pequenas e baratas. |
| Unitarios de aplicacao | Handlers e validators em `LedgerService.UnitTests/Application` e `BalanceService.UnitTests/Application` | Cobrem regras e orquestracao; alguns handlers tem mock setup pesado. |
| Unitarios de API/presentation | Mappers, binds, Swagger, auth extensions, middlewares | Uteis para helpers e filtros; risco de baixo valor quando validam apenas configuracao simples. |
| Infra leve com EF InMemory | Repositories em `*.UnitTests/Infrastructure/Persistence` e factories HTTP leves | Rapidos, mas nao substituem PostgreSQL para constraints, transacoes e SQL real. |
| API/contrato HTTP | `*.IntegrationTests/Api/*` | Validam status code, payload, headers, auth, CORS, problem details e wiring. |
| Integracao com banco real | `PostgresLedgerFixture`, `PostgresBalanceFixture`, testes de concorrencia e constraints | Muito valiosos para riscos que EF InMemory nao representa. |
| Worker/background service | `*.Worker.Tests` e alguns testes em `LedgerService.IntegrationTests/Outbox` | Cobrem orquestracao, Ack/Nack, DLQ, tracing e publicacao. |
| Contrato/eventos | Schema JSON em `CreateLancamentoCommandHandlerTests`, mappers Pub/Sub/Kafka, publisher emulator opcional | Protegem formato de evento e metadados de transporte. |
| Arquitetura | `Architecture.Tests` e algumas politicas em `LedgerService.UnitTests/Architecture` | Regras de fronteira, referencias e governanca. |

## Mapa de duplicidades entre camadas

| Comportamento | Testes relacionados | Diagnostico |
| --- | --- | --- |
| Criacao de lancamento, idempotencia e Outbox | `CreateLancamentoCommandHandlerTests` e `CreateLancamentoPostgresTests` | Complementares. O unitario valida decisao/orquestracao; o PostgreSQL valida replay real, linhas persistidas e unique constraint. Nao remover o teste PostgreSQL. |
| Estorno: solicitar, processar, status e autorizacao | `SolicitarEstornoLancamentoHandlerTests`, `ProcessarEstornoLancamentoHandlerTests`, `ObterStatusEstornoLancamentoHandlerTests`, `EstornosLancamentosEndpointTests`, `EstornoLancamentoConcurrencyTests` | Parcialmente sobrepostos. Endpoint deve manter contrato HTTP/auth; unitarios devem focar regras. Alguns casos 404/403 podem ser consolidados se ja cobertos em authorization dedicada. |
| Reprocessamento de lancamentos | Handlers/unitarios e `ReprocessamentosLancamentosEndpointTests` | Complementares quando endpoint valida contrato e handler valida regra. Rever casos de payload invalido simples que repetem validators. |
| Consolidacao de saldos | `GetDailyBalanceHandlerTests`, `GetPeriodBalanceHandlerTests`, `DailyBalanceDomainTests`, `ConsolidadosEndpointsTests`, `ApplyLedgerEntryCreatedConcurrencyTests` | Complementares. API cobre contrato e formato; dominio/handler cobrem calculo; PostgreSQL cobre concorrencia/idempotencia. |
| Autorizacao por scopes e merchants | `ScopeAuthorizationExtensionsTests`, `JwtTransportSecurityTests`, `LancamentosAuthorizationTests`, `BalanceAuthorizationTests` | Majoritariamente complementar. Testes de API protegem o pipeline real de auth e devem permanecer. |
| Workers Kafka/PubSub e mappers de transporte | Mapper tests, processor tests, consumer tests, publisher tests | Muitos testes validam metadados especificos; parecem valiosos para contratos de mensageria. Avaliar consolidacao apenas onde ha variacoes mecanicas. |

## Achados

### Achado 1

- Projeto: `LedgerService.UnitTests`
- Arquivo: `tests/LedgerService.UnitTests/Application/Lancamentos/Commands/CreateLancamentoCommandHandlerTests.cs`
- Teste ou classe de teste: `CreateLancamentoCommandHandlerTests`
- Camada atual: unitario de aplicacao
- Problema identificado: mock setup muito extenso e verificacoes de interacao rigidas (`MockBehavior.Strict`, `VerifyAll`, `VerifyNoOtherCalls`) em testes de handler.
- Por que isso e um problema: aumenta acoplamento ao fluxo interno do handler e torna refatoracoes pequenas mais caras, mesmo quando o comportamento observavel permanece correto.
- Recomendacao: extrair um fixture/fake pequeno para repositorios e unidade de trabalho, ou mover parte da verificacao de persistencia/orquestracao para testes de integracao ja existentes. Manter unitarios para idempotencia, conflito, replay e contrato do evento.
- Risco de alteracao: medio; uma simplificacao apressada pode remover protecao de regressao de Outbox/idempotencia.
- Prioridade: media.

### Achado 2

- Projeto: `BalanceService.UnitTests`
- Arquivo: `tests/BalanceService.UnitTests/Application/Balances/Commands/ApplyLedgerEntryCreatedHandlerTests.cs`
- Teste ou classe de teste: `ApplyLedgerEntryCreatedHandlerTests`
- Camada atual: unitario de aplicacao
- Problema identificado: alta dependencia de mocks para validar transacao, repositorios, clock e ausencia de chamadas.
- Por que isso e um problema: parte do teste valida mais a sequencia tecnica esperada do que o efeito final do processamento. A suite ja possui teste PostgreSQL de concorrencia para o mesmo fluxo critico.
- Recomendacao: manter os cenarios de idempotencia e aplicacao de evento, mas considerar fakes in-memory de repositorio para validar estado final com menos verificacao de chamadas.
- Risco de alteracao: medio; o fluxo de saldo e idempotencia e critico.
- Prioridade: media.

### Achado 3

- Projeto: `LedgerService.Worker.Tests`
- Arquivo: `tests/LedgerService.Worker.Tests/Composition/WorkerCoverageCompositionTests.cs`
- Teste ou classe de teste: `OutboxPublisherService_should_be_constructed_with_worker_dependencies`
- Camada atual: worker/composition
- Problema identificado: teste apenas instancia `OutboxPublisherService` e faz `Assert.NotNull`.
- Por que isso e um problema: o teste agrega pouca confianca; nao valida execucao do worker, configuracao real de DI, retry, publicacao ou efeitos observaveis.
- Recomendacao: remover em uma etapa pequena ou substituir por teste de DI/composition que resolva o hosted service a partir do `ServiceCollection` real e valide dependencias essenciais. Como ha outros testes de retry e worker, a remocao parece segura se a cobertura oficial nao depender artificialmente dele.
- Risco de alteracao: baixo.
- Prioridade: alta.

### Achado 4

- Projeto: `LedgerService.Worker.Tests` e `BalanceService.Worker.Tests`
- Arquivo: `tests/*Worker.Tests/Composition/WorkerCoverageCompositionTests.cs`
- Teste ou classe de teste: `WorkerCoverageCompositionTests`
- Camada atual: worker/composition
- Problema identificado: o nome da classe explicita foco em cobertura, nao em comportamento.
- Por que isso e um problema: incentiva manutencao orientada por percentual e mistura testes valiosos de configuracao com testes potencialmente mecanicos.
- Recomendacao: renomear para classes por responsabilidade, por exemplo `KafkaConsumerOptionsTests`, `KafkaDeadLetterPublisherTests`, `OutboxRetryStrategyTests`, e remover casos que apenas instanciam objetos.
- Risco de alteracao: baixo a medio, dependendo de filtros de teste existentes.
- Prioridade: media.

### Achado 5

- Projeto: `LedgerService.UnitTests`
- Arquivo: `tests/LedgerService.UnitTests/Application/Lancamentos/Queries/ObterStatusEstornoLancamentoHandlerTests.cs`
- Teste ou classe de teste: helper `CreateEstorno`/status mapping
- Camada atual: unitario de aplicacao
- Problema identificado: uso de reflection (`BindingFlags`, `GetProperty`) para alterar status de entidade.
- Por que isso e um problema: acopla o teste a estrutura interna/propriedade setavel e pode mascarar ausencia de uma API de dominio adequada para montar estados relevantes em teste.
- Recomendacao: preferir transicoes publicas de dominio para criar os estados ou um factory/test builder que use comportamentos publicos. Se nao houver transicao publica para todos os estados, registrar como lacuna de testabilidade do dominio.
- Risco de alteracao: medio; mexe em estados de dominio e pode exigir ajustes pequenos no helper.
- Prioridade: media.

### Achado 6

- Projeto: `LedgerService.UnitTests` e `BalanceService.UnitTests`
- Arquivo: `tests/*UnitTests/Infrastructure/Persistence/Repositories/*.cs`
- Teste ou classe de teste: testes de repositories com `UseInMemoryDatabase`
- Camada atual: infraestrutura leve, nomeada como unitario
- Problema identificado: testes de repository com EF InMemory aparecem nos projetos unitarios.
- Por que isso e um problema: EF InMemory nao representa SQL, constraints, locking, tipos Npgsql ou comportamento transacional; classificar como unitario pode gerar falsa leitura da piramide.
- Recomendacao: manter quando validam logica de filtro simples e estado agregado, mas documentar/classificar como "infra leve". Para queries/constraints/transacoes criticas, preferir PostgreSQL real em projetos de integracao.
- Risco de alteracao: baixo se for apenas organizacao; medio se mover testes de projeto.
- Prioridade: baixa.

### Achado 7

- Projeto: `LedgerService.IntegrationTests`
- Arquivo: `tests/LedgerService.IntegrationTests/Api/Lancamentos/EstornosLancamentosEndpointTests.cs`
- Teste ou classe de teste: classe completa
- Camada atual: API/integracao leve com EF InMemory
- Problema identificado: classe combina contrato HTTP, autorizacao, persistencia InMemory, idempotencia e processamento via `ISender`.
- Por que isso e um problema: alguns cenarios sao complementares, mas a classe ficou ampla; manutencao e diagnostico ficam mais dificeis quando falha.
- Recomendacao: separar por responsabilidade em `EstornosPostEndpointTests`, `EstornosGetEndpointTests`, `EstornosAuthorizationTests` ou manter a classe mas agrupar helpers e reduzir casos duplicados com `LancamentosAuthorizationTests`.
- Risco de alteracao: medio; pode afetar fixtures compartilhadas e autenticao de teste.
- Prioridade: media.

### Achado 8

- Projeto: `Architecture.Tests`
- Arquivo: `tests/Architecture.Tests/LayerDependencyTests.cs`
- Teste ou classe de teste: `Infrastructure_should_reference_ef_core_and_implement_repository_ports`
- Camada atual: arquitetura
- Problema identificado: regra exige que todo tipo em namespace de repositories implemente um port de Application ou Domain.
- Por que isso e um problema: e uma regra estrutural util, mas pode bloquear classes auxiliares legitimas no namespace de repositorios sem representar quebra arquitetural.
- Recomendacao: manter a regra por enquanto, mas avaliar se helpers futuros devem ficar fora desse namespace ou se a regra deve filtrar apenas tipos com sufixo `Repository`.
- Risco de alteracao: baixo.
- Prioridade: baixa.

### Achado 9

- Projeto: `Auth.IntegrationTests`
- Arquivo: `tests/Auth.IntegrationTests/Api/AuthEndpointsTests.cs`
- Teste ou classe de teste: Auth legado
- Camada atual: API/integracao
- Problema identificado: o Auth legado segue testado e ocupa espaco na suite principal, embora esteja fora da stack principal segundo `AGENTS.md`.
- Por que isso e um problema: custo de manutencao pode competir com os servicos principais.
- Recomendacao: manter enquanto `src/Auth.Api` existir, mas tratar como suite legada isolada; evitar expandir sem necessidade.
- Risco de alteracao: baixo.
- Prioridade: baixa.

## Testes possivelmente redundantes

| Area | Testes | Recomendacao |
| --- | --- | --- |
| Validacoes simples de payload em endpoints | Alguns casos `400` em `ConsolidadosEndpointsTests`, `EstornosLancamentosEndpointTests`, `ReprocessamentosLancamentosEndpointTests` e validators unitarios | Manter pelo menos um teste HTTP por endpoint para contrato de erro; consolidar variacoes extensas nos validators unitarios. |
| Helpers de Swagger/exemplos | `AuthorizeOperationFilterTests`, `*ExamplesOperationFilterTests`, `SwaggerExposurePolicyTests` | Manter se Swagger e contrato publico da POC forem relevantes; revisar se algum teste valida apenas propriedades obvias. |
| Composition/coverage | `WorkerCoverageCompositionTests` | Reescrever/remover testes que apenas instanciam objetos; manter validacoes de options, retry, DLQ e destination mapping. |

## Testes de baixo valor

- `OutboxPublisherService_should_be_constructed_with_worker_dependencies`: baixo valor claro por validar apenas instanciacao.
- Testes de composition com nome `WorkerCoverageCompositionTests`: nem todos sao baixo valor, mas o nome e a mistura de responsabilidades indicam risco de testes orientados a cobertura.
- Testes que apenas validam exposicao de Swagger por ambiente podem ser baixo valor se a politica ja estiver coberta no endpoint/host; revisar antes de remover.

## Testes frageis ou acoplados a implementacao

- Uso de `MockBehavior.Strict`, `VerifyAll` e `VerifyNoOtherCalls` em handlers de aplicacao aumenta acoplamento a chamadas internas.
- Uso de reflection em `ObterStatusEstornoLancamentoHandlerTests` acopla ao estado interno da entidade.
- Asserts por mensagem de exception com regex em options de workers podem quebrar com mudanca textual sem regressao de comportamento; prefira validar tipo, propriedade/campo invalido ou mensagem essencial quando possivel.

## Testes lentos ou caros demais para a camada

- Testes PostgreSQL com Testcontainers sao caros, mas justificados para:
  - unique constraints de idempotencia;
  - concorrencia de estorno;
  - concorrencia de saldo;
  - Outbox claim/retry/dead-letter;
  - timestamp/persistencia real.
- Testes Pub/Sub emulator sao corretamente opcionais e pulados sem `PUBSUB_EMULATOR_HOST`.
- Testes de API com EF InMemory sao custo moderado e protegem contrato HTTP; evitar ampliar esses testes para regras ja bem cobertas em unitarios.

## Lacunas de cobertura comportamental

- Contrato de eventos: ha protecao para `LedgerEntryCreated.v1`; avaliar se eventos de estorno/reprocessamento tambem devem ter schema formal e validacao similar.
- Workers: ha bons testes de mappers/processors, mas a cobertura de ciclo completo Pub/Sub/Kafka permanece majoritariamente por fakes; isso e aceitavel na POC, mas fluxos de DLQ/retry end-to-end podem merecer teste de integracao opcional se virarem decisao de produto.
- Performance/custo da suite: nao ha evidencia no repositorio de relatorio de duracao por projeto; medir tempos antes de remover testes por custo.
- BalanceService: manter atencao em ordering/idempotencia de eventos quando migrar de Kafka legado para Pub/Sub principal.

## Recomendacoes de reorganizacao

1. Renomear e dividir `WorkerCoverageCompositionTests` por responsabilidade.
2. Remover ou reescrever testes que apenas fazem `new Sut(...)` + `Assert.NotNull`.
3. Classificar testes EF InMemory de repository como "infra leve" em vez de unitarios puros.
4. Reduzir mocks rigidos em handlers com fakes/builders quando houver manutencao nesses fluxos.
5. Consolidar variacoes HTTP de `400` quando a mesma regra ja estiver coberta por validator unitario.
6. Preservar testes PostgreSQL de concorrencia, constraints, idempotencia e Outbox.
7. Separar classes grandes de endpoint por contrato, autorizacao e fluxo feliz quando isso melhorar diagnostico.

## Sugestao de manter, mover, reescrever ou remover

| Acao | Alvo | Justificativa |
| --- | --- | --- |
| Manter | Testes PostgreSQL de `LedgerService.IntegrationTests` e `BalanceService.IntegrationTests` | Cobrem riscos reais fora do alcance de unitarios/EF InMemory. |
| Manter | Testes de autorizacao HTTP por scope/merchant | Protegem pipeline real de auth e contratos de seguranca. |
| Reescrever | Handler tests com muitos mocks rigidos | Reduzir acoplamento e manter foco em comportamento. |
| Reescrever | `WorkerCoverageCompositionTests` | Separar responsabilidades e remover foco explicito em cobertura. |
| Remover ou substituir | `OutboxPublisherService_should_be_constructed_with_worker_dependencies` | Baixo valor isolado. |
| Mover/reclassificar | Repository tests com EF InMemory | Melhor refletir camada de infraestrutura leve. |
| Consolidar | Variacoes repetitivas de validacao HTTP simples | Reduzir custo sem perder protecao de contrato. |

## Priorizacao

### Alta prioridade

- Remover ou substituir o teste de simples instanciacao em `WorkerCoverageCompositionTests`.
  - Risco: baixo.
  - Beneficio: reduz ruido e sinaliza que a suite nao deve existir apenas por cobertura.

### Media prioridade

- Reorganizar `WorkerCoverageCompositionTests` por responsabilidade.
  - Risco: baixo a medio.
  - Beneficio: melhora manutencao e leitura da suite de workers.
- Reduzir acoplamento por mocks rigidos nos handlers mais centrais.
  - Risco: medio.
  - Beneficio: facilita refatoracoes sem perder confianca.
- Separar classes grandes de endpoint, principalmente estornos.
  - Risco: medio.
  - Beneficio: diagnostico melhor e menos sobreposicao.
- Eliminar reflection em testes de status de estorno.
  - Risco: medio.
  - Beneficio: maior aderencia a comportamento publico de dominio.

### Baixa prioridade

- Reclassificar/mover testes EF InMemory de repository.
  - Risco: baixo a medio.
  - Beneficio: leitura mais fiel da piramide.
- Ajustar regras de arquitetura para helpers futuros em repositories.
  - Risco: baixo.
  - Beneficio: evita falso positivo futuro.
- Manter Auth legado isolado e sem expansao desnecessaria.
  - Risco: baixo.
  - Beneficio: reduz custo de manutencao fora da stack principal.

## Mudancas propostas nesta etapa

Nenhuma remocao automatica foi aplicada nesta primeira etapa. A unica mudanca segura realizada foi gerar este relatorio. As remocoes/consolidacoes recomendadas devem ser feitas em PRs pequenos, com validacao de build/teste e revisao cuidadosa do impacto em cobertura comportamental.

## Comandos de validacao

Comandos executados apos gerar o relatorio:

```powershell
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Resultado em 2026-06-03:

- `dotnet restore`: aprovado.
- `dotnet build`: aprovado, com avisos de analyzers ja existentes.
- `dotnet test`: aprovado, com 489 testes aprovados e 2 testes opcionais do Pub/Sub emulator ignorados porque dependem de `PUBSUB_EMULATOR_HOST`.

Se `dotnet test` falhar por Testcontainers/Docker no Windows, repetir normalizando `DOCKER_HOST` apenas no processo do teste:

```powershell
$env:DOCKER_HOST='npipe://./pipe/docker_engine'
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```
