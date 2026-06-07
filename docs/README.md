# Documentacao do repositorio

Este indice organiza a documentacao por finalidade. O `README.md` da raiz e a porta de entrada; os detalhes tecnicos ficam nesta pasta.

## Tutorial

- [README do projeto](../README.md): problema, solucao, quickstart, comandos principais e links.
- [Desenvolvimento local](development/local-development.md): compose, Pub/Sub emulator padrao, Kafka legado opcional, portas, migrations, execucao no host, VS Code, Testcontainers e load tests.
- [Dev Container opcional](development/devcontainer.md): ambiente VS Code conteinerizado sem substituir o fluxo local do host.
- [Ferramentas auxiliares](development/tooling.md): Node.js LTS, npm, npx, tools .NET, Redocly CLI, LikeC4, Swashbuckle CLI e fluxo local de OpenAPI.
- [Validacao dos contratos OpenAPI](development/openapi-contract-validation.md): geracao, lint, drift e diff de breaking changes contra a main.
- [Politica de versionamento de contratos de eventos](development/event-contract-versioning.md): evoluir eventos versionados entre Ledger e Balance preservando compatibilidade em Pub/Sub e Kafka.
- [FAQ](faq.md): respostas curtas para as duvidas mais provaveis de leitura tecnica.
- [Maturidade tecnica do projeto](maturity.md): criterios atuais de documentacao, seguranca, testes, CI, observabilidade e pendencias.
- [Roadmap arquitetural consolidado](roadmap.md): proximas frentes por area de maturidade, sem representar compromisso de producao.

## How-to

- [Autenticacao e autorizacao](development/authentication.md): obter token local, validar scopes, audiences e autorizacao por merchant.
- [Mensageria, Outbox e DLQ](development/kafka-outbox.md): validar Pub/Sub principal, Kafka legado opcional, publicacao, consumo, DLQ, requeue e fluxos assincronos.
- [Cobertura de testes](development/test-coverage.md): executar testes com cobertura, interpretar falhas e entender os gates de 85% global e dos workers.
- [SonarQube local](quality/sonarqube.md): subir SonarQube com Docker Compose e executar analise estatica local.
- [Mutation testing com Stryker.NET](development/mutation-testing-stryker.md): executar mutation testing local e interpretar relatorios.
- [OWASP ZAP local](development/owasp-zap.md): executar DAST baseline local contra Ledger e Balance, com Auth.Api legado apenas opcional, salvando relatorios em `zap-reports/`.
- [Validacao de seguranca com Trivy](development/trivy-security-scan.md): validar Dockerfiles, Terraform, misconfigurations, secrets e filesystem no hook local e no CI.
- [Git hooks locais](development/git-hooks.md): instalar e entender `commit-msg`, `post-merge` e `pre-push`.
- [Setup local Terraform e GCP](development/terraform-gcp-local-setup.md): instalar Terraform CLI, Google Cloud CLI e TFLint no Windows e executar validacoes locais seguras.
- [Cloud SQL PostgreSQL local com Auth Proxy](development/cloudsql-postgres-local-setup.md): conectar aplicacao em debug ou Docker Compose a Cloud SQL sem authorized networks.
- [Checklist manual para primeiro apply Pub/Sub em GCP dev](development/pubsub-gcp-dev-apply-checklist.md): preparar projeto descartavel, revisar plano, autorizar apply manualmente e limpar recursos apos a validacao.
- [Contrato Pub/Sub entre infraestrutura e aplicacao](development/pubsub-infra-app-contract.md): mapear outputs Terraform para options dos workers, IAM minimo e checklist para GCP real.
- [Custo e free tier do Pub/Sub](development/pubsub-cost-and-free-tier.md): estimar throughput, identificar recursos que podem gerar custo e coletar dados para uma estimativa real.
- [Operacao do Pub/Sub](operations/pubsub.md): selecionar provider, subir emulator, aplicar Terraform dev manualmente, configurar workers e diagnosticar falhas comuns.
- [Runbook de recuperacao de eventos](operations/event-recovery-runbook.md): consolidar investigacao de DLQ, retry, replay, descarte, rebuild de projecao e relatorio de divergencia.
- [Replay e DLQ orientados por contrato](operations/event-replay-and-dlq.md): inspecionar DLQ, validar schema por versao, decidir discard, ack, nack ou redrive e preservar idempotencia.
- [Estrategia operacional de DLQ](operations/dlq-strategy.md): classificar falhas, decidir discard, retry ou replay/redrive em Pub/Sub e Kafka, preservar idempotencia e orientar observabilidade.
- [Estrategia operacional de replay seguro](operations/replay-strategy.md): diferenciar retry e replay, definir dry-run, filtros, idempotencia, auditoria e cuidados para Pub/Sub, Kafka, DLQ e Outbox.
- [Rebuild de projecao do Balance](operations/projection-rebuild.md): calcular saldo reconstruido em paralelo logico, comparar com a projecao atual e gerar relatorio de divergencia sem alterar dados.
- [Validacao de pull requests](development/pull-request-validation.md): entender checks obrigatorios, workflows e branch protection.
- [GitHub Pages e LikeC4](development/github-pages.md): gerar e publicar a documentacao arquitetural.
- [Releases e versionamento](development/releases.md): SemVer com GitVersion, commits semanticos, tags e GitHub Releases.
- [Troubleshooting](troubleshooting.md): diagnostico rapido de erros comuns.

