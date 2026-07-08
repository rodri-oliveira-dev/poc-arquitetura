# SonarQube Cloud

## Objetivo

O projeto usa SonarQube Cloud para complementar as validacoes locais e do GitHub Actions com analise estatica, quality gate, cobertura de testes, bugs, code smells, vulnerabilidades e acompanhamento historico de qualidade.

Essa integracao nao substitui build, testes automatizados, gate local de cobertura, revisao de codigo ou validacoes de seguranca do repositorio. Ela consolida sinais de qualidade em um servico externo.

## Modelo adotado

O modelo adotado e analise via GitHub Actions, usando o workflow `.github/workflows/dotnet.yml`.

Automatic Analysis deve ficar desabilitada no SonarQube Cloud. Automatic Analysis e CI Analysis nao devem ficar ativas ao mesmo tempo para o mesmo projeto, porque podem gerar analises duplicadas, resultados inconsistentes e conflitos de configuracao.

A analise via CI e a abordagem correta neste repositorio porque a cobertura .NET precisa ser gerada durante `dotnet test` e importada explicitamente pelo scanner.

## Organizacao no SonarQube Cloud

O repositorio usa uma organizacao SonarQube Cloud e multiplos projetos:

- Organization Key: `rodri-oliveira-dev`;
- projetos contextuais para Ledger, Balance, Transfer, Identity, Audit e Shared;
- projeto global `rodri-oliveira-dev_poc-arquitetura` mantido temporariamente para comparacao da migracao.

Todos os projetos devem usar GitHub Actions / CI Analysis. Automatic Analysis deve permanecer desabilitada para evitar analises duplicadas, resultados inconsistentes e conflito com a cobertura .NET importada via OpenCover.

## Analise por contexto

A fonte de verdade versionada para a configuracao de analise Sonar por contexto fica em:

```text
scripts/quality/sonar-contexts.json
```

Ela define `solution`, `projectKey`, `projectName`, `resultsDir`, `sonarReportDir` e `coverageReportPattern` para o projeto global atual e para os contextos `ledger`, `balance`, `transfer`, `identity`, `audit` e `shared`.

O resolvedor reutilizavel e:

```text
scripts/quality/sonar_context.py
```

O workflow principal executa a analise contextual por matrix chamando o reusable workflow `.github/workflows/sonarqube-context.yml`. Cada contexto roda em job isolado, com solution, Project Key, diretorio de resultados, diretorio Sonar e artifact proprios.

O projeto global permanece temporariamente para comparacao durante a migracao. Para evitar custo excessivo, a analise global em `.github/workflows/dotnet.yml` roda apenas em `push` para `main` e em `workflow_dispatch` com `sonar_context=all`. Em PR, o workflow executa somente os contextos Sonar impactados pelos paths alterados.

| Contexto | Solution | Project Key | Results Dir | Sonar Report Dir |
| --- | --- | --- | --- | --- |
| `global` | `PocArquitetura.slnx` | `rodri-oliveira-dev_poc-arquitetura` | `artifacts/test-results` | `artifacts/sonarqube` |
| `ledger` | `LedgerService.slnx` | `rodri-oliveira-dev_poc-arquitetura-ledger` | `artifacts/test-results/ledger` | `artifacts/sonarqube/ledger` |
| `balance` | `BalanceService.slnx` | `rodri-oliveira-dev_poc-arquitetura-balance` | `artifacts/test-results/balance` | `artifacts/sonarqube/balance` |
| `transfer` | `TransferService.slnx` | `rodri-oliveira-dev_poc-arquitetura-transfer` | `artifacts/test-results/transfer` | `artifacts/sonarqube/transfer` |
| `identity` | `IdentityService.slnx` | `rodri-oliveira-dev_poc-arquitetura-identity` | `artifacts/test-results/identity` | `artifacts/sonarqube/identity` |
| `audit` | `AuditService.slnx` | `rodri-oliveira-dev_poc-arquitetura-audit` | `artifacts/test-results/audit` | `artifacts/sonarqube/audit` |
| `shared` | `PocArquitetura.Shared.slnx` | `rodri-oliveira-dev_poc-arquitetura-shared` | `artifacts/test-results/shared` | `artifacts/sonarqube/shared` |

Os projetos contextuais precisam existir no SonarQube Cloud antes de qualquer analise real desses contextos. Nao habilite Automatic Analysis nesses projetos; mantenha CI Analysis como metodo esperado.

Valide externamente antes de exigir os jobs contextuais como gates obrigatorios:

