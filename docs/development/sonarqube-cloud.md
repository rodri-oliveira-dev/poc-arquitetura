# SonarQube Cloud

## Objetivo

O projeto usa SonarQube Cloud para complementar as validacoes locais e do GitHub Actions com analise estatica, quality gate, cobertura de testes, bugs, code smells, vulnerabilidades e acompanhamento historico de qualidade.

Essa integracao nao substitui build, testes automatizados, gate local de cobertura, revisao de codigo ou validacoes de seguranca do repositorio. Ela consolida sinais de qualidade em um servico externo.

## Modelo adotado

O modelo adotado e analise via GitHub Actions, usando o workflow `.github/workflows/dotnet.yml`.

Automatic Analysis deve ficar desabilitada no SonarQube Cloud. Automatic Analysis e CI Analysis nao devem ficar ativas ao mesmo tempo para o mesmo projeto, porque podem gerar analises duplicadas, resultados inconsistentes e conflitos de configuracao.

A analise via CI e a abordagem correta neste repositorio porque a cobertura .NET precisa ser gerada durante `dotnet test` e importada explicitamente pelo scanner.

## Configuracao no SonarQube Cloud

- Project Key: `rodri-oliveira-dev_poc-arquitetura`
- Organization Key: `rodri-oliveira-dev`
- Analysis Method: GitHub Actions / CI Analysis
- Automatic Analysis: desabilitada

O token de analise deve ser criado no SonarQube Cloud e salvo somente como secret no GitHub Actions. O token nao deve ser commitado, exibido em logs, documentado com valor real ou colocado em arquivos locais versionados.

## Preparacao para analise por contexto

A fonte de verdade versionada para a configuracao de analise Sonar por contexto fica em:

```text
scripts/quality/sonar-contexts.json
```

Ela define `solution`, `projectKey`, `projectName`, `resultsDir`, `sonarReportDir` e `coverageReportPattern` para o projeto global atual e para os contextos futuros `ledger`, `balance`, `transfer`, `identity`, `audit` e `shared`.

O resolvedor reutilizavel e:

```text
scripts/quality/sonar_context.py
```

O workflow principal executa a analise global e, durante o piloto incremental, tambem executa uma analise isolada do contexto `transfer`. Os demais contextos permanecem apenas configurados no mapa e nao rodam analise propria nesta etapa.

| Contexto | Solution | Project Key proposto | Results Dir | Sonar Report Dir |
| --- | --- | --- | --- | --- |
| `global` | `PocArquitetura.slnx` | `rodri-oliveira-dev_poc-arquitetura` | `artifacts/test-results` | `artifacts/sonarqube` |
| `ledger` | `LedgerService.slnx` | `rodri-oliveira-dev_poc-arquitetura-ledger` | `artifacts/test-results/ledger` | `artifacts/sonarqube/ledger` |
| `balance` | `BalanceService.slnx` | `rodri-oliveira-dev_poc-arquitetura-balance` | `artifacts/test-results/balance` | `artifacts/sonarqube/balance` |
| `transfer` | `TransferService.slnx` | `rodri-oliveira-dev_poc-arquitetura-transfer` | `artifacts/test-results/transfer` | `artifacts/sonarqube/transfer` |
| `identity` | `IdentityService.slnx` | `rodri-oliveira-dev_poc-arquitetura-identity` | `artifacts/test-results/identity` | `artifacts/sonarqube/identity` |
| `audit` | `AuditService.slnx` | `rodri-oliveira-dev_poc-arquitetura-audit` | `artifacts/test-results/audit` | `artifacts/sonarqube/audit` |
| `shared` | `PocArquitetura.Shared.slnx` | `rodri-oliveira-dev_poc-arquitetura-shared` | `artifacts/test-results/shared` | `artifacts/sonarqube/shared` |

