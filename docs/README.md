# Documentacao do repositorio

Este indice organiza a documentacao por finalidade. O `README.md` da raiz e a porta de entrada; os detalhes tecnicos ficam nesta pasta.

## Comecar

- [README do projeto](../README.md): resumo, comandos principais e links.
- [Desenvolvimento local](development/local-development.md): compose, portas, migrations, execucao no host, VS Code e load tests.
- [LedgerService API](development/ledger-api.md): contratos de escrita, headers, idempotencia, Outbox, solicitacao de estorno e consulta de status.
- [Padroes do repositorio](development/repository-standards.md): arquivos de padronizacao, tools, estilo, hooks e cuidados de manutencao.

## Entender a arquitetura

- [Documentacao arquitetural](architecture/README.md): modelo LikeC4 e publicacao no GitHub Pages.
- [Boundaries arquiteturais](architecture/boundaries.md): responsabilidades de `Api`, `Application`, `Domain` e `Infrastructure`.
- [Analise arquitetural e decisoes recomendadas](architecture/decisions.md): riscos, simplificacoes e roadmap pragmatico.
- [ADRs](adrs/README.md): historico de decisoes arquiteturais e pontos de melhoria.

## Operar e integrar

- [Autenticacao e autorizacao](development/authentication.md): JWT, JWKS, scopes e autorizacao por merchant.
- [Kafka, Outbox e DLQ](development/kafka-outbox.md): topicos, headers, fluxo de publicacao, consumo e validacao.
- [Observabilidade e operacao minima](observability.md): health, readiness, logs, traces, metricas, correlation id e configuracao.

## Desenvolver com seguranca

- [Cobertura de testes](development/test-coverage.md): comandos, gate de 80% e interpretacao de falhas.
- [Mutation testing com Stryker.NET](development/mutation-testing-stryker.md): alvos, execucao local, artifacts e leitura dos relatorios.
- [Validacao de pull requests](development/pull-request-validation.md): checks obrigatorios, workflows e branch protection.
- [Git hooks locais](development/git-hooks.md): `commit-msg`, `post-merge` e `pre-push`.
- [Artifacts dos workflows](development/workflow-artifacts.md): politica de publicacao e retencao.
- [Releases e versionamento](development/releases.md): SemVer com GitVersion, commits semanticos, tags e GitHub Releases.

## Relatorios

- [Avaliacao de .NET Aspire e riscos OWASP](reports/aspire-and-owasp-assessment.md): analise historica de adocao e riscos. Use como relatorio de contexto, nao como estado operacional mais recente.

## Manutencao da documentacao

- Mantenha informacoes detalhadas em `docs`, com resumo e link no `README.md`.
- Evite duplicar comandos longos entre documentos; prefira apontar para a fonte de verdade.
- Atualize ADRs quando houver decisao arquitetural nova ou mudanca relevante de comportamento.
- Atualize este indice quando adicionar, remover ou consolidar documentos.
