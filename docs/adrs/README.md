# ADRs e Pontos de Melhoria

Esta pasta contém os **Architecture Decision Records (ADRs)** do projeto: decisões arquiteturais registradas com contexto, racional, trade-offs e consequências.  
Também documentamos aqui **pontos de melhoria** (ADRs com status **Proposto**) para evoluir a PoC de forma incremental, mantendo rastreabilidade do porquê de cada escolha.

## Como usar

- **Aceito**: decisão em vigor e refletida no código/infra atual.
- **Proposto**: melhoria planejada, ainda não implementada (ou parcialmente).
- **Rejeitado/Substituído**: mantido apenas como histórico.

Padrão de arquivo sugerido: `NNNN-titulo-curto.md` (ex.: `0005-outbox-at-least-once.md`).

## Índice de ADRs (da mais nova para a mais antiga)

| ADR                                                        | Status   | Resumo                                                                                          |
| ---------------------------------------------------------- | -------- | ----------------------------------------------------------------------------------------------- |
| [ADR-0101](./0101-readiness-healthchecks-db-kafka.md)      | Proposto | Separar liveness/readiness e checar dependências (DB/Kafka) para operação robusta.              |
| [ADR-0100](./0100-keycloak-replace-auth-api.md)            | Proposto | Substituir o Auth.Api por Keycloak (OIDC) como provedor de identidade padrão.                   |
| [ADR-0014](./0014-health-liveness-public.md)               | Aceito   | Expor `/health` público como liveness simples para indicar processo ativo.                      |
| [ADR-0013](./0013-repo-standardization.md)                 | Aceito   | Padronizar repositório com `.gitattributes`, `Directory.*` e `.editorconfig` para consistência. |
| [ADR-0012](./0012-no-auto-migrations-startup.md)           | Aceito   | Não aplicar migrations automaticamente no startup, mantendo execução explícita via `dotnet-ef`. |
| [ADR-0011](./0011-nerdctl-compose-local-stack.md)          | Aceito   | Executar stack local via `nerdctl compose` com overrides por variáveis de ambiente.             |
| [ADR-0010](./0010-api-versioning-url-segment.md)           | Aceito   | Versionar API via URL segment (`api/v{version}`) com Swagger multi-versão.                      |
| [ADR-0009](./0009-correlation-id.md)                       | Aceito   | Propagar `X-Correlation-Id` em HTTP/logs e headers Kafka para rastreabilidade mínima.           |
| [ADR-0008](./0008-scopes-per-endpoint.md)                  | Aceito   | Implementar autorização por scopes por endpoint usando claim `scope`.                           |
| [ADR-0007](./0007-jwt-rs256-jwks-offline-validation.md)    | Aceito   | Validar JWT RS256 offline via JWKS com cache/refresh, sem introspecção por request.             |
| [ADR-0006](./0006-balance-read-model-daily-balances.md)    | Aceito   | Construir projeção de leitura `daily_balances` no Balance e expor queries diárias/período.      |
| [ADR-0005](./0005-outbox-at-least-once.md)                 | Aceito   | Publicar eventos com Outbox + BackgroundService garantindo entrega at-least-once.               |
| [ADR-0004](./0004-kafka-event-driven-integration.md)       | Aceito   | Integrar serviços via Kafka usando evento `LedgerEntryCreated` e tópico explícito no compose.   |
| [ADR-0003](./0003-database-per-service-postgres-efcore.md) | Aceito   | Manter um banco PostgreSQL por microserviço com EF Core e migrations no Infrastructure.         |
| [ADR-0002](./0002-clean-architecture-ddd.md)               | Aceito   | Adotar Clean Architecture com DDD separando Api/Application/Domain/Infrastructure.              |
| [ADR-0001](./0001-microservices-ledger-balance-auth.md)    | Aceito   | Decompor em três microserviços (Ledger, Balance, Auth) para separar responsabilidades.          |
| [ADR-0000](./0000-use-adrs.md)                             | Aceito   | Registrar decisões arquiteturais usando ADRs em Markdown nesta pasta.                           |