Os projetos contextuais precisam existir no SonarQube Cloud antes de qualquer analise real desses contextos. Nao habilite Automatic Analysis nesses projetos; mantenha CI Analysis como metodo esperado.

Para o piloto Transfer, valide externamente antes de exigir o job como gate obrigatorio:

- projeto SonarQube Cloud criado com Project Key `rodri-oliveira-dev_poc-arquitetura-transfer`;
- Organization Key `rodri-oliveira-dev`;
- Automatic Analysis desabilitada;
- Quality Gate organizacional/padrao configurado;
- New Code Definition configurada;
- token em `secrets.SONAR_TOKEN` com permissao de Execute Analysis para o projeto Transfer, ou token apropriado equivalente;
- PR binding funcional entre GitHub e SonarQube Cloud.

O workflow nao cria projeto remoto, nao altera permissoes e nao muda configuracao do Quality Gate. Se qualquer item acima ainda nao existir no SonarQube Cloud, o piloto fica bloqueado externamente ate a configuracao ser concluida.

## Configuracao no GitHub

Crie o secret do repositorio em:

```text
Settings > Secrets and variables > Actions > New repository secret > SONAR_TOKEN
```

O workflow le o token exclusivamente de `secrets.SONAR_TOKEN` e falha com mensagem clara quando o secret esta ausente ou vazio.

## Pipeline

A ordem correta do job global no workflow principal e:

1. checkout com historico completo (`fetch-depth: 0`);
2. setup .NET;
3. restore das tools locais;
4. restore das dependencias;
5. validacao do `SONAR_TOKEN`;
6. SonarQube Cloud begin;
7. build;
8. testes com cobertura;
9. validacao dos arquivos de cobertura;
10. SonarQube Cloud end;
11. geracao do relatorio resumido do SonarQube Cloud no GitHub Actions;
12. geracao do relatorio de cobertura;
13. gate local de cobertura;
14. upload de artifacts.

O `begin` do SonarQube Cloud precisa ocorrer antes do build. O `end` precisa ocorrer depois dos testes com cobertura para que o scanner consiga enviar a analise e importar o relatorio OpenCover.

O job piloto `sonar-transfer-pilot` preserva o mesmo boundary para Transfer:

1. checkout com historico completo (`fetch-depth: 0`);
2. setup .NET e restore das tools locais;
3. resolucao do contexto `transfer` em `scripts/quality/sonar-contexts.json`;
4. restore de `TransferService.slnx`;
5. validacao de `SONAR_TOKEN`;
6. SonarQube Cloud begin com Project Key `rodri-oliveira-dev_poc-arquitetura-transfer`;
7. build de `TransferService.slnx`;
8. testes de `TransferService.slnx` com cobertura em `artifacts/test-results/transfer`;
9. validacao de Cobertura e OpenCover contextuais;
10. SonarQube Cloud end com `sonar.qualitygate.wait=true`;
11. geracao do report da API em `artifacts/sonarqube/transfer`;
12. geracao de summary contextual de cobertura sem aplicar threshold;
13. upload do artifact `sonar-transfer`.

O build usado pela analise Transfer acontece dentro do boundary do scanner Transfer. O job roda em workspace isolado do job global; nao ha duas analises Sonar abertas no mesmo workspace.

## Cobertura de testes

O arquivo `coverlet.runsettings` gera dois formatos:

- `coverage.cobertura.xml`, usado pelo ReportGenerator, pelo resumo de cobertura e pelo gate local;
- `coverage.opencover.xml`, importado pelo SonarQube Cloud.

O parametro usado pelo scanner e:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/**/coverage.opencover.xml"
```

Nao use cobertura generica do Sonar para este caso. Para C#/.NET, a importacao deve usar `sonar.cs.opencover.reportsPaths` apontando para os arquivos OpenCover gerados pelo Coverlet.

Para analises contextuais futuras, use o padrao isolado do respectivo contexto, por exemplo:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/transfer/**/coverage.opencover.xml"
```