## Referencia

- [LedgerService API](development/ledger-api.md): contratos HTTP de escrita, headers, idempotencia, estornos e reprocessamentos.
- [BalanceService API](development/balance-api.md): contratos HTTP de leitura de consolidados diarios e por periodo.
- [Contratos logicos de eventos](events/README.md): payloads logicos atuais, produtores, consumidores e mapeamentos Pub/Sub/Kafka dos eventos.
- [JSON Schemas versionados de eventos](../contracts/events/README.md): schemas e exemplos para validar payloads logicos de eventos.
- [Versionamento de contratos de eventos](development/event-contract-versioning.md): politica de compatibilidade, transporte, schemas, testes e depreciacao.
- [Runbook de recuperacao de eventos](operations/event-recovery-runbook.md): guia operacional consolidado para decidir entre retry, replay, redrive, descarte, relatorio de divergencia e rebuild de projecao.
- [Replay e DLQ orientados por contrato](operations/event-replay-and-dlq.md): runbook operacional para Pub/Sub, Kafka legado, replay, redrive e validacao antes de reprocessar.
- [Estrategia operacional de DLQ](operations/dlq-strategy.md): criterios operacionais para DLQ de aplicacao, DLQ tecnica, idempotencia, contratos e troubleshooting.
- [Estrategia operacional de replay seguro](operations/replay-strategy.md): pre-condicoes, filtros, dry-run, auditoria e decisoes de replay seguro em Pub/Sub e Kafka.
- [Rebuild de projecao do Balance](operations/projection-rebuild.md): relatorio de divergencia para rebuild paralelo logico antes de qualquer correcao ou troca de projecao.
- [Contrato LedgerEntryCreated.v1](events/ledger-entry-created-v1.md): contrato legado sem `currency`, aceito para mensagens antigas.
- [Contrato LedgerEntryCreated.v2](events/ledger-entry-created-v2.md): contrato atual com `currency` obrigatoria.
- [Observabilidade e operacao minima](observability.md): health, readiness, logs, traces, metricas, dashboards, alertas e validacoes operacionais.
- [Padroes do repositorio](development/repository-standards.md): arquivos de padronizacao, tools, estilo, hooks e manutencao.
- [Artifacts dos workflows](development/workflow-artifacts.md): politica de publicacao, conteudo e retencao.
- [Qualidade](quality/README.md): guias de analise estatica e validacoes locais.
- [k6 load tests](../loadtests/k6/README.md): configuracao dos scripts k6 usados pelos runners.
- [Revisao Docker e Compose](reports/docker-compose-performance-review.md): diagnostico de build, cache, volumes, desempenho local e limpeza segura.
- [Revisao de abstracoes](reports/architecture-abstractions-review.md): classificacao de interfaces com implementacao unica e simplificacoes aplicadas.
- [Validacao final do backend](reports/final-backend-validation.md): build, testes, migrations, Compose, fluxo ponta a ponta, k6 e ressalvas operacionais apos as etapas de melhoria.
- [Baseline dos contratos OpenAPI](reports/openapi-contract-baseline.md): contratos gerados, contagem de endpoints, warnings e determinismo da geracao.
- [Diagnostico de contratos de eventos](reports/event-contracts-diagnostics.md): fluxo atual de eventos entre Ledger e Balance, Pub/Sub, Kafka, Outbox, DLQ, idempotencia e riscos de contrato.
- [Diagnostico de replay, DLQ e projecao](reports/replay-dlq-projection-diagnostics.md): estado atual de Outbox, retry, replay, redrive, idempotencia e reconstrucao de projecao.

