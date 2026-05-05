# Mutation testing com Stryker.NET

Este documento registra a configuracao local, incremental e opcional de mutation testing com Stryker.NET.

## O que e mutation testing

Mutation testing avalia a qualidade dos testes, nao apenas se uma linha foi executada.

Cobertura tradicional responde se o codigo foi exercitado. Mutation testing responde se os testes perceberiam uma pequena alteracao indevida no comportamento. O Stryker.NET gera mutantes no codigo, executa os testes e classifica o resultado de cada mutante.

## Alvos atuais

Alvos configurados ate agora:

- Alvo 1: `LedgerService.Application`
- Alvo 2: `BalanceService.Application`

Ambos sao executados localmente a partir dos respectivos projetos de testes unitarios. Nenhum alvo faz parte de workflow remoto obrigatorio.

## Por que LedgerService.Application

`LedgerService.Application` foi o primeiro alvo porque concentra regras de aplicacao e fluxos mais relevantes do que codigo de borda HTTP, bootstrap, infraestrutura ou configuracao.

Priorize a leitura do relatorio em codigo de:

- validacoes;
- fluxos de idempotencia;
- decisoes condicionais;
- handlers;
- use cases;
- services de aplicacao;
- regras que afetam criacao ou rejeicao de lancamentos.

## Por que BalanceService.Application

`BalanceService.Application` foi escolhido como segundo alvo por concentrar comportamento de aplicacao relacionado a saldos. Esse tipo de codigo deve ser validado por testes de forma observavel, especialmente quando uma pequena alteracao indevida muda o resultado de uma consulta, calculo, atualizacao ou regra de consistencia.

Priorize a analise de mutantes sobreviventes em areas como:

- handlers;
- use cases;
- validators;
- services;
- regras de calculo;
- regras de atualizacao de saldo;
- decisoes condicionais;
- tratamento de eventos de ledger;
- regras de rejeicao ou consistencia.

Mutation testing nao deve ser usado para perseguir score de forma cega. O objetivo e encontrar testes que executam codigo, mas nao protegem comportamento relevante.

## Como preparar o ambiente

O Stryker.NET e executado como ferramenta local do .NET versionada em `dotnet-tools.json`.

Confira os runtimes instalados:

```bash
dotnet --list-runtimes
```

O Stryker.NET 4.x precisa do runtime .NET 8 ou superior para executar a ferramenta. A aplicacao testada nao precisa mirar .NET 8 ou superior; ela pode continuar mirando o target framework definido nos `.csproj`.

Restaure as ferramentas locais:

```bash
dotnet tool restore
```

## Como executar

### LedgerService.Application

Comando manual recomendado:

```bash
cd tests/LedgerService.UnitTests
dotnet stryker
```

O arquivo `tests/LedgerService.UnitTests/stryker-config.json` e carregado automaticamente quando o comando roda a partir desse diretorio.

Scripts opcionais:

```powershell
./scripts/run-mutation-tests-ledger-application.ps1
```

```bash
./scripts/run-mutation-tests-ledger-application.sh
```

### BalanceService.Application

Comando manual recomendado:

```bash
cd tests/BalanceService.UnitTests
dotnet stryker
```

O arquivo `tests/BalanceService.UnitTests/stryker-config.json` e carregado automaticamente quando o comando roda a partir desse diretorio. Se quiser informar o arquivo explicitamente:

```bash
cd tests/BalanceService.UnitTests
dotnet stryker --config-file stryker-config.json
```

Scripts opcionais:

```powershell
./scripts/run-mutation-tests-balance-application.ps1
```

```bash
./scripts/run-mutation-tests-balance-application.sh
```

Esses comandos apenas restauram as ferramentas locais e executam `dotnet stryker` no diretorio do projeto de testes. Eles nao fazem parte do fluxo normal de build, teste, push ou pull request.

## Escopo configurado

As configuracoes atuais usam:

- `project` para escolher o projeto sob teste quando o projeto de testes possui varias referencias;
- `mutate` em `**/*.cs`, relativo ao projeto sob mutacao escolhido por `project`;
- exclusoes para `bin`, `obj` e arquivos gerados;
- `mutation-level: "Standard"` para reduzir ruido e custo inicial;
- `coverage-analysis: "perTest"` para melhorar performance usando informacao de cobertura por teste;
- reporters `progress`, `html` e `json`;
- thresholds `high: 80`, `low: 60` e `break: 0`.

O `break` fica em `0` porque esta fase e diagnostica e local. Futuramente o time pode elevar esse valor de forma progressiva depois de conhecer o score real de cada alvo.

