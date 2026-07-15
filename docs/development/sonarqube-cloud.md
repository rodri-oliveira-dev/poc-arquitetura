# SonarQube Cloud

## Objetivo

O projeto usa SonarQube Cloud para complementar as validacoes locais e do GitHub Actions com analise estatica, Quality Gate, cobertura de testes, bugs, code smells, vulnerabilidades e acompanhamento historico de qualidade.

Essa integracao nao substitui build, testes automatizados, gate local de cobertura, revisao de codigo ou validacoes de seguranca do repositorio. Ela consolida sinais de qualidade em um servico externo.

## Modelo oficial atual

O modelo oficial operacional e analise agregada unica via GitHub Actions, usando o workflow `.github/workflows/dotnet.yml`.

Automatic Analysis deve ficar desabilitada no SonarQube Cloud. Automatic Analysis e CI Analysis nao devem ficar ativas ao mesmo tempo para o mesmo projeto, porque podem gerar analises duplicadas, resultados inconsistentes e conflitos de configuracao.

A analise via CI e a abordagem correta neste repositorio porque a cobertura .NET precisa ser gerada durante `dotnet test` e importada explicitamente pelo scanner.

Configuracao oficial do projeto Sonar unico:

| Contexto | Solution | Project Key | Project Name | Test results | Sonar report | OpenCover |
| --- | --- | --- | --- | --- | --- | --- |
| `aggregate` | `./PocArquitetura.slnx` | `rodri-oliveira-dev_poc-arquitetura` | `poc-arquitetura` | `./artifacts/test-results/aggregate` | `./artifacts/sonarqube/aggregate` | `./artifacts/test-results/aggregate/**/coverage.opencover.xml` |

O contexto Shared nao possui projeto Sonar separado. Ele continua executando restore, auditoria NuGet, build, testes, cobertura, ReportGenerator, gate local de 80% e resumo do GitHub Actions, mas nao envia analise SonarQube Cloud.

O artifact unico do workflow e `test-results-coverage-and-sonarqube`, com retencao de 7 dias.

O projeto SonarQube Cloud oficial do CI principal e `rodri-oliveira-dev_poc-arquitetura`. Nao crie projetos remotos adicionais por bounded context, por solution ou para Shared; nao altere secrets e nao altere Quality Gates sem decisao explicita.

## Fluxo do CI

O workflow `main-dotnet-ci` roda em:

- `merge_group` com `checks_requested`;
- `pull_request`;
- `push` para `main`;
- `workflow_dispatch`.

Em `pull_request`, o workflow usa `scripts/ci/detect-dotnet-impact.py` para executar apenas os contextos necessarios. Alteracoes em `src/Shared/**`, `tests/Shared/**` ou `PocArquitetura.Shared.slnx` executam aggregate e Shared: aggregate envia a analise Sonar completa do projeto oficial, e Shared preserva o gate local de 80%. Em `merge_group`, `push` na `main` e `workflow_dispatch`, executa aggregate e Shared por seguranca.

O fluxo aggregate e:

```text
GitHub Event
  -> main-dotnet-ci
  -> detect-dotnet-impact.py
  -> resolve aggregate/shared/both/docs-only
  -> restore de ./PocArquitetura.slnx
  -> auditoria NuGet
  -> SonarQube Cloud begin
  -> build de ./PocArquitetura.slnx
  -> test de ./PocArquitetura.slnx + coverage
  -> coverage.cobertura.xml e coverage.opencover.xml
  -> ReportGenerator
  -> gate local de cobertura
  -> summary local de cobertura
  -> SonarQube Cloud end
  -> Quality Gate agregado
  -> consulta API SonarQube Cloud
  -> relatorio aggregate
  -> artifact unico do workflow
```

O fluxo Shared, quando impactado, roda no mesmo workflow sem Sonar:

```text
GitHub Event
  -> main-dotnet-ci
  -> detect-dotnet-impact.py
  -> restore de ./PocArquitetura.Shared.slnx
  -> auditoria NuGet
  -> build de ./PocArquitetura.Shared.slnx
  -> test de ./PocArquitetura.Shared.slnx + coverage
  -> coverage.cobertura.xml e coverage.opencover.xml
  -> ReportGenerator
  -> gate local de cobertura de 80%
  -> summary local de cobertura
  -> artifact unico do workflow
```

