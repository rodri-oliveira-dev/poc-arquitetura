# Validacao de Pull Requests e checks obrigatorios

Pull requests para qualquer branch sao validados pelo workflow `pr-build-and-test`, definido em `.github/workflows/pull-request-validation.yml`.

O workflow sempre e iniciado para PRs, sem `paths-ignore`, para que o check obrigatorio seja criado tambem em PRs documentais. No inicio da execucao ele detecta os arquivos alterados do PR.

Quando o PR altera apenas documentacao ou imagens de documentacao, o workflow registra um resumo e pula restore, build e testes. Sao tratados como documentais:

- arquivos em `docs/**`;
- arquivos `*.md`;
- imagens `*.png`, `*.jpg`, `*.jpeg`, `*.gif`, `*.svg` e `*.webp`.

Isso evita required check pendente em PRs que o GitHub poderia ignorar por filtro de arquivos e reduz custo de execucao em mudancas que nao impactam codigo.

Quando ha arquivos de codigo, configuracao ou automacao fora desse conjunto, o workflow classifica o impacto antes de executar restore, build e testes:

- mudancas apenas em `src/Shared/**`, `tests/Shared/**` ou `PocArquitetura.Shared.slnx` validam `./PocArquitetura.Shared.slnx`;
- mudancas apenas em servicos, testes de servico, `tests/Architecture.Tests/**`, `PocArquitetura.slnx` ou alguma solution de contexto de servico validam `./PocArquitetura.slnx`;
- mudancas que combinam Shared e servicos validam as duas solutions;
- mudancas globais validam as duas solutions;
- quando a deteccao de arquivos falha ou encontra arquivo impactante nao classificado, o workflow valida as duas solutions por seguranca.

Arquivos globais para o gate de PR:

- `global.json`;
- `NuGet.config`;
- `Directory.Build.props`;
- `Directory.Packages.props`;
- `.github/actions/setup-dotnet/**`;
- `.github/workflows/**`;
- `test.sh`;
- `test.ps1`.

Para cada solution impactada, o workflow executa:

- `dotnet restore`;
- `dotnet build --configuration Release --no-restore`;
- `dotnet test --configuration Release --no-build --no-restore`.

Ele nao executa verificacao de vulnerabilidades, cobertura ou publicacao de relatorios, e nao chama `test.sh`. Ele executa a suite de testes sem filtro, portanto inclui testes `Integration`, `Container` e `Contract` quando existirem e quando o runner disponibilizar as dependencias esperadas. As demais responsabilidades continuam nos workflows especificos, como `dependency-security-review`, `main-dotnet-ci` e `infra-security-and-terraform-validation`.

O workflow roda em:

- `pull_request` para qualquer branch;
- `merge_group`, quando a Merge Queue estiver habilitada;
- `workflow_dispatch`, para execucao manual.

Como este check deve ser obrigatorio para merge, ele nao usa `paths-ignore`. A otimizacao de PR documental acontece dentro do job, preservando a existencia do status check.

## Bloqueio de merge

Workflow nao bloqueia merge sozinho. Para impedir merge quando build ou testes falharem, configure a protecao da branch `main` no GitHub exigindo o status check:

```text
Build and test
```

Configuracao recomendada em `Settings > Branches > Branch protection rules` ou em `Settings > Rules > Rulesets`:

- exigir pull request antes do merge;
- exigir status checks passarem antes do merge;
- selecionar o check `Build and test`;
- exigir branch atualizada antes do merge, se o fluxo do repositorio usar essa politica;
- bloquear push direto na `main`, exceto para administracao operacional explicita.

O workflow `main-dotnet-ci` permanece como validacao completa de `push` na `main`, `pull_request` para `main` e execucao manual, incluindo cobertura, SonarQube Cloud e relatorios.

## Acoes compostas internas

Para reduzir repeticao entre workflows, o repositorio centraliza setup mecanico em composite actions locais:

- `.github/actions/setup-dotnet`: configura o SDK pelo `global.json`, define `NUGET_PACKAGES` para cache local do runner, restaura cache NuGet e pode executar `dotnet tool restore` quando o workflow precisa de ferramentas locais;
- `.github/actions/setup-node`: configura Node.js 22 com cache npm e executa `npm ci`;
- `.github/actions/trivy-repository-scan`: executa os scans Trivy de configuracao e filesystem usados pela validacao de infraestrutura.

