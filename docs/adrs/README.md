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
| [ADR-0034](./0034-boundaries-arquiteturais-e-estrutura-de-camadas.md) | Aceito      | Define boundaries arquiteturais, nivel de camadas por servico e documentacao LikeC4.             |
| [ADR-0033](./0033-governanca-documentacao-operacional.md)             | Aceito      | Define docs operacionais obrigatorios, responsaveis e criterios de atualizacao.                  |
| [ADR-0032](./0032-baseline-seguranca-containers.md)                   | Aceito      | Define usuario non-root, politica de tags/digests, scan de imagem e limites locais de recursos.  |
| [ADR-0031](./0031-baseline-observabilidade.md)                        | Aceito      | Padroniza logs, traces, metricas, OTLP opcional e operacao minima por ambiente.                  |
| [ADR-0030](./0030-baseline-minimo-hardening-auth-api.md)              | Aceito      | Aplica baseline minimo de hardening ao Auth.Api preservando diferencas da API de autenticacao.   |
| [ADR-0029](./0029-limites-operacionais-de-api.md)                     | Aceito      | Define limites configuraveis de body, periodo do Balance e rate limit por ambiente.              |
| [ADR-0028](./0028-baseline-transporte-seguro-jwks-kafka.md)           | Aceito      | Define HTTPS para JWKS e Kafka seguro fora da excecao local Development/Local.                   |
| [ADR-0027](./0027-exposicao-controlada-swagger-openapi.md)            | Aceito      | Controla exposicao de Swagger/OpenAPI por ambiente e configuracao explicita.                     |
| [ADR-0026](./0026-atualizar-opentelemetry-api-vulneravel.md)          | Aceito      | Atualiza `OpenTelemetry.Api` vulneravel e bloqueia vulnerabilidades NuGet moderadas ou superiores no CI. |
| [ADR-0025](./0025-gestao-de-dependencias-vulneraveis.md)              | Parcialmente substituido | Define politica NuGet inicial para vulnerabilidades high/critical, ajustada pela ADR-0026. |
| [ADR-0024](./0024-politica-autenticacao-auth-api-poc.md)              | Aceito      | Endurece Auth.Api da POC com credenciais configuradas, scopes explicitos e rate limit no login.  |
| [ADR-0023](./0023-autorizacao-por-merchant.md)                        | Aceito      | Define autorizacao explicita por merchant via claim `merchant_id` para evitar BOLA.              |
| [ADR-0022](./0022-padronizar-higiene-de-dependencias-e-containers.md) | Proposto    | Padroniza verificacao de vulnerabilidades NuGet, imagens e hardening de containers.              |
| [ADR-0021](./0021-padronizar-exposicao-operacional-swagger-cors-health.md) | Proposto    | Define politica por ambiente para Swagger, CORS, health e readiness.                             |
| [ADR-0020](./0020-padronizar-configuracao-segura-de-secrets-e-ambientes.md) | Proposto    | Padroniza secrets, placeholders, `.env` local e configuracao por ambiente.                       |
| [ADR-0019](./0019-endurecer-seguranca-de-apis-conforme-owasp.md)      | Proposto    | Endurece APIs conforme OWASP, incluindo Auth.Api, Swagger e autorizacao por merchant.            |
| [ADR-0018](./0018-avaliar-adocao-incremental-dotnet-aspire.md)        | Proposto    | Avalia adocao incremental do .NET Aspire com AppHost e ServiceDefaults para desenvolvimento.     |
| [ADR-0017](./0017-implementar-dlq-versionamento-eventos-readiness-operacional.md) | Aceito      | Implementa DLQ real, `LedgerEntryCreated.v1`, headers Kafka e readiness operacional.             |
| [ADR-0016](./0016-contrato-http-explicito-swagger-e-controllers-magros.md) | Aceito      | Contrato HTTP explícito com Swagger e controllers magros com bind/map em componentes dedicados. |
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
