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
| [ADR-0082](./0082-cloud-sql-postgresql-desenvolvimento-local-auth-proxy.md) | Aceito | Registra Cloud SQL PostgreSQL no Terraform dev com acesso local via Auth Proxy e suporte por Compose. |
| [ADR-0081](./0081-postgres-local-unico-com-schemas-por-servico.md) | Aceito | Usa um PostgreSQL local unico com schemas e usuarios separados por servico e responsabilidade. |
| [ADR-0080](./0080-backend-remoto-gcs-terraform-dev.md) | Aceito | Configura backend remoto GCS parcial para o Terraform dev, com state separado por ambiente e migracao manual. |
| [ADR-0079](./0079-terraform-state-local-e-backend-remoto.md) | Aceito | Registra riscos do state local, gatilhos e estrategia que orientaram a adocao posterior do backend remoto GCS. |
| [ADR-0078](./0078-pubsub-provider-principal-local-emulator.md) | Aceito | Define Pub/Sub como provider principal, emulator como default local e Kafka como opcao legada explicita. |
| [ADR-0077](./0077-pubsub-provider-mensageria.md) | Substituido | Define a fase incremental de Pub/Sub como provider alternativo, encerrada pela ADR-0078. |
| [ADR-0076](./0076-formalizar-contrato-ledger-entry-created-v1.md) | Aceito | Formaliza `LedgerEntryCreated.v1` em JSON Schema e mantem `BRL` como limitacao conhecida fora do payload. |
| [ADR-0075](./0075-mensageria-ports-adapters-kafka-provider.md) | Parcialmente substituido | Define o boundary de mensageria por ports and adapters na fase em que Kafka era o provider atual; a ADR-0078 promove Pub/Sub a principal. |
| [ADR-0074](./0074-keycloak-como-identidade-principal.md) | Aceito | Define Keycloak como identidade principal, remove Auth.Api da stack principal e o mantem apenas como legado por overlay. |
| [ADR-0073](./0073-plano-migracao-auth-api-keycloak-oidc.md) | Substituido | Define plano executavel para migrar Auth.Api para Keycloak/OIDC mantendo JWT offline via JWKS e claims atuais na primeira fase; fechado pela ADR-0074. |
| [ADR-0072](./0072-load-balance-local-ledger-nginx.md) | Aceito | Demonstra load balance local do LedgerService.Api com duas instancias atras do Nginx e `least_conn`. |
| [ADR-0071](./0071-borda-local-nginx-https.md) | Aceito | Adiciona overlay opcional com Nginx, HTTPS local, portal e subdominios `.localhost` para Swagger. |
| [ADR-0070](./0070-dlq-outbox-banco-backoff-requeue.md) | Aceito | Padroniza DLQ em banco para Outbox com backoff exponencial, inspecao e requeue administrativo por id. |
| [ADR-0069](./0069-remocao-fluentassertions-xunit-nativo.md) | Aceito | Remove FluentAssertions por risco de licenca comercial nas versoes 8+ e padroniza asserts nativos do xUnit. |
| [ADR-0068](./0068-gate-cobertura-workers-coverlet.md) | Aceito | Eleva o gate para 85% e exige emissao explicita dos assemblies Worker no relatorio Coverlet. |
| [ADR-0067](./0067-separacao-workers-processos-api.md) | Aceito | Separa APIs e workers em processos distintos, com composition root explicito e validacao contra HostedServices duplicados. |
| [ADR-0066](./0066-cobertura-minima-workers.md) | Aceito | Exige cobertura minima dedicada para os assemblies `LedgerService.Worker` e `BalanceService.Worker`. |
| [ADR-0065](./0065-workers-dedicados-no-compose-local.md) | Aceito | Sobe APIs e workers em containers dedicados no Docker Compose local. |
| [ADR-0064](./0064-ledger-worker-processo-dedicado.md) | Aceito | Separa os workers do Ledger em `LedgerService.Worker`, sem hospedar background services na API HTTP. |
| [ADR-0063](./0063-loki-alloy-logs-centralizados-locais.md) | Aceito | Adiciona Loki e Grafana Alloy para centralizacao local de logs dos containers com labels de baixa cardinalidade. |
| [ADR-0062](./0062-alertas-tecnicos-prometheus-alertmanager-locais.md) | Aceito | Adiciona regras de alertas tecnicos locais no Prometheus e Alertmanager sem integracoes externas. |
| [ADR-0061](./0061-prometheus-grafana-metricas-tecnicas-locais.md) | Aceito | Adiciona Prometheus e Grafana locais para metricas tecnicas via OpenTelemetry Collector. |
| [ADR-0060](./0060-opentelemetry-collector-local.md) | Aceito | Introduz OpenTelemetry Collector no compose local entre as APIs e o Jaeger. |
| [ADR-0059](./0059-metricas-customizadas-system-diagnostics.md) | Aceito | Padroniza metricas customizadas com `System.Diagnostics.Metrics` e baixa cardinalidade. |
| [ADR-0058](./0058-propagacao-w3c-outbox-kafka.md) | Aceito | Persiste contexto W3C na Outbox para continuidade HTTP -> Outbox -> Kafka -> Balance. |
| [ADR-0057](./0057-requeue-administrativo-outbox-failed.md) | Aceito | Cria endpoint administrativo protegido para recolocar mensagens Outbox Failed em Pending com auditoria operacional. |
| [ADR-0056](./0056-testcontainers-postgresql-testes-integracao.md) | Aceito | Padroniza Testcontainers para testes de integracao que dependem de PostgreSQL real, com porta dinamica e isolamento por collection. |
| [ADR-0055](./0055-runtime-docker-compatible-testcontainers.md) | Aceito | Padroniza Docker-compatible API para Testcontainers e comandos `docker compose` sem exigir Docker Desktop. |
| [ADR-0054](./0054-controle-concorrencia-estornos-ledger.md) | Aceito | Define indice unico filtrado, claim atomico e lock por linha para concorrencia em estornos do LedgerService. |
| [ADR-0053](./0053-lock-transacional-por-chave-no-balance.md) | Aceito | Usa lock transacional por chave no PostgreSQL para evitar lost update em daily_balances sob concorrencia. |
| [ADR-0052](./0052-processamento-assincrono-reprocessamento-lancamentos-ledger.md) | Aceito | Processa reprocessamentos no Ledger por consumer Kafka, faz replay idempotente de LedgerEntryCreated.v1 e mantem o Balance fora da solicitacao operacional. |
| [ADR-0051](./0051-solicitacao-assincrona-reprocessamento-lancamentos.md) | Aceito | Registra solicitacoes de reprocessamento por merchant/periodo com MediatR, idempotencia, Outbox e resposta 202. |
| [ADR-0050](./0050-processamento-assincrono-estornos-ledger.md) | Aceito | Processa estornos no Ledger por worker, cria lancamento compensatorio e publica evento financeiro final para o Balance. |
| [ADR-0049](./0049-solicitacao-assincrona-estorno-lancamento-mediator.md) | Aceito | Adota MediatR no Ledger para solicitar estorno assincrono e consultar seu status com persistencia, idempotencia e Outbox. |
| [ADR-0048](./0048-versionamento-semantico-gitversion-commits-semanticos.md) | Aceito | Adota GitVersion como fonte oficial de SemVer baseado em commits semanticos. |
| [ADR-0047](./0047-plano-futuro-adocao-incremental-testcontainers.md) | Proposto | Define plano futuro, incremental e condicionado a spike para adotar Testcontainers nos testes de integracao com PostgreSQL real. |
| [ADR-0046](./0046-plano-futuro-adocao-incremental-dotnet-aspire.md) | Proposto | Define plano futuro, incremental e condicionado a spike para avaliar .NET Aspire sem substituir Compose ou testes atuais. |
| [ADR-0045](./0045-retencao-e-exposicao-de-artifacts-github-actions.md) | Aceito | Reduz exposicao de artifacts publicados por workflows mantendo diagnostico e retencao explicita. |
| [ADR-0044](./0044-mutation-testing-informativo-github-actions.md) | Aceito | Executa mutation testing informativo no GitHub Actions apos push na main. |
| [ADR-0043](./0043-mutation-testing-local-stryker-balance-application.md) | Aceito | Adota mutation testing local e opcional com Stryker.NET para BalanceService.Application. |
| [ADR-0042](./0042-mutation-testing-local-stryker-ledger-application.md) | Aceito | Adota mutation testing local e opcional com Stryker.NET para LedgerService.Application. |
| [ADR-0041](./0041-validacao-pull-requests-branch-protection.md) | Aceito | Cria workflow dedicado de validacao de PRs e define o check obrigatorio de branch protection. |
| [ADR-0040](./0040-padronizacao-commands-queries-validacao-entrada-apis.md) | Aceito | Padroniza politica de commands/queries e valida amount decimal no contrato do Ledger. |
| [ADR-0039](./0039-publicacao-indicadores-qualidade-documentacao-arquitetural-pages.md) | Aceito | Publica badges de qualidade no README e documentacao LikeC4 no GitHub Pages. |
| [ADR-0038](./0038-automacao-releases-prs-mergeados-main.md)           | Aceito      | Automatiza tags e GitHub Releases a partir de PRs mergeados na main.                             |
| [ADR-0037](./0037-otimizacao-hooks-workflows-arquivos-impactantes.md) | Aceito | Otimiza hooks e workflows usando classificacao de arquivos impactantes. |
| [ADR-0036](./0036-padronizacao-cobertura-testes-solution.md) | Aceito | Padroniza cobertura consolidada da solution com gate minimo de 80%. |
| [ADR-0035](./0035-padronizacao-git-hooks-locais.md) | Aceito | Padroniza hooks locais para commit, post-merge e pre-push. |
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
| [ADR-0011](./0011-padronizacao-repo-cpm-build-props-editorconfig.md)  | Substituído | Consolidado em [`docs/development/repository-standards.md`](../development/repository-standards.md). |
| [ADR-0010](./0010-migrations-nao-automaticas-no-startup.md)           | Substituído | Consolidado em [`docs/development/local-development.md`](../development/local-development.md). |
| [ADR-0009](./0009-stack-local-compose-nerdctl.md)                     | Substituído | Substituido por [`ADR-0055`](./0055-runtime-docker-compatible-testcontainers.md) e consolidado em [`docs/development/local-development.md`](../development/local-development.md). |
| [ADR-0008](./0008-scopes-por-endpoint-policy-based.md)                | Substituído | Consolidado em ADR-0004 (segurança).                                                             |
| [ADR-0007](./0007-banco-por-microservico-postgres-efcore.md)          | Aceito      | Banco por microserviço (PostgreSQL) com EF Core.                                                 |
| [ADR-0006](./0006-migrar-auth-api-para-keycloak.md)                   | Proposto    | Ponto de melhoria: substituir Auth.Api por Keycloak (OIDC) mantendo validação via JWKS.          |
| [ADR-0005](./0005-observabilidade-correlationid-otel.md)              | Aceito      | Correlação via `X-Correlation-Id` e base para tracing/métricas (OpenTelemetry opcional).         |
| [ADR-0004](./0004-autenticacao-jwt-rs256-via-jwks.md)                 | Aceito      | JWT RS256 com validação offline via JWKS publicado pelo Auth.Api (sem introspecção por request). |
| [ADR-0003](./0003-integracao-assincrona-kafka-com-outbox.md)          | Aceito      | Integração assíncrona via Kafka usando Outbox para entrega *at-least-once*.                      |
| [ADR-0002](./0002-clean-architecture-ddd-por-servico.md)              | Aceito      | Clean Architecture + DDD (camadas Domain/Application/Infrastructure/Api) por microserviço.       |
| [ADR-0001](./0001-separar-ledger-e-balance-com-projecao.md)           | Aceito      | Separar escrita (Ledger) e leitura (Balance) com projeção assíncrona (CQRS básico).              |
| [ADR-0000](./0000-use-adrs.md)                                        | Substituído | Consolidado neste README.                                                                        |
