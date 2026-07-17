# poc-arquitetura

[![Build](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=build)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Tests](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/dotnet.yml?branch=main&label=tests)](https://github.com/rodri-oliveira-dev/poc-arquitetura/actions/workflows/dotnet.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_poc-arquitetura&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_poc-arquitetura)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=rodri-oliveira-dev_poc-arquitetura&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=rodri-oliveira-dev_poc-arquitetura)
[![Architecture Docs](https://img.shields.io/github/actions/workflow/status/rodri-oliveira-dev/poc-arquitetura/pages-architecture.yml?branch=main&label=architecture%20docs)](https://rodri-oliveira-dev.github.io/poc-arquitetura/)

POC educacional de microservicos em .NET para estudar arquitetura de software com codigo real: Clean Architecture, DDD, PostgreSQL, Kafka, Outbox, Inbox, JWT/JWKS com Keycloak, observabilidade, seguranca, contratos e testes automatizados.

Ela demonstra um problema comum em sistemas financeiros: registrar fatos de forma transacional, publicar eventos com confiabilidade, projetar saldos em outro servico e operar falhas sem esconder consistencia eventual. O repositorio tambem mostra contextos de identidade, transferencia, pagamento externo e auditoria funcional para exercitar trade-offs de integracao.

Este projeto e util para:

- quem esta aprendendo arquitetura e quer ver os conceitos aplicados;
- desenvolvedores .NET que querem executar, testar e alterar uma stack local;
- arquitetos que querem avaliar decisoes, limites e riscos;
- avaliadores tecnicos que querem entender a proposta rapidamente.

## Visao Geral

```mermaid
flowchart LR
    Client[Cliente ou teste] --> Keycloak[Keycloak OIDC]
    Client --> LedgerApi[LedgerService.Api]
    Client --> BalanceApi[BalanceService.Api]
    Client --> TransferApi[TransferService.Api]
    Client --> PaymentApi[PaymentService.Api]
    Client --> IdentityApi[IdentityService.Api]
    Client --> AuditApi[AuditService.Api]

    LedgerApi --> LedgerDb[(PostgreSQL schema ledger)]
    LedgerDb --> LedgerWorker[LedgerService.Worker]
    LedgerWorker --> Kafka[(Kafka)]
    Kafka --> BalanceWorker[BalanceService.Worker]
    BalanceWorker --> BalanceDb[(PostgreSQL schema balance)]
    BalanceApi --> BalanceDb

    TransferApi --> TransferDb[(schema transfer)]
    TransferWorker[TransferService.Worker] --> LedgerApi
    TransferDb --> TransferWorker
    TransferWorker --> Kafka

    PaymentApi --> PaymentDb[(schema payment)]
    Stripe[Stripe ou provider fake] --> PaymentApi
    PaymentDb --> PaymentWorker[PaymentService.Worker]
    PaymentWorker --> LedgerApi

    IdentityApi --> IdentityDb[(schema identity)]
    IdentityApi --> Keycloak
    IdentityApi --> Mailpit[Mailpit local]

    Kafka --> AuditWorker[AuditService.Worker]
    AuditApi --> AuditDb[(schema audit)]
    AuditWorker --> AuditDb
```

No modo local padrao, Kafka e o transporte principal dos workers de Ledger, Balance, Transfer e Audit. Pub/Sub permanece como alternativa explicita/legada apenas para Ledger/Balance. O `PaymentService` nao publica eventos financeiros diretamente: depois de confirmar um pagamento, o worker chama o `LedgerService.Api`, e o Balance continua sendo atualizado pelos eventos do Ledger.

## O Que Voce Aprende

- Como separar escrita (`Ledger`) e leitura (`Balance`) sem perder rastreabilidade.
- Por que Outbox ajuda quando banco e broker precisam andar juntos sem transacao distribuida.
- Como Inbox deduplica webhooks externos antes do processamento assincrono.
- Como idempotencia protege retries HTTP, webhooks e consumidores.
- Como uma Saga orquestrada coordena transferencia entre merchants.
- Como JWT/JWKS, scopes e autorizacao por merchant aparecem nas APIs.
- Como health, readiness, logs, traces, metricas, DLQ e replay entram na operacao.
- Como ADRs, specs SDD, contratos e runbooks sustentam decisoes ao longo do tempo.

## Bounded Contexts

| Contexto | Papel no laboratorio |
| --- | --- |
| `LedgerService` | Fonte de verdade dos fatos financeiros, Outbox, estornos e reprocessamentos. |
| `BalanceService` | Projecao de saldo consumindo eventos do Ledger. |
| `TransferService` | Saga de transferencia entre merchants, Kafka-only. |
| `PaymentService` | Pagamentos externos, ACL Stripe/fake provider, webhook assinado, Inbox e materializacao no Ledger. |
| `IdentityService` | Cadastro de usuarios, vinculo local, `MerchantId`, Keycloak Admin API e e-mail local. |
| `AuditService` | Auditoria funcional por HTTP e consumer Kafka de `AuditRecordRequested.v1`; os demais dominios ainda nao publicam eventos reais de auditoria. |
| `Auth.Api` | Legado preservado no repositorio para rastreabilidade historica; Keycloak e o emissor principal da stack local. |

Os servicos seguem a separacao `Api`, `Application`, `Domain`, `Infrastructure` e, quando aplicavel, `Worker`. A explicacao das fronteiras fica em [docs/architecture/boundaries.md](docs/architecture/boundaries.md).

## Quickstart

Pre-requisitos: .NET SDK conforme [global.json](global.json), CLI `docker` com `docker compose` e uma Docker-compatible API acessivel.

Valide build e testes:

```powershell
dotnet tool restore
dotnet restore ./PocArquitetura.slnx
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Suba o core funcional local no Windows:

```powershell
./scripts/local/create-env-local.ps1
./scripts/local/start-stack.ps1
```

No Linux/macOS:

```bash
./scripts/local/create-env-local.sh
./scripts/local/start-stack.sh
```

O script sobe PostgreSQL, Kafka, Keycloak, Mailpit, APIs e workers principais, aplica migrations pelo host e preserva volumes. O passo a passo completo, portas, debug, Testcontainers, observabilidade e limpeza ficam em [desenvolvimento local](docs/development/local-development.md).

Para incluir observabilidade local completa:

```powershell
./scripts/local/start-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/local/start-stack.sh
```

## Exemplos De Fluxo

**Lancamento financeiro**

1. `LedgerService.Api` recebe o comando HTTP.
2. Ledger grava o fato e a mensagem de Outbox na mesma transacao.
3. `LedgerService.Worker` publica no Kafka.
4. `BalanceService.Worker` consome, aplica idempotencia e atualiza a projecao.
5. `BalanceService.Api` consulta o saldo materializado.

**Pagamento externo**

1. `PaymentService.Api` cria o pagamento no provider fake ou Stripe por uma ACL.
2. Webhooks Stripe entram por endpoint assinado e sao persistidos na Inbox.
3. `PaymentService.Worker` processa a Inbox com retry e lease.
4. Pagamentos confirmados viram lancamentos no Ledger por chamada HTTP idempotente.

**Transferencia**

1. `TransferService.Api` registra a Saga.
2. `TransferService.Worker` chama Ledger para debito e credito.
3. Falha apos debito dispara compensacao por estorno no Ledger.
4. Eventos da Saga sao publicados no Kafka para rastreabilidade operacional.

## Jornada De Leitura

| Jornada | Ordem recomendada |
| --- | --- |
| Rapida, 10 a 15 min | Este README -> [FAQ](docs/faq.md) -> [Maturidade](docs/maturity.md) -> [Arquitetura](docs/architecture/README.md) |
| Iniciante | Este README -> [docs/README.md](docs/README.md) -> [Boundaries](docs/architecture/boundaries.md) -> [Catalogo de padroes](docs/architecture/patterns-catalog.md) -> [Mensageria, Outbox e DLQ](docs/development/kafka-outbox.md) |
| Desenvolvedor | [Desenvolvimento local](docs/development/local-development.md) -> [Autenticacao](docs/development/authentication.md) -> guias de API em `docs/development/*-api.md` -> [Testes e cobertura](docs/development/test-coverage.md) |
| Arquitetural | [Arquitetura](docs/architecture/README.md) -> [ADRs](docs/adrs/README.md) -> [Production readiness](docs/architecture/production-readiness.md) -> [Roadmap](docs/roadmap.md) |
| Operacional | [Observabilidade](docs/observability.md) -> [Runbook de recuperacao](docs/operations/event-recovery-runbook.md) -> [DLQ](docs/operations/dlq-strategy.md) -> [Replay](docs/operations/replay-strategy.md) |

O indice completo fica em [docs/README.md](docs/README.md). A documentacao visual LikeC4 publicada fica em <https://rodri-oliveira-dev.github.io/poc-arquitetura/>.

## Documentos Principais

- [Arquitetura](docs/architecture/README.md)
- [Catalogo de padroes](docs/architecture/patterns-catalog.md)
- [Desenvolvimento local](docs/development/local-development.md)
- [Autenticacao e autorizacao](docs/development/authentication.md)
- [Mensageria, Outbox e DLQ](docs/development/kafka-outbox.md)
- [Eventos](docs/events/README.md)
- [Contratos OpenAPI](docs/openapi)
- [Observabilidade](docs/observability.md)
- [Runbooks operacionais](docs/operations/event-recovery-runbook.md)
- [ADRs](docs/adrs/README.md)
- [Specs SDD](docs/specs)
- [Maturidade](docs/maturity.md)
- [Roadmap](docs/roadmap.md)

## Limites Da POC

Esta POC nao deve ser lida como pronta para producao. Ela e um laboratorio local, com varias decisoes deliberadamente proporcionais ao estudo:

- secrets locais ficam fora do Git, mas nao ha secret store produtivo;
- Kafka local e o caminho padrao; Pub/Sub e legado/opt-in para Ledger/Balance;
- rate limiting e local a cada replica;
- Nginx, observabilidade, SonarQube e k6 sao overlays opcionais;
- nao ha redrive publico versionado para todas as DLQs;
- `AuditService.Worker` consome o contrato canonico, mas os demais dominios ainda nao produzem auditoria funcional real;
- `IdentityService` cria usuarios no Keycloak e compensa falhas conhecidas, mas envio de e-mail ainda nao usa Outbox duravel;
- referencias ao `Auth.Api` sao historicas ou de compatibilidade; Keycloak e a identidade principal.

Para avaliar evolucao produtiva, leia [baseline de evolucao produtiva](docs/architecture/production-readiness.md).

## Comandos Uteis

| Tarefa | Comando |
| --- | --- |
| Testes com cobertura e gate | `./test.ps1` ou `./test.sh` |
| Stack local padrao | `./scripts/local/start-stack.ps1` ou `./scripts/local/start-stack.sh` |
| Stack completa com Nginx | `./scripts/local/start-full-stack.ps1` ou `./scripts/local/start-full-stack.sh` |
| Pub/Sub legado | `./scripts/local/start-stack-pubsub.ps1` ou `./scripts/local/start-stack-pubsub.sh` |
| Gerar OpenAPI | `./scripts/contracts/openapi/generate.ps1` ou `./scripts/contracts/openapi/generate.sh` |
| Validar eventos | `npm run events:validate` |
| Gerar LikeC4 | `npm run architecture:build` |
| Load test smoke Kafka | `./scripts/performance/run-loadtests.ps1 -Mode smoke-kafka` ou `./scripts/performance/run-loadtests.sh smoke-kafka` |

## Contribuicao E Seguranca

Leia [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md) e [AGENTS.md](AGENTS.md) antes de propor mudancas. Mudancas em contratos HTTP exigem regenerar `docs/openapi`; mudancas arquiteturais relevantes devem atualizar a documentacao correspondente e, quando houver decisao nova, registrar ADR.
