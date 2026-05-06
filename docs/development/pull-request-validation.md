# Validacao de Pull Requests e checks obrigatorios

Pull requests para qualquer branch sao validados pelo workflow `.github/workflows/pull-request-validation.yml`.

O workflow executa:

- `dotnet restore ./LedgerService.slnx`;
- `dotnet build ./LedgerService.slnx --configuration Release --no-restore`;
- `dotnet test ./LedgerService.slnx --configuration Release --no-build`.

Ele nao executa verificacao de vulnerabilidades, cobertura ou publicacao de relatorios. Essas responsabilidades continuam nos workflows especificos, como `dependency-review` e `dotnet-ci`.

O workflow roda em:

- `pull_request` para qualquer branch;
- `merge_group`, quando a Merge Queue estiver habilitada;
- `workflow_dispatch`, para execucao manual.

Como este check deve ser obrigatorio para merge, ele nao usa `paths-ignore`. Isso evita que PRs fiquem bloqueados com required check pendente quando o GitHub pula um workflow por filtro de arquivos.

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

O workflow `dotnet-ci` permanece como validacao completa de `push` na `main` e execucao manual, incluindo cobertura e relatorios.

## Matriz de workflows

| Workflow | Arquivo | Evento | Papel | Classificacao |
| --- | --- | --- | --- | --- |
| `pull-request-validation` | `.github/workflows/pull-request-validation.yml` | `pull_request`, `merge_group`, `workflow_dispatch` | Gate minimo de PR. Executa restore, build e testes sem cobertura para sempre reportar o status esperado pela protecao de branch. | Bloqueante |
| `dotnet-ci` | `.github/workflows/dotnet.yml` | `push` na `main`, `workflow_dispatch` | Validacao completa pos-merge/manual, com restore, vulnerabilidades NuGet, build, testes, cobertura, gate de 80% e artifacts de diagnostico. | Informativo para PR; bloqueante apenas se uma regra externa decidir exigir esse check |
| `dependency-review` | `.github/workflows/dependency-review.yml` | `pull_request` para `main` | Revisa dependencias alteradas no PR e falha para vulnerabilidades `moderate` ou superior. | Bloqueante se exigido por branch protection/ruleset |
| `codeql` | `.github/workflows/codeql.yml` | `push` na `main`, `pull_request` para `main`, `schedule` semanal | Analise estatica de seguranca C# via CodeQL. | Bloqueante se exigido por branch protection/ruleset |
| `Mutation Tests` | `.github/workflows/mutation-tests.yml` | `push` na `main`, `workflow_dispatch` | Diagnostico de qualidade por Stryker.NET com relatorios HTML publicados como artifacts. Nao roda em PR e nao deve virar gate obrigatorio sem decisao explicita. | Informativo |
| `pages-architecture` | `.github/workflows/pages-architecture.yml` | `push` na `main`, `pull_request` para `main`, `workflow_dispatch` quando ha mudancas em `docs/architecture/**` ou `.github/workflows/**` | Build LikeC4 em PRs afetados e publicacao da documentacao arquitetural no GitHub Pages apos merge/manual. | Operacional |
| `release` | `.github/workflows/release.yml` | `pull_request` fechado para `main` quando o PR foi mergeado | Cria tag e GitHub Release a partir do merge do PR. Nao repete build/testes. | Operacional |

O required check recomendado para proteger a `main` e `Build and test`, job do workflow `pull-request-validation`.

`dotnet-ci` continua sendo a validacao completa pos-merge/manual e alimenta os badges de build/testes do README por meio do arquivo `.github/workflows/dotnet.yml`.

## Cuidados antes de renomear checks

Renomear workflows, jobs ou arquivos usados por badges pode quebrar required status checks externos ou deixar badges apontando para execucoes antigas/inexistentes.

Antes de renomear qualquer um destes itens, revise a configuracao externa do GitHub e a documentacao do repositorio:

- job `Build and test` em `.github/workflows/pull-request-validation.yml`;
- workflow `pull-request-validation`;
- workflow `dotnet-ci` e arquivo `.github/workflows/dotnet.yml`;
- workflow `pages-architecture` e arquivo `.github/workflows/pages-architecture.yml`;
- workflow `Mutation Tests` e arquivo `.github/workflows/mutation-tests.yml`;
- badges e links no `README.md`;
- referencias em `docs/development`, `docs/architecture` e ADRs relacionadas.

Se o job `Build and test` for renomeado, a branch protection ou ruleset que exige esse status precisa ser atualizada no GitHub antes do merge da alteracao. Se `dotnet-ci`, `pages-architecture` ou seus arquivos forem renomeados, revise tambem os badges do README que apontam para `dotnet.yml` e `pages-architecture.yml`.

Nao promova `Mutation Tests` para gate obrigatorio apenas por renomeacao ou ajuste operacional. Essa mudanca exige decisao explicita, porque aumenta tempo de feedback e pode bloquear merges por uma validacao originalmente informativa.

## Configuracao externa esperada no GitHub

A protecao de branch e os rulesets sao configuracoes externas ao repositorio. O YAML cria os checks, mas nao impede merge sozinho.

Checklist esperado em `Settings > Branches > Branch protection rules` ou `Settings > Rules > Rulesets`:

- proteger a branch `main`;
- exigir pull request antes do merge;
- exigir que status checks passem antes do merge;
- selecionar o required check `Build and test`;
- preservar checks de seguranca ja exigidos, como `dependency-review` ou `codeql`, quando estiverem configurados;
- exigir branch atualizada antes do merge, se essa for a politica operacional do repositorio;
- bloquear push direto na `main`, exceto para administracao operacional explicita;
- revisar a regra sempre que workflow, job ou arquivo usado em badge/status check for renomeado.
