# ADR-0044: Mutation testing informativo no GitHub Actions

## Status
Parcialmente substituido pela ADR-0107

## Data
2026-05-05

## Contexto
O repositorio ja possui configuracao local e opcional de mutation testing com Stryker.NET para `LedgerService.Application` e `BalanceService.Application`.

Nota historica: o gatilho automatico direto por `push` e o uso de `continue-on-error` nos steps do Stryker foram substituidos pela orquestracao pos-CI da `main` na [ADR-0107](./0107-orquestracao-pos-ci-main-release-zap-mutation.md). Esta ADR permanece como historico da introducao do workflow informativo.

As ADRs anteriores mantiveram essa pratica fora de workflows remotos para evitar custo e atrito nos pull requests enquanto o time ainda formava baseline dos scores.

Agora e desejavel ter visibilidade continua dos relatorios apos integracao na `main`, sem transformar mutation testing em quality gate e sem alterar a validacao obrigatoria de PR.

## Decisao
Criar um workflow separado em `.github/workflows/mutation-tests.yml`, chamado `Mutation Tests`, para executar mutation testing em `push` para `main` e por `workflow_dispatch`.

O workflow executa:

- `dotnet tool restore`;
- `dotnet restore ./LedgerService.slnx`;
- `dotnet build ./LedgerService.slnx --configuration Release --no-restore`;
- `dotnet stryker` em `tests/LedgerService.UnitTests`;
- `dotnet stryker` em `tests/BalanceService.UnitTests`.

As execucoes do Stryker usam `continue-on-error: true`, preservando o carater informativo. Os arquivos `stryker-config.json` continuam com `thresholds.break` em `0`.

Os relatorios sao publicados como artifacts separados:

- `stryker-ledger-service-application`;
- `stryker-balance-service-application`.

O upload dos artifacts usa `if: always()` para manter os relatorios disponiveis mesmo quando o Stryker retornar erro.

O workflow nao roda em `pull_request`, nao altera branch protection e nao participa de deploy.

## Consequencias

### Beneficios
- Cria visibilidade continua do mutation score integrado na `main`.
- Mantem o fluxo de PR sem custo adicional de mutation testing.
- Preserva a estrategia incremental e diagnostica.
- Facilita analise posterior com relatorios HTML/JSON publicados como artifacts.

### Trade-offs / custos
- A execucao em `main` pode consumir tempo relevante de runner.
- Como o Stryker e informativo, problemas encontrados exigem triagem manual posterior.
- Os artifacts tem retencao limitada e nao criam historico permanente de score.

### Riscos
- Tratar score baixo como falha implicita pode incentivar testes artificiais.
- Aumentar `thresholds.break` antes de baseline estavel pode transformar diagnostico em bloqueio prematuro.
- Se a duracao crescer, pode ser necessario separar Ledger e Balance em jobs independentes.

## Alternativas consideradas

1. **Adicionar mutation testing ao workflow principal de build/teste**
   - Foi rejeitado para evitar misturar diagnostico caro e informativo com o fluxo principal.

2. **Executar em pull requests**
   - Foi rejeitado nesta etapa para nao aumentar custo e tempo de feedback dos PRs.

3. **Criar quality gate por mutation score**
   - Foi rejeitado porque ainda nao ha baseline estavel nem acordo do time para bloqueio.

4. **Rodar Ledger e Balance em jobs paralelos**
   - Foi adiado para reduzir complexidade inicial; pode ser avaliado se a duracao do workflow justificar.

## Proximos passos
- Medir a duracao media do workflow.
- Avaliar publicar o score no GitHub Step Summary.
- Avaliar execucao agendada semanal.
- Avaliar paralelismo por alvo.
- Avaliar threshold progressivo somente depois de baseline estavel.
