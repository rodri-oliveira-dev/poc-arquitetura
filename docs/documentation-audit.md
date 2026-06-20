# Auditoria de documentacao

Data: 2026-05-19

## Escopo revisado

A auditoria considerou a documentacao Markdown autoral do repositorio:

- `README.md`
- `AGENTS.md`
- `docs/README.md`
- `docs/faq.md`
- `docs/troubleshooting.md`
- `docs/observability.md`
- `docs/architecture/*.md`
- `docs/development/*.md`
- `docs/adrs/*.md`
- `docs/reports/*.md`
- `loadtests/k6/README.md`
- `.agents/skills/*/SKILL.md`

Arquivos Markdown dentro de `.dotnet/`, `.dotnet-home/`, `.nuget/`, `TestResults/`, `artifacts/` e `zap-reports/` foram classificados como dependencias restauradas ou artefatos gerados. Eles nao foram editados porque nao representam documentacao autoral versionada do projeto.

## Papel dos documentos

| Documento | Papel |
| --- | --- |
| `README.md` | README principal, porta de entrada tecnica, quickstart e navegacao. |
| `docs/README.md` | Indice geral por finalidade: tutorial, how-to, referencia e explicacao. |
| `docs/faq.md` | FAQ curta para orientar leitura tecnica e apontar fontes de verdade. |
| `docs/troubleshooting.md` | Diagnostico rapido de erros comuns e links para guias detalhados. |
| `docs/observability.md` | Referencia operacional de health, readiness, logs, traces, metricas, dashboards, alertas e validacoes. |
| `docs/architecture/README.md` | Guia de arquitetura e modelo LikeC4. |
| `docs/architecture/boundaries.md` | Explicacao de boundaries entre camadas e servicos. |
| `docs/architecture/decisions.md` | Analise arquitetural, riscos, simplificacoes e roadmap. |
| `docs/development/local-development.md` | How-to de execucao local, compose, migrations, Testcontainers, VS Code e load tests. |
| `docs/development/ledger-api.md` | Referencia de contratos HTTP do LedgerService. |
| `docs/development/authentication.md` | Referencia/how-to de JWT, JWKS, scopes e merchant authorization. |
| `docs/development/kafka-outbox.md` | Referencia/how-to de mensageria, Outbox, DLQ, requeue e validacao. |
| `docs/development/test-coverage.md` | How-to de testes com cobertura e gate minimo. |
| `docs/development/mutation-testing-stryker.md` | How-to de mutation testing local e informativo no CI. |
| `docs/development/pull-request-validation.md` | Referencia de validacao de PRs e branch protection. |
| `docs/development/git-hooks.md` | How-to de hooks locais. |
| `docs/development/github-pages.md` | How-to de build/publicacao LikeC4. |
| `docs/development/repository-standards.md` | Referencia de padroes do repositorio. |
| `docs/development/releases.md` | Referencia/how-to de versionamento e releases. |
| `docs/development/workflow-artifacts.md` | Referencia de artifacts de workflows. |
| `docs/adrs/README.md` | Indice de ADRs e estados de decisao. |
| `docs/adrs/NNNN-*.md` | ADRs historicas, aceitas, propostas ou substituidas. |
| `docs/reports/aspire-and-owasp-assessment.md` | Relatorio historico de avaliacao. |
| `loadtests/k6/README.md` | Referencia curta dos scripts k6. |
| `AGENTS.md` | Instrucao global para Codex trabalhar no repositorio. |
| `.agents/skills/*/SKILL.md` | Fluxos especializados de Codex. |

## Problemas encontrados

- O README anterior misturava visao geral, comandos, documentacao, qualidade, load tests, decisoes e troubleshooting rapido, mas ainda deixava implicito o problema tecnico resolvido.
- Nao havia FAQ dedicada para responder rapidamente duvidas de leitura avaliativa.
- Nao havia troubleshooting central; diagnosticos estavam espalhados entre README, desenvolvimento local, observabilidade e Kafka/Outbox.
- `docs/README.md` listava bons documentos, mas nao separava claramente tutorial, how-to, referencia e explicacao.
- `AGENTS.md` estava longo para orientacao recorrente do Codex e duplicava explicacoes que ja existem em `docs/`.
- `docs/observability.md` esta grande para leitura confortavel. Ele contem conteudo relevante, mas mistura referencia, explicacao e roteiros longos de validacao operacional.
- Alguns documentos grandes, como `docs/development/local-development.md`, `docs/development/ledger-api.md`, `docs/development/kafka-outbox.md` e `docs/development/mutation-testing-stryker.md`, sao aceitaveis como guias especializados, mas exigem navegacao clara a partir do indice.
- ADRs historicas preservam decisoes e contexto; nao devem ser reescritas como documentacao operacional corrente.

## Alteracoes feitas

- Reescrito `README.md` como porta de entrada tecnica com problema, solucao, arquitetura, pre-requisitos, quickstart, comandos principais, testes, documentacao e FAQ curta.
- Reorganizado `docs/README.md` por finalidade: Tutorial, How-to, Referencia, Explicacao e Agentes.
- Criado `docs/faq.md` com respostas objetivas e links para documentos fonte.
- Criado `docs/troubleshooting.md` com diagnosticos rapidos e links para guias detalhados.
- Reduzido `AGENTS.md` para regras recorrentes, fontes de verdade, boundaries, comandos baseline, skills e politica de commit.
- Adicionada secao de navegacao em `docs/observability.md` para reduzir custo de leitura sem mover conteudo operacional sensivel.
- Criado este relatorio em `docs/documentation-audit.md`.

## Documentos quebrados em partes menores