- projetos SonarQube Cloud criados com os seis Project Keys contextuais listados na tabela;
- Organization Key `rodri-oliveira-dev`;
- Automatic Analysis desabilitada;
- mesmo Quality Gate inicial configurado para os seis projetos;
- New Code Definition coerente entre os seis projetos;
- token em `secrets.SONAR_TOKEN` com permissao de Execute Analysis para os seis projetos, ou token apropriado equivalente;
- PR binding funcional entre GitHub e SonarQube Cloud.

O workflow nao cria projeto remoto, nao altera permissoes e nao muda configuracao do Quality Gate. Se qualquer item acima ainda nao existir no SonarQube Cloud, a analise contextual correspondente fica bloqueada externamente ate a configuracao ser concluida.

## Arquitetura final do CI Sonar

O fluxo esperado e:

```text
PR
  -> contextos impactados
  -> Sonar contextual
  -> Quality Gate contextual
  -> summary consolidado

main
  -> todos os contextos
  -> dashboards contextuais atualizados

workflow_dispatch
  -> todos ou contexto escolhido
```

Em PR, o job `detect-sonar-contexts` escolhe a matrix contextual por paths. Em `push` para `main`, nao ha seletividade: os seis contextos rodam para manter dashboards e historico recentes. Em `workflow_dispatch`, o input `sonar_context` permite `all` ou um contexto especifico.

`tests/Architecture.Tests` permanece como validacao arquitetural global dentro de `PocArquitetura.slnx` e dos gates globais de teste. Ele nao possui projeto Sonar dedicado, porque nao representa um bounded context nem ownership de source produtivo. A diferenca de governanca e:

- validacao arquitetural global: garante regras transversais do repositorio por testes;
- analise Sonar contextual: mede qualidade, issues, cobertura e Quality Gate por contexto de ownership.

## Selecao contextual em Pull Requests

O job `detect-sonar-contexts` calcula a matriz contextual de PRs com `scripts/quality/sonar_context_impact.py`. O script reutiliza `scripts/quality/sonar-contexts.json` para montar solution, Project Key, diretorios de resultado e artifact de cada contexto, evitando uma segunda matriz Sonar hardcoded no workflow.

Matriz de paths para ownership Sonar em PR:

| Paths | Contexto Sonar |
| --- | --- |
| `src/ledger/**`, `tests/ledger/**`, `LedgerService.slnx`, `tools/ComposeEnvGen/**` | `ledger` |
| `src/balance/**`, `tests/balance/**`, `BalanceService.slnx` | `balance` |
| `src/transfer/**`, `tests/transfer/**`, `TransferService.slnx` | `transfer` |
| `src/identity/**`, `tests/identity/**`, `IdentityService.slnx` | `identity` |
| `src/audit/**`, `tests/audit/**`, `AuditService.slnx` | `audit` |
| `src/Shared/**`, `tests/Shared/**`, `PocArquitetura.Shared.slnx`, `src/Shared/Directory.*` | `shared` |
| `global.json`, `NuGet.config`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig`, `.globalconfig`, `coverlet.runsettings`, `.github/actions/setup-dotnet/**`, `.github/workflows/dotnet.yml`, `.github/workflows/sonarqube-context.yml`, `scripts/quality/sonar-contexts.json`, `scripts/quality/sonar_context.py`, `scripts/quality/sonar_context_impact.py`, `scripts/quality/sonarqube_cloud_report.py`, `scripts/quality/sonarqube_context_summary.py` | todos |
| `contracts/events/**` | nenhum ownership Sonar contextual automatico |
| `docs/**`, `*.md`, imagens de documentacao | nenhum |

Mudancas em Shared executam apenas o projeto Sonar `shared` em PR. Os consumidores nao sao propagados automaticamente porque a execucao completa em `main` mantem dashboards de Ledger, Balance, Transfer, Identity e Audit atualizados sem duplicar ownership de source compartilhado.

Mudancas em `contracts/events/**` continuam separadas conceitualmente: a validacao de schema fica no workflow `event-contract-validation`, e o build/test impactante e coberto pelo gate `pr-build-and-test` via solution agregadora. Esses paths nao selecionam Ledger ou Balance para analise Sonar contextual, evitando atribuir artificialmente ownership Sonar dos contratos a dois projetos.

Os contratos versionados em `contracts/events/**` tem ownership compartilhado de governanca de contratos, nao ownership Sonar de um contexto unico. O workflow `event-contract-validation` valida JSON Schemas e exemplos em `pull_request`, `push` para `main` e `workflow_dispatch` quando paths relacionados mudam. O gate `pr-build-and-test` continua validando build/test conforme a classificacao do PR. Como os schemas nao entram em projeto Sonar contextual automatico, evita-se duplicar issues e LOC de contrato em Ledger e Balance.

## Configuracao no GitHub

Crie o secret do repositorio em:

```text
Settings > Secrets and variables > Actions > New repository secret > SONAR_TOKEN
```

O workflow le o token exclusivamente de `secrets.SONAR_TOKEN` e falha com mensagem clara quando o secret esta ausente ou vazio.

O modelo atual documentado e token unico no secret `SONAR_TOKEN`, com permissao de Execute Analysis para os projetos envolvidos. O valor nunca deve ser replicado em documentacao, logs ou arquivos versionados.

Trade-offs do token unico:

- escopo operacional simples para a POC e para a matrix de seis contextos;
- blast radius maior que tokens por contexto, porque um vazamento permitiria analise nos projetos cobertos pelo token;
- ownership centralizado na configuracao de Actions do repositorio;
- rotacao feita substituindo o secret `SONAR_TOKEN` no GitHub e revogando o token anterior no SonarQube Cloud.

Tokens por contexto podem reduzir blast radius no futuro, mas aumentam quantidade de secrets, matriz de configuracao e manutencao. Nao foram adotados nesta etapa porque nao ha evidencia de necessidade operacional na POC. SOT/PAT de administracao nao devem ser usados pelo workflow de analise; se existirem para operacao manual, ficam fora do pipeline e seguem politica de menor privilegio.

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

Cada job contextual preserva o mesmo boundary:

1. checkout com historico completo (`fetch-depth: 0`);
2. setup .NET e restore das tools locais;
3. validacao da configuracao recebida contra `scripts/quality/sonar-contexts.json`;
4. restore da solution contextual;
5. validacao de `SONAR_TOKEN`;
6. SonarQube Cloud begin com o Project Key contextual;
7. build da solution contextual;
8. testes da solution contextual com cobertura no diretorio contextual;
9. validacao de Cobertura e OpenCover contextuais;
10. SonarQube Cloud end com `sonar.qualitygate.wait=true`;
11. geracao do report da API no diretorio Sonar contextual;
12. geracao de summary contextual de cobertura sem aplicar threshold;
13. upload do artifact contextual.

O build usado por cada analise acontece dentro do boundary do scanner daquele contexto. Cada job roda em workspace isolado; nao ha duas analises Sonar abertas no mesmo workspace.

## Cobertura de testes

O arquivo `coverlet.runsettings` gera dois formatos:

- `coverage.cobertura.xml`, usado pelo ReportGenerator, pelo resumo de cobertura e pelo gate local;
- `coverage.opencover.xml`, importado pelo SonarQube Cloud.

O parametro usado pelo scanner e:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/**/coverage.opencover.xml"
```

Nao use cobertura generica do Sonar para este caso. Para C#/.NET, a importacao deve usar `sonar.cs.opencover.reportsPaths` apontando para os arquivos OpenCover gerados pelo Coverlet.

Para analises contextuais, use o padrao isolado do respectivo contexto, por exemplo:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/transfer/**/coverage.opencover.xml"
```

Evite glob global em analises contextuais para nao importar cobertura de outra solution.

No contexto Transfer, o scanner usa somente:

```text
sonar.cs.opencover.reportsPaths="./artifacts/test-results/transfer/**/coverage.opencover.xml"
```

O reusable workflow falha se encontrar `coverage.opencover.xml` fora do diretorio contextual dentro do workspace do job. Isso protege a validacao contra contaminacao por cobertura global ou de outro contexto.

Os contextos de servico excluem `src/Shared/**` e `tests/Shared/**` de `sonar.exclusions`; o contexto `shared` e o dono explicito da analise desses sources. Essa regra evita duplicar ownership de Shared em Ledger, Balance, Transfer, Identity e Audit enquanto preserva a analise independente de `PocArquitetura.Shared.slnx`.

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

Os seis projetos contextuais devem iniciar com o mesmo Quality Gate e New Code Definition coerente. A aplicacao e independente por projeto: uma falha de Quality Gate em Ledger nao altera o dashboard de Balance, mas falha o job contextual correspondente. A strategy da matrix usa `fail-fast: false`, entao uma falha contextual nao cancela os demais contextos; isso preserva diagnostico completo no summary.

Nao ajuste thresholds remotamente como parte de manutencao de YAML. Divergencias de Quality Gate, New Code Definition ou regras entre projetos devem ser registradas e corrigidas no SonarQube Cloud como uma decisao operacional explicita.

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

