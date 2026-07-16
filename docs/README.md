# Documentacao do repositorio

Este indice organiza a documentacao por finalidade. O `README.md` da raiz e a porta de entrada; os detalhes tecnicos ficam nesta pasta.

## Tutorial

- [README do projeto](../README.md): problema, solucao, quickstart, comandos principais e links.
- [Desenvolvimento local](development/local-development.md): compose, Kafka como default dos workers principais, Pub/Sub explicito/legado, portas, migrations, execucao no host, VS Code, Testcontainers e load tests.
- [Dev Container opcional](development/devcontainer.md): ambiente VS Code conteinerizado sem substituir o fluxo local do host.
- [Ferramentas auxiliares](development/tooling.md): Node.js LTS, npm, npx, tools .NET, Redocly CLI, LikeC4, Swashbuckle CLI e fluxo local de OpenAPI.
- [Política de wrappers de scripts](development/scripts.md): caminhos atuais e compatibilidade dos wrappers antigos em `scripts/`.
- [Manutencao Docker local](development/docker-maintenance.md): diagnostico de disco, cache BuildKit limitado a 5GB, classificacao de volumes e limpeza segura por retencao.
- [Baseline de Dockerfiles e Docker Compose](development/container-baseline.md): validadores estruturais, build real de imagens no CI, checklists para executaveis e ProjectReference.
- [Validacao dos contratos OpenAPI](development/openapi-contract-validation.md): geracao, lint, drift e diff de breaking changes contra a main.
- [AuditService API](development/audit-api.md): contrato HTTP canonico de auditoria funcional, headers, scopes, idempotencia, filtros e limitacoes da etapa isolada.
- [Validacao local de webhooks Stripe com Stripe CLI](development/stripe-cli-webhooks.md): instalacao/verificacao da CLI, `stripe listen`, `whsec_...`, eventos sinteticos, smoke correlacionado e troubleshooting do PaymentService.
- [Politica de versionamento de contratos de eventos](development/event-contract-versioning.md): evoluir eventos versionados entre Ledger e Balance preservando compatibilidade em Pub/Sub e Kafka.
- [FAQ](faq.md): respostas curtas para as duvidas mais provaveis de leitura tecnica.
- [Maturidade tecnica do projeto](maturity.md): criterios atuais de documentacao, seguranca, testes, CI, observabilidade e pendencias.
- [Roadmap arquitetural consolidado](roadmap.md): proximas frentes por area de maturidade, sem representar compromisso de producao.

## How-to