Os workflows continuam mantendo explicitos os comandos de negocio do pipeline, como `dotnet restore`, `dotnet build`, `dotnet test`, geracao OpenAPI, Stryker, ZAP e validacao Terraform. A abstracao fica restrita ao setup repetitivo para preservar legibilidade e evitar overengineering.

## Matriz de workflows

| Workflow | Arquivo | Evento | Papel | Bloqueante / informativo / operacional |
| --- | --- | --- | --- | --- |
| `pr-build-and-test` | `.github/workflows/pull-request-validation.yml` | `pull_request`, `merge_group`, `workflow_dispatch` | Gate minimo de PR. Em PR documental, cria o status check e pula restore/build/test. Em PR impactante, escolhe `PocArquitetura.slnx`, `PocArquitetura.Shared.slnx` ou ambas conforme arquivos alterados. | Bloqueante |
| `main-dotnet-ci` | `.github/workflows/dotnet.yml` | `push` na `main`, `pull_request` para `main`, `workflow_dispatch` | Validacao completa da solution principal, com restore, vulnerabilidades NuGet, SonarQube Cloud, build, testes, cobertura, gate de 85% e artifacts de diagnostico. Mudancas exclusivas em Shared nao disparam este workflow; a validacao Shared fica no gate de PR e no workflow dedicado de publish. | Bloqueante se exigido por branch protection/ruleset |
| `dependency-security-review` | `.github/workflows/dependency-review.yml` | `pull_request` para `main` | Revisa dependencias alteradas no PR e falha para vulnerabilidades `moderate` ou superior. | Bloqueante se exigido por branch protection/ruleset |
| `codeql-security-analysis` | `.github/workflows/codeql.yml` | `push` na `main`, `pull_request` para `main`, `schedule` semanal | Analise estatica de seguranca C# via CodeQL. | Bloqueante se exigido por branch protection/ruleset |
| `pr-advisory-checks` | `.github/workflows/pr-advisory-checks.yml` | `pull_request` | Publica recomendacoes de analyzers para arquivos C# alterados. Escolhe `PocArquitetura.slnx`, `PocArquitetura.Shared.slnx` ou ambas pela mesma separacao Shared/servicos/globais do gate de PR. O resultado e advisory e nao bloqueia o PR. | Informativo |
| `event-contract-validation` | `.github/workflows/event-contracts.yml` | `pull_request`, `push` na `main`, `workflow_dispatch` quando ha mudancas em contratos, exemplos, docs e tooling de eventos | Valida JSON Schemas e exemplos versionados dos eventos. | Bloqueante se exigido por branch protection/ruleset |
| `openapi-contract-validation` | `.github/workflows/openapi-contracts.yml` | `pull_request`, `push` na `main`, `workflow_dispatch` quando ha mudancas em APIs, contratos OpenAPI ou tooling relacionado | Gera, linta, compara breaking changes e valida drift dos contratos OpenAPI. Mudancas exclusivas em Shared nao disparam o workflow, exceto `src/Shared/ApiDefaults/**`, que pode alterar Swagger, autenticacao, headers, middlewares ou comportamento observavel das APIs. | Bloqueante se exigido por branch protection/ruleset |
| `infra-security-and-terraform-validation` | `.github/workflows/terraform-validation.yml` | `pull_request` e `push` para `main` quando ha mudancas em `infra/terraform/**`, Dockerfiles, Compose, `.github/actions/trivy-repository-scan/**` ou no proprio workflow; `workflow_dispatch` | Executa Trivy para Dockerfile, Terraform, misconfigurations, secrets e filesystem; depois executa `fmt -check`, `init -backend=false`, `validate` e TFLint. Nao executa `plan`, `apply` nem `destroy`; o `init` sem backend e apenas validacao sintatica sem credenciais. Plan real no CI deve inicializar o backend remoto GCS e nao usar `-lock=false`. | Bloqueante se exigido por branch protection/ruleset |
| `mutation-tests` | `.github/workflows/mutation-tests.yml` | `push` na `main`, `workflow_dispatch` | Diagnostico de qualidade por Stryker.NET para alvos de servico com relatorios HTML publicados como artifacts. Mudancas exclusivas em Shared nao disparam mutation de servicos; mutation de Shared exige decisao futura explicita. Nao roda em PR e nao deve virar gate obrigatorio sem decisao explicita. | Informativo |
| `smoke-load-tests` | `.github/workflows/loadtests-smoke.yml` | `workflow_dispatch` | Executa testes k6 smoke contra a stack local inicializada no runner. | Operacional/manual |
| `owasp-zap-baseline` | `.github/workflows/owasp-zap.yml` | `workflow_dispatch` | Executa OWASP ZAP baseline contra LedgerService.Api e BalanceService.Api em stack HTTP controlada no runner e publica relatorios como artifacts. Nao roda em PR. | Operacional/manual |
| `architecture-pages` | `.github/workflows/pages-architecture.yml` | `push` na `main`, `pull_request` para `main`, `workflow_dispatch` quando ha mudancas em `docs/architecture/**` ou `.github/workflows/pages-architecture.yml` | Build LikeC4 em PRs afetados e publicacao da documentacao arquitetural no GitHub Pages apos merge/manual. | Operacional |
| `release-on-merge` | `.github/workflows/release.yml` | `pull_request` fechado para `main` quando o PR foi mergeado | Cria tag e GitHub Release a partir do merge do PR. Nao repete build/testes. | Operacional |

