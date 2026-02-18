# ADRs e Pontos de Melhoria

Esta pasta contém os **Architecture Decision Records (ADRs)** do projeto: decisões arquiteturais registradas com contexto, racional, trade-offs e consequências.  
Também documentamos aqui **pontos de melhoria** (ADRs com status **Proposto**) para evoluir a PoC de forma incremental, mantendo rastreabilidade do porquê de cada escolha.

## Como usar

- **Aceito**: decisão em vigor e refletida no código/infra atual.
- **Proposto**: melhoria planejada, ainda não implementada (ou parcialmente).
- **Rejeitado/Substituído**: mantido apenas como histórico.

Padrão de arquivo sugerido: `NNNN-titulo-curto.md` (ex.: `0005-outbox-at-least-once.md`).

## Índice de ADRs (da mais nova para a mais antiga)

| ADR                                                                   | Status      | Resumo                                                                                           |
| --------------------------------------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------ |
| [ADR-0015](./0015-api-resilience-timeouts-retries-circuit-breaker.md) | Proposto    | Ponto de melhoria: timeouts/retries/circuit breaker padronizados (HTTP/JWKS e readiness).        |
| [ADR-0014](./0014-contratos-eventos-kafka-versionamento-e-dlq.md)     | Proposto    | Ponto de melhoria: versionamento/compatibilidade de eventos e DLQ para poison messages no Kafka. |
| [ADR-0013](./0013-readiness-healthchecks-db-kafka.md)                 | Proposto    | Ponto de melhoria: endpoint de readiness verificando DB e Kafka com timeouts.                    |
| [ADR-0012](./0012-health-liveness-publico.md)                         | Substituído | Consolidado em observabilidade + readiness.                                                      |
| [ADR-0011](./0011-padronizacao-repo-cpm-build-props-editorconfig.md)  | Substituído | Consolidado no README.md.                                                                        |
| [ADR-0010](./0010-migrations-nao-automaticas-no-startup.md)           | Substituído | Consolidado no README.md.                                                                        |
| [ADR-0009](./0009-stack-local-compose-nerdctl.md)                     | Substituído | Consolidado no README.md.                                                                        |
| [ADR-0008](./0008-scopes-por-endpoint-policy-based.md)                | Substituído | Consolidado em ADR-0004 (segurança).                                                             |
| [ADR-0007](./0007-banco-por-microservico-postgres-efcore.md)          | Aceito      | Banco por microserviço (PostgreSQL) com EF Core.                                                 |
| [ADR-0006](./0006-migrar-auth-api-para-keycloak.md)                   | Proposto    | Ponto de melhoria: substituir Auth.Api por Keycloak (OIDC) mantendo validação via JWKS.          |
| [ADR-0005](./0005-observabilidade-correlationid-otel.md)              | Aceito      | Correlação via `X-Correlation-Id` e base para tracing/métricas (OpenTelemetry opcional).         |
| [ADR-0004](./0004-autenticacao-jwt-rs256-via-jwks.md)                 | Aceito      | JWT RS256 com validação offline via JWKS publicado pelo Auth.Api (sem introspecção por request). |
| [ADR-0003](./0003-integracao-assincrona-kafka-com-outbox.md)          | Aceito      | Integração assíncrona via Kafka usando Outbox para entrega *at-least-once*.                      |
| [ADR-0002](./0002-clean-architecture-ddd-por-servico.md)              | Aceito      | Clean Architecture + DDD (camadas Domain/Application/Infrastructure/Api) por microserviço.       |
| [ADR-0001](./0001-separar-ledger-e-balance-com-projecao.md)           | Aceito      | Separar escrita (Ledger) e leitura (Balance) com projeção assíncrona (CQRS básico).              |
| [ADR-0000](./0000-use-adrs.md)                                        | Substituído | Consolidado neste README.                                                                        |
