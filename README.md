# poc-arquitetura

[![Build](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=build)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=tests)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Coverage](https://img.shields.io/badge/coverage-%3E%3D85%25-brightgreen)](docs/development/test-coverage.md)
[![Architecture Docs](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/pages-architecture.yml?branch=main&label=architecture%20docs)](https://rodri-oliveira-dev.github.io/poc-arquitetura/)

Projeto de estudos arquiteturais em .NET para evoluir Clean Architecture, DDD, PostgreSQL, Outbox, mensageria por ports and adapters com Pub/Sub principal e Kafka legado opcional, autenticacao JWT/OIDC com Keycloak e JWKS, observabilidade, contratos e testes automatizados. Nasceu como POC de microservicos e evoluiu para um laboratorio continuo de arquitetura, contratos, seguranca, observabilidade, testes e operacao.

## Problema

O projeto modela um cenario comum em sistemas financeiros: registrar lancamentos de forma transacional, publicar eventos de forma confiavel e manter uma projecao de saldo separada para consulta. A solucao tambem cobre preocupacoes de revisao tecnica que costumam aparecer nesse tipo de arquitetura: idempotencia, autorizacao por merchant, consistencia eventual, reprocessamento, estorno, observabilidade e validacao automatizada.

## Solucao

A arquitetura separa escrita e leitura em servicos distintos e separa APIs HTTP de workers. O `LedgerService.Api` recebe comandos de lancamento, estorno e reprocessamento, persiste os dados e grava eventos em Outbox na mesma transacao. O `LedgerService.Worker` publica a Outbox pelo provider de mensageria selecionado e executa processamentos assincronos do Ledger. O `BalanceService.Worker` consome os eventos financeiros pelo provider selecionado e atualiza saldos consolidados; o `BalanceService.Api` atende consultas HTTP. Pub/Sub e o provider principal e usa emulator por padrao no fluxo local. Kafka permanece disponivel como opcao legada explicita via `Messaging:Provider=Kafka`. O Keycloak local emite tokens JWT RS256 e publica JWKS para validacao offline pelas APIs de negocio. O `Auth.Api` foi depreciado como emissor legado de POC e nao faz parte da stack principal.

Principais servicos:

| Servico | Papel |
| --- | --- |
| Keycloak | Emite JWT RS256 via OIDC para desenvolvimento local e publica JWKS do realm `poc`. |
| `LedgerService.Api` | API de escrita para lancamentos, estornos, reprocessamentos, Outbox e status operacionais. |
| `LedgerService.Worker` | Processo dedicado para publicar Outbox pelo provider de mensageria selecionado e processar estornos/reprocessamentos do Ledger. |
| `BalanceService.Api` | API de leitura de saldos consolidados projetados pelo Worker. |
| `BalanceService.Worker` | Processo dedicado para consumir eventos financeiros do Ledger pelo provider selecionado e atualizar a projecao de saldos. |

## Arquitetura

`LedgerService` e `BalanceService` usam projetos por camada:

- `Api`: entrada HTTP, autenticacao, autorizacao, Swagger, health/readiness e composicao via DI.
- `Shared/ApiDefaults`: defaults HTTP tecnicos compartilhados pelas APIs de negocio, sem regras de dominio ou policies especificas.
- `Worker`: host de `BackgroundService` sem superficie HTTP.
- `Application`: casos de uso, handlers, validacao de entrada, idempotencia e orquestracao.
- `Domain`: entidades, invariantes e regras de dominio sem dependencia de infraestrutura.
- `Infrastructure`: EF Core, PostgreSQL, repositorios, migrations e implementacoes tecnicas compartilhadas pelos processos.

`Auth.Api` permanece no repositorio apenas como legado testado e rastreavel; quando necessario, ele pode ser iniciado pelo overlay `compose.auth-legacy.yaml`. A leitura arquitetural completa fica em [docs/architecture](docs/architecture/README.md) e as decisoes historicas ficam em [docs/adrs](docs/adrs/README.md).

Documentacao arquitetural publicada:

<https://rodri-oliveira-dev.github.io/poc-arquitetura/>

## Mensageria: Kafka e Pub/Sub

Kafka e Pub/Sub coexistem como adapters do boundary de mensageria dos workers. Pub/Sub e o provider principal e o desenvolvimento local usa o emulator. Kafka permanece suportado como opcao legada explicita, selecionada com `Messaging:Provider=Kafka`, sem tentar esconder as diferencas semanticas entre providers.

| Kafka | Pub/Sub |
| --- | --- |
| Usa topic, headers, key, partition, offset e commit. | Usa topic e subscription, attributes, `ack`/`nack` e ordering key. |
| A key influencia particionamento e ordenacao dentro da partition. | A ordering key preserva ordenacao quando habilitada, mas nao representa uma partition. |
| O consumer controla commit de offset. | O consumer confirma ou rejeita a entrega com `ack` ou `nack`. |

As portas compartilhadas preservam Outbox, idempotencia e o contrato logico dos eventos. Conceitos especificos continuam nos respectivos adapters: Pub/Sub nao deve expor nem simular partition, offset ou commit.

Leitura complementar:

- [ADR-0078: Pub/Sub como provider principal](docs/adrs/0078-pubsub-provider-principal-local-emulator.md)
- [Operacao do Pub/Sub e emulator local](docs/operations/pubsub.md)
- [Runbook de recuperacao de eventos](docs/operations/event-recovery-runbook.md)
- [Replay e DLQ orientados por contrato](docs/operations/event-replay-and-dlq.md)
- [Estrategia operacional de DLQ](docs/operations/dlq-strategy.md)
- [Estrategia operacional de replay seguro](docs/operations/replay-strategy.md)
- [Contrato Pub/Sub entre Terraform e aplicacao](docs/development/pubsub-infra-app-contract.md)
- [Modulo Terraform Pub/Sub Ledger Events](infra/terraform/modules/pubsub-ledger-events/README.md)
- [Modulo Terraform Cloud SQL PostgreSQL](infra/terraform/modules/cloudsql-postgres/README.md)
- [Custo e free tier do Pub/Sub](docs/development/pubsub-cost-and-free-tier.md)
- [Execucao local com Pub/Sub emulator](docs/development/local-development.md#pubsub-emulator-local)

## Pre-requisitos

- .NET SDK conforme `global.json`.
- Docker-compatible API para Testcontainers e stack local.
- CLI `docker` com suporte a `docker compose` para a stack local.
- PostgreSQL e o provider de mensageria selecionado acessiveis quando rodar APIs e workers fora de container. Pub/Sub emulator e o default local; Pub/Sub real exige configuracao explicita sem `PUBSUB_EMULATOR_HOST`, e Kafka permanece opcional.

O projeto nao exige Docker Desktop como premissa. No Windows sem Docker Desktop, o ambiente recomendado e Rancher Desktop com `moby/dockerd`.

Ha tambem suporte opcional a Dev Container para VS Code, documentado em [docs/development/devcontainer.md](docs/development/devcontainer.md), sem substituir o fluxo local no host.

## Quickstart

Restaure ferramentas, dependencias, build e testes:

```powershell
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Suba o core funcional local no Windows:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Esse script sobe o core funcional local: PostgreSQL persistente unico com schemas `ledger` e `balance`, Pub/Sub emulator, Keycloak, APIs e workers. Ele aplica migrations pelo host e inicia as APIs depois do schema estar pronto. O passo a passo manual fica em [desenvolvimento local](docs/development/local-development.md).

Para incluir observabilidade local completa:

```powershell
./scripts/start-local-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/start-local-stack.sh
```

A observabilidade fica no overlay `compose.observability.yaml`. O modo padrao de desenvolvimento nao sobe Jaeger, Collector, Prometheus, Loki, Alloy, Alertmanager nem Grafana, mas continua subindo `ledger-worker` e `balance-worker` para preservar o fluxo ponta a ponta.

Os aliases abaixo continuam disponiveis para explicitar o mesmo fluxo Pub/Sub local:

```powershell
./scripts/start-local-stack-pubsub.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack-pubsub.sh
```

Esse fluxo usa o `compose.yaml` principal, cria topic principal, topic de DLQ, subscription do Balance e subscription de inspecao da DLQ de aplicacao de forma idempotente e inicia os workers com `Messaging:Provider=PubSub`. Kafka nao e iniciado. Para usar o provider legado, execute `./scripts/start-local-stack-kafka.ps1` ou `./scripts/start-local-stack-kafka.sh`. Detalhes ficam em [desenvolvimento local](docs/development/local-development.md#pubsub-emulator-local) e no runbook de [operacao do Pub/Sub](docs/operations/pubsub.md).

Para subir a stack completa com observabilidade e Nginx HTTPS local, gere antes os certificados em `infra/nginx/certs/` conforme [desenvolvimento local](docs/development/local-development.md#borda-local-https-com-nginx):

```powershell
./scripts/start-full-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-full-stack.sh
```

Se houver containers antigos ou rede local presa do proprio projeto, o script pergunta se pode liberar esses recursos com limpeza nao destrutiva, sem remover volumes. Em automacao local, use `./scripts/start-full-stack.ps1 -Cleanup` ou `./scripts/start-full-stack.sh --cleanup`.

## Comandos principais

| Tarefa | Comando |
| --- | --- |
| Restaurar tools | `dotnet tool restore` |
| Restaurar pacotes | `dotnet restore ./LedgerService.slnx` |
| Build Release | `dotnet build ./LedgerService.slnx --configuration Release --no-restore` |
| Testes sem rebuild | `dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings` |
| Testes com cobertura e gate | `./test.ps1` ou `./test.sh` |
| SonarQube local | `docker compose -f compose.sonar.yaml --profile quality up -d` |
| Analise SonarQube local | `bash scripts/sonar-analyze.sh` |
| Stack local minima | `./scripts/start-local-stack.ps1` ou `./scripts/start-local-stack.sh` |
| Stack com observabilidade | `./scripts/start-local-stack.ps1 -Observability` ou `OBSERVABILITY=true ./scripts/start-local-stack.sh` |
| Stack local com Pub/Sub emulator | `./scripts/start-local-stack-pubsub.ps1` ou `./scripts/start-local-stack-pubsub.sh` |
| Stack local com Kafka legado | `./scripts/start-local-stack-kafka.ps1` ou `./scripts/start-local-stack-kafka.sh` |
| Stack completa com Nginx | `./scripts/start-full-stack.ps1` ou `./scripts/start-full-stack.sh` |
| Parar stack completa | `./scripts/stop-full-stack.ps1` ou `./scripts/stop-full-stack.sh` |
| Diagnosticar disco Docker | `./scripts/docker-disk-report.ps1` ou `./scripts/docker-disk-report.sh` |
| Limpeza segura Docker | `./scripts/docker-clean-safe.ps1` ou `./scripts/docker-clean-safe.sh` |
| Load test smoke | `./scripts/run-loadtests.ps1 -Mode smoke` ou `./scripts/run-loadtests.sh smoke` |
| OWASP ZAP local | `./scripts/run-owasp-zap.ps1` ou `./scripts/run-owasp-zap.sh` |

## Testes

O fluxo recomendado para validar uma mudanca localmente e:

```powershell
./test.ps1
```

No Linux/macOS:

```bash
./test.sh
```

Os scripts executam testes com cobertura e aplicam gate minimo de 85% de cobertura total de linhas e dos assemblies Worker. Alguns testes de integracao usam Testcontainers com PostgreSQL real e precisam acessar uma Docker-compatible API. Detalhes ficam em [cobertura de testes](docs/development/test-coverage.md) e [desenvolvimento local](docs/development/local-development.md#testcontainers-e-docker-compatible-api).

## Documentacao

- [Indice geral da documentacao](docs/README.md)
- [Maturidade tecnica do projeto](docs/maturity.md)
- [Roadmap arquitetural consolidado](docs/roadmap.md)
- [Desenvolvimento local](docs/development/local-development.md)
- [Ferramentas auxiliares](docs/development/tooling.md)
- [Dev Container opcional](docs/development/devcontainer.md)
- [LedgerService API](docs/development/ledger-api.md)
- [BalanceService API](docs/development/balance-api.md)
- [Contratos logicos de eventos](docs/events/README.md)
- [Politica de versionamento de contratos de eventos](docs/development/event-contract-versioning.md)
- [Arquitetura](docs/architecture/README.md)
- [Boundaries arquiteturais](docs/architecture/boundaries.md)
- [ADRs](docs/adrs/README.md)
- [Autenticacao e autorizacao](docs/development/authentication.md)
- [Kafka, Outbox e DLQ](docs/development/kafka-outbox.md)
- [Pub/Sub: operacao e emulator local](docs/operations/pubsub.md)
- [Runbook de recuperacao de eventos](docs/operations/event-recovery-runbook.md)
- [Replay e DLQ orientados por contrato](docs/operations/event-replay-and-dlq.md)
- [Estrategia operacional de DLQ](docs/operations/dlq-strategy.md)
- [Estrategia operacional de replay seguro](docs/operations/replay-strategy.md)
- [Pub/Sub: contrato entre Terraform e aplicacao](docs/development/pubsub-infra-app-contract.md)
- [Pub/Sub: custo e free tier](docs/development/pubsub-cost-and-free-tier.md)
- [Observabilidade e operacao minima](docs/observability.md)
- [Testes e cobertura](docs/development/test-coverage.md)
- [SonarQube local](docs/quality/sonarqube.md)
- [OWASP ZAP local](docs/development/owasp-zap.md)
- [Troubleshooting](docs/troubleshooting.md)
- [FAQ](docs/faq.md)
- [Instrucoes para Codex e agentes](AGENTS.md)

## FAQ

**O que este projeto demonstra tecnicamente?**

Microservicos .NET com separacao de escrita/leitura, Clean Architecture/DDD, Outbox, mensageria por ports and adapters com Pub/Sub principal e Kafka legado opcional, PostgreSQL, JWT/JWKS, idempotencia, observabilidade e validacao automatizada. Veja [FAQ completa](docs/faq.md).

**Como executo localmente?**

Use `./scripts/start-local-stack.ps1` no Windows ou `./scripts/start-local-stack.sh` no Linux/macOS. O guia completo fica em [desenvolvimento local](docs/development/local-development.md).

**Onde encontro as decisoes arquiteturais?**

Use o indice de [ADRs](docs/adrs/README.md) e a leitura de [arquitetura](docs/architecture/README.md).

**Como resolvo erros comuns?**

Consulte [troubleshooting](docs/troubleshooting.md), especialmente para migrations, Docker-compatible API, Testcontainers, Swagger, Pub/Sub, Kafka legado, Outbox e observabilidade local. Para Pub/Sub, use tambem o runbook de [operacao](docs/operations/pubsub.md#troubleshooting).

## Observacoes

Os testes de carga ficam em `loadtests/k6` e rodam em container dentro da rede do compose, usando `compose.k6.yaml`. Arquivos gerados como `.env.k6.auto`, `artifacts/k6` e `TestResults` nao devem ser versionados.
