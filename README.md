# poc-arquitetura

[![Build](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=build)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=tests)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_poc-arquitetura&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_poc-arquitetura)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_poc-arquitetura&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_poc-arquitetura)
[![Architecture Docs](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/pages-architecture.yml?branch=main&label=architecture%20docs)](https://rodri-oliveira-dev.github.io/poc-arquitetura/)

Projeto de estudos arquiteturais em .NET para evoluir Clean Architecture, DDD, PostgreSQL, Outbox, mensageria por ports and adapters com Kafka como default dos workers principais e Pub/Sub explicito/legado, autenticacao JWT/OIDC com Keycloak e JWKS, observabilidade, contratos e testes automatizados. Nasceu como POC de microservicos e evoluiu para um laboratorio continuo de arquitetura, contratos, seguranca, observabilidade, testes e operacao.

## Problema

O projeto modela um cenario comum em sistemas financeiros: registrar lancamentos de forma transacional, publicar eventos de forma confiavel e manter uma projecao de saldo separada para consulta. A solucao tambem cobre preocupacoes de revisao tecnica que costumam aparecer nesse tipo de arquitetura: idempotencia, autorizacao por merchant, consistencia eventual, reprocessamento, estorno, observabilidade e validacao automatizada.

## Solucao

A arquitetura separa escrita e leitura em servicos distintos e separa APIs HTTP de workers. O `LedgerService.Api` recebe comandos de lancamento, estorno e reprocessamento, persiste os dados e grava eventos em Outbox na mesma transacao. O `LedgerService.Worker` publica a Outbox pelo provider de mensageria selecionado e executa processamentos assincronos do Ledger. O `BalanceService.Worker` consome os eventos financeiros pelo provider selecionado e atualiza saldos consolidados; o `BalanceService.Api` atende consultas HTTP. Kafka e o provider default dos workers principais quando `Messaging:Provider` esta ausente. Pub/Sub permanece disponivel por selecao explicita via `Messaging:Provider=PubSub`. O Keycloak local emite tokens JWT RS256 e publica JWKS para validacao offline pelas APIs de negocio. O `Auth.Api` foi depreciado como emissor legado de POC e nao faz parte da stack principal.

Principais servicos:

| Servico | Papel |
| --- | --- |
| Keycloak | Emite JWT RS256 via OIDC para desenvolvimento local e publica JWKS do realm `poc`. |
| `LedgerService.Api` | API de escrita para lancamentos, estornos, reprocessamentos, Outbox e status operacionais. |
| `LedgerService.Worker` | Processo dedicado para publicar Outbox pelo provider de mensageria selecionado e processar estornos/reprocessamentos do Ledger. |
| `BalanceService.Api` | API de leitura de saldos consolidados projetados pelo Worker. |
| `BalanceService.Worker` | Processo dedicado para consumir eventos financeiros do Ledger pelo provider selecionado e atualizar a projecao de saldos. |
| `TransferService.Api` / `TransferService.Worker` | Bounded context de transferencias com Saga orquestrada, POST/consulta HTTP, persistencia EF Core, Outbox transacional e publicacao Kafka explicita dos eventos da Saga. |

## Arquitetura

`LedgerService`, `BalanceService` e `TransferService` usam projetos por camada:

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

Kafka e Pub/Sub coexistem como adapters do boundary de mensageria dos workers. Kafka e o default para Ledger/Balance, selecionado quando `Messaging:Provider` esta ausente ou configurado como `Kafka`. Pub/Sub permanece suportado como opcao explicita/legada para Ledger/Balance, selecionada com `Messaging:Provider=PubSub`, sem tentar esconder as diferencas semanticas entre providers. O fluxo de Saga do `TransferService` tambem usa Kafka como transporte explicito dos eventos da Saga e nao usa Pub/Sub.

| Kafka | Pub/Sub |
| --- | --- |
| Usa topic, headers, key, partition, offset e commit. | Usa topic e subscription, attributes, `ack`/`nack` e ordering key. |
| A key influencia particionamento e ordenacao dentro da partition. | A ordering key preserva ordenacao quando habilitada, mas nao representa uma partition. |
| O consumer controla commit de offset. | O consumer confirma ou rejeita a entrega com `ack` ou `nack`. |

As portas compartilhadas preservam Outbox, idempotencia e o contrato logico dos eventos. Conceitos especificos continuam nos respectivos adapters: Pub/Sub nao deve expor nem simular partition, offset ou commit.

Leitura complementar:

- [ADR-0088: Kafka como default dos workers principais](docs/adrs/0088-kafka-default-ledger-balance-workers.md)
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
- PostgreSQL e o provider de mensageria selecionado acessiveis quando rodar APIs e workers fora de container. Kafka local usa `127.0.0.1:19092`; Pub/Sub exige configuracao explicita sem `PUBSUB_EMULATOR_HOST` para ambiente real.

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

Crie primeiro as variaveis locais descartaveis:

```powershell
./scripts/local/init-env.ps1
```

No Linux/macOS:

```bash
./scripts/local/init-env.sh
```

Suba o core funcional local no Windows:

```powershell
./scripts/local/start-stack.ps1
```

No Linux/macOS:

```bash
./scripts/local/start-stack.sh
```

Esse script sobe o core funcional local: PostgreSQL persistente unico com schemas `ledger`, `balance` e `transfer`, Kafka, Keycloak, APIs e workers, incluindo `TransferService.Worker`. Ele aplica migrations pelo host e inicia as APIs depois do schema estar pronto. O passo a passo manual fica em [desenvolvimento local](docs/development/local-development.md).

Para incluir observabilidade local completa:

```powershell
./scripts/local/start-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/local/start-stack.sh
```

A observabilidade fica no overlay `compose.observability.yaml`. O modo padrao de desenvolvimento nao sobe Jaeger, Collector, Prometheus, Loki, Alloy, Alertmanager nem Grafana, mas continua subindo `ledger-worker` e `balance-worker` para preservar o fluxo ponta a ponta.

Pub/Sub permanece disponivel como caminho explicito/legado:

```powershell
./scripts/local/start-stack-pubsub.ps1
```

No Linux/macOS:

```bash
./scripts/local/start-stack-pubsub.sh
```

Esse fluxo usa `compose.pubsub.yaml`, habilita o profile `legacy-pubsub`, cria topic principal, topic de DLQ, subscription do Balance e subscription de inspecao da DLQ de aplicacao de forma idempotente e inicia os workers de Ledger/Balance com `Messaging:Provider=PubSub`. Kafka nao e iniciado nesse modo. Detalhes ficam em [desenvolvimento local](docs/development/local-development.md#pubsub-emulator-local) e no runbook de [operacao do Pub/Sub](docs/operations/pubsub.md).

A Saga orquestrada do `TransferService` usa Kafka explicitamente. Como Kafka e o default local, o `TransferService.Worker` sobe no fluxo padrao e pode processar Sagas automaticamente. Consulte [TransferService API](docs/development/transfer-api.md) e [desenvolvimento local](docs/development/local-development.md#kafka-local-e-saga-do-transferservice).

Para subir a stack completa com observabilidade e Nginx HTTPS local, gere antes os certificados em `infra/nginx/certs/` conforme [desenvolvimento local](docs/development/local-development.md#borda-local-https-com-nginx):

```powershell
./scripts/local/start-full-stack.ps1
```

No Linux/macOS:

```bash
./scripts/local/start-full-stack.sh
```

Se houver containers antigos ou rede local presa do proprio projeto, o script pergunta se pode liberar esses recursos com limpeza nao destrutiva, sem remover volumes. Em automacao local, use `./scripts/local/start-full-stack.ps1 -Cleanup` ou `./scripts/local/start-full-stack.sh --cleanup`.

## Comandos principais

| Tarefa | Comando |
| --- | --- |
| Restaurar tools | `dotnet tool restore` |
| Restaurar pacotes | `dotnet restore ./LedgerService.slnx` |
| Build Release | `dotnet build ./LedgerService.slnx --configuration Release --no-restore` |
| Testes sem rebuild | `dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings` |
| Testes com cobertura e gate | `./test.ps1` ou `./test.sh` |
| Criar `.env.local` de onboarding | `./scripts/local/init-env.ps1` ou `./scripts/local/init-env.sh` |
| SonarQube local | `docker compose --env-file .env.local -f compose.sonar.yaml --profile quality up -d` |
| Analise SonarQube local | `./scripts/quality/sonar-analyze.sh` |
| Stack local minima | `./scripts/local/start-stack.ps1` ou `./scripts/local/start-stack.sh` |
| Stack com observabilidade | `./scripts/local/start-stack.ps1 -Observability` ou `OBSERVABILITY=true ./scripts/local/start-stack.sh` |
| Stack local com Pub/Sub emulator | `./scripts/local/start-stack-pubsub.ps1` ou `./scripts/local/start-stack-pubsub.sh` |
| Dependencias Kafka para debug | `docker compose --env-file .env.local -f compose.yaml up -d postgres-db kafka kafka-init-topics keycloak` |
| Stack local com Kafka | `./scripts/local/start-stack-kafka.ps1` ou `./scripts/local/start-stack-kafka.sh` |
| Stack completa com Nginx | `./scripts/local/start-full-stack.ps1` ou `./scripts/local/start-full-stack.sh` |
| Parar stack completa | `./scripts/local/stop-full-stack.ps1` ou `./scripts/local/stop-full-stack.sh` |
| Diagnosticar disco Docker | `./scripts/docker/disk-report.ps1` ou `./scripts/docker/disk-report.sh` |
| Limpeza segura Docker | `./scripts/docker/clean-safe.ps1` ou `./scripts/docker/clean-safe.sh` |
| Prune dry-run de volumes locais | `./scripts/docker/prune-volumes.ps1` ou `./scripts/docker/prune-volumes.sh` |
| Load test smoke Kafka | `./scripts/performance/run-loadtests.ps1 -Mode smoke-kafka` ou `./scripts/performance/run-loadtests.sh smoke-kafka` |
| Load test Kafka leve | `./scripts/performance/run-loadtests.ps1 -Mode load-kafka` ou `./scripts/performance/run-loadtests.sh load-kafka` |
| TransferService smoke Kafka | `./scripts/performance/run-loadtests.ps1 -Mode transfer-smoke-kafka` ou `./scripts/performance/run-loadtests.sh transfer-smoke-kafka` |
| TransferService full-stack Kafka | `./scripts/performance/run-loadtests.ps1 -Mode transfer-fullstack-kafka` ou `./scripts/performance/run-loadtests.sh transfer-fullstack-kafka` |
| OWASP ZAP local | `./scripts/security/run-owasp-zap.ps1` ou `./scripts/security/run-owasp-zap.sh` |

## Organizacao dos scripts

Os comandos principais ficam organizados por finalidade em subpastas de `scripts/`. Use os caminhos novos em comandos e documentacao nova; a politica de wrappers antigos fica em [docs/development/scripts.md](docs/development/scripts.md).

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
- [Baseline de evolucao produtiva](docs/architecture/production-readiness.md)
- [ADRs](docs/adrs/README.md)
- [Autenticacao e autorizacao](docs/development/authentication.md)
- [Kafka, Outbox e DLQ](docs/development/kafka-outbox.md)
- [Runbook DLQ e replay da Saga do TransferService](docs/operations/transfer-saga-kafka.md)
- [Pub/Sub: operacao e emulator local](docs/operations/pubsub.md)
- [Runbook de recuperacao de eventos](docs/operations/event-recovery-runbook.md)
- [Replay e DLQ orientados por contrato](docs/operations/event-replay-and-dlq.md)
- [Estrategia operacional de DLQ](docs/operations/dlq-strategy.md)
- [Estrategia operacional de replay seguro](docs/operations/replay-strategy.md)
- [Pub/Sub: contrato entre Terraform e aplicacao](docs/development/pubsub-infra-app-contract.md)
- [Pub/Sub: custo e free tier](docs/development/pubsub-cost-and-free-tier.md)
- [Observabilidade e operacao minima](docs/observability.md)
- [Testes e cobertura](docs/development/test-coverage.md)
- [SonarQube Cloud](docs/development/sonarqube-cloud.md)
- [SonarQube local](docs/quality/sonarqube.md)
- [OWASP ZAP local](docs/development/owasp-zap.md)
- [Troubleshooting](docs/troubleshooting.md)
- [FAQ](docs/faq.md)
- [Instrucoes para Codex e agentes](AGENTS.md)

## FAQ

**O que este projeto demonstra tecnicamente?**

Microservicos .NET com separacao de escrita/leitura, Clean Architecture/DDD, Outbox, mensageria por ports and adapters com Kafka default e Pub/Sub explicito/legado, PostgreSQL, JWT/JWKS, idempotencia, observabilidade e validacao automatizada. Veja [FAQ completa](docs/faq.md).

**Como executo localmente?**

Use `./scripts/local/start-stack.ps1` no Windows ou `./scripts/local/start-stack.sh` no Linux/macOS. O guia completo fica em [desenvolvimento local](docs/development/local-development.md).

**Onde encontro as decisoes arquiteturais?**

Use o indice de [ADRs](docs/adrs/README.md) e a leitura de [arquitetura](docs/architecture/README.md).

**Como resolvo erros comuns?**

Consulte [troubleshooting](docs/troubleshooting.md), especialmente para migrations, Docker-compatible API, Testcontainers, Swagger, Pub/Sub, Kafka, Outbox e observabilidade local. Para Pub/Sub, use tambem o runbook de [operacao](docs/operations/pubsub.md#troubleshooting).

## Observacoes

Os testes de carga ficam em `loadtests/k6` e rodam em container dentro da rede do compose, usando `compose.k6.yaml`. Os modos padrao usam Kafka: `smoke-kafka` valida Ledger -> Kafka -> Balance, `load-kafka` executa carga leve de leitura, `transfer-smoke-kafka` e `transfer-load-kafka` cobrem os endpoints HTTP do TransferService sem exigir conclusao da Saga pelo Worker, e `transfer-fullstack-kafka` valida API + Worker + LedgerService + Kafka no fluxo feliz da Saga. Arquivos gerados como `.env.k6.auto`, `artifacts/k6` e `TestResults` nao devem ser versionados.
