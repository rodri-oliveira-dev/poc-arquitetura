# SonarQube Cloud

## Objetivo

O projeto usa SonarQube Cloud para complementar as validacoes locais e do GitHub Actions com analise estatica, Quality Gate, cobertura de testes, bugs, code smells, vulnerabilidades e acompanhamento historico de qualidade.

Essa integracao nao substitui build, testes automatizados, gate local de cobertura, revisao de codigo ou validacoes de seguranca do repositorio. Ela consolida sinais de qualidade em um servico externo.

## Modelo oficial atual

O modelo oficial operacional e analise consolidada via GitHub Actions, usando o workflow `.github/workflows/dotnet.yml`.

Automatic Analysis deve ficar desabilitada no SonarQube Cloud. Automatic Analysis e CI Analysis nao devem ficar ativas ao mesmo tempo para o mesmo projeto, porque podem gerar analises duplicadas, resultados inconsistentes e conflitos de configuracao.

A analise via CI e a abordagem correta neste repositorio porque a cobertura .NET precisa ser gerada durante `dotnet test` e importada explicitamente pelo scanner.

Configuracao oficial:

| Item | Valor |
| --- | --- |
| Organization Key | `rodri-oliveira-dev` |
| Project Key | `rodri-oliveira-dev_poc-arquitetura` |
| Project Name | `poc-arquitetura` |
| Solution | `./PocArquitetura.slnx` |
| Test results | `./artifacts/test-results` |
| Sonar report | `./artifacts/sonarqube` |
| OpenCover | `./artifacts/test-results/**/coverage.opencover.xml` |
| Artifact | `test-results-coverage-and-sonarqube` |

O projeto global `rodri-oliveira-dev_poc-arquitetura` e o unico projeto SonarQube Cloud oficial ativo neste momento. Nao crie projetos remotos, nao altere secrets, nao altere Quality Gates e nao habilite projetos contextuais sem decisao explicita.

## Fluxo do CI

O workflow `main-dotnet-ci` roda em:

- `pull_request` para `main`;
- `push` para `main`;
- `workflow_dispatch`.

O fluxo executado e sempre consolidado:

```text
GitHub Event
  -> main-dotnet-ci
  -> restore ./PocArquitetura.slnx
  -> SonarQube Cloud begin
  -> build ./PocArquitetura.slnx
  -> test ./PocArquitetura.slnx + coverage
  -> coverage.cobertura.xml e coverage.opencover.xml
  -> SonarQube Cloud end
  -> Quality Gate consolidado
  -> consulta API SonarQube Cloud
  -> relatorio consolidado
  -> ReportGenerator
  -> gate local de cobertura
  -> artifact consolidado
```

O `begin` do SonarQube Cloud precisa ocorrer antes do build. O `end` precisa ocorrer depois dos testes com cobertura para que o scanner consiga enviar a analise e importar o relatorio OpenCover.

O workflow limpa `./artifacts/test-results` e `./artifacts/sonarqube` antes da execucao consolidada. Isso evita reaproveitar arquivos de cobertura ou relatorios de execucoes anteriores, inclusive de experimentos contextuais locais.

## Infraestrutura contextual preservada e inativa

Existe infraestrutura versionada para uma possivel evolucao futura de SonarQube por contexto:

- `.github/workflows/sonarqube-context.yml`;
- `scripts/quality/sonar-contexts.json`;
- `scripts/quality/sonar_context.py`;
- `scripts/quality/sonar_context_impact.py`;
- `scripts/quality/sonarqube_context_summary.py`;
- suporte parametrizado em `scripts/quality/sonarqube_cloud_report.py`;
- suporte opcional em `scripts/quality/sonar-analyze.sh <contexto>`.

Essa infraestrutura permanece no repositorio porque e reutilizavel, mas esta inativa operacionalmente: o workflow oficial nao chama a matrix contextual, nao publica artifacts `sonar-*`, nao espera Quality Gates contextuais e nao executa projetos Sonar por Ledger, Balance, Transfer, Identity, Audit ou Shared.