- [Autenticacao e autorizacao](development/authentication.md): obter token local, validar scopes, audiences e autorizacao por merchant.
- [Mensageria, Outbox e DLQ](development/kafka-outbox.md): validar Kafka default, Pub/Sub explicito/legado, publicacao, consumo, DLQ, requeue e fluxos assincronos.
- [Cobertura de testes](development/test-coverage.md): executar testes com cobertura, interpretar falhas e entender os gates de 85% global, 80% Shared e 85% dos workers.
- [SonarQube Cloud](development/sonarqube-cloud.md): configurar analise via GitHub Actions, token, importacao OpenCover, quality gate e troubleshooting.
- [SonarQube local](quality/sonarqube.md): subir SonarQube com Docker Compose e executar analise estatica local.
- [Mutation testing com Stryker.NET](development/mutation-testing-stryker.md): executar mutation testing local e interpretar relatorios.
- [OWASP ZAP local e pos-CI](development/owasp-zap.md): executar DAST baseline local, manual ou apos sucesso do CI da `main` contra Ledger e Balance, salvando relatorios em artifacts ou `zap-reports/`.
- [Validacao de seguranca com Trivy](development/trivy-security-scan.md): validar Dockerfiles, Terraform, misconfigurations, secrets e filesystem no hook local e no CI.
- [Git hooks locais](development/git-hooks.md): instalar e entender `commit-msg`, `post-merge` e `pre-push`.
- [Setup local Terraform e GCP](development/terraform-gcp-local-setup.md): instalar Terraform CLI, Google Cloud CLI e TFLint no Windows e executar validacoes locais seguras.
- [Cloud SQL PostgreSQL local com Auth Proxy](development/cloudsql-postgres-local-setup.md): conectar aplicacao em debug ou Docker Compose a Cloud SQL sem authorized networks.
- [Checklist manual para primeiro apply Pub/Sub em GCP dev](development/pubsub-gcp-dev-apply-checklist.md): preparar projeto descartavel, revisar plano, autorizar apply manualmente e limpar recursos apos a validacao.
- [Contrato Pub/Sub entre infraestrutura e aplicacao](development/pubsub-infra-app-contract.md): mapear outputs Terraform para options dos workers, IAM minimo e checklist para GCP real.
- [Custo e free tier do Pub/Sub](development/pubsub-cost-and-free-tier.md): estimar throughput, identificar recursos que podem gerar custo e coletar dados para uma estimativa real.
- [Baseline de evolucao produtiva](architecture/production-readiness.md): referencia arquitetural para secrets, identidade de workload, TLS, Pub/Sub real, Cloud SQL, imagens, WAF, observabilidade, operacao e governanca, sem declarar prontidao produtiva.
- [Operacao do Pub/Sub](operations/pubsub.md): selecionar provider, subir emulator, aplicar Terraform dev manualmente, configurar workers e diagnosticar falhas comuns.
- [Operacao do AuditService.Worker](operations/audit-worker.md): retry, DLQ, logs, metricas e validacao isolada do consumer `AuditRecordRequested.v1`.
- [Operacao do PaymentService.Worker](operations/payment-worker.md): polling da Inbox Stripe, claim concorrente, lease, retry persistido, DeadLetter logico, integracao Payment -> Ledger, metricas e troubleshooting.
- [Runbook de recuperacao de eventos](operations/event-recovery-runbook.md): consolidar investigacao de DLQ, retry, replay, descarte, rebuild de projecao e relatorio de divergencia.
- [Replay e DLQ orientados por contrato](operations/event-replay-and-dlq.md): inspecionar DLQ, validar schema por versao, decidir discard, ack, nack ou redrive e preservar idempotencia.
- [Estrategia operacional de DLQ](operations/dlq-strategy.md): classificar falhas, decidir discard, retry ou replay/redrive em Pub/Sub e Kafka, preservar idempotencia e orientar observabilidade.
- [Estrategia operacional de replay seguro](operations/replay-strategy.md): diferenciar retry e replay, definir dry-run, filtros, idempotencia, auditoria e cuidados para Pub/Sub, Kafka, DLQ e Outbox.
- [Runbook DLQ e replay da Saga do TransferService](operations/transfer-saga-kafka.md): diagnosticar DLQ Kafka, Outbox e reprocessamento local da Saga de transferencia.
- [Rebuild de projecao do Balance](operations/projection-rebuild.md): calcular saldo reconstruido em paralelo logico, comparar com a projecao atual e gerar relatorio de divergencia sem alterar dados.
- [Validacao de pull requests](development/pull-request-validation.md): entender checks obrigatorios, workflows e branch protection.
- [GitHub Pages e LikeC4](development/github-pages.md): gerar e publicar a documentacao arquitetural.
- [Releases e versionamento](development/releases.md): SemVer com GitVersion, commits semanticos, tags e GitHub Releases.
- [Troubleshooting](troubleshooting.md): diagnostico rapido de erros comuns.

## Referencia

