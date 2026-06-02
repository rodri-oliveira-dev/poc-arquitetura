# FAQ

## O que este projeto demonstra tecnicamente?

Ele demonstra uma arquitetura de microservicos em .NET com escrita e leitura separadas, Clean Architecture/DDD, PostgreSQL por servico, Pub/Sub principal, Kafka legado opcional, Outbox, DLQ, autenticacao JWT com JWKS, autorizacao por merchant, observabilidade local e testes automatizados. A visao resumida fica no [README](../README.md) e a leitura arquitetural fica em [docs/architecture](architecture/README.md).

No estado atual, APIs HTTP e workers rodam como processos separados: `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`. Essa separacao torna escala, deploy, readiness, troubleshooting e observabilidade independentes por `ServiceName`.

## Qual problema este projeto resolve?

O projeto resolve, em formato de POC, o fluxo de registrar lancamentos financeiros com consistencia transacional local, publicar eventos de forma confiavel e manter uma projecao de saldo para consulta. O desenho evita atualizar saldo diretamente no mesmo request de escrita e usa consistencia eventual via Pub/Sub por padrao.

## Como executo o projeto localmente?

Use o script da stack local:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

O guia completo, incluindo portas, migrations, execucao no host e Testcontainers, fica em [desenvolvimento local](development/local-development.md).

## Quais pre-requisitos preciso instalar?

Os pre-requisitos principais sao .NET SDK conforme `global.json`, Docker-compatible API, CLI `docker` com `docker compose` e, para execucao fora de container, PostgreSQL e Pub/Sub emulator acessiveis. Kafka e necessario apenas no modo legado. Veja [pre-requisitos de desenvolvimento local](development/local-development.md#pre-requisitos).

## Como rodo os testes?

Para a validacao completa com cobertura e gate:

```powershell
./test.ps1
```

No Linux/macOS:

```bash
./test.sh
```

Detalhes sobre cobertura, Testcontainers e interpretacao de falhas ficam em [cobertura de testes](development/test-coverage.md).

## Onde encontro as decisoes arquiteturais?

As decisoes ficam em [ADRs](adrs/README.md). A leitura consolidada da arquitetura fica em [documentacao arquitetural](architecture/README.md), [boundaries](architecture/boundaries.md) e [analise arquitetural](architecture/decisions.md).

## Onde vejo a organizacao das camadas e modulos?

O resumo esta no [README](../README.md#arquitetura). A explicacao detalhada das responsabilidades de `Api`, `Application`, `Domain` e `Infrastructure` fica em [boundaries arquiteturais](architecture/boundaries.md).

## Quais padroes arquiteturais foram usados?

O projeto usa Clean Architecture/DDD nos servicos de negocio, CQRS pragmatico entre escrita e leitura, Outbox para publicacao confiavel, Pub/Sub para integracao assincrona principal e JWT/JWKS para autenticacao. Kafka permanece como adapter legado opcional. Os trade-offs estao em [analise arquitetural](architecture/decisions.md) e nas [ADRs](adrs/README.md).

## Como configuro variaveis de ambiente?

Use variaveis com `__` para separar secoes, por exemplo `ConnectionStrings__DefaultConnection` e `PubSub__Producer__ProjectId`. Exemplos ficam em [desenvolvimento local](development/local-development.md#configuracao), [autenticacao](development/authentication.md) e [observabilidade](observability.md#configuracao).

## Como faco troubleshooting de erros comuns?

Use [troubleshooting](troubleshooting.md). O guia aponta para diagnosticos de migrations, Testcontainers, Docker-compatible API, Swagger, Pub/Sub, Kafka legado, Outbox, readiness, Grafana/Prometheus/Loki e load tests.

## Onde ficam as instrucoes para Codex ou agentes?

As instrucoes globais ficam em [AGENTS.md](../AGENTS.md). Fluxos especializados ficam em `.agents/skills/` e devem ser usados quando o pedido combinar com a descricao da skill.

## Como reviso seguranca e autenticacao?

Comece por [autenticacao e autorizacao](development/authentication.md), depois consulte ADRs relacionadas a JWT/JWKS, autorizacao por merchant, Swagger, hardening e transporte seguro no [indice de ADRs](adrs/README.md).

## Como reviso mensageria, Outbox e DLQ?

Use [Kafka, Outbox e DLQ](development/kafka-outbox.md). O documento descreve topicos, headers, eventos, estados da Outbox, requeue operacional, DLQ, metricas e governanca.

## Onde vejo observabilidade?

Use [observabilidade e operacao minima](observability.md). O documento cobre health/readiness, logs, traces, metricas, dashboards Grafana, Prometheus, Loki, Alertmanager e validacoes operacionais.

## Workers expoem endpoints HTTP?

Nao. `LedgerService.Worker` e `BalanceService.Worker` sao Generic Hosts sem superficie HTTP nesta POC. Health/readiness HTTP pertencem as APIs; a saude operacional dos workers deve ser acompanhada por startup, logs, metricas, traces e efeitos em Outbox/mensageria/bancos.
