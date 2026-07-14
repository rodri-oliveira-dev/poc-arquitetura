# SonarQube Cloud

## Objetivo

O projeto usa SonarQube Cloud para complementar as validacoes locais e do GitHub Actions com analise estatica, Quality Gate, cobertura de testes, bugs, code smells, vulnerabilidades e acompanhamento historico de qualidade.

Essa integracao nao substitui build, testes automatizados, gate local de cobertura, revisao de codigo ou validacoes de seguranca do repositorio. Ela consolida sinais de qualidade em um servico externo.

## Modelo oficial atual

O modelo oficial operacional e analise contextual por aggregate e Shared via GitHub Actions, usando o workflow `.github/workflows/dotnet.yml`.

Automatic Analysis deve ficar desabilitada no SonarQube Cloud. Automatic Analysis e CI Analysis nao devem ficar ativas ao mesmo tempo para o mesmo projeto, porque podem gerar analises duplicadas, resultados inconsistentes e conflitos de configuracao.

A analise via CI e a abordagem correta neste repositorio porque a cobertura .NET precisa ser gerada durante `dotnet test` e importada explicitamente pelo scanner.

Configuracao oficial:

| Contexto | Solution | Project Key | Project Name | Test results | Sonar report | OpenCover |
| --- | --- | --- | --- | --- | --- | --- |
| `aggregate` | `./PocArquitetura.slnx` | `rodri-oliveira-dev_poc-arquitetura` | `poc-arquitetura` | `./artifacts/test-results/aggregate` | `./artifacts/sonarqube/aggregate` | `./artifacts/test-results/aggregate/**/coverage.opencover.xml` |
| `shared` | `./PocArquitetura.Shared.slnx` | `rodri-oliveira-dev_poc-arquitetura-shared` | `poc-arquitetura-shared` | `./artifacts/test-results/shared` | `./artifacts/sonarqube/shared` | `./artifacts/test-results/shared/**/coverage.opencover.xml` |

O artifact unico do workflow e `test-results-coverage-and-sonarqube`, com retencao de 7 dias.

Os projetos SonarQube Cloud oficiais do CI principal sao `rodri-oliveira-dev_poc-arquitetura` para o contexto aggregate e `rodri-oliveira-dev_poc-arquitetura-shared` para o contexto Shared. Nao crie projetos remotos adicionais, nao altere secrets e nao altere Quality Gates sem decisao explicita.

## Fluxo do CI

O workflow `main-dotnet-ci` roda em:

- `merge_group` com `checks_requested`;
- `pull_request`;
- `push` para `main`;
- `workflow_dispatch`.

Em `pull_request`, o workflow usa `scripts/ci/detect-dotnet-impact.py` para executar apenas os contextos necessarios. Em `merge_group`, `push` na `main` e `workflow_dispatch`, executa aggregate e Shared por seguranca.

O fluxo por contexto e:

```text
GitHub Event
  -> main-dotnet-ci
  -> detect-dotnet-impact.py
  -> resolve aggregate/shared/both/docs-only
  -> restore da solution do contexto
  -> auditoria NuGet
  -> SonarQube Cloud begin
  -> build da solution do contexto
  -> test da solution do contexto + coverage
  -> coverage.cobertura.xml e coverage.opencover.xml
  -> SonarQube Cloud end
  -> Quality Gate do contexto
  -> consulta API SonarQube Cloud
  -> relatorio do contexto
  -> ReportGenerator
  -> gate local de cobertura
  -> artifact unico do workflow
```

O `begin` do SonarQube Cloud precisa ocorrer antes do build. O `end` precisa ocorrer depois dos testes com cobertura para que o scanner consiga enviar a analise e importar o relatorio OpenCover.

O workflow limpa `./artifacts/test-results` e `./artifacts/sonarqube` antes da execucao. Cada contexto grava em subpastas isoladas: `artifacts/test-results/aggregate`, `artifacts/test-results/shared`, `artifacts/sonarqube/aggregate` e `artifacts/sonarqube/shared`.

O workflow reutilizavel `.github/workflows/sonarqube-context.yml` foi removido para eliminar uma segunda implementacao completa de restore, build, testes, cobertura, Sonar begin/end, relatorio e artifact.

## Cobertura de testes

O arquivo `coverlet.runsettings` gera dois formatos:

- `coverage.cobertura.xml`, usado pelo ReportGenerator, pelo resumo de cobertura e pelo gate local;
- `coverage.opencover.xml`, importado pelo SonarQube Cloud.

Os parametros usados pelo scanner seguem o contexto executado:

```text
aggregate: sonar.cs.opencover.reportsPaths="./artifacts/test-results/aggregate/**/coverage.opencover.xml"
shared: sonar.cs.opencover.reportsPaths="./artifacts/test-results/shared/**/coverage.opencover.xml"
```

Nao use cobertura generica do Sonar para este caso. Para C#/.NET, a importacao deve usar `sonar.cs.opencover.reportsPaths` apontando para os arquivos OpenCover gerados pelo Coverlet.

O scanner exclui da metrica de cobertura do SonarQube Cloud os diretorios `.github/`, `docs/`, `infra/` e `loadtests/`, alem de `Program.cs`, migrations EF e arquivos gerados. Esses arquivos continuam analisados por regras de qualidade e seguranca quando suportado pelo Sonar, mas nao entram no denominador de cobertura porque a cobertura oficial do repositorio vem dos testes .NET via OpenCover.