- [LedgerService API](development/ledger-api.md): contratos HTTP de escrita, headers, idempotencia, estornos e reprocessamentos.
- [BalanceService API](development/balance-api.md): contratos HTTP de leitura de consolidados diarios e por periodo.
- [TransferService API](development/transfer-api.md): contratos HTTP para solicitacao e consulta de sagas de transferencia.
- [PaymentService API](development/payment-api.md): contratos HTTP para criacao de pagamento externo via provider fake/Stripe, consulta local, webhook Stripe assinado, Inbox duravel, idempotencia e materializacao assincrona no Ledger pelo Worker.
- [Validacao local de webhooks Stripe com Stripe CLI](development/stripe-cli-webhooks.md): guia operacional opcional para testar `POST /api/v1/webhooks/stripe` em `http://localhost:5234` sem tornar a Stripe CLI dependencia de build.
- [IdentityService API](development/identity-api.md): contrato HTTP de cadastro de usuarios, `Idempotency-Key`, retries e conflitos.
- [AuditService API](development/audit-api.md): contrato HTTP de criacao e consulta de registros funcionais de auditoria, agnostico ao servico chamador.
- [Spec SDD de idempotencia do IdentityService](specs/identity-idempotency.md): comportamento esperado para `Idempotency-Key` opcional em `POST /api/v1/users`.
- [Spec SDD de consistencia em cancelamentos do IdentityService](specs/identity-cancellation-consistency/requirements.md): requisitos, design e tarefas para compensacao Keycloak quando o cadastro e cancelado entre efeitos.
- [Spec SDD do catalogo de testes arquiteturais](specs/architecture-test-catalog/requirements.md): catalogo declarativo de bounded contexts, regras por camada, providers permitidos e governanca para novos contextos.
- [Spec SDD de Forwarded Headers confiaveis](specs/trusted-forwarded-headers/requirements.md): requisitos, design e tarefas para confiar em `X-Forwarded-*` somente por ambiente e proxy/rede configurados.
- [Spec SDD de CORS configuravel](specs/configurable-cors/requirements.md): requisitos, design, tarefas e relatorio para CORS tipado, fechado por padrao e validado por ambiente.
- [Spec SDD de ordenacao de middlewares e seguranca do Swagger](specs/api-middleware-security-order/requirements.md): ordem canonica das APIs, headers de seguranca para OpenAPI/Swagger UI e CSP restrita por superficie.
- [Spec SDD PaymentService + Stripe - requisitos](specs/payment-stripe/requirements.md): contexto, objetivos, nao objetivos, atores, regras, riscos e criterios para o novo fluxo de pagamentos externos.
- [Spec SDD PaymentService + Stripe - design](specs/payment-stripe/design.md): bounded context, ACL Stripe, Inbox, webhook security, integracao com Ledger, observabilidade, testes e configuracao futura.
- [Spec SDD PaymentService + Stripe - state machine](specs/payment-stripe/state-machine.md): estados internos, transicoes, eventos duplicados, atrasados, fora de ordem e refund futuro.
- [Spec SDD PaymentService + Stripe - fluxos](specs/payment-stripe/integration-flows.md): diagramas Mermaid para criacao, webhook, deduplicacao, Inbox, Ledger, retry, timeout desconhecido e refund futuro.
- [Spec SDD PaymentService + Stripe - tasks](specs/payment-stripe/tasks.md): plano incremental de implementacao futura com escopo, criterios de aceite, testes e documentacao afetada.
- [Spec SDD do AuditRecordRequested.v1](specs/audit/audit-record-requested-v1.md): contrato canonico futuro de auditoria funcional, com schema e exemplos, sem integracao ativa.
- [Spec SDD do consumer AuditService.Worker](specs/audit/audit-service-worker-consumer.md): consumo Kafka de `AuditRecordRequested.v1`, idempotencia por `source_event_id` e validacoes isoladas.
- [Spec SDD de hardening do AuditService.Worker](specs/audit/audit-service-worker-hardening.md): retry, DLQ, idempotencia, metricas e testes isolados do consumer de auditoria.
- [Validacao isolada do AuditService](specs/audit/audit-service-isolated-validation.md): revisao de isolamento, referencias, schema, endpoints, idempotencia, consultas, testes e riscos remanescentes.
- [Estrategia futura de integracao assincrona do AuditService](specs/audit/audit-async-integration-strategy.md): spec curta para futura integracao por Outbox local, Kafka e `AuditService.Worker`, sem implementacao ativa.
- [Contratos logicos de eventos](events/README.md): payloads logicos atuais, produtores, consumidores e mapeamentos Pub/Sub/Kafka dos eventos.
- [JSON Schemas versionados de eventos](../contracts/events/README.md): schemas e exemplos para validar payloads logicos de eventos.
- [Versionamento de contratos de eventos](development/event-contract-versioning.md): politica de compatibilidade, transporte, schemas, testes e depreciacao.
- [Runbook de recuperacao de eventos](operations/event-recovery-runbook.md): guia operacional consolidado para decidir entre retry, replay, redrive, descarte, relatorio de divergencia e rebuild de projecao.
- [Replay e DLQ orientados por contrato](operations/event-replay-and-dlq.md): runbook operacional para Pub/Sub, Kafka, replay, redrive e validacao antes de reprocessar.
- [Estrategia operacional de DLQ](operations/dlq-strategy.md): criterios operacionais para DLQ de aplicacao, DLQ tecnica, idempotencia, contratos e troubleshooting.
- [Estrategia operacional de replay seguro](operations/replay-strategy.md): pre-condicoes, filtros, dry-run, auditoria e decisoes de replay seguro em Pub/Sub e Kafka.
- [Runbook DLQ e replay da Saga do TransferService](operations/transfer-saga-kafka.md): operacao local da DLQ e replay controlado da Outbox/Saga do TransferService com Kafka.
- [Rebuild de projecao do Balance](operations/projection-rebuild.md): relatorio de divergencia para rebuild paralelo logico antes de qualquer correcao ou troca de projecao.
- [Contrato LedgerEntryCreated.v1](events/ledger-entry-created-v1.md): contrato legado sem `currency`, aceito para mensagens antigas.
- [Contrato LedgerEntryCreated.v2](events/ledger-entry-created-v2.md): contrato atual com `currency` obrigatoria.
- [Contrato AuditRecordRequested.v1](events/audit-record-requested-v1.md): solicitacao canonica futura para registro de auditoria funcional.
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
- [Diagnostico de cobertura do Shared](reports/shared-coverage-diagnostic.md): baseline local, matriz por classe/metodo, gaps priorizados e ordem recomendada para evoluir `PocArquitetura.Shared.slnx`.

