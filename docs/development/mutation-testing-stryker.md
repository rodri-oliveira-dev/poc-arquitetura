# Mutation testing com Stryker.NET

Este documento registra a configuracao inicial e opcional de mutation testing para o `LedgerService.Application`.

## O que e mutation testing

Mutation testing avalia a qualidade dos testes, nao apenas se uma linha foi executada.

Cobertura tradicional responde se o codigo foi exercitado. Mutation testing responde se os testes perceberiam uma pequena alteracao indevida no comportamento. O Stryker.NET gera mutantes no codigo, executa os testes e classifica o resultado de cada mutante.

## Por que LedgerService.Application

O primeiro alvo e `LedgerService.Application` porque essa camada concentra regras de aplicacao e fluxos mais relevantes do que codigo de borda HTTP, bootstrap, infraestrutura ou configuracao.

Priorize a leitura do relatorio em codigo de:

- validacoes;
- fluxos de idempotencia;
- decisoes condicionais;
- handlers;
- use cases;
- services de aplicacao;
- regras que afetam criacao ou rejeicao de lancamentos.

Este passo inicial nao altera regras de negocio, nao altera testes existentes e nao torna mutation testing obrigatorio no CI.

## Como preparar o ambiente

O Stryker.NET e executado como ferramenta local do .NET versionada em `dotnet-tools.json`.

Confira os runtimes instalados:

```bash
dotnet --list-runtimes
```

O Stryker.NET 4.x precisa do runtime .NET 8 ou superior para executar a ferramenta. O projeto pode continuar mirando o target framework definido nos `.csproj`.

Restaure as ferramentas locais:

```bash
dotnet tool restore
```

## Como executar

Comando manual recomendado:

```bash
cd tests/LedgerService.UnitTests
dotnet stryker
```

O arquivo `tests/LedgerService.UnitTests/stryker-config.json` e carregado automaticamente quando o comando roda a partir desse diretorio.

Tambem existem scripts opcionais na raiz:

```powershell
./scripts/run-mutation-tests-ledger-application.ps1
```

```bash
./scripts/run-mutation-tests-ledger-application.sh
```

Esses scripts apenas restauram as ferramentas locais e executam `dotnet stryker` no diretorio `tests/LedgerService.UnitTests`. Eles nao fazem parte do fluxo normal de build, teste, push ou pull request.

## Escopo configurado

A configuracao inicial usa:

- `project: "LedgerService.Application.csproj"` para escolher o projeto sob teste quando o projeto de testes possui varias referencias;
- `mutate` em `**/*.cs`, relativo ao projeto sob mutacao escolhido por `project`;
- exclusoes para `bin`, `obj` e arquivos gerados;
- `mutation-level: "Standard"` para reduzir ruido e custo inicial;
- `coverage-analysis: "perTest"` para melhorar performance;
- reporters `progress`, `html` e `json`;
- thresholds `high: 80`, `low: 60` e `break: 0`.

O `break` fica em `0` porque esta fase e diagnostica e local. Futuramente o time pode elevar esse valor de forma progressiva, por exemplo para 40, 50 ou 60, depois de conhecer o score real do projeto.

## Onde fica o relatorio

O Stryker gera os relatorios dentro do diretorio `StrykerOutput` no diretorio em que foi executado.

Para esta configuracao, o relatorio HTML fica em:

```text
tests/LedgerService.UnitTests/StrykerOutput/**/reports/mutation-report.html
```

O relatorio JSON fica no mesmo diretorio de reports. A pasta `StrykerOutput/` nao deve ser versionada.

## Como ler o relatorio

Principais status:

- `Killed`: o teste falhou quando o codigo foi mutado. E um bom sinal.
- `Survived`: os testes continuaram passando mesmo com a alteracao no codigo. Deve ser analisado.
- `NoCoverage`: o mutante nao foi coberto por nenhum teste.
- `Timeout`: a execucao excedeu o tempo esperado. Pode indicar mutante problematico, teste lento ou loop.
- `Ignored`: mutante ignorado por configuracao ou comentario.
- `CompileError`: a mutacao gerou codigo que nao compila.

O Mutation Score resume quantos mutantes foram detectados pelos testes. Score alto indica que os testes matam boa parte dos mutantes. Score baixo indica comportamento pouco protegido. O score nao deve ser perseguido cegamente: o foco e melhorar testes que protegem comportamento real.

## Como agir diante de mutantes sobreviventes

1. Verifique se o mutante altera comportamento relevante.
2. Se alterar, crie ou melhore testes orientados a comportamento.
3. Nao teste detalhe interno sem necessidade.
4. Nao escreva teste artificial so para matar mutante.
5. Se o mutante for equivalente ou irrelevante, avalie ignorar com configuracao, mas documente o motivo.
6. Priorize mutantes sobreviventes em regra de negocio, validacao e decisao condicional.

## O que evitar

- Nao rode inicialmente na solution inteira.
- Nao aplique em migrations.
- Nao aplique em codigo gerado.
- Nao aplique em `Program.cs` neste primeiro momento.
- Nao torne obrigatorio no CI ainda.
- Nao suba o `break` threshold sem baseline real.
- Nao altere regra de negocio para agradar a ferramenta.
- Nao enfraqueca asserts existentes para reduzir mutantes sobreviventes.

## Proximos passos

1. Rodar localmente e registrar o score inicial.
2. Melhorar testes dos mutantes sobreviventes relevantes.
3. Avaliar `BalanceService.Application` como segundo alvo.
4. Avaliar job manual no CI futuramente.
5. Avaliar threshold progressivo somente depois de estabilizar o score.

## Referencias

- Stryker.NET Getting started: <https://stryker-mutator.io/docs/stryker-net/getting-started/>
- Stryker.NET Configuration: <https://stryker-mutator.io/docs/stryker-net/configuration/>
- Stryker.NET Reporters: <https://stryker-mutator.io/docs/stryker-net/reporters/>
