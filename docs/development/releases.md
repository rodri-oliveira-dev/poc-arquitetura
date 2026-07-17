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
- o workflow `main-dotnet-ci` roda no push resultante para a `main`;
- o workflow de release roda somente apos o `main-dotnet-ci` da `main` concluir com sucesso;
- a tag SemVer criada pelo workflow passa a ser a fonte de versao para releases seguintes.

As tags validas de release usam o prefixo `v` seguido de SemVer estrito, por exemplo:

```text
v1.2.3
```

Tags historicas fora de SemVer, como tags sequenciais por data, permanecem no historico, mas nao devem ser usadas como fonte de novas versoes.

## Quando a release e criada

O workflow escuta o evento `workflow_run` do workflow `main-dotnet-ci`, com `types: [completed]` e filtro para a branch `main`. O job so executa quando:

```yaml
github.event.workflow_run.conclusion == 'success'
github.event.workflow_run.event == 'push'
github.event.workflow_run.head_branch == 'main'
```

Com isso:

- CI da `main` com sucesso pode criar release;
- CI da `main` com falha ou cancelamento nao cria release;
- PR fechado sem merge nao cria release diretamente;
- reexecucao do workflow nao cria uma segunda release para o mesmo commit aprovado.

O workflow nao executa build/testes novamente. Ele usa o SHA aprovado pelo CI, `${{ github.event.workflow_run.head_sha }}`, para checkout, calculo de versao, tag e target da GitHub Release. A protecao da branch `main` deve exigir o check `Build and test`, produzido pelo workflow `main-dotnet-ci`, antes do merge.

Se o GitVersion calcular uma versao cuja tag ja existe em outro commit, o workflow nao cria uma nova tag nem uma nova release. Esse e o comportamento esperado para PRs que nao geram incremento SemVer.

Publicacoes nao devem ser canceladas no meio. Por isso, `release-on-merge` usa `concurrency.cancel-in-progress: false`, evitando interromper uma criacao de tag ou GitHub Release ja iniciada.

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

## Publicacao dos pacotes NuGet compartilhados

Os pacotes em `src/Shared` usam a mesma fonte de versao do repositorio: o `GitVersion.Tool` configurado em `GitVersion.yml`. O workflow de empacotamento calcula a versao uma unica vez, exporta esse valor em `PACKAGE_VERSION` e reutiliza-o em todos os `dotnet pack` da execucao.

A propriedade do GitVersion usada como base para `PackageVersion` e `SemVer`:

```powershell
dotnet tool restore
$rawPackageVersion = dotnet tool run dotnet-gitversion /showvariable SemVer
$env:PACKAGE_VERSION = if ($rawPackageVersion -match '^([0-9]+\.[0-9]+\.[0-9]+)-([0-9]+)$') { "$($Matches[1])-main.$($Matches[2])" } else { $rawPackageVersion }
dotnet pack ./src/Shared/HttpResilienceDefaults/HttpResilienceDefaults.csproj --configuration Release /p:PackageVersion=$env:PACKAGE_VERSION
dotnet pack ./src/Shared/ApplicationDefaults/ApplicationDefaults.csproj --configuration Release /p:PackageVersion=$env:PACKAGE_VERSION
dotnet pack ./src/Shared/ApiDefaults/ApiDefaults.csproj --configuration Release /p:PackageVersion=$env:PACKAGE_VERSION
```

No GitHub Actions, o mesmo valor deve ser exportado para o ambiente antes dos packs:

```bash
RAW_PACKAGE_VERSION="$(dotnet tool run dotnet-gitversion /showvariable SemVer)"
PACKAGE_VERSION="$RAW_PACKAGE_VERSION"
if [[ "$PACKAGE_VERSION" =~ ^([0-9]+\.[0-9]+\.[0-9]+)-([0-9]+)$ ]]; then
  PACKAGE_VERSION="${BASH_REMATCH[1]}-main.${BASH_REMATCH[2]}"
fi
echo "PACKAGE_VERSION=$PACKAGE_VERSION" >> "$GITHUB_ENV"
dotnet pack ./src/Shared/HttpResilienceDefaults/HttpResilienceDefaults.csproj --configuration Release /p:PackageVersion="$PACKAGE_VERSION"
dotnet pack ./src/Shared/ApplicationDefaults/ApplicationDefaults.csproj --configuration Release /p:PackageVersion="$PACKAGE_VERSION"
dotnet pack ./src/Shared/ApiDefaults/ApiDefaults.csproj --configuration Release /p:PackageVersion="$PACKAGE_VERSION"
```

`SemVer` e adequado para NuGet porque gera uma versao SemVer sem metadados de build (`+...`), por exemplo `0.18.1-lib.1` em branch de feature ou `0.18.1` em uma versao estavel. Quando o GitVersion calcular uma pre-release apenas numerica em `main`, como `0.18.1-8`, o workflow deve normalizar o valor para `0.18.1-main.8` antes do `dotnet pack`, porque o NuGet.org rejeita esse sufixo numerico puro no push. Na versao atual do `GitVersion.Tool` usada pelo repositorio, `NuGetVersionV2` e `NuGetVersion` nao estao disponiveis como variaveis de saida; por isso o workflow deve extrair `SemVer` e aplicar essa normalizacao pequena.

O workflow `.github/workflows/publish-shared-nuget.yml` restaura, compila, testa, empacota, valida os metadados dos `.nupkg` e, somente quando a execucao pedir publicacao, publica os pacotes no NuGet.org. A publicacao usa Trusted Publishing com GitHub Actions OIDC por meio de `NuGet/login@v1`; nao ha API key persistente nem secret `NUGET_API_KEY`. A permissao `id-token: write` fica restrita ao job de publicacao.

Antes do pack, o workflow valida os consumidores em modo integrado:

```powershell
dotnet restore ./PocArquitetura.slnx -p:UseLocalSharedProjects=true
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore -p:UseLocalSharedProjects=true
dotnet test ./PocArquitetura.slnx --configuration Release --no-build -p:UseLocalSharedProjects=true
```

Nesse modo, os consumidores deixam de usar temporariamente os pacotes
`PocArquitetura.*` publicados e passam a usar `ProjectReference` para
`src/Shared`, permitindo detectar quebra de API publica antes de publicar uma
nova versao.

O workflow usa a solution dedicada `PocArquitetura.Shared.slnx`, que contem apenas os tres pacotes Shared e seus testes em `tests/Shared`, para restore, build, test e pack dos pacotes. Ele nao publica diretamente em `push`: a execucao automatica ocorre por `workflow_run`, apos sucesso do `main-dotnet-ci` na `main`, e faz checkout de `${{ github.event.workflow_run.head_sha }}`, o mesmo SHA validado pelo CI. Antes de empacotar automaticamente, o workflow confere se o commit aprovado alterou entradas relevantes para os pacotes: `src/Shared/**`, `tests/Shared/**`, `PocArquitetura.Shared.slnx`, `GitVersion.yml`, o proprio workflow, `LICENSE` e os arquivos `Directory.Build.props`/`Directory.Build.targets`/`Directory.Packages.props` da raiz e de `src/Shared`. Os arquivos `Directory.*` da raiz permanecem relevantes porque os projetos de teste em `tests/Shared` os herdam e porque `Directory.Build.targets` governa o modo integrado usado antes da publicacao; os arquivos `Directory.*` de `src/Shared` permanecem porque definem propriedades e versoes usadas pelos pacotes publicados.

Assim como releases, a publicacao NuGet usa `concurrency.cancel-in-progress: false`. Isso serializa execucoes e evita cancelar um pack, login OIDC ou push de pacote no meio da operacao.

Na execucao manual, o input `publish` separa empacotamento de publicacao:

| `publish` | Comportamento |
| --- | --- |
| `false` | Restaura, compila, testa, empacota, valida e publica o artifact, sem login NuGet e sem push. |
| `true` | Executa as mesmas validacoes e, apenas depois delas, faz login via Trusted Publishing e publica. |

Para a publicacao funcionar, deve existir no NuGet.org uma Trusted Publishing policy com:

| Campo | Valor |
| --- | --- |
| Package Owner | `rodri-oliveira-dev` |
| Repository Owner | `rodri-oliveira-dev` |
| Repository | `poc-arquitetura` |
| Workflow File | `publish-shared-nuget.yml` |
| Environment | vazio, enquanto o workflow nao usar GitHub Environment |

Os pacotes sao publicados em ordem para respeitar a dependencia de `ApiDefaults` sobre `HttpResilienceDefaults`:

1. `PocArquitetura.HttpResilienceDefaults`
2. `PocArquitetura.ApplicationDefaults`
3. `PocArquitetura.ApiDefaults`

Antes do upload do artifact e da publicacao, o workflow abre cada `.nupkg` e valida `id`, versao, descricao, autores, tags, `projectUrl`, licenca MIT, `README.md`, repository metadata e o `README.md` na raiz do pacote. Para `PocArquitetura.ApiDefaults`, tambem valida a dependencia interna para `PocArquitetura.HttpResilienceDefaults`. O job de publicacao baixa o artifact validado e confirma novamente que os tres arquivos esperados existem antes de iniciar qualquer `dotnet nuget push`.

Os comandos `dotnet nuget push` usam `--skip-duplicate`. Reruns da mesma versao ou cenarios em que parte dos pacotes ja exista no NuGet.org nao devem falhar apenas por duplicidade; falhas reais de autenticacao, arquivo ausente, validacao ou erro operacional continuam falhando a execucao.

O artifact `shared-nuget-packages` continua sendo enviado em toda execucao bem-sucedida de pack, mesmo quando a publicacao tambem ocorre. Para baixa-lo, abra a execucao do workflow no GitHub Actions e use a secao **Artifacts** da pagina da run.

Para conferir a publicacao no NuGet.org, pesquise os IDs dos pacotes acima ou acesse a pagina do owner `rodri-oliveira-dev` e valide se a versao calculada pelo GitVersion aparece nos tres pacotes.

Falhas de autenticacao OIDC normalmente indicam divergencia entre a policy e o workflow executado. Confira se `permissions.id-token: write` esta presente, se a policy usa apenas o nome do arquivo `publish-shared-nuget.yml`, se o owner/repository batem com o repositorio real e se o campo Environment esta vazio quando o job nao declara `environment`.

Nao adicione `Version` fixa aos `.csproj`, nao use `GitVersion.MsBuild` para este fluxo e nao altere consumidores para `PackageReference` ate existir uma tarefa especifica para consumo dos pacotes publicados.

## Conteudo da release

A release usa a tag SemVer como titulo e inclui:

- numero, titulo e link da PR associada ao commit aprovado, quando encontrada;
- autor da PR, quando encontrada;
- branch de origem da PR, quando encontrada;
- commit aprovado pelo CI;
- versao calculada;
- descricao da PR como changelog simples, quando encontrada;
- lista resumida dos commits desde a tag SemVer anterior, quando disponivel.

O corpo da release usa apenas metadados do PR e commits do repositorio. Secrets nao devem ser colocados em descricoes de PR, mensagens de commit ou titulos.

## Como evitar release

Para evitar release automatica, nao faca merge do PR na `main` sem alinhar a excecao operacional. Fechar o PR sem merge nao dispara release, e falha/cancelamento do `main-dotnet-ci` na `main` tambem impede a release.

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

`contents: write` permite criar tags e releases. `pull-requests: read` permite localizar a PR associada ao commit aprovado, quando existir.
