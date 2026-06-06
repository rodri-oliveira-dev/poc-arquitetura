# Releases e versionamento

As releases do repositorio sao criadas automaticamente pelo workflow `.github/workflows/release.yml` e usam Semantic Versioning calculado pelo GitVersion.

## Estrategia de versionamento

O repositorio usa SemVer no formato:

```text
MAJOR.MINOR.PATCH
```

A versao e calculada a partir do historico Git pelo `GitVersion.Tool`, configurado em `GitVersion.yml` e versionado como ferramenta local em `.config/dotnet-tools.json`.

O fluxo considerado e GitHub Flow:

- PRs sao abertos a partir de branches curtos para `main`;
- o check `Build and test` deve passar antes do merge;
- o workflow de release roda quando um PR e mergeado na `main`;
- a tag SemVer criada pelo workflow passa a ser a fonte de versao para releases seguintes.

As tags validas de release usam o prefixo `v` seguido de SemVer estrito, por exemplo:

```text
v1.2.3
```

Tags historicas fora de SemVer, como tags sequenciais por data, permanecem no historico, mas nao devem ser usadas como fonte de novas versoes.

## Quando a release e criada

O workflow escuta o evento `pull_request` com tipo `closed` para a branch `main`, mas o job so executa quando `github.event.pull_request.merged == true`.

Com isso:

- PR mergeado na `main` cria release;
- PR fechado sem merge nao cria release;
- push direto na `main` nao cria release;
- reexecucao do workflow nao cria uma segunda release para o mesmo commit de merge.

O workflow nao executa build/testes novamente. A protecao da branch `main` deve exigir o check `Build and test`, do workflow `pull-request-validation`, antes do merge.

Se o GitVersion calcular uma versao cuja tag ja existe em outro commit, o workflow nao cria uma nova tag nem uma nova release. Esse e o comportamento esperado para PRs que nao geram incremento SemVer.

## Como commits influenciam a versao

O GitVersion usa as mensagens de commit para decidir o incremento:

| Mensagem | Incremento esperado |
| --- | --- |
| `fix: corrige validacao de payload` | PATCH |
| `feat: adiciona endpoint de consulta` | MINOR |
| `feat!: altera contrato publico da API` | MAJOR |
| `refactor: simplifica pipeline de validacao` | Sem incremento |
| `docs: documenta estrategia de versionamento` | Sem incremento |

Tambem gera MAJOR quando o corpo do commit contem um rodape `BREAKING CHANGE:`.

Tipos que incrementam versao:

- `fix:` gera PATCH;
- `feat:` gera MINOR;
- qualquer tipo aceito com `!` antes de `:` gera MAJOR;
- qualquer tipo aceito com `BREAKING CHANGE:` no corpo gera MAJOR.

Tipos que nao incrementam versao, exceto quando declaram breaking change:

- `build:`;
- `chore:`;
- `ci:`;
- `docs:`;
- `refactor:`;
- `style:`;
- `test:`.

Para pular incremento explicitamente em uma mensagem que o GitVersion avaliaria, use `+semver: skip` ou `+semver: none` no corpo do commit.

## Validacao local

Restaure as ferramentas e calcule a versao localmente:

```powershell
dotnet tool restore
dotnet tool run dotnet-gitversion
dotnet tool run dotnet-gitversion /showvariable MajorMinorPatch
dotnet tool run dotnet-gitversion /showvariable FullSemVer
```

Em branches de feature, o GitVersion pode gerar pre-release com o nome do branch. Na `main`, o workflow usa `MajorMinorPatch` para criar a tag estavel `vMAJOR.MINOR.PATCH`.

## Conteudo da release

A release usa a tag SemVer como titulo e inclui:

- numero, titulo e link do PR;
- autor do PR;
- branch de origem;
- commit de merge;
- versao calculada;
- descricao do PR como changelog simples;
- lista resumida dos commits do PR quando disponivel.

O corpo da release usa apenas metadados do PR e commits do repositorio. Secrets nao devem ser colocados em descricoes de PR, mensagens de commit ou titulos.

## Como evitar release

Para evitar release automatica, nao faca merge do PR na `main`. Fechar o PR sem merge nao dispara release.

Se uma alteracao precisa entrar na `main` sem release, isso deve ser tratado como excecao operacional e discutido antes do merge, porque push direto na `main` tambem deve ser evitado pela protecao da branch.

## Como corrigir uma release incorreta

Se uma release for criada incorretamente:

1. Edite ou exclua a release no GitHub.
2. Avalie se a tag deve ser mantida por rastreabilidade ou removida.
3. Se remover a tag, remova tanto a tag remota quanto qualquer tag local usada na correcao.
4. Registre a correcao no PR, issue ou canal operacional correspondente.

Evite reutilizar uma tag para outro conteudo. Prefira corrigir a causa, fazer novo PR com commit semantico adequado e deixar o GitVersion calcular a proxima versao.

## Permissoes

O workflow usa o `GITHUB_TOKEN` padrao com permissoes minimas para esta automacao:

```yaml
permissions:
  contents: write
  pull-requests: read
```

`contents: write` permite criar tags e releases. `pull-requests: read` permite ler os metadados do PR mergeado.
