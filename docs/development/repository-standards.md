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
| `PocArquitetura.slnx` | Solution agregadora/principal do repositorio. |

## Solutions

| Solution | Escopo |
| --- | --- |
| `PocArquitetura.slnx` | Agregadora global com contextos, Shared, `tests/Architecture.Tests` e tooling necessario para fechamento das dependencias. |
| `LedgerService.slnx` | Projetos em `src/ledger/`, testes em `tests/ledger/` e `tools/ComposeEnvGen`, usado pelos testes do Ledger. |
| `BalanceService.slnx` | Projetos em `src/balance/` e testes em `tests/balance/`. |
| `TransferService.slnx` | Projetos em `src/transfer/` e testes em `tests/transfer/`. |
| `IdentityService.slnx` | Projetos em `src/identity/` e testes em `tests/identity/`. |
| `AuditService.slnx` | Projetos em `src/audit/` e testes em `tests/audit/`. |
| `PocArquitetura.Shared.slnx` | Projetos em `src/Shared/` e testes em `tests/Shared/`. |

`tests/Architecture.Tests` e transversal e fica somente na agregadora.

Use `PocArquitetura.slnx` para validacoes globais, cobertura consolidada,
workflows gerais, alteracoes transversais e testes arquiteturais. Use uma
solution contextual quando a mudanca estiver restrita ao contexto e a validacao
contextual for suficiente para o ciclo local. A decisao esta registrada na
[ADR-0100](../adrs/0100-organizacao-solutions-contexto-agregadora.md).

## Central Package Management

O repositorio usa Central Package Management. Projetos `.csproj` devem referenciar pacotes sem `Version=`.

Quando uma versao precisar mudar, ajuste `Directory.Packages.props` e valide restore/build/testes proporcionais ao impacto.

## Build e testes

Comandos baseline:

```bash
dotnet tool restore
dotnet restore ./PocArquitetura.slnx
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings
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

- `pr-build-and-test`: restore, build e testes para PRs;
- `main-dotnet-ci`: validacao completa pos-merge/manual, cobertura e artifacts;
- `dependency-security-review`: revisao de dependencias em PRs;
- `codeql-security-analysis`: analise estatica de seguranca;
- `event-contract-validation`: validacao de schemas e exemplos de eventos;
- `openapi-contract-validation`: geracao, lint, diff e drift de contratos OpenAPI;
- `infra-security-and-terraform-validation`: Trivy, Terraform validate e TFLint;
- `mutation-tests`: mutation testing informativo;
- `smoke-load-tests`: smoke load tests manuais com k6;
- `architecture-pages`: build e publicacao LikeC4;
- `release-on-merge`: tags e GitHub Releases apos merge na `main`.

Detalhes ficam em [validacao de pull requests](pull-request-validation.md), [artifacts dos workflows](workflow-artifacts.md) e [releases](releases.md).

## Manutencao

- Preserve as fronteiras entre `Api`, `Application`, `Domain` e `Infrastructure`.
- Nao mova regras de negocio para controllers, endpoints, middlewares ou infraestrutura.
- Nao coloque detalhes de infraestrutura no `Domain`.
- Nao altere migrations existentes sem necessidade explicita.
- Nao introduza segredos no repositorio.
- Atualize documentacao e ADRs quando houver mudanca de contrato, fluxo arquitetural, setup local ou comportamento relevante.