## Explicacao

- [Documentacao arquitetural](architecture/README.md): modelo LikeC4 e publicacao no GitHub Pages.
- [Boundaries arquiteturais](architecture/boundaries.md): responsabilidades de `Api`, `Application`, `Domain` e `Infrastructure`.
- [Analise arquitetural e decisoes recomendadas](architecture/decisions.md): riscos, simplificacoes e roadmap pragmatico.
- [Roadmap arquitetural consolidado](roadmap.md): leitura consolidada das frentes feitas, parciais, proximos passos e itens fora de escopo por enquanto.
- [ADRs](adrs/README.md): historico de decisoes arquiteturais e pontos de melhoria.
- [Terraform state local e backend remoto](adrs/0079-terraform-state-local-e-backend-remoto.md): registra os riscos do state local, gatilhos e estrategia que antecederam a adocao do backend remoto GCS.
- [Backend remoto GCS para Terraform dev](adrs/0080-backend-remoto-gcs-terraform-dev.md): registra a adocao do backend remoto parcial em GCS, separacao por ambiente e migracao manual de state.
- [Mensageria por ports and adapters](adrs/0075-mensageria-ports-adapters-kafka-provider.md): historico da introducao do boundary quando Kafka ainda era o provider atual.
- [Pub/Sub como provider principal](adrs/0078-pubsub-provider-principal-local-emulator.md): adota Pub/Sub como caminho principal, emulator como default local e Kafka como opcao legada.
- [LedgerEntryCreated.v2 com currency explicita](adrs/0084-ledger-entry-created-v2-currency-explicita.md): cria v2 com `currency` obrigatoria e mantem leitura de v1 como legado.
- [Pub/Sub como provider alternativo](adrs/0077-pubsub-provider-mensageria.md): historico do plano incremental que precedeu a adocao principal.
- [Plano de migracao Auth.Api para Keycloak/OIDC](adrs/0073-plano-migracao-auth-api-keycloak-oidc.md): execucao incremental mantendo validacao JWT offline via JWKS.
- [Keycloak como identidade principal](adrs/0074-keycloak-como-identidade-principal.md): decisao final de remover Auth.Api da stack principal e mante-lo apenas como legado por overlay.
- [Avaliacao de .NET Aspire e riscos OWASP](reports/aspire-and-owasp-assessment.md): relatorio historico de contexto, nao estado operacional mais recente.

## Agentes

- [AGENTS.md](../AGENTS.md): instrucoes globais para Codex trabalhar neste repositorio.
- [Skills em `.agents/skills`](../.agents/skills): fluxos especializados usados quando o pedido combinar com a descricao da skill.

## Manutencao

- Mantenha informacoes detalhadas em `docs`, com resumo e link no `README.md`.
- Evite duplicar comandos longos entre documentos; prefira apontar para a fonte de verdade.
- Atualize ADRs quando houver decisao arquitetural nova ou mudanca relevante de comportamento.
- Atualize este indice quando adicionar, remover ou consolidar documentos.
- Registre revisoes estruturais em [documentation-audit.md](documentation-audit.md).
