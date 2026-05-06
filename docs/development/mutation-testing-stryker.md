# Mutation testing com Stryker.NET

Este documento registra a configuracao local, incremental e opcional de mutation testing com Stryker.NET.

## O que e mutation testing

Mutation testing avalia a qualidade dos testes, nao apenas se uma linha foi executada.

Cobertura tradicional responde se o codigo foi exercitado. Mutation testing responde se os testes perceberiam uma pequena alteracao indevida no comportamento. O Stryker.NET gera mutantes no codigo, executa os testes e classifica o resultado de cada mutante.

## Alvos atuais

Alvos configurados ate agora:

- Alvo 1: `LedgerService.Application`
- Alvo 2: `BalanceService.Application`

Ambos sao executados localmente a partir dos respectivos projetos de testes unitarios e tambem em uma pipeline informativa apos integracao na `main`. Nenhum alvo faz parte de workflow remoto obrigatorio ou quality gate.

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

## Execucao informativa no GitHub Actions

O workflow `Mutation Tests` fica em `.github/workflows/mutation-tests.yml`.

Ele roda em:

- `push` para a branch `main`; na pratica, isso cobre merges de PR para `main`;
- `workflow_dispatch`, para execucao manual opcional pela aba Actions.

Ele nao roda em `pull_request` nesta etapa. A intencao e dar visibilidade continua do mutation score integrado na `main`, sem aumentar custo e tempo do fluxo de PR.

### Caracteristica nao impeditiva

Esta pipeline e informativa:

- falhas do Stryker nao bloqueiam merge;
- mutation score baixo nao quebra a pipeline;
- os steps de Stryker usam `continue-on-error: true`;
- os artifacts sao publicados com `if: always()`, mesmo se a execucao do Stryker retornar erro;
- `thresholds.break` permanece em `0` nos arquivos `stryker-config.json`;
- transformar mutation testing em quality gate e uma decisao futura.

O workflow ainda executa restore e build antes do Stryker para falhar cedo em problemas basicos de ambiente ou compilacao da solution.

### Artifacts publicados

Cada alvo gera um artifact separado:

- `stryker-ledger-service-application`
- `stryker-balance-service-application`

Os artifacts publicam apenas o `mutation-report.html` gerado pelo Stryker e ficam retidos por 7 dias. A pasta `StrykerOutput/` completa e o JSON detalhado nao sao publicados como artifacts.

O HTML pode conter paths, nomes de tipos/testes e trechos de codigo mutado. Ele e mantido porque e o relatorio primario para analise humana de mutantes; a retencao curta reduz exposicao desnecessaria.

### Como acessar o relatorio no GitHub

1. Abra a aba **Actions** no GitHub.
2. Selecione o workflow **Mutation Tests**.
3. Abra a execucao desejada.
4. Baixe os artifacts `stryker-ledger-service-application` e/ou `stryker-balance-service-application`.
5. Extraia o artifact localmente.
6. Abra o arquivo `mutation-report.html` extraido do artifact no navegador.

O relatorio JSON continua sendo gerado localmente pelo Stryker quando configurado, mas nao e publicado pelo workflow. Nesta etapa, a leitura principal continua sendo humana pelo HTML.

### Como interpretar resultados da pipeline

Use os relatorios para observar tendencias e pontos frageis nos testes, nao para correcao automatica e cega.

- `Killed`: mutante detectado pelos testes.
- `Survived`: mutante sobreviveu e precisa de analise.
- `NoCoverage`: nao havia teste cobrindo aquele mutante.
- `Timeout`: execucao excedeu o tempo esperado.
- `Ignored`: mutante ignorado por configuracao.
- `CompileError`: mutacao gerou codigo invalido.

### Como agir diante dos resultados

1. Priorize `Survived` em regras de negocio e fluxos de aplicacao.
2. Priorize `NoCoverage` em fluxos criticos.
3. Ignore mutantes equivalentes apenas com justificativa.
4. Nao altere implementacao correta para agradar o Stryker.
5. Nao crie testes artificiais sem valor de comportamento.
6. Melhore testes de forma incremental.
7. Compare a evolucao do score ao longo do tempo.

## Execucao pelo VS Code Tasks

O workspace possui tasks locais em `.vscode/tasks.json` para executar mutation testing sem digitar os comandos manualmente.

Tasks disponiveis:

- `test:mutation:ledger`: executa mutation testing para `LedgerService.Application` a partir de `tests/LedgerService.UnitTests`.
- `test:mutation:balance`: executa mutation testing para `BalanceService.Application` a partir de `tests/BalanceService.UnitTests`.
- `test:mutation:all`: executa os dois alvos em sequencia, primeiro Ledger e depois Balance.

Como executar no VS Code:

1. Abra o Command Palette.
2. Execute `Tasks: Run Task`.
3. Escolha `test:mutation:ledger`, `test:mutation:balance` ou `test:mutation:all`.
4. Acompanhe a saida no terminal integrado.

Tambem e possivel configurar um keybinding local para qualquer uma dessas tasks, mas o repositorio nao versiona atalhos obrigatorios.

As tasks individuais executam `dotnet tool restore` antes de `dotnet stryker` e preservam o exit code do Stryker. Ao final, elas tentam imprimir o caminho completo de qualquer arquivo `mutation-report.html` encontrado em `StrykerOutput`.

Os relatorios HTML esperados ficam em:

```text
tests/LedgerService.UnitTests/StrykerOutput/**/reports/mutation-report.html
tests/BalanceService.UnitTests/StrykerOutput/**/reports/mutation-report.html
```

Para abrir o relatorio:

1. Copie o caminho exibido no terminal integrado.
2. Abra o arquivo `mutation-report.html` no navegador.
3. Analise o resumo geral.
4. Navegue por namespace, classe e mutante.
5. Priorize `Survived` e `NoCoverage` em regras de negocio e comportamento de aplicacao.

Cuidados:

- mutation testing pode ser demorado;
- durante o desenvolvimento, prefira executar um alvo por vez;
- use `test:mutation:all` quando quiser uma visao local mais completa;
- nao versione `StrykerOutput/`;
- nao persiga mutation score cegamente;
- nao altere regra de negocio para matar mutante;
- melhore testes apenas quando o mutante sobrevivente representar comportamento relevante.

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

1. Avaliar duracao media do workflow `Mutation Tests`.
2. Avaliar separar Ledger e Balance em jobs paralelos ou matrix.
3. Avaliar publicar score no GitHub Step Summary.
4. Avaliar rodar semanalmente com `schedule`.
5. Avaliar comentar resultado em PR futuramente.
6. Avaliar threshold progressivo somente depois de baseline estavel.
7. Avaliar quality gate apenas quando o time concordar com a maturidade dos testes.

## Referencias

- Stryker.NET Getting started: <https://stryker-mutator.io/docs/stryker-net/getting-started/>
- Stryker.NET Configuration: <https://stryker-mutator.io/docs/stryker-net/configuration/>
- Stryker.NET Reporters: <https://stryker-mutator.io/docs/stryker-net/reporters/>
