# Documentacao do repositorio

Este e o mapa da documentacao. O [README da raiz](../README.md) apresenta a POC; esta pagina ajuda a escolher o proximo documento sem precisar abrir tudo.

## Como Escolher A Leitura

| Se voce quer... | Comece por |
| --- | --- |
| Entender a proposta em poucos minutos | [README](../README.md), [FAQ](faq.md) e [maturidade](maturity.md) |
| Aprender os conceitos progressivamente | [Boundaries](architecture/boundaries.md), [catalogo de padroes](architecture/patterns-catalog.md) e [mensageria, Outbox e DLQ](development/kafka-outbox.md) |
| Executar ou alterar o projeto | [Desenvolvimento local](development/local-development.md), [autenticacao](development/authentication.md) e os guias de API |
| Avaliar decisoes e trade-offs | [Arquitetura](architecture/README.md), [ADRs](adrs/README.md), [production readiness](architecture/production-readiness.md) e [roadmap](roadmap.md) |
| Diagnosticar falhas operacionais | [Observabilidade](observability.md), [runbook de recuperacao](operations/event-recovery-runbook.md), [DLQ](operations/dlq-strategy.md) e [replay](operations/replay-strategy.md) |
| Ver contratos | [Eventos](events/README.md), [schemas versionados](../contracts/events/README.md) e [OpenAPI](openapi) |
| Entender o historico de uma mudanca | [ADRs](adrs/README.md), [specs SDD](specs) e [relatorios](reports) |

## Jornadas Recomendadas

### Jornada rapida, 10 a 15 minutos

1. [README](../README.md)
2. [FAQ](faq.md)
3. [Maturidade tecnica](maturity.md)
4. [Arquitetura](architecture/README.md)

### Jornada de iniciante

1. [README](../README.md)
2. [Boundaries arquiteturais](architecture/boundaries.md)
3. [Catalogo de padroes](architecture/patterns-catalog.md)
4. [Mensageria, Outbox e DLQ](development/kafka-outbox.md)
5. [Eventos](events/README.md)
6. [Observabilidade](observability.md)

### Jornada de desenvolvedor

1. [Desenvolvimento local](development/local-development.md)
2. [Ferramentas auxiliares](development/tooling.md)
3. [Autenticacao e autorizacao](development/authentication.md)
4. Guias de API: [Ledger](development/ledger-api.md), [Balance](development/balance-api.md), [Transfer](development/transfer-api.md), [Payment](development/payment-api.md), [Identity](development/identity-api.md), [Audit](development/audit-api.md)
5. [Testes e cobertura](development/test-coverage.md)
6. [Validacao OpenAPI](development/openapi-contract-validation.md)

### Jornada arquitetural

1. [Arquitetura visual e LikeC4](architecture/README.md)
2. No LikeC4: `systemLandscape` -> container view do bounded context ->
   component view do container -> dynamic view do fluxo ->
   `localCoreDeployment` -> overlay especifico quando necessario.
3. Para entender referencias de projeto, use as views `*CodeDependencies`
   somente depois das views de runtime.
4. [Boundaries](architecture/boundaries.md)
5. [Catalogo de padroes](architecture/patterns-catalog.md)
6. [ADRs](adrs/README.md)
7. [Baseline de evolucao produtiva](architecture/production-readiness.md)
8. [Roadmap](roadmap.md)

### Jornada operacional

1. [Observabilidade e operacao minima](observability.md)
2. [Runbook de recuperacao de eventos](operations/event-recovery-runbook.md)
3. [Estrategia de DLQ](operations/dlq-strategy.md)
4. [Estrategia de replay seguro](operations/replay-strategy.md)
5. [Operacao do PaymentService.Worker](operations/payment-worker.md)
6. [Operacao do AuditService.Worker](operations/audit-worker.md)
7. [Troubleshooting](troubleshooting.md)

## Taxonomia

### Tutoriais

Documentos que ensinam uma atividade do inicio ao fim.

- [README](../README.md)
- [Desenvolvimento local](development/local-development.md)
- [Dev Container opcional](development/devcontainer.md)
- [Ferramentas auxiliares](development/tooling.md)
- [Validacao local de webhooks Stripe com Stripe CLI](development/stripe-cli-webhooks.md)
- [k6 load tests](../loadtests/k6/README.md)

### How-to

Documentos para resolver uma tarefa especifica.

- [Autenticacao e autorizacao](development/authentication.md)
- [Cobertura de testes](development/test-coverage.md)
- [Git hooks locais](development/git-hooks.md)
- [Validacao de pull requests](development/pull-request-validation.md)
- [Releases e versionamento](development/releases.md)
- [Validacao OpenAPI](development/openapi-contract-validation.md)
- [OWASP ZAP](development/owasp-zap.md)
- [Trivy](development/trivy-security-scan.md)
- [SonarQube Cloud](development/sonarqube-cloud.md)
- [SonarQube local](quality/sonarqube.md)
- [Mutation testing](development/mutation-testing-stryker.md)
- [Manutencao Docker local](development/docker-maintenance.md)
- [Terraform e GCP local](development/terraform-gcp-local-setup.md)
- [Cloud SQL local com Auth Proxy](development/cloudsql-postgres-local-setup.md)
- [Pub/Sub emulator e GCP dev](operations/pubsub.md)

