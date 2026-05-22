# poc-arquitetura

[![Build](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=build)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=tests)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Coverage](https://img.shields.io/badge/coverage-%3E%3D85%25-brightgreen)](docs/development/test-coverage.md)
[![Architecture Docs](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/pages-architecture.yml?branch=main&label=architecture%20docs)](https://rodri-oliveira-dev.github.io/poc-arquitetura/)

POC de microservicos em .NET para validar Clean Architecture, DDD, PostgreSQL, Kafka, Outbox, autenticacao JWT com JWKS, observabilidade e testes automatizados.

## Problema

O projeto modela um cenario comum em sistemas financeiros: registrar lancamentos de forma transacional, publicar eventos de forma confiavel e manter uma projecao de saldo separada para consulta. A solucao tambem cobre preocupacoes de revisao tecnica que costumam aparecer nesse tipo de arquitetura: idempotencia, autorizacao por merchant, consistencia eventual, reprocessamento, estorno, observabilidade e validacao automatizada.

## Solucao

A POC separa escrita e leitura em servicos distintos e separa APIs HTTP de workers. O `LedgerService.Api` recebe comandos de lancamento, estorno e reprocessamento, persiste os dados e grava eventos em Outbox na mesma transacao. O `LedgerService.Worker` publica a Outbox no Kafka e executa processamentos assincronos do Ledger. O `BalanceService.Worker` consome os eventos financeiros e atualiza saldos consolidados; o `BalanceService.Api` atende consultas HTTP. O `Auth.Api` emite tokens JWT RS256 e publica JWKS para validacao offline pelas APIs de negocio.

Principais servicos:

| Servico | Papel |
| --- | --- |
| `Auth.Api` | Emite JWT RS256 por `POST /auth/login` e publica JWKS em `GET /.well-known/jwks.json`. |
| `LedgerService.Api` | API de escrita para lancamentos, estornos, reprocessamentos, Outbox e status operacionais. |
| `LedgerService.Worker` | Processo dedicado para publicar Outbox no Kafka e processar estornos/reprocessamentos do Ledger. |
| `BalanceService.Api` | API de leitura de saldos consolidados projetados pelo Worker. |
| `BalanceService.Worker` | Processo dedicado para consumir eventos Kafka do Ledger e atualizar a projecao de saldos. |

## Arquitetura

`LedgerService` e `BalanceService` usam projetos por camada:

- `Api`: entrada HTTP, autenticacao, autorizacao, Swagger, health/readiness e composicao via DI.
- `Worker`: host de `BackgroundService` sem superficie HTTP.
- `Application`: casos de uso, handlers, validacao de entrada, idempotencia e orquestracao.
- `Domain`: entidades, invariantes e regras de dominio sem dependencia de infraestrutura.
- `Infrastructure`: EF Core, PostgreSQL, repositorios, migrations e implementacoes tecnicas compartilhadas pelos processos.

`Auth.Api` permanece em projeto unico porque o escopo atual de autenticacao da POC e pequeno. A leitura arquitetural completa fica em [docs/architecture](docs/architecture/README.md) e as decisoes historicas ficam em [docs/adrs](docs/adrs/README.md).

Documentacao arquitetural publicada:

<https://rodri-oliveira-dev.github.io/poc-arquitetura/>

## Pre-requisitos

- .NET SDK conforme `global.json`.
- Docker-compatible API para Testcontainers e stack local.
- CLI `docker` com suporte a `docker compose` para a stack local.
- PostgreSQL e Kafka acessiveis quando rodar APIs e workers fora de container.

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

Suba a stack local minima no Windows:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Esse script sobe infraestrutura, aplica migrations pelo host e inicia as APIs depois do schema estar pronto. O passo a passo manual fica em [desenvolvimento local](docs/development/local-development.md).

Para incluir observabilidade local completa:

```powershell
./scripts/start-local-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/start-local-stack.sh
```

## Comandos principais

| Tarefa | Comando |
| --- | --- |
| Restaurar tools | `dotnet tool restore` |
| Restaurar pacotes | `dotnet restore ./LedgerService.slnx` |
| Build Release | `dotnet build ./LedgerService.slnx --configuration Release --no-restore` |
| Testes sem rebuild | `dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings` |
| Testes com cobertura e gate | `./test.ps1` ou `./test.sh` |
| Stack local minima | `./scripts/start-local-stack.ps1` ou `./scripts/start-local-stack.sh` |
| Stack com observabilidade | `./scripts/start-local-stack.ps1 -Observability` ou `OBSERVABILITY=true ./scripts/start-local-stack.sh` |
| Load test smoke | `./scripts/run-loadtests.ps1 -Mode smoke` ou `./scripts/run-loadtests.sh smoke` |

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
- [Desenvolvimento local](docs/development/local-development.md)
- [Dev Container opcional](docs/development/devcontainer.md)
- [LedgerService API](docs/development/ledger-api.md)
- [Arquitetura](docs/architecture/README.md)
- [Boundaries arquiteturais](docs/architecture/boundaries.md)
- [ADRs](docs/adrs/README.md)
- [Autenticacao e autorizacao](docs/development/authentication.md)
- [Kafka, Outbox e DLQ](docs/development/kafka-outbox.md)
- [Observabilidade e operacao minima](docs/observability.md)
- [Testes e cobertura](docs/development/test-coverage.md)
- [Troubleshooting](docs/troubleshooting.md)
- [FAQ](docs/faq.md)
- [Instrucoes para Codex e agentes](AGENTS.md)

## FAQ

**O que este projeto demonstra tecnicamente?**

Microservicos .NET com separacao de escrita/leitura, Clean Architecture/DDD, Outbox, Kafka, PostgreSQL, JWT/JWKS, idempotencia, observabilidade e validacao automatizada. Veja [FAQ completa](docs/faq.md).

**Como executo localmente?**

Use `./scripts/start-local-stack.ps1` no Windows ou `./scripts/start-local-stack.sh` no Linux/macOS. O guia completo fica em [desenvolvimento local](docs/development/local-development.md).

**Onde encontro as decisoes arquiteturais?**

Use o indice de [ADRs](docs/adrs/README.md) e a leitura de [arquitetura](docs/architecture/README.md).

**Como resolvo erros comuns?**

Consulte [troubleshooting](docs/troubleshooting.md), especialmente para migrations, Docker-compatible API, Testcontainers, Swagger, Kafka/Outbox e observabilidade local.

## Observacoes

Os testes de carga ficam em `loadtests/k6` e rodam em container dentro da rede do compose, usando `compose.k6.yaml`. Arquivos gerados como `.env.k6.auto`, `artifacts/k6` e `TestResults` nao devem ser versionados.
