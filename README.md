# poc-arquitetura

[![Build](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=build)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=tests)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Coverage](https://img.shields.io/badge/coverage-%3E%3D80%25-brightgreen)](docs/development/test-coverage.md)
[![Architecture Docs](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/pages-architecture.yml?branch=main&label=architecture%20docs)](https://rodri-oliveira-dev.github.io/poc-arquitetura/)

POC de microservicos em .NET para validar Clean Architecture, DDD, PostgreSQL, Kafka, Outbox, autenticacao JWT com JWKS, observabilidade e testes automatizados.

O repositorio e um laboratorio tecnico. Algumas decisoes estao aceitas, outras aparecem como propostas ou pontos de melhoria nas ADRs.

## Servicos

- `Auth.Api`: emite JWT RS256 por `POST /auth/login` e publica JWKS em `GET /.well-known/jwks.json`.
- `LedgerService.Api`: API de escrita para lancamentos em `POST /api/v1/lancamentos`, solicitacao/processamento de estorno, solicitacao/processamento de reprocessamento em `POST /api/v1/lancamentos/reprocessar` e consultas de status, com idempotencia e Outbox.
- `BalanceService.Api`: API de leitura de consolidados, alimentada por eventos Kafka do Ledger.

Componentes principais:

- `src/Auth.Api`
- `src/LedgerService.Api`, `src/LedgerService.Application`, `src/LedgerService.Domain`, `src/LedgerService.Infrastructure`
- `src/BalanceService.Api`, `src/BalanceService.Application`, `src/BalanceService.Domain`, `src/BalanceService.Infrastructure`
- `tests/*`

## Primeiros passos

Pre-requisitos principais:

- .NET SDK conforme `global.json`.
- Docker-compatible API para Testcontainers e stack local.
- CLI `docker` com suporte a `docker compose` para a stack completa local.
- PostgreSQL e Kafka, quando rodar as APIs fora de container.

Comandos mais usados:

```powershell
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Fluxo local completo:

```powershell
docker compose up -d --build
```

Na primeira execucao com banco vazio, aplique as migrations manualmente antes de usar as APIs. O passo a passo fica em [desenvolvimento local](docs/development/local-development.md).

Testes de integracao selecionados usam Testcontainers com PostgreSQL real. O Testcontainers precisa de uma Docker-compatible API, nao da CLI `docker` nem de Docker Desktop especificamente. No Windows sem Docker Desktop, o ambiente recomendado e Rancher Desktop com `moby/dockerd`.

## Documentacao

- [Indice geral da documentacao](docs/README.md)
- [Desenvolvimento local](docs/development/local-development.md)
- [LedgerService API](docs/development/ledger-api.md)
- [Arquitetura](docs/architecture/README.md)
- [Boundaries arquiteturais](docs/architecture/boundaries.md)
- [ADRs](docs/adrs/README.md)
- [Autenticacao e autorizacao](docs/development/authentication.md)
- [Kafka, Outbox e DLQ](docs/development/kafka-outbox.md)
- [Observabilidade e operacao minima](docs/observability.md)
- [Cobertura de testes](docs/development/test-coverage.md)
- [Validacao de pull requests](docs/development/pull-request-validation.md)
- [Mutation testing](docs/development/mutation-testing-stryker.md)
- [GitHub Pages e LikeC4](docs/development/github-pages.md)

Documentacao arquitetural publicada:

<https://rodri-oliveira-dev.github.io/poc-arquitetura/>

## Qualidade e validacao

O fluxo recomendado para validar uma mudanca e:

```powershell
./test.ps1
```

Esse script executa testes com cobertura e aplica o gate minimo de 80% de cobertura total de linhas. Detalhes ficam em [cobertura de testes](docs/development/test-coverage.md).

Pull requests devem passar pelo check `Build and test`, definido no workflow `pull-request-validation`. A validacao completa pos-merge/manual fica no workflow `dotnet-ci`.

## Decisoes arquiteturais

As decisoes do projeto ficam em [docs/adrs](docs/adrs/README.md). Use as ADRs como fonte de verdade para historico, trade-offs, decisoes aceitas e pontos de melhoria.

## Troubleshooting rapido

- Erro ao aplicar migrations: confira a connection string e se o PostgreSQL esta acessivel.
- Swagger nao abre: confirme se a API esta rodando e se Swagger esta habilitado para o ambiente.
- Outbox com logs repetidos: comportamento esperado do polling configurado em `Outbox:Publisher`.
- Testcontainers nao encontra o Docker daemon: confira `docker version`, `docker ps` e o `DOCKER_HOST` documentado em [desenvolvimento local](docs/development/local-development.md).
