# Validacao de Pull Requests e checks obrigatorios

Pull requests para qualquer branch sao validados pelo workflow `pr-build-and-test`, definido em `.github/workflows/pull-request-validation.yml`.

O workflow sempre e iniciado para PRs, sem `paths-ignore`, para que o check obrigatorio seja criado tambem em PRs documentais. No inicio da execucao ele detecta os arquivos alterados do PR.

Quando o PR altera apenas documentacao ou imagens de documentacao, o workflow registra um resumo e pula restore, build e testes. Sao tratados como documentais:

- arquivos em `docs/**`;
- arquivos `*.md`;
- imagens `*.png`, `*.jpg`, `*.jpeg`, `*.gif`, `*.svg` e `*.webp`.

Isso evita required check pendente em PRs que o GitHub poderia ignorar por filtro de arquivos e reduz custo de execucao em mudancas que nao impactam codigo.

Quando ha qualquer arquivo fora desse conjunto, ou quando a deteccao de arquivos falha, o workflow executa:

- `dotnet restore ./LedgerService.slnx`;
- `dotnet build ./LedgerService.slnx --configuration Release --no-restore`;
- `dotnet test ./LedgerService.slnx --configuration Release --no-build --no-restore`.

Ele nao executa verificacao de vulnerabilidades, cobertura ou publicacao de relatorios, e nao chama `test.sh`. Essas responsabilidades continuam nos workflows especificos, como `dependency-security-review` e `main-dotnet-ci`.

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

O workflow `main-dotnet-ci` permanece como validacao completa de `push` na `main` e execucao manual, incluindo cobertura e relatorios.

## Matriz de workflows

| Workflow | Arquivo | Evento | Papel | Bloqueante / informativo / operacional |
| --- | --- | --- | --- | --- |
| `pr-build-and-test` | `.github/workflows/pull-request-validation.yml` | `pull_request`, `merge_group`, `workflow_dispatch` | Gate minimo de PR. Em PR documental, cria o status check e pula restore/build/test. Em PR com mudanca impactante, executa restore, build e testes sem cobertura e sem recompilar depois do build. | Bloqueante |
| `main-dotnet-ci` | `.github/workflows/dotnet.yml` | `push` na `main`, `workflow_dispatch` | Validacao completa pos-merge/manual, com restore, vulnerabilidades NuGet, build, testes, cobertura, gate de 85% e artifacts de diagnostico. | Informativo para PR; bloqueante apenas se uma regra externa decidir exigir esse check |
| `dependency-security-review` | `.github/workflows/dependency-review.yml` | `pull_request` para `main` | Revisa dependencias alteradas no PR e falha para vulnerabilidades `moderate` ou superior. | Bloqueante se exigido por branch protection/ruleset |
| `codeql-security-analysis` | `.github/workflows/codeql.yml` | `push` na `main`, `pull_request` para `main`, `schedule` semanal | Analise estatica de seguranca C# via CodeQL. | Bloqueante se exigido por branch protection/ruleset |
| `pr-advisory-checks` | `.github/workflows/pr-advisory-review.yml` | `pull_request`, `pull_request_target`, `workflow_dispatch` | Atribui o autor como responsavel do PR quando a API permite e publica recomendacoes de analyzers para arquivos C# alterados. Falhas de atribuicao de responsavel sao registradas como warning e nao bloqueiam o PR. | Informativo |
| `event-contract-validation` | `.github/workflows/event-contracts.yml` | `pull_request`, `push` na `main`, `workflow_dispatch` quando ha mudancas em contratos, exemplos, docs e tooling de eventos | Valida JSON Schemas e exemplos versionados dos eventos. | Bloqueante se exigido por branch protection/ruleset |
| `openapi-contract-validation` | `.github/workflows/openapi-contracts.yml` | `pull_request`, `push` na `main`, `workflow_dispatch` quando ha mudancas em APIs, contratos OpenAPI ou tooling relacionado | Gera, linta, compara breaking changes e valida drift dos contratos OpenAPI. | Bloqueante se exigido por branch protection/ruleset |
| `infra-security-and-terraform-validation` | `.github/workflows/terraform-validation.yml` | `pull_request` e `push` para `main` quando ha mudancas em `infra/terraform/**`, Dockerfiles, Compose ou no proprio workflow; `workflow_dispatch` | Executa Trivy para Dockerfile, Terraform, misconfigurations, secrets e filesystem; depois executa `fmt -check`, `init -backend=false`, `validate` e TFLint. Nao executa `plan`, `apply` nem `destroy`; o `init` sem backend e apenas validacao sintatica sem credenciais. Plan real no CI deve inicializar o backend remoto GCS e nao usar `-lock=false`. | Bloqueante se exigido por branch protection/ruleset |
| `mutation-tests` | `.github/workflows/mutation-tests.yml` | `push` na `main`, `workflow_dispatch` | Diagnostico de qualidade por Stryker.NET com relatorios HTML publicados como artifacts. Nao roda em PR e nao deve virar gate obrigatorio sem decisao explicita. | Informativo |
| `smoke-load-tests` | `.github/workflows/loadtests-smoke.yml` | `workflow_dispatch` | Executa testes k6 smoke contra a stack local inicializada no runner. | Operacional/manual |
| `owasp-zap-baseline` | `.github/workflows/owasp-zap.yml` | `workflow_dispatch` | Executa OWASP ZAP baseline contra LedgerService.Api e BalanceService.Api em stack HTTP controlada no runner e publica relatorios como artifacts. Nao roda em PR. | Operacional/manual |
| `architecture-pages` | `.github/workflows/pages-architecture.yml` | `push` na `main`, `pull_request` para `main`, `workflow_dispatch` quando ha mudancas em `docs/architecture/**` ou `.github/workflows/pages-architecture.yml` | Build LikeC4 em PRs afetados e publicacao da documentacao arquitetural no GitHub Pages apos merge/manual. | Operacional |
| `release-on-merge` | `.github/workflows/release.yml` | `pull_request` fechado para `main` quando o PR foi mergeado | Cria tag e GitHub Release a partir do merge do PR. Nao repete build/testes. | Operacional |

O required check recomendado para proteger a `main` e `Build and test`, job do workflow `pr-build-and-test`.

`main-dotnet-ci` continua sendo a validacao completa pos-merge/manual e alimenta os badges de build/testes do README por meio do arquivo `.github/workflows/dotnet.yml`.

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

As actions usadas em `.github/workflows/` devem ser referenciadas por SHA completo de commit, mantendo um comentario com a ref semantica original, por exemplo `# v4`.

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
