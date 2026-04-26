# Cobertura de testes

Este repositorio valida cobertura de testes na solution inteira (`LedgerService.slnx`) usando:

- `dotnet test` com `--collect:"XPlat Code Coverage"`;
- `coverlet.runsettings` como configuracao unica de coleta;
- ReportGenerator para consolidar os arquivos `coverage.cobertura.xml`;
- gate minimo de 80% de cobertura total de linhas.

## Comando oficial

Windows (PowerShell):

```powershell
./test.ps1
```

Linux/macOS:

```bash
./test.sh
```

Comando equivalente:

```bash
dotnet test ./LedgerService.slnx \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --settings ./coverlet.runsettings \
  --results-directory ./TestResults
```

Depois da coleta, os scripts executam o ReportGenerator e leem `TestResults/coverage-report/Summary.json`.

## Regra de cobertura

- A validacao considera a cobertura consolidada da solution inteira, nao projeto por projeto.
- O minimo aceito e 80% de cobertura total de linhas.
- O mesmo limite deve ser usado localmente, no `pre-push` e no CI.
- Relatorios ficam em `TestResults/`, que nao e versionado.

## Exclusoes permitidas

`coverlet.runsettings` exclui itens marcados com:

- `System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute`;
- `ExcludeFromCodeCoverageAttribute`;
- `GeneratedCodeAttribute`;
- `CompilerGeneratedAttribute`.

Tambem sao excluidos arquivos de hosting minimo, migrations EF e codigo gerado:

- `**/Program.cs`;
- `**/Migrations/*.cs`;
- `**/*.g.cs`;
- `**/*.g.*.cs`.

Use `ExcludeFromCodeCoverage` apenas para codigo que nao representa comportamento testavel relevante, como adaptadores puramente mecanicos, codigo gerado ou pontos de composicao sem regra de negocio. Nao exclua codigo de producao apenas para elevar a cobertura.

## Interpretando falhas

Quando o gate falhar:

1. Abra `TestResults/coverage-report/Summary.txt` ou `Summary.json`.
2. Identifique os assemblies ou arquivos com baixa cobertura.
3. Priorize testes que validem comportamento de dominio, aplicacao, infraestrutura critica ou contratos HTTP.
4. Use exclusao somente quando houver justificativa tecnica clara e localizada.

O CI publica os resultados de testes e cobertura como artefato quando executado no GitHub Actions.
