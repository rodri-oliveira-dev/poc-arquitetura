# Cobertura de testes

Este repositorio valida cobertura de testes na solution inteira (`LedgerService.slnx`) usando:

- `dotnet test` com `--collect:"XPlat Code Coverage"`;
- `coverlet.runsettings` como configuracao unica de coleta;
- ReportGenerator para consolidar os arquivos `coverage.cobertura.xml`;
- gate minimo de 85% de cobertura total de linhas;
- gate minimo de 85% de cobertura de linhas para `LedgerService.Worker` e `BalanceService.Worker`.

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

## Testes com Testcontainers

Alguns testes de integracao usam Testcontainers com PostgreSQL real. Esses testes devem ser executados fora do sandbox, porque precisam acessar a Docker-compatible API do ambiente.

No Windows com Rancher Desktop/Docker-compatible API, o Docker CLI pode funcionar com `DOCKER_HOST=npipe:////./pipe/docker_engine`, mas o Testcontainers/Docker.DotNet espera o formato `npipe://./pipe/docker_engine`. Quando necessario, normalize a variavel apenas no processo do teste:

```powershell
$env:DOCKER_HOST='npipe://./pipe/docker_engine'
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Se a suite falhar com `npipe:////pipe/docker_engine is not a valid npipe URI`, repita com o `DOCKER_HOST` acima antes de investigar falhas funcionais dos testes.

Depois da coleta, os scripts executam o ReportGenerator e leem `TestResults/coverage-report/Summary.json`.

## Regra de cobertura

- A validacao considera a cobertura consolidada da solution inteira.
- `LedgerService.Worker` e `BalanceService.Worker` tambem precisam atingir 85% de cobertura de linhas por assembly.
- O minimo aceito e 85% de cobertura de linhas.
- O mesmo limite deve ser usado localmente, no `pre-push` e no CI.
- Se `LedgerService.Worker` ou `BalanceService.Worker` estiver ausente do `Summary.json`/`Summary.txt`, o gate falha; assembly ausente nao e tratado como sucesso.
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

Os projetos `LedgerService.Worker` e `BalanceService.Worker` preservam o contexto de compilacao para que o Coverlet consiga resolver dependencias transitivas durante a instrumentacao dos hosts `Microsoft.NET.Sdk.Worker`.

## Interpretando falhas

Quando o gate falhar:

1. Abra `TestResults/coverage-report/Summary.txt` ou `Summary.json`.
2. Identifique os assemblies ou arquivos com baixa cobertura.
3. Priorize testes que validem comportamento de dominio, aplicacao, infraestrutura critica ou contratos HTTP.
4. Use exclusao somente quando houver justificativa tecnica clara e localizada.

O CI publica os resultados de testes e cobertura como artifact `test-results-and-coverage` por 7 dias quando executado no GitHub Actions.

O artifact contem arquivos `.trx`, `coverage.cobertura.xml`, `coverage-report/Summary.json` e `coverage-report/Summary.txt`. O HTML completo do ReportGenerator nao e publicado como artifact porque o XML e os summaries atendem ao diagnostico principal com menor exposicao de paths e trechos renderizados.

Detalhes da politica de artifacts: [`workflow-artifacts.md`](workflow-artifacts.md).
