# Validacao de Pull Requests e checks obrigatorios

Pull requests, Merge Queue, pushes na `main` e execucoes manuais sao validados pelo workflow `main-dotnet-ci`, definido em `.github/workflows/dotnet.yml`.

O workflow sempre e iniciado para PRs, sem `paths-ignore`, para que o check obrigatorio seja criado tambem em PRs documentais. No inicio da execucao ele detecta os arquivos alterados usando `scripts/ci/detect-dotnet-impact.py`, a fonte unica da classificacao .NET do CI.

O job mantem o nome estavel:

```text
Build and test
```

Esse e o status check recomendado para branch protection/rulesets. Em PR documental, o job fica verde e registra no summary que restore, auditoria NuGet, SonarQube, build, testes e cobertura foram ignorados.

## Eventos

O CI principal roda em:

- `pull_request`;
- `merge_group` com `checks_requested`;
- `push` na branch `main`;
- `workflow_dispatch`.

Em `pull_request`, a lista de arquivos vem da API do GitHub e passa pelo detector centralizado. Em `merge_group`, `push` na `main` e `workflow_dispatch`, o comportamento e conservador: aggregate e Shared sao validados.

## Detecao de impacto

Sao tratados como documentais:

- arquivos em `docs/**`;
- arquivos `*.md`;
- imagens `*.png`, `*.jpg`, `*.jpeg`, `*.gif`, `*.svg` e `*.webp`.

Quando ha arquivos de codigo, configuracao ou automacao fora desse conjunto, o workflow classifica o impacto antes de executar restore, auditoria, SonarQube, build, testes e cobertura:

- mudancas apenas em `src/Shared/**`, `tests/Shared/**` ou `PocArquitetura.Shared.slnx` validam somente `./PocArquitetura.Shared.slnx`;
- mudancas apenas em servicos, testes de servico, `tests/Architecture.Tests/**`, `tools/**`, `PocArquitetura.slnx` ou alguma solution de contexto de servico validam somente `./PocArquitetura.slnx`;
- mudancas globais validam `./PocArquitetura.slnx` e `./PocArquitetura.Shared.slnx`;
- quando a deteccao de arquivos falha, o workflow valida aggregate e Shared por seguranca.

Os servicos classificados para a solution agregada sao `audit`, `balance`, `identity`, `ledger`, `payment` e `transfer`.

Arquivos globais para o CI principal:

- `global.json`;
- `NuGet.config`;
- `Directory.Build.props`;
- `Directory.Build.targets`;
- `Directory.Packages.props`;
- `.config/dotnet-tools.json`;
- `dotnet-tools.json`;
- `coverlet.runsettings`;
- `.github/actions/**`;
- `.github/workflows/**`;
- `scripts/ci/**`;
- `scripts/quality/**`;
- `scripts/contracts/openapi/**`;
- `test.sh`;
- `test.ps1`.

Arquivos de workflow e actions tambem sao validados pelo workflow `script-quality`, via `actionlint`, sem forcar a suite completa de scripts quando a mudanca e exclusivamente em `.github/workflows/**` ou `.github/actions/**`.

Arquivos `.sln` e `.slnx` tambem sao tratados como globais por padrao, exceto quando a regra mais especifica da solution Shared ou de uma solution de servico ja classifica o impacto.

## Validacoes executadas

Para cada contexto impactado, o workflow executa:

- `dotnet restore` em Release com `NuGetAuditMode=all`;
- `dotnet list package --vulnerable --include-transitive` e bloqueio para severidades `moderate`, `high` e `critical`;
- SonarQube Cloud begin/end, quando o token esta disponivel e o evento pode acessar secrets com seguranca;
- `dotnet build --configuration Release --no-restore`;
- `dotnet test --configuration Release --no-build` com `coverlet.runsettings`;
- validacao de arquivos Cobertura e OpenCover;
- ReportGenerator;
- gate de cobertura conforme o contexto validado.

No contexto `aggregate`, a cobertura total de linhas precisa atingir 85% e os assemblies `LedgerService.Worker` e `BalanceService.Worker` tambem precisam atingir 85%. No contexto `shared`, o gate de cobertura total usa o baseline minimo de 40% enquanto a suite Shared e elevada gradualmente; o Quality Gate do SonarQube Cloud Shared continua obrigatorio.

