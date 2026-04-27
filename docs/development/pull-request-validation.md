# Validacao de Pull Requests

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