O workflow publica o artifact global `test-results-coverage-and-sonarqube` por 7 dias quando a analise global roda.

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

Os jobs contextuais publicam artifacts independentes por 7 dias:

- `sonar-ledger`;
- `sonar-balance`;
- `sonar-transfer`;
- `sonar-identity`;
- `sonar-audit`;
- `sonar-shared`.

Cada artifact contem:

- resultados `.trx` do contexto;
- `coverage.cobertura.xml` do contexto;
- `coverage.opencover.xml` do contexto;
- `coverage-report/Summary.json`;
- `coverage-report/Summary.txt`;
- `quality-gate.json`;
- `measures.json`;
- `issues.json`;
- `sonarqube-cloud-report.md`;
- `report.md`.

O job final `sonar-summary` baixa os artifacts `sonar-*` disponiveis e publica uma tabela consolidada com Contexto, Execucao, Quality Gate, Coverage, Bugs, Vulnerabilities e Code Smells. O resumo distingue:

- `PASSED`: Quality Gate remoto OK;
- `FAILED`: Quality Gate remoto reprovado;
- `SKIPPED`: contexto nao selecionado nesta execucao;
- `UNAVAILABLE`: artifact ou API indisponivel.

Contexto pulado deve aparecer como `SKIPPED`, nao como sucesso. API ou artifact indisponivel deve aparecer como `UNAVAILABLE`, nao como zero, para evitar falso sinal de ausencia de bugs, vulnerabilidades ou code smells.

O summary e diagnostico; a falha de qualquer job contextual continua falhando o workflow por meio do resultado normal da matrix.

O relatorio do GitHub Actions e apenas um snapshot da execucao do CI. Ele facilita triagem no proprio workflow, mas nao substitui o dashboard oficial do SonarQube Cloud, que continua sendo a fonte principal para historico, detalhes navegaveis, configuracao de quality gate, regras, tendencias e estado mais recente do projeto.

## Workflow atual

O workflow `main-dotnet-ci` roda em:

- `push` para `main`;
- `pull_request` para `main`;
- `workflow_dispatch`.

As permissoes declaradas sao minimas para leitura do repositorio e contexto do pull request:

- `contents: read`;
- `pull-requests: read`.

O job global publica o artifact `test-results-coverage-and-sonarqube` por 7 dias com resultados `.trx`, arquivos `coverage.cobertura.xml`, arquivos `coverage.opencover.xml`, summaries do ReportGenerator e o snapshot resumido do SonarQube Cloud. Ele fica restrito a `push` para `main` e `workflow_dispatch` durante a janela de comparacao.

A matrix contextual roda em:

- `pull_request` para `main`, respeitando os `paths-ignore` documentais e executando apenas os contextos impactados;
- `workflow_dispatch`, executando todos os contextos ou um contexto selecionado pelo input `sonar_context`;
- `push` para `main`, executando todos os seis contextos.

Em `push` para `main` nao ha seletividade: os seis contextos rodam para manter dashboards, historico e Quality Gates recentes mesmo quando algum PR anterior executou apenas parte da matriz.

## Projeto global

Decisao atual: `MANTER TEMPORARIAMENTE`.

Justificativa:

- o projeto global duplica parte dos sources e issues ja atribuiveis aos projetos contextuais;
- a duplicacao permanente piora clareza de ownership, LOC contabilizada e custo de analise;
- o dashboard global ainda tem valor temporario para comparar baseline, cobertura consolidada e historico durante a transicao;
- `tests/Architecture.Tests` nao justifica criar ou preservar um projeto Sonar global permanente, porque a validacao arquitetural continua melhor representada como gate de teste global;
- as execucoes remotas acessiveis mais recentes ainda mostram o workflow global anterior, sem evidencia publica suficiente de uma execucao contextual estavel em PR, `main` e `workflow_dispatch`.

O destino preferencial e aposentar a analise global depois de evidenciar:

- pelo menos uma execucao de PR contextual simples com summary correto;
- pelo menos uma execucao de PR multi-contexto com matrix correta;
- pelo menos uma execucao completa em `main` com os seis contextos e artifacts `sonar-*`;
- pelo menos uma execucao `workflow_dispatch` com `all` ou contexto especifico;
- comportamento observado de falha de Quality Gate em job contextual;
- ausencia conhecida de contaminacao de cobertura fora de `artifacts/test-results/{context}`.