Evite glob global em analises contextuais para nao importar cobertura de outra solution.

No piloto Transfer, o scanner usa somente:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/transfer/**/coverage.opencover.xml"
```

O job tambem falha se encontrar `coverage.opencover.xml` fora de `artifacts/test-results/transfer` dentro do workspace do piloto. Isso protege a validacao contra contaminacao por cobertura global ou de outro contexto.

O scanner mantem a deteccao geral de credenciais hard-coded ativa, mas ignora issues somente em linhas cujo contexto pareca credencial e cujo valor seja um placeholder uppercase de secret entre `<...>`, como `Password=<LEDGER_DB_PASSWORD>`, `KEYCLOAK_CLIENT_SECRET=<KEYCLOAK_CLIENT_SECRET>` ou `--token "<TOKEN>"`. Essa supressao cobre os placeholders versionados em `.env.example`, `.env.local.example`, `appsettings*.json` e exemplos operacionais em `docs/`, sem excluir arquivos inteiros da analise. O trade-off e que o Sonar ignora todas as issues na linha que casar com esse padrao, por isso o regex e restrito a placeholders uppercase com sufixo de secret. Valores reais ou literais, como `Password=postgres`, `Password=123456`, `Password=localpassword` ou `Password=my-secret`, continuam fora desse padrao e devem ser tratados como achados reais.

O scanner exclui da metrica de cobertura do SonarQube Cloud os diretorios `.github/`, `docs/`, `infra/`, `loadtests/` e `scripts/`. Esses arquivos continuam analisados por regras de qualidade e seguranca quando suportado pelo Sonar, mas nao entram no denominador de cobertura porque a cobertura oficial do repositorio vem dos testes .NET via OpenCover.

Nao use essa exclusao para esconder codigo produtivo .NET sem testes. Se um arquivo C# de `src/` precisar sair da cobertura, registre uma justificativa localizada e revise se o `coverlet.runsettings` tambem precisa ser ajustado.

## Quality Gate

O SonarQube Cloud aplica seu proprio quality gate com base nas regras configuradas no projeto e na organizacao.

O workflow tambem possui um gate local de cobertura, hoje com minimo de 85% para cobertura total de linhas e para os assemblies `LedgerService.Worker` e `BalanceService.Worker`.

Esses gates tem responsabilidades diferentes:

- o gate local verifica cobertura a partir do relatorio Cobertura consolidado pelo ReportGenerator;
- o quality gate do Sonar avalia a analise enviada ao SonarQube Cloud, incluindo cobertura importada, bugs, code smells, vulnerabilidades e regras configuradas no servico.

O parametro `sonar.qualitygate.wait=true` faz sentido para este projeto porque transforma a decisao do quality gate remoto em feedback do workflow. O custo e aguardar a avaliacao do SonarQube Cloud durante o job.

## Relatorio no GitHub Actions

Apos o step `SonarQube Cloud end`, o workflow executa o step `Generate SonarQube Cloud report`.

Esse step chama `scripts/quality/sonarqube_cloud_report.py`, consulta a API do SonarQube Cloud com `secrets.SONAR_TOKEN`, sem imprimir o token em logs, e grava um snapshot da execucao em:

```text
artifacts/sonarqube/
```

Arquivos gerados:

- `quality-gate.json`: retorno bruto do endpoint de quality gate;
- `measures.json`: retorno bruto das metricas principais do projeto;
- `issues.json`: retorno bruto das issues abertas retornadas pela API;
- `sonarqube-cloud-report.md`: resumo em Markdown com dashboard, quality gate, metricas, condicoes e issues.
- `report.md`: alias do resumo em Markdown para uso por automacoes contextuais futuras.

O mesmo conteudo de `sonarqube-cloud-report.md` e adicionado ao GitHub Step Summary do job. Para consultar:

1. abra a execucao do workflow no GitHub Actions;
2. entre no job `Build, test and coverage`;
3. veja a aba ou secao `Summary` da execucao.

Se `SONAR_TOKEN` estiver ausente ou se a API do SonarQube Cloud nao responder, o step registra uma mensagem clara, gera arquivos de erro em `artifacts/sonarqube` e nao quebra o restante do job. O quality gate remoto continua sendo aplicado pelo scanner quando `SonarQube Cloud end` executa com sucesso.

Em eventos de pull request, o relatorio consulta a API com `pullRequest=<numero>`. Isso evita confundir o status do projeto principal com o Quality Gate especifico do PR.

O script aceita parametros para uso futuro por contexto:

```bash
python scripts/quality/sonarqube_cloud_report.py \
  --project-key rodri-oliveira-dev_poc-arquitetura-transfer \
  --organization-key rodri-oliveira-dev \
  --output-dir artifacts/sonarqube/transfer