Pull requests vindos de forks nao recebem `SONAR_TOKEN`. Nesses casos, a analise SonarQube Cloud e ignorada para nao expor secrets a codigo nao confiavel, mas restore, auditoria NuGet, build, testes e cobertura continuam rodando.

Em PRs internos, `push` na `main`, Merge Queue e execucao manual, a ausencia de `SONAR_TOKEN` falha o job quando ha impacto .NET.

## SonarQube e cobertura

A estrategia oficial e contextual por aggregate e Shared dentro de um unico workflow:

| Contexto | Solution | Projeto SonarQube Cloud | Resultados |
| --- | --- | --- | --- |
| `aggregate` | `./PocArquitetura.slnx` | `rodri-oliveira-dev_poc-arquitetura` | `artifacts/test-results/aggregate`, `artifacts/sonarqube/aggregate` |
| `shared` | `./PocArquitetura.Shared.slnx` | `rodri-oliveira-dev_poc-arquitetura-shared` | `artifacts/test-results/shared`, `artifacts/sonarqube/shared` |

O workflow reutilizavel `.github/workflows/sonarqube-context.yml` foi removido. Sonar begin/end, cobertura, ReportGenerator, relatorio e upload de artifacts existem apenas em `.github/workflows/dotnet.yml`.

## Matriz de workflows

| Workflow | Arquivo | Evento | Papel | Bloqueante / informativo / operacional |
| --- | --- | --- | --- | --- |
| `main-dotnet-ci` | `.github/workflows/dotnet.yml` | `pull_request`, `merge_group`, `push` na `main`, `workflow_dispatch` | CI principal com deteccao de impacto, auditoria NuGet, SonarQube Cloud, build, testes, cobertura e artifacts. Em PR documental, produz check verde sem build. | Bloqueante |
| `dependency-security-review` | `.github/workflows/dependency-review.yml` | `pull_request` para `main` | Revisa dependencias alteradas no PR e falha para vulnerabilidades `moderate` ou superior. | Bloqueante se exigido por branch protection/ruleset |
| `codeql-security-analysis` | `.github/workflows/codeql.yml` | `push` na `main`, `pull_request` para `main`, `schedule` semanal | Analise estatica de seguranca C# via CodeQL. | Bloqueante se exigido por branch protection/ruleset |
| `pr-advisory-checks` | `.github/workflows/pr-advisory-checks.yml` | `pull_request` | Publica recomendacoes de analyzers para arquivos C# alterados usando a mesma separacao Shared/servicos/globais. | Informativo |
| `event-contract-validation` | `.github/workflows/event-contracts.yml` | `pull_request`, `push` na `main`, `workflow_dispatch` quando ha mudancas em contratos, exemplos, docs e tooling de eventos | Valida JSON Schemas e exemplos versionados dos eventos. | Bloqueante se exigido por branch protection/ruleset |
| `openapi-contract-validation` | `.github/workflows/openapi-contracts.yml` | `pull_request`, `push` na `main`, `workflow_dispatch` quando ha mudancas em APIs, contratos OpenAPI ou tooling relacionado | Gera, linta, compara breaking changes e valida drift dos contratos OpenAPI. | Bloqueante se exigido por branch protection/ruleset |
| `infrastructure-security` | `.github/workflows/infrastructure-security.yml` | `pull_request` e `push` para `main` quando ha mudancas em infraestrutura coberta; `workflow_dispatch` | Executa Trivy para Dockerfile, Compose, Terraform, misconfigurations, secrets e filesystem. | Bloqueante se exigido por branch protection/ruleset |
| `terraform-validation` | `.github/workflows/terraform-validation.yml` | `pull_request` e `push` para `main` quando ha mudancas Terraform cobertas; `workflow_dispatch` | Executa `fmt -check`, `init -backend=false`, `validate` e TFLint. | Bloqueante se exigido por branch protection/ruleset |
| `container-baseline` | `.github/workflows/container-baseline.yml` | `pull_request` e `push` para `main` quando ha mudancas de container cobertas; `workflow_dispatch` | Valida Compose, estrutura de containers e build da stack base. | Bloqueante se exigido por branch protection/ruleset |
| `script-quality` | `.github/workflows/script-quality.yml` | `pull_request` e `push` para `main` quando ha mudancas em `scripts/**`, workflows, composite actions ou tooling Node; `workflow_dispatch` | Valida scripts por impacto e valida workflows/composite actions com `actionlint` fixado e verificado por SHA256. | Bloqueante se exigido por branch protection/ruleset |
| `mutation-tests` | `.github/workflows/mutation-tests.yml` | `workflow_run` apos sucesso do `main-dotnet-ci` na `main`, `workflow_dispatch` | Mutation testing informativo para alvos de servico, usando o SHA validado pelo CI. | Informativo |
| `publish-shared-nuget` | `.github/workflows/publish-shared-nuget.yml` | `workflow_run` apos sucesso do `main-dotnet-ci` na `main`, `workflow_dispatch` com input `publish` | Empacota e valida os pacotes Shared; publica automaticamente apenas quando o SHA aprovado alterou entradas Shared relevantes, ou manualmente quando `publish=true`. | Operacional |
| `smoke-load-tests` | `.github/workflows/loadtests-smoke.yml` | `workflow_dispatch` | Executa testes k6 smoke contra stack local no runner. | Operacional/manual |
| `owasp-zap-baseline` | `.github/workflows/owasp-zap.yml` | `workflow_run` apos sucesso do `main-dotnet-ci` na `main`, `workflow_dispatch` | Executa OWASP ZAP baseline contra APIs em stack controlada, usando o SHA validado pelo CI. | Operacional/informativo |
| `architecture-pages` | `.github/workflows/pages-architecture.yml` | `push` na `main`, `pull_request` para `main`, `workflow_dispatch` quando ha mudancas de arquitetura | Build LikeC4 em PRs afetados e publicacao da documentacao arquitetural no GitHub Pages. | Operacional |
| `release-on-merge` | `.github/workflows/release.yml` | `workflow_run` apos sucesso do `main-dotnet-ci` na `main` | Cria tag e GitHub Release para o SHA validado pelo CI. Nao repete build/testes e nao depende de ZAP ou mutation. | Operacional |