Arquivos nao C# dentro de `scripts/` ficam fora da analise por `sonar.exclusions`, com a lista explicita baseada no inventario atual: `scripts/**/*.sh`, `scripts/**/*.ps1`, `scripts/**/*.py`, `scripts/**/*.json` e `scripts/**/*.mjs`. Nao use `scripts/**`: arquivos C# futuros em `scripts/` devem continuar elegiveis para analise e cobertura.

Nao use essa exclusao para esconder codigo produtivo .NET sem testes. Se um arquivo C# de `src/` precisar sair da cobertura, registre uma justificativa localizada e revise se o `coverlet.runsettings` tambem precisa ser ajustado.

## Quality Gate

O SonarQube Cloud aplica seu proprio Quality Gate com base nas regras configuradas em cada projeto do contexto executado.

O workflow tambem possui um gate local de cobertura, hoje com minimo de 85% para cobertura total de linhas em cada contexto executado. No contexto aggregate, os assemblies `LedgerService.Worker` e `BalanceService.Worker` tambem precisam atingir 85%.

Esses gates tem responsabilidades diferentes:

- o gate local verifica cobertura a partir do relatorio Cobertura consolidado pelo ReportGenerator;
- o Quality Gate do Sonar avalia a analise enviada ao SonarQube Cloud, incluindo cobertura importada, bugs, code smells, vulnerabilidades e regras configuradas no servico.

O parametro `sonar.qualitygate.wait=true` permanece ativo para transformar a decisao do Quality Gate remoto em feedback do workflow.

Nao ajuste thresholds remotamente como parte de manutencao de YAML. Divergencias de Quality Gate, New Code Definition ou regras devem ser registradas e corrigidas no SonarQube Cloud como uma decisao operacional explicita.

## Relatorio no GitHub Actions

Apos o `SonarQube Cloud end` de cada contexto, o workflow chama `scripts/quality/sonarqube_cloud_report.py`, consulta a API do SonarQube Cloud com `secrets.SONAR_TOKEN`, sem imprimir o token em logs, e grava um snapshot da execucao em:

```text
artifacts/sonarqube/<contexto>/
```

Arquivos gerados:

- `quality-gate.json`: retorno bruto do endpoint de Quality Gate;
- `measures.json`: retorno bruto das metricas principais do projeto;
- `issues.json`: retorno bruto das issues abertas retornadas pela API;
- `sonarqube-cloud-report.md`: resumo em Markdown com dashboard, Quality Gate, metricas, condicoes e issues;
- `report.md`: alias do resumo em Markdown para uso por automacoes futuras.

Em eventos de pull request, o relatorio consulta a API com `pullRequest=<numero>`. Isso evita confundir o status do projeto principal com o Quality Gate especifico do PR.

O script aceita parametros por contexto. O workflow oficial usa os project keys e diretorios do contexto executado:

```bash
python scripts/quality/sonarqube_cloud_report.py \
  --project-key rodri-oliveira-dev_poc-arquitetura \
  --organization-key rodri-oliveira-dev \
  --output-dir artifacts/sonarqube/aggregate
```

Para Shared, o project key e `rodri-oliveira-dev_poc-arquitetura-shared` e o output e `artifacts/sonarqube/shared`.

## Artifact do GitHub Actions

O workflow publica o artifact consolidado `test-results-coverage-and-sonarqube` por 7 dias.

Esse artifact contem:

- resultados de testes `.trx`;
- arquivos `coverage.cobertura.xml` usados pelo ReportGenerator e pelo gate local;
- arquivos `coverage.opencover.xml` importados pelo SonarQube Cloud;
- summaries de cobertura `coverage-report/Summary.json` e `coverage-report/Summary.txt`;
- resumo do SonarQube Cloud em `artifacts/sonarqube/<contexto>/sonarqube-cloud-report.md`;
- alias do resumo em `artifacts/sonarqube/<contexto>/report.md`;
- JSONs retornados pela API do SonarQube Cloud em `artifacts/sonarqube/<contexto>/*.json`;
- JSONs `nuget-vulnerabilities-<contexto>.json`.

O workflow oficial nao publica artifacts separados por job; aggregate e Shared ficam dentro do artifact unico `test-results-coverage-and-sonarqube`.

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

Depois confirme se o workflow esta usando o path do contexto executado, por exemplo:

```text
./artifacts/test-results/aggregate/**/coverage.opencover.xml
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

- O workflow executa restore, auditoria NuGet, build e testes com cobertura para os contextos impactados.
- O SonarQube Cloud recebe analise aggregate e/ou Shared conforme a deteccao de impacto.
- O SonarQube Cloud exibe cobertura de testes importada via OpenCover do contexto.
- O GitHub Step Summary exibe o resumo do SonarQube Cloud quando a API pode ser consultada.
- O artifact `test-results-coverage-and-sonarqube` contem `artifacts/sonarqube`.
- O workflow falha com mensagem clara quando `SONAR_TOKEN` nao esta configurado.
- O workflow falha com mensagem clara quando `coverage.opencover.xml` nao e gerado.
- Nao existe workflow Sonar contextual separado de `.github/workflows/dotnet.yml`.
- Nenhum secret ou token e exposto no repositorio.
- As validacoes de cobertura existentes permanecem preservadas.
