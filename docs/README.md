# Documentacao do repositorio

Este indice organiza a documentacao por finalidade. O `README.md` da raiz e a porta de entrada; os detalhes tecnicos ficam nesta pasta.

## Tutorial

- [README do projeto](../README.md): problema, solucao, quickstart, comandos principais e links.
- [Desenvolvimento local](development/local-development.md): compose, Pub/Sub emulator opcional, portas, migrations, execucao no host, VS Code, Testcontainers e load tests.
- [Dev Container opcional](development/devcontainer.md): ambiente VS Code conteinerizado sem substituir o fluxo local do host.
- [FAQ](faq.md): respostas curtas para as duvidas mais provaveis de leitura tecnica.
- [Maturidade tecnica da POC](maturity.md): criterios atuais de documentacao, seguranca, testes, CI, observabilidade e pendencias.

## How-to

- [Autenticacao e autorizacao](development/authentication.md): obter token local, validar scopes, audiences e autorizacao por merchant.
- [Kafka, Outbox e DLQ](development/kafka-outbox.md): validar mensageria, provider Kafka atual, publicacao, consumo, DLQ, requeue e fluxos assincronos.
- [Cobertura de testes](development/test-coverage.md): executar testes com cobertura, interpretar falhas e entender os gates de 85% global e dos workers.
- [SonarQube local](quality/sonarqube.md): subir SonarQube com Docker Compose e executar analise estatica local.
- [Mutation testing com Stryker.NET](development/mutation-testing-stryker.md): executar mutation testing local e interpretar relatorios.
- [OWASP ZAP local](development/owasp-zap.md): executar DAST baseline local contra Ledger e Balance, com Auth.Api legado apenas opcional, salvando relatorios em `zap-reports/`.
- [Git hooks locais](development/git-hooks.md): instalar e entender `commit-msg`, `post-merge` e `pre-push`.
- [Setup local Terraform e GCP](development/terraform-gcp-local-setup.md): instalar Terraform CLI, Google Cloud CLI e TFLint no Windows e executar validacoes locais seguras.
- [Contrato Pub/Sub entre infraestrutura e aplicacao](development/pubsub-infra-app-contract.md): mapear outputs Terraform para options dos workers, IAM minimo e checklist para GCP real.
- [Custo e free tier do Pub/Sub](development/pubsub-cost-and-free-tier.md): estimar throughput, identificar recursos que podem gerar custo e coletar dados para uma estimativa real.
- [Operacao do Pub/Sub](operations/pubsub.md): selecionar provider, subir emulator, aplicar Terraform dev manualmente, configurar workers e diagnosticar falhas comuns.
- [Validacao de pull requests](development/pull-request-validation.md): entender checks obrigatorios, workflows e branch protection.
- [GitHub Pages e LikeC4](development/github-pages.md): gerar e publicar a documentacao arquitetural.
- [Releases e versionamento](development/releases.md): SemVer com GitVersion, commits semanticos, tags e GitHub Releases.
- [Troubleshooting](troubleshooting.md): diagnostico rapido de erros comuns.

## Referencia

- [LedgerService API](development/ledger-api.md): contratos HTTP de escrita, headers, idempotencia, estornos e reprocessamentos.
- [BalanceService API](development/balance-api.md): contratos HTTP de leitura de consolidados diarios e por periodo.
- [Contrato LedgerEntryCreated.v1](contracts/events/LedgerEntryCreated.v1.md): schema, exemplo, semantica, compatibilidade e limitacao atual de moeda.
- [Observabilidade e operacao minima](observability.md): health, readiness, logs, traces, metricas, dashboards, alertas e validacoes operacionais.
- [Padroes do repositorio](development/repository-standards.md): arquivos de padronizacao, tools, estilo, hooks e manutencao.
- [Artifacts dos workflows](development/workflow-artifacts.md): politica de publicacao, conteudo e retencao.
- [Qualidade](quality/README.md): guias de analise estatica e validacoes locais.
- [k6 load tests](../loadtests/k6/README.md): configuracao dos scripts k6 usados pelos runners.
- [Revisao Docker e Compose](reports/docker-compose-performance-review.md): diagnostico de build, cache, volumes, desempenho local e limpeza segura.
- [Revisao de abstracoes](reports/architecture-abstractions-review.md): classificacao de interfaces com implementacao unica e simplificacoes aplicadas.
- [Validacao final do backend](reports/final-backend-validation.md): build, testes, migrations, Compose, fluxo ponta a ponta, k6 e ressalvas operacionais apos as etapas de melhoria.

## Explicacao

- [Documentacao arquitetural](architecture/README.md): modelo LikeC4 e publicacao no GitHub Pages.
- [Boundaries arquiteturais](architecture/boundaries.md): responsabilidades de `Api`, `Application`, `Domain` e `Infrastructure`.
- [Analise arquitetural e decisoes recomendadas](architecture/decisions.md): riscos, simplificacoes e roadmap pragmatico.
- [ADRs](adrs/README.md): historico de decisoes arquiteturais e pontos de melhoria.
- [Mensageria por ports and adapters](adrs/0075-mensageria-ports-adapters-kafka-provider.md): Kafka como provider atual e Pub/Sub apenas como adapter futuro.
- [Pub/Sub como provider alternativo](adrs/0077-pubsub-provider-mensageria.md): plano incremental para adicionar Pub/Sub sem remover Kafka nem esconder diferencas semanticas entre providers.
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