### Explicacoes conceituais

Documentos que explicam por que algo existe, quais problemas resolve e quais trade-offs foram aceitos.

- [Arquitetura](architecture/README.md)
- [Boundaries arquiteturais](architecture/boundaries.md)
- [Catalogo de padroes](architecture/patterns-catalog.md)
- [Arquitetura do PaymentService](architecture/payment-service.md)
- [Arquitetura do AuditService](architecture/audit-service.md)
- [Decisoes recomendadas](architecture/decisions.md)
- [Baseline de evolucao produtiva](architecture/production-readiness.md)
- [Maturidade tecnica](maturity.md)
- [Roadmap](roadmap.md)

### Referencias

Documentos com contratos, comandos, opcoes e detalhes exatos.

- APIs: [Ledger](development/ledger-api.md), [Balance](development/balance-api.md), [Transfer](development/transfer-api.md), [Payment](development/payment-api.md), [Identity](development/identity-api.md), [Audit](development/audit-api.md)
- [Contratos logicos de eventos](events/README.md)
- [Schemas JSON versionados](../contracts/events/README.md)
- [OpenAPI versionado](openapi)
- [Versionamento de eventos](development/event-contract-versioning.md)
- [Scripts do repositorio](development/scripts.md)
- [Padroes do repositorio](development/repository-standards.md)
- [Qualidade](quality/README.md)
- [Artifacts de workflows](development/workflow-artifacts.md)
- [Modulos Terraform](../infra/terraform/modules/cloudsql-postgres/README.md)

### ADRs

ADRs preservam decisoes historicas. Elas nao devem ser lidas como manual operacional atual sem conferir documentos mais recentes.

- [Indice de ADRs](adrs/README.md)

### Especificacoes SDD

Specs registram requisitos, design, tarefas e relatorio de uma mudanca. Elas explicam o processo historico e nao substituem a documentacao principal.

- [Specs SDD](specs)
- [Revisao C4/LikeC4 da arquitetura](specs/c4-likec4-architecture-review/report.md)
- [Conclusao semantica C4/LikeC4](specs/c4-likec4-semantic-completion/report.md)
- [Revisao da experiencia de documentacao](specs/documentation-experience-review/requirements.md)

### Runbooks

Runbooks devem partir de sintomas e orientar diagnostico, decisao, execucao e validacao.

- [Runbook de recuperacao de eventos](operations/event-recovery-runbook.md)
- [Replay e DLQ orientados por contrato](operations/event-replay-and-dlq.md)
- [Estrategia de DLQ](operations/dlq-strategy.md)
- [Estrategia de replay seguro](operations/replay-strategy.md)
- [Rebuild de projecao do Balance](operations/projection-rebuild.md)
- [Saga do TransferService](operations/transfer-saga-kafka.md)
- [PaymentService.Worker](operations/payment-worker.md)
- [AuditService.Worker](operations/audit-worker.md)
- [Troubleshooting](troubleshooting.md)

## Guia Por Area

### Arquitetura

- [Arquitetura visual e LikeC4](architecture/README.md)
- [Boundaries](architecture/boundaries.md)
- [Catalogo de padroes](architecture/patterns-catalog.md)
- [PaymentService](architecture/payment-service.md)
- [AuditService](architecture/audit-service.md)
- [Production readiness](architecture/production-readiness.md)

### Desenvolvimento

- [Desenvolvimento local](development/local-development.md)
- [Autenticacao](development/authentication.md)
- [Mensageria, Outbox e DLQ](development/kafka-outbox.md)
- [Shared por NuGet e modo integrado](specs/local-shared-project-mode/requirements.md)
- [Container baseline](development/container-baseline.md)

### Operacao

- [Observabilidade](observability.md)
- [Event recovery](operations/event-recovery-runbook.md)
- [DLQ](operations/dlq-strategy.md)
- [Replay](operations/replay-strategy.md)
- [Pub/Sub](operations/pubsub.md)

### Seguranca e qualidade

- [SECURITY](../SECURITY.md)
- [OWASP ZAP](development/owasp-zap.md)
- [Trivy](development/trivy-security-scan.md)
- [CodeQL workflow](../.github/workflows/codeql.yml)
- [SonarQube Cloud](development/sonarqube-cloud.md)
- [Cobertura](development/test-coverage.md)

## Manutencao

- Mantenha o README da raiz curto e orientado a decisao de leitura.
- Evite copiar comandos longos em muitos documentos; use links para a fonte de verdade.
- Atualize `docs/README.md` quando adicionar, remover, consolidar ou renomear documentos.
- Preserve ADRs e specs como historico. Quando uma decisao mudar, registre a evolucao em ADR nova ou no indice, sem apagar o contexto original.
- Ao alterar contratos HTTP, gere novamente `docs/openapi` pelos scripts versionados.
