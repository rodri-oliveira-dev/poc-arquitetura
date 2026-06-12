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

## Configuracao no GitHub

Crie o secret do repositorio em:

```text
Settings > Secrets and variables > Actions > New repository secret > SONAR_TOKEN
```

O workflow le o token exclusivamente de `secrets.SONAR_TOKEN` e falha com mensagem clara quando o secret esta ausente ou vazio.

## Pipeline

A ordem correta no workflow principal e:

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
11. geracao do relatorio de cobertura;
12. gate local de cobertura;
13. upload de artifacts.

O `begin` do SonarQube Cloud precisa ocorrer antes do build. O `end` precisa ocorrer depois dos testes com cobertura para que o scanner consiga enviar a analise e importar o relatorio OpenCover.

## Cobertura de testes

O arquivo `coverlet.runsettings` gera dois formatos:

- `coverage.cobertura.xml`, usado pelo ReportGenerator, pelo resumo de cobertura e pelo gate local;
- `coverage.opencover.xml`, importado pelo SonarQube Cloud.

O parametro usado pelo scanner e:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/**/coverage.opencover.xml"
```

Nao use cobertura generica do Sonar para este caso. Para C#/.NET, a importacao deve usar `sonar.cs.opencover.reportsPaths` apontando para os arquivos OpenCover gerados pelo Coverlet.

## Quality Gate

O SonarQube Cloud aplica seu proprio quality gate com base nas regras configuradas no projeto e na organizacao.

O workflow tambem possui um gate local de cobertura, hoje com minimo de 85% para cobertura total de linhas e para os assemblies `LedgerService.Worker` e `BalanceService.Worker`.

Esses gates tem responsabilidades diferentes:

- o gate local verifica cobertura a partir do relatorio Cobertura consolidado pelo ReportGenerator;
- o quality gate do Sonar avalia a analise enviada ao SonarQube Cloud, incluindo cobertura importada, bugs, code smells, vulnerabilidades e regras configuradas no servico.

O parametro `sonar.qualitygate.wait=true` faz sentido para este projeto porque transforma a decisao do quality gate remoto em feedback do workflow. O custo e aguardar a avaliacao do SonarQube Cloud durante o job.

## Workflow atual

O workflow `main-dotnet-ci` roda em:

- `push` para `main`;
- `pull_request` para `main`;
- `workflow_dispatch`.

As permissoes declaradas sao minimas para leitura do repositorio e contexto do pull request:

- `contents: read`;
- `pull-requests: read`.

O workflow publica o artifact `test-results-and-coverage` por 7 dias com resultados `.trx`, arquivos `coverage.cobertura.xml`, arquivos `coverage.opencover.xml` e summaries do ReportGenerator.

## Ferramentas locais

As tools usadas pelo fluxo estao declaradas em `.config/dotnet-tools.json`:

- `dotnet-sonarscanner`;
- `dotnet-reportgenerator-globaltool`.

O `dotnet tool restore` executado pela composite action `.github/actions/setup-dotnet` e suficiente para disponibilizar essas ferramentas no workflow.

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
- O workflow falha com mensagem clara quando `SONAR_TOKEN` nao esta configurado.
- O workflow falha com mensagem clara quando `coverage.opencover.xml` nao e gerado.
- A documentacao explica como manter, corrigir e evoluir a integracao.
- Nenhum secret ou token e exposto no repositorio.
- As validacoes de cobertura existentes permanecem preservadas.