- O README foi reduzido conceitualmente: conteudo operacional detalhado passou a ser apontado para `docs/development/local-development.md`, `docs/development/test-coverage.md`, `docs/troubleshooting.md` e `docs/faq.md`.
- Troubleshooting saiu do README e passou a ter documento proprio em `docs/troubleshooting.md`.
- FAQ saiu de uma secao curta no README para `docs/faq.md`, mantendo no README apenas perguntas essenciais.
- `AGENTS.md` deixou de concentrar detalhes longos e passou a apontar para `docs/` e `.agents/skills/`.

## Links corrigidos ou adicionados

- Adicionados links do README para `docs/faq.md`, `docs/troubleshooting.md` e `AGENTS.md`.
- Adicionados links de `docs/README.md` para FAQ, troubleshooting, k6, AGENTS e skills.
- Adicionados links cruzados em `docs/faq.md` e `docs/troubleshooting.md` para guias existentes.
- Adicionada navegacao interna em `docs/observability.md`.

## Duplicidades removidas ou reduzidas

- O README deixou de duplicar explicacoes extensas de load tests, decisoes arquiteturais e troubleshooting.
- A FAQ aponta para documentos fonte em vez de reproduzir guias completos.
- O troubleshooting centraliza sintomas e encaminha para documentos especializados.
- `AGENTS.md` nao repete fluxos longos ja documentados em desenvolvimento local, padroes do repositorio, arquitetura e ADRs.

## Pendencias que exigem validacao humana

- Avaliar se `docs/observability.md` deve ser dividido em documentos menores, por exemplo `docs/observability.md`, `docs/operations/validation.md` e `docs/operations/metrics.md`. A auditoria evitou essa quebra para nao arriscar links internos existentes.
- Confirmar se a documentacao deve adotar acentuacao plena em portugues ou manter ASCII predominante. A revisao preservou o estilo ASCII ja presente em boa parte do repositorio.
- Validar se os documentos gerados em `zap-reports/` devem permanecer no repositorio ou ser tratados apenas como artefatos externos.
- Avaliar se os ADRs propostos antigos ainda representam backlog desejado ou se alguns devem ser substituidos por decisoes mais recentes.

## Recomendacoes futuras

- Criar uma validacao automatica simples de links relativos Markdown, caso a documentacao continue crescendo.
- Considerar markdownlint ou ferramenta equivalente se houver padrao editorial desejado, sem bloquear alteracoes documentais pequenas por regras cosmeticas.
- Dividir `docs/observability.md` quando houver nova mudanca operacional relevante, preservando anchors ou adicionando redirects manuais por links.
- Manter o README com limite de porta de entrada: problema, solucao, quickstart, comandos principais e links.
- Atualizar `docs/documentation-audit.md` em revisoes editoriais amplas futuras.

## Revisao documental de maturidade tecnica

Data: 2026-05-25

### Arquivos alterados

- `README.md`
- `docs/README.md`
- `docs/maturity.md`
- `docs/development/authentication.md`
- `docs/development/pull-request-validation.md`
- `docs/reports/aspire-and-owasp-assessment.md`
- `loadtests/k6/README.md`
- `docs/documentation-audit.md`

### Motivo da revisao

Consolidar a maturidade documental da POC, alinhando autenticacao/autorizacao, status atual dos achados OWASP, testes de carga k6, validacao de pull requests, criterios de maturidade tecnica e trilha de auditoria ao estado atual dos arquivos versionados.

### O que foi alinhado

- Tabela de scopes por endpoint, incluindo lancamentos, estornos, reprocessamentos, Outbox/DLQ e consolidados.
- Regra de autorizacao por `merchant_id` para endpoints que recebem `merchantId` e para endpoints que inferem o merchant a partir de recurso persistido.
- Status atual dos achados OWASP, preservando o relatorio historico e separando achados mitigados, parcialmente mitigados, ainda validos e historicos.
- Documentacao dos modos k6 `smoke`, `balance50` e `resilience`, com criterios de aceite locais e limites da interpretacao dos resultados.
- Comportamento real do workflow `pr-build-and-test`, incluindo skip interno de restore/build/test em PRs apenas documentais.
- Pagina `docs/maturity.md` com criterios objetivos, evidencias e pendencias.
- Links de navegacao no README principal e no indice de documentacao.

### O que continua pendente

- Executar e registrar DAST/OWASP ZAP ou pentest quando houver necessidade de validacao dinamica.
- Remover definitivamente o `Auth.Api` legado quando nao houver mais necessidade de compatibilidade; o fluxo operacional local ja usa Keycloak, incluindo `ledger.read`.
- Definir baseline produtivo para secrets, TLS interno, Kafka autenticado, bancos, scans de imagem e rate limits por identidade antes de qualquer promocao fora da POC local.
- Definir thresholds formais de latencia p95/p99 para k6 somente apos linha de base local reprodutivel.

Build, testes, k6 e scanners de seguranca nao foram executados nesta revisao, por se tratar de alteracao exclusivamente documental.

## Revisao documental OWASP ZAP local

Data: 2026-05-25

### Scripts criados

- `scripts/security/run-owasp-zap.ps1`
- `scripts/security/run-owasp-zap.sh`

### Documentacao alterada

- `README.md`
- `docs/README.md`
- `docs/development/local-development.md`
- `docs/development/owasp-zap.md`
- `docs/reports/aspire-and-owasp-assessment.md`
- `docs/maturity.md`
- `docs/troubleshooting.md`
- `docs/documentation-audit.md`

### Observacoes

- A execucao local do ZAP salva relatorios em `zap-reports/<timestamp>/`, usando timestamp `yyyyMMdd-HHmmss`.
- `zap-reports/` permanece tratado como artefato gerado e nao deve ser versionado.
- A maturidade de OWASP/ZAP foi atualizada para execucao local versionada e documentada, ainda sem gate automatizado em workflow.
