# Maturidade tecnica da POC

Status documental em 2026-05-25. A tabela resume o estado atual da POC com base nos arquivos versionados e em leitura estatica do codigo. Esta pagina nao substitui build, testes, k6, DAST, pentest ou scanners recentes.

| Criterio | Status | Evidencia | Observacao |
| --- | --- | --- | --- |
| Objetivo e escopo da POC documentados | Atendido | `README.md`, `docs/README.md`, ADRs iniciais | Escopo local e limites de POC estao descritos. |
| Contratos HTTP documentados | Atendido | `docs/development/ledger-api.md`, `docs/development/balance-api.md`, `docs/development/authentication.md` | Inclui lancamentos, estornos, reprocessamentos, consolidados, scopes e merchant authorization. |
| Swagger/OpenAPI alinhado ao codigo | Parcialmente atendido | Controllers com annotations Swagger e filtros de autorizacao | Alinhamento e feito por codigo, mas nao houve geracao ou validacao automatica de OpenAPI nesta revisao. |
| LikeC4 representando implementacao real | Atendido | `docs/architecture/model.c4`, `deployment.c4`, `views.c4` | Modelo cobre APIs, workers, bancos, Kafka, observabilidade e Nginx local. |
| Testes automatizados documentados | Atendido | `docs/development/test-coverage.md`, `README.md` | Testes unitarios e de integracao estao documentados com fluxo local. |
| Cobertura minima documentada | Atendido | `coverlet.runsettings`, `docs/development/test-coverage.md`, badges do README | Gate documentado de 85% global e workers. |
| CI com build/test/cobertura | Atendido | `.github/workflows/dotnet.yml`, `docs/development/pull-request-validation.md` | PR usa gate minimo; workflow pos-merge/manual documenta cobertura e relatorios. |
| Seguranca estatica documentada | Parcialmente atendido | `.github/workflows/codeql.yml`, `docs/development/pull-request-validation.md`, relatorio OWASP | CodeQL esta documentado, mas esta pagina nao afirma resultado recente de SAST. |
| Dependency review / vulnerabilidades NuGet | Parcialmente atendido | `.github/workflows/dependency-review.yml`, `Directory.Packages.props`, ADR de pruning NuGet | Dependency review existe; ausencia de CVEs exige execucao atual de scanner. |
| OWASP/ZAP ou DAST | Parcialmente atendido | `scripts/run-owasp-zap.ps1`, `scripts/run-owasp-zap.sh`, `docs/development/owasp-zap.md`, `docs/reports/aspire-and-owasp-assessment.md` | Ha execucao local versionada e documentada contra Auth, Ledger e Balance, mas ainda nao ha gate automatizado em workflow nem resultado recente registrado nesta pagina. |
| Testes de carga documentados | Atendido | `loadtests/k6/README.md`, `docs/development/local-development.md` | Cenarios smoke, balance50 e resilience estao documentados como validacao local/controlada. |
| Observabilidade documentada | Atendido | `docs/observability.md`, LikeC4, dashboards versionados | Stack local cobre OTLP, Jaeger, Prometheus, Loki, Alloy, Alertmanager e Grafana. |
| Execucao local documentada | Atendido | `docs/development/local-development.md`, `README.md` | Inclui compose, migrations, portas, Nginx opcional, Testcontainers e k6. |
| Troubleshooting documentado | Atendido | `docs/troubleshooting.md`, secoes em `local-development.md` | Cobre erros recorrentes de banco, Docker-compatible API, Swagger, Kafka/Outbox e observabilidade local. |
| Limitacoes conhecidas documentadas | Parcialmente atendido | `docs/reports/aspire-and-owasp-assessment.md`, `docs/architecture/decisions.md`, ADRs propostas | Limitacoes principais estao registradas, mas devem ser revisitadas quando a POC mudar de escopo. |
| Decisoes arquiteturais registradas | Atendido | `docs/adrs/README.md`, `docs/adrs/*.md` | ADRs cobrem arquitetura, seguranca, CI, observabilidade, workers, Nginx e fluxos assincronos. |

## Pendencias principais

- Executar e registrar DAST/OWASP ZAP ou pentest quando houver necessidade de avaliar exposicao dinamica alem do script local versionado.
- Definir baseline produtivo fora da POC local para secrets, TLS interno, Kafka autenticado, bancos, scans de imagem, WAF e rate limits por identidade.
- Resolver o desalinhamento operacional entre endpoints `ledger.read` e o catalogo local de scopes emitidos pelo `Auth.Api`, caso as consultas de status precisem ser exercitadas com token local real.
- Formalizar thresholds de latencia p95/p99 para k6 somente depois de obter linha de base local reprodutivel.