## Visao final do pipeline

```text
Pull request / merge queue
├── CI .NET
├── advisory analyzers
├── CodeQL
├── dependency review
├── contracts
├── container baseline
├── infrastructure security
├── Terraform validation
└── script/workflow quality

Main CI aprovado
├── release
├── publish Shared NuGet, quando aplicavel
├── OWASP ZAP advisory
└── mutation testing advisory
```

## Matriz de gatilhos

| Area alterada | Workflows esperados |
| --- | --- |
| Codigo .NET de servico, testes de servico, solutions ou build global | `main-dotnet-ci`; `pr-advisory-checks` quando houver C# em PR nao draft |
| Apenas documentacao/imagens de documentacao | `main-dotnet-ci` cria check verde com skip interno; workflows com path especifico rodam somente se seus filtros forem atingidos |
| API HTTP, `ApiDefaults`, gerador OpenAPI, Redocly ou `docs/openapi/**` | `openapi-contract-validation` |
| Dockerfile isolado em `src/**` | `container-baseline` e `infrastructure-security`; nao aciona `openapi-contract-validation` |
| Compose, `.dockerignore`, Dockerfiles, validador de containers ou workflow de container | `container-baseline`; `infrastructure-security` quando coberto pelos filtros de seguranca |
| Terraform | `terraform-validation` e `infrastructure-security` |
| Scripts em `scripts/**` ou tooling Node usado por scripts | job de scripts do `script-quality` |
| Workflows ou composite actions | job `actionlint` do `script-quality`; nao executa toda a suite de scripts por essa razao isolada |
| Titulo ou descricao de PR | Nenhum analyzer por evento `edited`; `pr-advisory-checks` roda apenas em `opened`, `reopened`, `synchronize` e `ready_for_review` |
| PR draft | `pr-advisory-checks` fica pulado; `ready_for_review` preserva a primeira execucao consultiva |

## Matriz de gates