O `begin` do SonarQube Cloud precisa ocorrer antes do build. O `end` precisa ocorrer depois dos testes com cobertura para que o scanner consiga enviar a analise e importar o relatorio OpenCover.

O `main-dotnet-ci` executa o SonarScanner for .NET com `sonar.scanner.scanAll=false`. Isso desliga a analise multi-language automatica do scanner .NET e evita que sensores de IaC/Terraform, YAML, JSON, shell ou outros arquivos fora do build MSBuild entrem no caminho critico de build/test/cobertura. Infraestrutura e Terraform continuam cobertos pelos workflows dedicados `infrastructure-security` e `terraform-validation`; eles nao devem ser reintroduzidos no Sonar do CI .NET sem nova decisao explicita.

O workflow limpa `./artifacts/test-results` e `./artifacts/sonarqube` antes da execucao. Os resultados locais gravam em `artifacts/test-results/aggregate` e `artifacts/test-results/shared`; o relatorio Sonar existe somente em `artifacts/sonarqube/aggregate`.

O workflow reutilizavel `.github/workflows/sonarqube-context.yml` foi removido e deve permanecer ausente. SonarQube Cloud, restore, build, testes, cobertura, ReportGenerator, relatorio e artifact ficam concentrados em `.github/workflows/dotnet.yml`; nao deve existir uma segunda implementacao completa desse fluxo.

## Cobertura de testes

O arquivo `coverlet.runsettings` gera dois formatos:

- `coverage.cobertura.xml`, usado pelo ReportGenerator, pelo resumo de cobertura e pelo gate local;
- `coverage.opencover.xml`, importado pelo SonarQube Cloud.

O parametro usado pelo scanner aponta para a cobertura aggregate:

```text
aggregate: sonar.cs.opencover.reportsPaths="./artifacts/test-results/aggregate/**/coverage.opencover.xml"
```

Nao use cobertura generica do Sonar para este caso. Para C#/.NET, a importacao deve usar `sonar.cs.opencover.reportsPaths` apontando para os arquivos OpenCover gerados pelo Coverlet.

O scanner exclui da metrica de cobertura do SonarQube Cloud os diretorios `.github/`, `docs/`, `infra/` e `loadtests/`, alem de `Program.cs`, migrations EF e arquivos gerados. Com `sonar.scanner.scanAll=false`, os arquivos fora dos projetos MSBuild nao entram na analise multi-language automatica; essas exclusoes permanecem defensivas para a metrica de cobertura e para eventuais arquivos incluidos explicitamente em projetos .NET.

Arquivos nao C# dentro de `scripts/` ficam fora da analise por `sonar.exclusions`, com a lista explicita baseada no inventario atual: `scripts/**/*.sh`, `scripts/**/*.ps1`, `scripts/**/*.py`, `scripts/**/*.json` e `scripts/**/*.mjs`. Nao use `scripts/**`: arquivos C# futuros em `scripts/` devem continuar elegiveis para analise e cobertura se forem incluidos em projetos MSBuild.

Nao use essa exclusao para esconder codigo produtivo .NET sem testes. Se um arquivo C# de `src/` precisar sair da cobertura, registre uma justificativa localizada e revise se o `coverlet.runsettings` tambem precisa ser ajustado.

## Quality Gate

O SonarQube Cloud aplica seu proprio Quality Gate com base nas regras configuradas no projeto agregado.

O workflow tambem possui um gate local de cobertura: 85% para cobertura total de linhas no contexto aggregate e 80% para cobertura total de linhas no contexto Shared. No contexto aggregate, os assemblies `LedgerService.Worker` e `BalanceService.Worker` tambem precisam atingir 85%.

Esses gates tem responsabilidades diferentes:

- o gate local verifica cobertura a partir do relatorio Cobertura consolidado pelo ReportGenerator;
- o Quality Gate do Sonar avalia a analise enviada ao SonarQube Cloud, incluindo cobertura importada, bugs, code smells, vulnerabilidades e regras configuradas no servico.