Quando esses criterios estiverem atendidos, remova a analise global do workflow em uma mudanca propria, atualize badges/documentacao e preserve o historico remoto conforme as opcoes da plataforma. Nao exclua projeto remoto automaticamente pelo CI.

Enquanto o projeto global existir, compare o artifact global `test-results-coverage-and-sonarqube`, os artifacts contextuais `sonar-*` e os dashboards SonarQube Cloud dos projetos:

- global: `rodri-oliveira-dev_poc-arquitetura`;
- contextuais: `rodri-oliveira-dev_poc-arquitetura-ledger`, `rodri-oliveira-dev_poc-arquitetura-balance`, `rodri-oliveira-dev_poc-arquitetura-transfer`, `rodri-oliveira-dev_poc-arquitetura-identity`, `rodri-oliveira-dev_poc-arquitetura-audit` e `rodri-oliveira-dev_poc-arquitetura-shared`.

Pontos de comparacao:

- arquivos source atribuidos a cada projeto;
- cobertura importada em cada contexto versus cobertura global;
- issues abertas e severidade;
- metricas principais em `measures.json`;
- status e condicoes do Quality Gate;
- tempo do job global versus jobs contextuais;
- runner-minutes observaveis na execucao do GitHub Actions;
- existencia de source duplicado inesperado;
- ausencia de cobertura contaminada fora de `artifacts/test-results/{context}`.

Os percentuais de cobertura nao precisam ser iguais. O projeto global usa a solution agregadora e tem denominador maior; cada projeto contextual usa sua solution, testes e fontes atribuiveis ao proprio contexto.

## Onboarding de novo contexto

Processo minimo para adicionar um novo contexto Sonar:

1. Criar ou atualizar a solution `.slnx` do contexto.
2. Criar o projeto no SonarQube Cloud.
3. Definir `projectKey` e `projectName`.
4. Desabilitar Automatic Analysis no projeto.
5. Configurar Quality Gate e New Code Definition coerentes com os demais projetos.
6. Adicionar o contexto em `scripts/quality/sonar-contexts.json`.
7. Definir paths de ownership em `scripts/quality/sonar_context_impact.py`.
8. Definir `resultsDir` contextual em `artifacts/test-results/{context}`.
9. Definir artifact `sonar-{context}`.
10. Validar um PR que impacte somente esse contexto.
11. Validar uma execucao em `main`.
12. Atualizar esta documentacao e referencias de coverage/artifacts quando necessario.

Nao adicione projeto Sonar novo para testes transversais sem ownership de source produtivo. Use a solution agregadora e os gates globais para validacoes arquiteturais.

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

### Erro: project not found

Causa:

O `projectKey` contextual ainda nao existe no SonarQube Cloud, foi digitado incorretamente em `scripts/quality/sonar-contexts.json` ou o token nao tem acesso ao projeto.

Correcao:

Crie ou corrija o projeto remoto, mantenha Automatic Analysis desabilitada e confirme permissao de Execute Analysis para o token usado pelo workflow.

### Erro: token unauthorized

Causa:

`SONAR_TOKEN` existe, mas nao possui permissao para o projeto ou foi revogado.

Correcao:

Rotacione o token no SonarQube Cloud, atualize o secret `SONAR_TOKEN` no GitHub Actions e reexecute o workflow.

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

Em analise contextual, o caminho deve apontar para o contexto, como `./artifacts/test-results/ledger/**/coverage.opencover.xml`.

### Erro: Quality Gate timeout

Causa:

O scanner aguardou a avaliacao remota por causa de `sonar.qualitygate.wait=true`, mas o SonarQube Cloud nao respondeu dentro do tempo esperado.

Correcao:

Reexecute o job se houver incidente temporario no servico. Se for recorrente, registre evidencia antes de avaliar timeout maior ou mudanca de estrategia.

### Relatorio API indisponivel

Causa:

`scripts/quality/sonarqube_cloud_report.py` nao conseguiu consultar a API por token ausente, autorizacao, indisponibilidade ou falta de dados do PR/projeto.

Correcao:

Use o dashboard do SonarQube Cloud como fonte principal e o artifact gerado como diagnostico. No summary consolidado, esse caso deve aparecer como `UNAVAILABLE`, nunca como zero.

### Projeto skipped

Causa:

O contexto nao foi selecionado pelo PR ou por `workflow_dispatch` com contexto especifico.

Correcao:

Verifique o resumo do job `Detect SonarQube contextual impact`. `SKIPPED` e um estado esperado para contexto nao impactado; nao trate como sucesso de Quality Gate.

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