## Onde fica o relatorio

O Stryker gera os relatorios dentro do diretorio `StrykerOutput` no diretorio em que foi executado.

Para `LedgerService.Application`, o relatorio HTML fica em:

```text
tests/LedgerService.UnitTests/StrykerOutput/**/reports/mutation-report.html
```

Para `BalanceService.Application`, o relatorio HTML fica em:

```text
tests/BalanceService.UnitTests/StrykerOutput/**/reports/mutation-report.html
```

Na validacao local de 2026-05-05, a execucao em `tests/BalanceService.UnitTests` gerou:

```text
tests/BalanceService.UnitTests/StrykerOutput/2026-05-05.18-36-39/reports/mutation-report.html
```

Baseline observado nessa execucao: score 27,91%, com 24 `Killed`, 36 `Survived`, 26 `NoCoverage`, 0 `Timeout`, 10 `Ignored` e 3 `CompileError`.

O relatorio JSON fica no mesmo diretorio de reports e pode ser usado futuramente para automacoes. Nesta fase, o foco e leitura humana pelo HTML report.

A pasta `StrykerOutput/` nao deve ser versionada.

## Como ler o relatorio

Principais status:

- `Killed`: o teste falhou quando o codigo foi mutado. E um bom sinal.
- `Survived`: os testes continuaram passando mesmo com a alteracao no codigo. Deve ser analisado.
- `NoCoverage`: nenhum teste cobriu aquele mutante.
- `Timeout`: a execucao excedeu o tempo esperado. Pode indicar loop, teste lento, dependencia externa ou mutante problematico.
- `Ignored`: mutante ignorado por configuracao ou comentario.
- `CompileError`: a mutacao gerou codigo que nao compila.

O Mutation Score resume quantos mutantes foram detectados pelos testes. Score alto nao garante ausencia de bug. Score baixo nao deve gerar correcao automatica sem analise. Mutantes sobreviventes em regras de negocio sao mais importantes do que mutantes sobreviventes em codigo trivial.

Mutantes equivalentes podem existir. Eles representam alteracoes que, na pratica, nao mudam comportamento observavel e precisam de analise tecnica antes de qualquer ignore.

## Como agir diante de mutantes sobreviventes

1. Verifique se o mutante altera comportamento observavel.
2. Se alterar comportamento relevante, melhore o teste.
3. Priorize testes por comportamento, nao por detalhe interno.
4. Evite acoplar teste a implementacao sem necessidade.
5. Nao altere a regra de negocio para matar mutante.
6. Nao crie teste artificial sem valor funcional.
7. Se o mutante for equivalente ou irrelevante, avalie ignorar com justificativa.
8. Registre padroes recorrentes encontrados para orientar melhorias futuras.

Para `BalanceService.Application`, priorize sobreviventes em:

- calculo de saldo;
- decisoes condicionais;
- validacoes;
- regras de consistencia;
- tratamento de eventos;
- regras de sucesso e falha;
- valores limites;
- nulidade;
- comparacoes;
- operadores booleanos.

## O que evitar

- Nao rode inicialmente na solution inteira.
- Nao aplique em `Infrastructure` nesta etapa.
- Nao aplique em `Api` nesta etapa.
- Nao aplique em migrations.
- Nao aplique em `Program.cs`.
- Nao aplique em codigo gerado.
- Nao torne obrigatorio no CI ainda.
- Nao suba o `break` threshold sem baseline real.
- Nao ignore mutantes apenas para melhorar score.
- Nao escreva testes frageis focados em implementacao interna.
- Nao altere regra de negocio para agradar a ferramenta.
- Nao enfraqueca asserts existentes para reduzir mutantes sobreviventes.

## Proximos passos

1. Rodar Stryker localmente no `LedgerService.Application` e `BalanceService.Application`.
2. Comparar os scores dos dois alvos.
3. Identificar padroes de mutantes sobreviventes.
4. Melhorar testes onde houver comportamento relevante nao protegido.
5. Avaliar job local agregado para os dois alvos.
6. Avaliar job manual em pipeline remota futuramente.
7. So depois avaliar threshold progressivo.

## Referencias

- Stryker.NET Getting started: <https://stryker-mutator.io/docs/stryker-net/getting-started/>
- Stryker.NET Configuration: <https://stryker-mutator.io/docs/stryker-net/configuration/>
- Stryker.NET Reporters: <https://stryker-mutator.io/docs/stryker-net/reporters/>
