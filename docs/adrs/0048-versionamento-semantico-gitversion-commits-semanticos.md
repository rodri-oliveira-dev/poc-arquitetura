# ADR-0048: Versionamento Semantico com GitVersion e Commits Semanticos

## Status
Aceito

## Data
2026-05-06

## Contexto
O repositorio possui automacao de release por GitHub Actions apos merge de PRs na `main`, registrada na ADR-0038. A estrategia anterior usava tags sequenciais por data no formato `vYYYY.MM.DD.N`, porque ainda nao havia SemVer formal nem convencao automatizada de bump por tipo de commit.

O repositorio tambem ja possui hook `commit-msg` com Conventional Commits, workflows de build/teste e branch protection documentada para exigir o check `Build and test` antes do merge. Isso cria os pre-requisitos minimos para substituir as tags por data por uma estrategia SemVer calculada a partir do historico Git.

Nao ha versao manual em `.csproj`, `Directory.Build.props`, scripts de build ou Docker tags de aplicacao. As versoes existentes em `Directory.Packages.props` sao versoes de dependencias NuGet e nao representam a versao dos servicos.

## Decisao
Adotar GitVersion como fonte oficial de calculo de versao SemVer do repositorio.

A decisao implementada e:

- usar `GitVersion.Tool` como ferramenta local versionada em `dotnet-tools.json`;
- configurar o GitVersion em `GitVersion.yml`;
- usar `workflow: GitHubFlow/v1`, coerente com PRs para `main` e release apos merge;
- manter uma versao unica para a solution e para os tres servicos da POC;
- usar tags no formato `vMAJOR.MINOR.PATCH` como fonte de versoes liberadas;
- manter tags historicas por data apenas como historico, sem usa-las como fonte das proximas versoes;
- calcular a tag no workflow `.github/workflows/release.yml` com `dotnet tool run dotnet-gitversion /showvariable MajorMinorPatch`;
- nao adicionar `GitVersion.MsBuild`, porque nao ha pacote NuGet proprio nem necessidade atual de injetar versao de assembly em todos os projetos;
- nao usar `next-version`, para evitar uma segunda fonte manual de versao.

Commits semanticos passam a influenciar o incremento:

- `fix:` gera PATCH;
- `feat:` gera MINOR;
- `!` no tipo do commit gera MAJOR;
- rodape `BREAKING CHANGE:` gera MAJOR;
- `build:`, `chore:`, `ci:`, `docs:`, `refactor:`, `style:` e `test:` nao geram incremento, exceto quando declaram breaking change.

O workflow nao cria nova release quando a versao calculada ja possui tag apontando para outro commit. Esse comportamento evita publicar release para PRs sem incremento SemVer.

## Consequencias

### Beneficios
- A versao passa a expressar compatibilidade semantica, nao apenas data de execucao.
- A fonte de verdade passa a ser o historico Git e as tags SemVer.
- O build local consegue calcular a mesma versao usada pelo CI com `dotnet tool run dotnet-gitversion`.
- Nao ha acoplamento de versionamento aos `.csproj`, preservando a menor mudanca possivel.
- Commits semanticos deixam de ser apenas convencao textual e passam a afetar governanca de release.

### Trade-offs / custos
- PRs sem commit que gere bump SemVer podem nao criar nova release, mesmo sendo mergeados na `main`.
- A qualidade da versao depende da qualidade das mensagens de commit.
- Tags antigas por data permanecem no repositorio, mas nao servem mais como base de calculo SemVer.
- Se no futuro houver pacotes NuGet ou necessidade de versionar assemblies, a decisao por nao usar `GitVersion.MsBuild` deve ser reavaliada.

## Alternativas consideradas

1. **GitVersion.Tool via ferramenta local**
   - Escolhida. E suficiente para calcular versao no workflow e localmente, sem adicionar pacote em cada projeto.

2. **GitVersion.MsBuild**
   - Rejeitada no momento. Injetaria versao no build MSBuild de todos os projetos sem demanda atual por assembly/package versioning.

3. **GitVersion apenas no CI/CD**
   - Rejeitada como estrategia completa. Resolveria a release, mas impediria validacao local simples e reprodutivel.

4. **Manter tags sequenciais por data**
   - Rejeitada. Continua simples, mas nao expressa compatibilidade SemVer e nao usa commits semanticos para governanca.

5. **Configurar `next-version`**
   - Rejeitada no momento. Criaria uma fonte manual adicional antes da primeira tag SemVer. O primeiro baseline deve vir do calculo do GitVersion e, depois disso, das tags `vMAJOR.MINOR.PATCH`.

## Criterios de revisao futura
- Reavaliar `GitVersion.MsBuild` se o repositorio passar a publicar pacotes NuGet ou exigir versao de assembly padronizada.
- Reavaliar estrategia unica de versao se os servicos passarem a ter ciclos de release independentes.
- Reavaliar automacao de changelog se releases precisarem de notas geradas por tipo de commit.
- Reavaliar a regra de no-bump se o time decidir que `perf:` ou `revert:` devem gerar PATCH.