| Categoria | Workflows/checks | Uso recomendado em branch protection/rulesets |
| --- | --- | --- |
| Bloqueante principal | `Build and test` do `main-dotnet-ci` | Required check |
| Bloqueantes de seguranca/contrato/infra | `dependency-security-review`, `codeql-security-analysis`, `openapi-contract-validation`, `event-contract-validation`, `container-baseline`, `infrastructure-security`, `terraform-validation`, `script-quality` | Required quando o repositorio quiser bloquear merges nessas superficies; todos possuem escopo por paths ou skip interno |
| Consultivos | `pr-advisory-checks`, `mutation-tests`, achados do `owasp-zap-baseline` quando `fail_on_alerts=false` | Nao marcar como required sem decisao explicita |
| Operacionais pos-CI | `release-on-merge`, `publish-shared-nuget`, deploy de `architecture-pages` | Nao marcar como required de PR; publicacoes usam `cancel-in-progress: false` |

## Fluxo pos-CI da main

Quando o workflow `main-dotnet-ci` conclui na branch `main`, os workflows `release-on-merge`, `publish-shared-nuget`, `owasp-zap-baseline` e `mutation-tests` recebem o mesmo evento `workflow_run`.

Cada job automatico valida `github.event.workflow_run.conclusion == 'success'`, `github.event.workflow_run.event == 'push'` e `github.event.workflow_run.head_branch == 'main'`. Quando a conclusao e `failure` ou `cancelled`, ou quando o CI aprovado nao veio de push da `main`, os jobs ficam pulados.

Os workflows automaticos fazem checkout do SHA aprovado:

```yaml
ref: ${{ github.event.workflow_run.head_sha }}
```

Matriz esperada:

| CI | Release | NuGet Shared | ZAP | Mutation |
| --- | --- | --- | --- | --- |
| `success` | Inicia e pode criar tag/release para o SHA aprovado, respeitando idempotencia e SemVer. | Detecta arquivos alterados no SHA aprovado; empacota/publica apenas quando entradas Shared relevantes mudaram. | Inicia em paralelo, com alertas consultivos e falhas operacionais vermelhas. | Inicia em paralelo, com score consultivo e falhas operacionais vermelhas. |
| `failure` | Nao executa job automatico. | Nao executa job automatico de pack/publicacao. | Nao executa job automatico. | Nao executa job automatico. |
| `cancelled` | Nao executa job automatico. | Nao executa job automatico de pack/publicacao. | Nao executa job automatico. | Nao executa job automatico. |

`publish-shared-nuget`, `owasp-zap-baseline` e `mutation-tests` nao usam `needs` entre si nem dependem da release. Falhas operacionais desses workflows ficam visiveis em suas proprias runs, mas nao bloqueiam criacao da release. Achados consultivos do ZAP e mutation score nao devem ser tratados como required checks enquanto nao houver decisao explicita de gate.

## Branch protection

Workflow nao bloqueia merge sozinho. Para impedir merge quando CI falhar, configure a protecao da branch `main` no GitHub exigindo o status check:

```text
Build and test
```

Configuracao recomendada em `Settings > Branches > Branch protection rules` ou em `Settings > Rules > Rulesets`:

- exigir pull request antes do merge;
- exigir status checks passarem antes do merge;
- selecionar o check `Build and test`;
- preservar checks de seguranca ja exigidos, como `dependency-security-review` ou `codeql-security-analysis`;
- nao marcar `owasp-zap-baseline`, `mutation-tests` ou `release-on-merge` como required checks enquanto eles forem pos-CI informativos/operacionais;
- exigir branch atualizada antes do merge, se o fluxo do repositorio usar essa politica;
- bloquear push direto na `main`, exceto para administracao operacional explicita.

O nome do check foi preservado, mas sua origem mudou: antes vinha de `.github/workflows/pull-request-validation.yml`; agora vem de `.github/workflows/dotnet.yml`. Se a regra externa estiver presa ao app/workflow antigo alem do nome do check, atualize o ruleset no GitHub.

## Pinagem de GitHub Actions

As actions externas usadas em `.github/workflows/` e `.github/actions/` devem ser referenciadas por SHA completo de commit, mantendo um comentario com a ref semantica original, por exemplo `# v4`.

Para atualizar uma action:

1. Consulte o repositorio oficial da action, sem usar forks.
2. Obtenha o commit completo da tag ou branch semantica usada pelo comentario.
3. Substitua somente o SHA, preservando owner, repositorio, subaction e comentario da versao original.
4. Revise o diff para confirmar que nao houve mudanca de nomes de workflows, nomes de jobs, permissoes, artifacts ou logica de execucao.
