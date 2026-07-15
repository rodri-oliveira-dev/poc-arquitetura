# Artifacts dos workflows

Este documento registra quais artifacts os workflows publicam no GitHub Actions, por quanto tempo ficam retidos e por que continuam publicados.

Artifacts podem expor nomes de testes, paths internos do repositorio, stack traces e trechos de codigo. Por isso, a politica atual e publicar apenas o necessario para diagnostico, com retencao explicita e curta.

## main-dotnet-ci

Workflow: `.github/workflows/dotnet.yml`

Artifacts:

- `test-results-coverage-and-sonarqube`

Retencao: 7 dias

Conteudo publicado:

- arquivos `.trx` de resultados de testes;
- arquivos `coverage.cobertura.xml` coletados pelo Coverlet;
- arquivos `coverage.opencover.xml` importados pelo SonarQube Cloud;
- `coverage-report/Summary.json`;
- `coverage-report/Summary.txt`;
- `artifacts/sonarqube/aggregate/quality-gate.json`;
- `artifacts/sonarqube/aggregate/measures.json`;
- `artifacts/sonarqube/aggregate/issues.json`;
- `artifacts/sonarqube/aggregate/sonarqube-cloud-report.md`;
- `artifacts/sonarqube/aggregate/report.md`;
- `artifacts/nuget-vulnerabilities-<contexto>.json`.

Motivo:

- os arquivos `.trx` ajudam a diagnosticar falhas de teste;
- o XML Cobertura e os summaries permitem validar a cobertura consolidada, a cobertura dos assemblies Worker e investigar os gates minimos de 85%;
- o XML OpenCover permite diagnosticar falhas de importacao de cobertura pelo SonarQube Cloud;
- o resumo Markdown e os JSONs do SonarQube Cloud permitem analisar no GitHub Actions um snapshot da execucao do CI sem depender apenas da interface externa durante a triagem;
- os JSONs da auditoria NuGet preservam a evidencia do scan por contexto;
- o relatorio HTML completo do ReportGenerator nao e publicado como artifact, porque os summaries e o XML ja atendem ao diagnostico principal com menor exposicao de paths e trechos renderizados.

O workflow oficial publica apenas o artifact unico acima. Os contextos `aggregate` e `shared` ficam em subpastas do mesmo artifact; nao ha workflow Sonar reutilizavel separado.

Risco residual:

- os JSONs do SonarQube Cloud podem conter paths, mensagens de regras, contagens de achados e detalhes de arquivos analisados;
- o dashboard oficial do SonarQube Cloud continua sendo a fonte principal da analise, enquanto o artifact e apenas um snapshot retido por 7 dias.

## mutation-tests

Workflow: `.github/workflows/mutation-tests.yml`

Artifacts:

- `stryker-ledger-service-application`
- `stryker-balance-service-application`

Retencao: 7 dias

Conteudo publicado:

- `mutation-report.html` gerado pelo Stryker.NET para cada alvo.

Motivo:

- mutation testing e informativo e nao bloqueia merge nem release;
- o HTML e mantido porque e o relatorio primario para analisar mutantes `Survived`, `NoCoverage`, `Timeout` e `CompileError`;
- a publicacao nao inclui a pasta `StrykerOutput/` completa nem o JSON detalhado, reduzindo volume e exposicao desnecessaria.

Risco residual:

- o `mutation-report.html` pode conter paths, nomes de tipos, nomes de testes e trechos de codigo mutado;
- por isso, a retencao e curta e o workflow continua restrito a execucao pos-CI da `main` e execucao manual, sem rodar em todo pull request.

## owasp-zap-baseline

Workflow: `.github/workflows/owasp-zap.yml`

Artifact: `owasp-zap-baseline-reports`

Retencao: 7 dias

Conteudo publicado:

- relatorios HTML gerados pelo ZAP, quando disponiveis;
- relatorios JSON gerados pelo ZAP, quando disponiveis;
- relatorios Markdown ou texto gerados pelo runner;
- `summary.md` com alvos, arquivos e exit code por API.

Motivo:

- o workflow apoia triagem DAST manual ou pos-CI da `main` sem virar gate obrigatorio de PR ou release nesta etapa;
- os relatorios sao a saida primaria do OWASP ZAP para analise de achados;
- a retencao curta reduz exposicao de paths, endpoints, headers e detalhes do ambiente local do runner.

Risco residual:

- os relatorios podem conter inventario de endpoints, paths internos e detalhes de configuracao observados dinamicamente;
- por isso, o workflow nao roda em `pull_request` e os artifacts expiram em 7 dias.

## Regras de manutencao

- Todo uso de `actions/upload-artifact` deve declarar `retention-days`.
- Preserve `if-no-files-found: warn` quando o upload roda com `if: always()`, para evitar falha secundaria falsa quando uma etapa anterior falhar antes de gerar arquivos.
- Antes de publicar um novo artifact, avalie se o GitHub Step Summary, logs do job ou um summary menor ja atendem ao diagnostico.
- Nao publique relatorios HTML ou JSON detalhados por padrao quando summaries forem suficientes.