## Explicacao

- [Documentacao arquitetural](architecture/README.md): modelo LikeC4 e publicacao no GitHub Pages.
- [Catalogo de padroes arquiteturais e de design](architecture/patterns-catalog.md): padroes realmente usados no repositorio, problemas resolvidos, evidencias, status e trade-offs.
- [Arquitetura do AuditService](architecture/audit-service.md): papel do bounded context, schema `audit`, contrato canonico, seguranca, metadata e limites da etapa sem integracao.
- [Arquitetura do PaymentService](architecture/payment-service.md): estrutura, state machine, schema `payment`, ACL Stripe/fake provider, webhook, Inbox, Worker e integracao idempotente com Ledger para credito e estorno, preservando limites sem Balance direto/Kafka/refund parcial.
- [Boundaries arquiteturais](architecture/boundaries.md): responsabilidades de `Api`, `Application`, `Domain` e `Infrastructure`.
- [Analise arquitetural e decisoes recomendadas](architecture/decisions.md): riscos, simplificacoes e roadmap pragmatico.
- [Baseline de evolucao produtiva](architecture/production-readiness.md): requisitos recomendados para evolucao futura fora do laboratorio local, ainda sem implementacao produtiva.
- [Roadmap arquitetural consolidado](roadmap.md): leitura consolidada das frentes feitas, parciais, proximos passos e itens fora de escopo por enquanto.
- [ADRs](adrs/README.md): historico de decisoes arquiteturais e pontos de melhoria.
- [SonarQube Cloud com projeto unico agregado](adrs/0110-sonarqube-cloud-projeto-unico-agregado.md): consolida a analise Sonar no contexto aggregate e mantem Shared com validacoes locais proprias.
- [Organizacao de solutions por contexto](adrs/0100-organizacao-solutions-contexto-agregadora.md): registra a agregadora `PocArquitetura.slnx`, as solutions contextuais e a diferenca entre organizacao de desenvolvimento e topologia runtime.
- [PaymentService como bounded context](adrs/0101-payment-service-bounded-context.md): registra a decisao historica que originou o contexto de pagamentos externos preservando Ledger e Balance.
- [ACL Stripe para PaymentService](adrs/0102-stripe-anti-corruption-layer.md): registra a decisao de integrar Stripe por porta interna sem vazar tipos do SDK.
- [Inbox Pattern para webhooks Stripe](adrs/0103-inbox-pattern-webhooks-stripe.md): registra a decisao de persistir e deduplicar webhooks antes do processamento assincrono.
- [Integracao PaymentService -> LedgerService](adrs/0104-payment-ledger-integration.md): registra a decisao de criar efeito financeiro via contrato HTTP idempotente do Ledger.
- [Ordenacao e deduplicacao de eventos externos de pagamento](adrs/0105-payment-provider-event-ordering-deduplication.md): registra a decisao de state machine monotona para eventos duplicados, atrasados e fora de ordem.
- [Terraform state local e backend remoto](adrs/0079-terraform-state-local-e-backend-remoto.md): registra os riscos do state local, gatilhos e estrategia que antecederam a adocao do backend remoto GCS.
- [Backend remoto GCS para Terraform dev](adrs/0080-backend-remoto-gcs-terraform-dev.md): registra a adocao do backend remoto parcial em GCS, separacao por ambiente e migracao manual de state.
- [Mensageria por ports and adapters](adrs/0075-mensageria-ports-adapters-kafka-provider.md): historico da introducao do boundary quando Kafka ainda era o provider atual.
- [Kafka como default dos workers](adrs/0088-kafka-default-ledger-balance-workers.md): adota Kafka como default para Ledger/Balance e mantem Pub/Sub por selecao explicita.
- [Bounded context de auditoria funcional](adrs/0097-functional-audit-service.md): registra AuditService separado, schema `audit`, contrato HTTP canonico e ausencia de integracao inicial.
- [Estrategia de integracao assincrona do AuditService](adrs/0099-audit-async-integration-strategy.md): registra Outbox transacional local + Kafka como estrategia futura, sem alterar os servicos financeiros nesta etapa.
- [LedgerEntryCreated.v2 com currency explicita](adrs/0084-ledger-entry-created-v2-currency-explicita.md): cria v2 com `currency` obrigatoria e mantem leitura de v1 como legado.
- [Separacao de configuracoes locais sensiveis](adrs/0085-separacao-configuracoes-locais-sensiveis-arquivos-versionados.md): registra a decisao de manter secrets locais fora dos arquivos versionados usando exemplos com placeholders.
- [Saga orquestrada no TransferService com Kafka](adrs/0087-saga-orquestrada-transfer-service-kafka.md): planeja o estudo de transferencias entre merchants com orquestracao central, Outbox transacional, worker assincrono, Kafka explicito, idempotencia por etapa e DLQ de aplicacao.
- [Pub/Sub como provider alternativo](adrs/0077-pubsub-provider-mensageria.md): historico do plano incremental que precedeu a adocao principal.
- [Plano de migracao Auth.Api para Keycloak/OIDC](adrs/0073-plano-migracao-auth-api-keycloak-oidc.md): execucao incremental mantendo validacao JWT offline via JWKS.
- [Keycloak como identidade principal](adrs/0074-keycloak-como-identidade-principal.md): decisao historica que removeu Auth.Api da stack principal e consolidou Keycloak como identidade local.
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