```

Por default, o workflow passa esses valores por variaveis resolvidas a partir de `scripts/quality/sonar-contexts.json`.

## Artifact do GitHub Actions

O workflow publica o artifact global `test-results-coverage-and-sonarqube` por 7 dias.

Para baixar:

1. abra a execucao do workflow no GitHub Actions;
2. role ate `Artifacts`;
3. baixe `test-results-coverage-and-sonarqube`.

Esse artifact contem:

- resultados de testes `.trx`;
- arquivos `coverage.cobertura.xml` usados pelo ReportGenerator e pelo gate local;
- arquivos `coverage.opencover.xml` importados pelo SonarQube Cloud;
- summaries de cobertura `coverage-report/Summary.json` e `coverage-report/Summary.txt`;
- resumo do SonarQube Cloud em `artifacts/sonarqube/sonarqube-cloud-report.md`;
- alias do resumo em `artifacts/sonarqube/report.md`;
- JSONs retornados pela API do SonarQube Cloud em `artifacts/sonarqube/*.json`.

Durante o piloto, o job `sonar-transfer-pilot` tambem publica o artifact `sonar-transfer` por 7 dias, contendo:

- resultados `.trx` de Transfer;
- `coverage.cobertura.xml` de Transfer;
- `coverage.opencover.xml` de Transfer;
- `artifacts/test-results/transfer/coverage-report/Summary.json`;
- `artifacts/test-results/transfer/coverage-report/Summary.txt`;
- `artifacts/sonarqube/transfer/quality-gate.json`;
- `artifacts/sonarqube/transfer/measures.json`;
- `artifacts/sonarqube/transfer/issues.json`;
- `artifacts/sonarqube/transfer/sonarqube-cloud-report.md`;
- `artifacts/sonarqube/transfer/report.md`.

O relatorio do GitHub Actions e apenas um snapshot da execucao do CI. Ele facilita triagem no proprio workflow, mas nao substitui o dashboard oficial do SonarQube Cloud, que continua sendo a fonte principal para historico, detalhes navegaveis, configuracao de quality gate, regras, tendencias e estado mais recente do projeto.

## Workflow atual

O workflow `main-dotnet-ci` roda em:

- `push` para `main`;
- `pull_request` para `main`;
- `workflow_dispatch`.

As permissoes declaradas sao minimas para leitura do repositorio e contexto do pull request:

- `contents: read`;
- `pull-requests: read`.

O job global publica o artifact `test-results-coverage-and-sonarqube` por 7 dias com resultados `.trx`, arquivos `coverage.cobertura.xml`, arquivos `coverage.opencover.xml`, summaries do ReportGenerator e o snapshot resumido do SonarQube Cloud.

O piloto Transfer roda nos mesmos triggers do workflow enquanto a comparacao estiver ativa:

- `pull_request` para `main`, respeitando os `paths-ignore` atuais do workflow;
- `workflow_dispatch`;
- `push` para `main`.

Nesta etapa nao ha selecao granular por paths de Transfer, porque o objetivo e validar o pipeline contextual antes de otimizar custo.

## Comparacao do piloto Transfer

Compare o artifact global `test-results-coverage-and-sonarqube`, o artifact `sonar-transfer` e os dashboards SonarQube Cloud dos projetos:

- global: `rodri-oliveira-dev_poc-arquitetura`;
- Transfer: `rodri-oliveira-dev_poc-arquitetura-transfer`.

Pontos de comparacao:

- arquivos source atribuidos a cada projeto;
- cobertura importada em Transfer versus cobertura global;
- issues abertas e severidade;
- metricas principais em `measures.json`;
- status e condicoes do Quality Gate;
- tempo do job global versus `sonar-transfer-pilot`;
- runner-minutes observaveis na execucao do GitHub Actions;
- existencia de source duplicado inesperado;
- ausencia de cobertura contaminada fora de `artifacts/test-results/transfer`.

Os percentuais de cobertura nao precisam ser iguais. O projeto global usa a solution agregadora e tem denominador maior; o projeto Transfer usa `TransferService.slnx`, testes e fontes atribuiveis ao contexto Transfer.

## Ferramentas locais

As tools usadas pelo fluxo estao declaradas em `.config/dotnet-tools.json`:

- `dotnet-sonarscanner`;
- `dotnet-reportgenerator-globaltool`.

O `dotnet tool restore` executado pela composite action `.github/actions/setup-dotnet` e suficiente para disponibilizar essas ferramentas no workflow.

O script local `scripts/quality/sonar-analyze.sh` continua executando a analise global por default:

```bash
bash scripts/quality/sonar-analyze.sh
```

Ele tambem aceita um contexto preparado no mapa, sem executar todos os contextos automaticamente:

```bash
bash scripts/quality/sonar-analyze.sh transfer
```

Esse modo contextual depende de o projeto correspondente ja existir no SonarQube usado pela analise e de o token possuir permissao para enviar analises.

## Exclusoes de cobertura

O `coverlet.runsettings` mantem OpenCover e Cobertura habilitados:

```xml
<Format>cobertura,opencover</Format>
```

As exclusoes atuais cobrem atributos explicitos de exclusao, codigo gerado pelo compilador, state machines async, `Program.cs`, migrations EF Core e arquivos `.g.cs`. Essas exclusoes sao aceitaveis para evitar que composicao de host, migrations e codigo gerado distorcam o denominador de cobertura.

Nao adicione novas exclusoes apenas para elevar percentual. Qualquer nova exclusao deve ter justificativa tecnica localizada.

## Troubleshooting

### Erro: sonar.token= is invalid

Causa:

`SONAR_TOKEN` vazio ou ausente.

Correcao:

Crie o secret `SONAR_TOKEN` no GitHub Actions.

### Erro: Automatic Analysis is enabled

Causa:

O projeto esta com Automatic Analysis ativa no SonarQube Cloud e ao mesmo tempo tentando executar analise via CI.

Correcao:

Desabilite Automatic Analysis em:

```text
SonarQube Cloud > Project > Administration > Analysis Method > Automatic Analysis
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

## Criterios de aceite

- O workflow executa restore, build e testes com cobertura com sucesso.
- O SonarQube Cloud recebe a analise do projeto.
- O SonarQube Cloud exibe cobertura de testes importada via OpenCover.
- O GitHub Step Summary exibe o resumo do SonarQube Cloud quando a API pode ser consultada.
- O artifact do workflow contem `artifacts/sonarqube`.
- O workflow falha com mensagem clara quando `SONAR_TOKEN` nao esta configurado.
- O workflow falha com mensagem clara quando `coverage.opencover.xml` nao e gerado.
- A documentacao explica como manter, corrigir e evoluir a integracao.
- Nenhum secret ou token e exposto no repositorio.
- As validacoes de cobertura existentes permanecem preservadas.