O required check recomendado para proteger a `main` e `Build and test`, job do workflow `pr-build-and-test`.

`main-dotnet-ci` continua sendo a validacao completa e alimenta os badges de build/testes do README por meio do arquivo `.github/workflows/dotnet.yml`.

## Cuidados antes de renomear checks

Renomear workflows, jobs ou arquivos usados por badges pode quebrar required status checks externos ou deixar badges apontando para execucoes antigas/inexistentes.

Antes de renomear qualquer um destes itens, revise a configuracao externa do GitHub e a documentacao do repositorio:

- job `Build and test` em `.github/workflows/pull-request-validation.yml`;
- workflow `pr-build-and-test`;
- workflow `main-dotnet-ci` e arquivo `.github/workflows/dotnet.yml`;
- workflow `architecture-pages` e arquivo `.github/workflows/pages-architecture.yml`;
- workflow `mutation-tests` e arquivo `.github/workflows/mutation-tests.yml`;
- badges e links no `README.md`;
- referencias em `docs/development`, `docs/architecture` e ADRs relacionadas.

Se o job `Build and test` for renomeado, a branch protection ou ruleset que exige esse status precisa ser atualizada no GitHub antes do merge da alteracao. Se `main-dotnet-ci`, `architecture-pages` ou seus arquivos forem renomeados, revise tambem os badges do README que apontam para `dotnet.yml` e `pages-architecture.yml`.

Nao promova `mutation-tests` para gate obrigatorio apenas por renomeacao ou ajuste operacional. Essa mudanca exige decisao explicita, porque aumenta tempo de feedback e pode bloquear merges por uma validacao originalmente informativa.

Nao promova `owasp-zap-baseline` para gate obrigatorio apenas por existir workflow manual. DAST exige ambiente alvo estavel, criterio de triagem, politica para falsos positivos e severidades bloqueantes antes de entrar em branch protection ou ruleset.

## Pinagem de GitHub Actions

As actions externas usadas em `.github/workflows/` e `.github/actions/` devem ser referenciadas por SHA completo de commit, mantendo um comentario com a ref semantica original, por exemplo `# v4`.

Para atualizar uma action:

1. Consulte o repositorio oficial da action, sem usar forks.
2. Obtenha o commit completo da tag ou branch semantica usada pelo comentario, por exemplo `git ls-remote https://github.com/actions/checkout.git refs/tags/v4 refs/tags/v4^{}` ou `git ls-remote https://github.com/actions/dependency-review-action.git refs/heads/v4`.
3. Substitua somente o SHA, preservando o owner, repositorio, subaction e comentario da versao original.
4. Revise o diff para confirmar que nao houve mudanca de nomes de workflows, nomes de jobs, `paths`, permissoes, artifacts ou logica de execucao.

## Configuracao externa esperada no GitHub

A protecao de branch e os rulesets sao configuracoes externas ao repositorio. O YAML cria os checks, mas nao impede merge sozinho.

Checklist esperado em `Settings > Branches > Branch protection rules` ou `Settings > Rules > Rulesets`:

- proteger a branch `main`;
- exigir pull request antes do merge;
- exigir que status checks passem antes do merge;
- selecionar o required check `Build and test`;
- preservar checks de seguranca ja exigidos, como `dependency-security-review` ou `codeql-security-analysis`, quando estiverem configurados;
- exigir branch atualizada antes do merge, se essa for a politica operacional do repositorio;
- bloquear push direto na `main`, exceto para administracao operacional explicita;
- revisar a regra sempre que workflow, job ou arquivo usado em badge/status check for renomeado.