O parametro `sonar.qualitygate.wait=true` permanece ativo para transformar a decisao do Quality Gate remoto em feedback do workflow.

Nao ajuste thresholds remotamente como parte de manutencao de YAML. Divergencias de Quality Gate, New Code Definition ou regras devem ser registradas e corrigidas no SonarQube Cloud como uma decisao operacional explicita.

## Relatorio no GitHub Actions

Apos o `SonarQube Cloud end` do aggregate, o workflow chama `scripts/quality/sonarqube_cloud_report.py`, consulta a API do SonarQube Cloud com `secrets.SONAR_TOKEN`, sem imprimir o token em logs, e grava um snapshot da execucao em:

```text
artifacts/sonarqube/aggregate/
```

Arquivos gerados:

- `quality-gate.json`: retorno bruto do endpoint de Quality Gate;
- `measures.json`: retorno bruto das metricas principais do projeto;
- `issues.json`: retorno bruto das issues abertas retornadas pela API;
- `sonarqube-cloud-report.md`: resumo em Markdown com dashboard, Quality Gate, metricas, condicoes e issues;
- `report.md`: alias do resumo em Markdown para uso por automacoes futuras.

Em eventos de pull request, o relatorio consulta a API com `pullRequest=<numero>`. Isso evita confundir o status do projeto principal com o Quality Gate especifico do PR.

O workflow oficial usa o project key agregado e o diretorio aggregate:

```bash
python scripts/quality/sonarqube_cloud_report.py \
  --project-key rodri-oliveira-dev_poc-arquitetura \
  --organization-key rodri-oliveira-dev \
  --output-dir artifacts/sonarqube/aggregate
```

## Artifact do GitHub Actions

O workflow publica o artifact consolidado `test-results-coverage-and-sonarqube` por 7 dias.

Esse artifact contem:

- resultados de testes `.trx`;
- arquivos `coverage.cobertura.xml` usados pelo ReportGenerator e pelo gate local;
- arquivos `coverage.opencover.xml` importados pelo SonarQube Cloud;
- summaries de cobertura `coverage-report/Summary.json` e `coverage-report/Summary.txt`;
- resumo do SonarQube Cloud em `artifacts/sonarqube/aggregate/sonarqube-cloud-report.md`;
- alias do resumo em `artifacts/sonarqube/aggregate/report.md`;
- JSONs retornados pela API do SonarQube Cloud em `artifacts/sonarqube/aggregate/*.json`;
- JSONs `nuget-vulnerabilities-<contexto>.json`.

O workflow oficial nao publica artifacts separados por job. Aggregate e Shared ficam dentro do artifact unico `test-results-coverage-and-sonarqube`, mas apenas aggregate possui subdiretorio Sonar.

## Scripts locais

Para SonarQube local:

```bash
./scripts/quality/sonar-analyze.sh
```

Por default, o script usa o contexto `global`, que resolve para `PocArquitetura.slnx` e para o project key global configurado em `scripts/quality/sonar-contexts.json`. O fluxo oficial de CI usa somente esse projeto agregado para SonarQube Cloud.

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
- O SonarQube Cloud recebe analise aggregate quando ha impacto .NET que exige Sonar.
- Alteracoes em Shared tambem disparam aggregate para preservar a analise completa do projeto oficial.
- O contexto Shared executa build, testes e gate local proprio sem projeto Sonar separado.
- O SonarQube Cloud exibe cobertura de testes importada via OpenCover do aggregate.
- O GitHub Step Summary exibe o resumo do SonarQube Cloud quando a API pode ser consultada.
- O artifact `test-results-coverage-and-sonarqube` contem `artifacts/sonarqube`.
- O workflow falha com mensagem clara quando `SONAR_TOKEN` nao esta configurado.
- O workflow falha com mensagem clara quando `coverage.opencover.xml` nao e gerado.
- Nao existe workflow Sonar contextual separado de `.github/workflows/dotnet.yml`.
- Nenhum secret ou token e exposto no repositorio.
- As validacoes de cobertura existentes permanecem preservadas.
