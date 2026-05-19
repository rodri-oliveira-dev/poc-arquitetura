# Documentacao do repositorio

Este indice organiza a documentacao por finalidade. O `README.md` da raiz e a porta de entrada; os detalhes tecnicos ficam nesta pasta.

## Tutorial

- [README do projeto](../README.md): problema, solucao, quickstart, comandos principais e links.
- [Desenvolvimento local](development/local-development.md): compose, portas, migrations, execucao no host, VS Code, Testcontainers e load tests.
- [FAQ](faq.md): respostas curtas para as duvidas mais provaveis de leitura tecnica.

## How-to

- [Autenticacao e autorizacao](development/authentication.md): obter token local, validar scopes, audiences e autorizacao por merchant.
- [Kafka, Outbox e DLQ](development/kafka-outbox.md): validar publicacao, consumo, DLQ, requeue e fluxos assincronos.
- [Cobertura de testes](development/test-coverage.md): executar testes com cobertura, interpretar falhas e entender o gate de 80%.
- [Mutation testing com Stryker.NET](development/mutation-testing-stryker.md): executar mutation testing local e interpretar relatorios.
- [Git hooks locais](development/git-hooks.md): instalar e entender `commit-msg`, `post-merge` e `pre-push`.
- [Validacao de pull requests](development/pull-request-validation.md): entender checks obrigatorios, workflows e branch protection.
- [GitHub Pages e LikeC4](development/github-pages.md): gerar e publicar a documentacao arquitetural.
- [Releases e versionamento](development/releases.md): SemVer com GitVersion, commits semanticos, tags e GitHub Releases.
- [Troubleshooting](troubleshooting.md): diagnostico rapido de erros comuns.

## Referencia

- [LedgerService API](development/ledger-api.md): contratos HTTP de escrita, headers, idempotencia, estornos e reprocessamentos.
- [Observabilidade e operacao minima](observability.md): health, readiness, logs, traces, metricas, dashboards, alertas e validacoes operacionais.
- [Padroes do repositorio](development/repository-standards.md): arquivos de padronizacao, tools, estilo, hooks e manutencao.
- [Artifacts dos workflows](development/workflow-artifacts.md): politica de publicacao, conteudo e retencao.
- [k6 load tests](../loadtests/k6/README.md): configuracao dos scripts k6 usados pelos runners.

## Explicacao

- [Documentacao arquitetural](architecture/README.md): modelo LikeC4 e publicacao no GitHub Pages.
- [Boundaries arquiteturais](architecture/boundaries.md): responsabilidades de `Api`, `Application`, `Domain` e `Infrastructure`.
- [Analise arquitetural e decisoes recomendadas](architecture/decisions.md): riscos, simplificacoes e roadmap pragmatico.
- [ADRs](adrs/README.md): historico de decisoes arquiteturais e pontos de melhoria.
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
