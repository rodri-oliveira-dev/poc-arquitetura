# Padroes do repositorio

Este documento resume convencoes de build, estilo, ferramentas e manutencao do repositorio.

## Arquivos de padronizacao

| Arquivo | Papel |
| --- | --- |
| `.gitattributes` | Normaliza line endings e reduz ruido de diff. |
| `.editorconfig` | Padroniza formatacao e regras de estilo entre editores e IDEs. |
| `Directory.Packages.props` | Centraliza versoes de pacotes NuGet com Central Package Management. |
| `Directory.Build.props` | Centraliza configuracoes MSBuild comuns. |
| `global.json` | Fixa o SDK .NET esperado. |
| `coverlet.runsettings` | Define coleta e exclusoes de cobertura. |
| `.config/dotnet-tools.json` | Versiona ferramentas locais do .NET. |
| `LedgerService.slnx` | Solution principal do repositorio. |

## Central Package Management

O repositorio usa Central Package Management. Projetos `.csproj` devem referenciar pacotes sem `Version=`.

Quando uma versao precisar mudar, ajuste `Directory.Packages.props` e valide restore/build/testes proporcionais ao impacto.

## Build e testes

Comandos baseline:

```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Para cobertura com gate, use:

```powershell
./test.ps1
```

ou:

```bash
./test.sh
```

Detalhes ficam em [cobertura de testes](test-coverage.md).

## Hooks locais

Hooks ficam em `.githooks/` e sao documentados em [git hooks locais](git-hooks.md).

Resumo:

- `commit-msg`: valida Conventional Commits;
- `post-merge`: restaura tools e dependencias;
- `pre-push`: executa validacoes completas quando houver arquivos impactantes.

## Workflows

Workflows principais:

- `pull-request-validation`: restore, build e testes para PRs;
- `dotnet-ci`: validacao completa pos-merge/manual, cobertura e artifacts;
- `dependency-review`: revisao de dependencias em PRs;
- `codeql`: analise estatica de seguranca;
- `Mutation Tests`: mutation testing informativo;
- `pages-architecture`: build e publicacao LikeC4;
- `release`: tags e GitHub Releases apos merge na `main`.

Detalhes ficam em [validacao de pull requests](pull-request-validation.md), [artifacts dos workflows](workflow-artifacts.md) e [releases](releases.md).

## Manutencao

- Preserve as fronteiras entre `Api`, `Application`, `Domain` e `Infrastructure`.
- Nao mova regras de negocio para controllers, endpoints, middlewares ou infraestrutura.
- Nao coloque detalhes de infraestrutura no `Domain`.
- Nao altere migrations existentes sem necessidade explicita.
- Nao introduza segredos no repositorio.
- Atualize documentacao e ADRs quando houver mudanca de contrato, fluxo arquitetural, setup local ou comportamento relevante.