Para retomar esse modelo no futuro, sera necessaria uma decisao explicita e validacao remota previa dos projetos, tokens, Quality Gates, New Code Definition, custos de runner, artifacts e branch protection. A ausencia de uma variavel ou configuracao remota nao deve ativar execucao contextual.

## Cobertura de testes

O arquivo `coverlet.runsettings` gera dois formatos:

- `coverage.cobertura.xml`, usado pelo ReportGenerator, pelo resumo de cobertura e pelo gate local;
- `coverage.opencover.xml`, importado pelo SonarQube Cloud.

O parametro usado pelo scanner e:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/**/coverage.opencover.xml"
```

Nao use cobertura generica do Sonar para este caso. Para C#/.NET, a importacao deve usar `sonar.cs.opencover.reportsPaths` apontando para os arquivos OpenCover gerados pelo Coverlet.

O scanner exclui da metrica de cobertura do SonarQube Cloud os diretorios `.github/`, `docs/`, `infra/` e `loadtests/`, alem de `Program.cs`, migrations EF e arquivos gerados. Esses arquivos continuam analisados por regras de qualidade e seguranca quando suportado pelo Sonar, mas nao entram no denominador de cobertura porque a cobertura oficial do repositorio vem dos testes .NET via OpenCover.

Arquivos nao C# dentro de `scripts/` ficam fora da analise por `sonar.exclusions`, com a lista explicita baseada no inventario atual: `scripts/**/*.sh`, `scripts/**/*.ps1`, `scripts/**/*.py`, `scripts/**/*.json` e `scripts/**/*.mjs`. Nao use `scripts/**`: arquivos C# futuros em `scripts/` devem continuar elegiveis para analise e cobertura.

Nao use essa exclusao para esconder codigo produtivo .NET sem testes. Se um arquivo C# de `src/` precisar sair da cobertura, registre uma justificativa localizada e revise se o `coverlet.runsettings` tambem precisa ser ajustado.

## Quality Gate

O SonarQube Cloud aplica seu proprio Quality Gate com base nas regras configuradas no projeto global.

O workflow tambem possui um gate local de cobertura, hoje com minimo de 85% para cobertura total de linhas e para os assemblies `LedgerService.Worker` e `BalanceService.Worker`.

Esses gates tem responsabilidades diferentes:

- o gate local verifica cobertura a partir do relatorio Cobertura consolidado pelo ReportGenerator;
- o Quality Gate do Sonar avalia a analise enviada ao SonarQube Cloud, incluindo cobertura importada, bugs, code smells, vulnerabilidades e regras configuradas no servico.

O parametro `sonar.qualitygate.wait=true` permanece ativo para transformar a decisao do Quality Gate remoto em feedback do workflow.

Nao ajuste thresholds remotamente como parte de manutencao de YAML. Divergencias de Quality Gate, New Code Definition ou regras devem ser registradas e corrigidas no SonarQube Cloud como uma decisao operacional explicita.

## Relatorio no GitHub Actions

Apos o step `SonarQube Cloud end`, o workflow executa `Generate SonarQube Cloud report`.

Esse step chama `scripts/quality/sonarqube_cloud_report.py`, consulta a API do SonarQube Cloud com `secrets.SONAR_TOKEN`, sem imprimir o token em logs, e grava um snapshot da execucao em:

```text
artifacts/sonarqube/
```

Arquivos gerados:

- `quality-gate.json`: retorno bruto do endpoint de Quality Gate;
- `measures.json`: retorno bruto das metricas principais do projeto;
- `issues.json`: retorno bruto das issues abertas retornadas pela API;
- `sonarqube-cloud-report.md`: resumo em Markdown com dashboard, Quality Gate, metricas, condicoes e issues;
- `report.md`: alias do resumo em Markdown para uso por automacoes futuras.

Em eventos de pull request, o relatorio consulta a API com `pullRequest=<numero>`. Isso evita confundir o status do projeto principal com o Quality Gate especifico do PR.

O script aceita parametros para uso futuro, mas o workflow oficial usa o project key global e o output global:

```bash
python scripts/quality/sonarqube_cloud_report.py \
  --project-key rodri-oliveira-dev_poc-arquitetura \
  --organization-key rodri-oliveira-dev \
  --output-dir artifacts/sonarqube
```

## Artifact do GitHub Actions

O workflow publica o artifact consolidado `test-results-coverage-and-sonarqube` por 7 dias.

Esse artifact contem:

- resultados de testes `.trx`;
- arquivos `coverage.cobertura.xml` usados pelo ReportGenerator e pelo gate local;
- arquivos `coverage.opencover.xml` importados pelo SonarQube Cloud;
- summaries de cobertura `coverage-report/Summary.json` e `coverage-report/Summary.txt`;
- resumo do SonarQube Cloud em `artifacts/sonarqube/sonarqube-cloud-report.md`;
- alias do resumo em `artifacts/sonarqube/report.md`;
- JSONs retornados pela API do SonarQube Cloud em `artifacts/sonarqube/*.json`.

O workflow oficial nao publica automaticamente `sonar-ledger`, `sonar-balance`, `sonar-transfer`, `sonar-identity`, `sonar-audit`, `sonar-shared` ou `sonar-summary`.

## Scripts locais

Para SonarQube local:

```bash
./scripts/quality/sonar-analyze.sh
```

Por default, o script usa o contexto `global`, que resolve para `PocArquitetura.slnx` e para o project key global configurado em `scripts/quality/sonar-contexts.json`. O modo por contexto continua disponivel para experimento local ou retomada futura, mas nao faz parte do fluxo oficial de CI.

## Tratativas de erro

### Erro: cobertura duplicada ou residual

Causa:

Arquivos antigos em `artifacts/test-results` podem contaminar globs do scanner ou do ReportGenerator.

Correcao:

O workflow oficial ja limpa `artifacts/test-results` e `artifacts/sonarqube`. Em execucao local, remova `artifacts/test-results` antes de investigar falhas de importacao.

### Erro: Automatic Analysis

Causa:

Automatic Analysis habilitada no SonarQube Cloud junto com CI Analysis.

Correcao:

Mantenha Automatic Analysis desabilitada no projeto global:

```text
Administration > Analysis Method > Automatic Analysis
```

### Erro: coverage.opencover.xml nao encontrado

Causa:

Coverlet nao gerou OpenCover ou o caminho configurado em `sonar.cs.opencover.reportsPaths` nao encontra os arquivos gerados.

Correcao:

Valide `coverlet.runsettings` e confirme:

```xml
<Format>cobertura,opencover</Format>
```

Depois confirme se o workflow esta usando:

```text
./artifacts/test-results/**/coverage.opencover.xml
```

### Erro: Quality Gate timeout

Causa:

O scanner aguardou a avaliacao remota por causa de `sonar.qualitygate.wait=true`, mas o SonarQube Cloud nao respondeu dentro do tempo esperado.

Correcao:

Reexecute o job se houver incidente temporario no servico. Se for recorrente, registre evidencia antes de avaliar timeout maior ou mudanca de estrategia.

### Relatorio API indisponivel

Causa:

`scripts/quality/sonarqube_cloud_report.py` nao conseguiu consultar a API por token ausente, autorizacao, indisponibilidade ou falta de dados do PR/projeto.

Correcao:

Use o dashboard do SonarQube Cloud como fonte principal e o artifact gerado como diagnostico. Esse caso nao deve ser tratado como ausencia de bugs, vulnerabilidades ou code smells.

## Criterios de aceite

- O workflow executa restore, build e testes com cobertura usando `./PocArquitetura.slnx`.
- O SonarQube Cloud recebe uma unica analise do projeto global por execucao.
- O SonarQube Cloud exibe cobertura de testes importada via OpenCover consolidado.
- O GitHub Step Summary exibe o resumo do SonarQube Cloud quando a API pode ser consultada.
- O artifact `test-results-coverage-and-sonarqube` contem `artifacts/sonarqube`.
- O workflow falha com mensagem clara quando `SONAR_TOKEN` nao esta configurado.
- O workflow falha com mensagem clara quando `coverage.opencover.xml` nao e gerado.
- Nenhum job contextual roda automaticamente em PR, `main` ou `workflow_dispatch`.
- Nenhum secret ou token e exposto no repositorio.
- As validacoes de cobertura existentes permanecem preservadas.
