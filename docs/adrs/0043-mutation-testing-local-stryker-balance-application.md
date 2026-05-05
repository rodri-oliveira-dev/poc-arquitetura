# ADR-0043: Mutation testing local com Stryker no BalanceService.Application

## Status
Aceito

## Data
2026-05-05

## Contexto
O repositorio ja possui uma estrategia incremental e opcional de mutation testing local com Stryker.NET para `LedgerService.Application`.

O proximo alvo avaliado e `BalanceService.Application`, por concentrar fluxos de aplicacao ligados a consulta, calculo, atualizacao e consistencia de saldos. Esses comportamentos devem ser protegidos por testes que falhem diante de alteracoes indevidas observaveis.

Mutation testing continua sendo um diagnostico local. A execucao pode ser custosa e ainda nao ha baseline real suficiente para transformar score em bloqueio remoto.

## Decisao
Adicionar `BalanceService.Application` como segundo alvo local de mutation testing com Stryker.NET.

A configuracao fica em `tests/BalanceService.UnitTests/stryker-config.json`, executada a partir do projeto de testes unitarios que referencia `BalanceService.Application`.

O alvo sob mutacao e escolhido por `project: "BalanceService.Application.csproj"`, necessario porque `BalanceService.UnitTests` possui multiplas referencias de projeto.

A configuracao usa `mutation-level: "Standard"`, `coverage-analysis: "perTest"`, reporters `progress`, `html` e `json`, e thresholds com `break: 0`.

A execucao permanece opcional e local. Nenhum workflow remoto passa a executar mutation testing como quality gate nesta decisao.

A documentacao operacional fica em `docs/development/mutation-testing-stryker.md`.

## Consequencias

### Beneficios
- Expande a avaliacao de forca dos testes para o segundo contexto de aplicacao.
- Mantem a adocao incremental, com alvo pequeno e configuracao versionada.
- Ajuda a identificar testes que executam codigo de saldo, mas nao protegem comportamento relevante.
- Evita aumentar o tempo de pull requests e CI remoto nesta fase.

### Trade-offs / custos
- A execucao local pode ser mais lenta que testes comuns.
- Mutantes sobreviventes exigem triagem manual antes de qualquer ajuste.
- Mutantes equivalentes ou irrelevantes podem aparecer e precisam de justificativa para serem ignorados.

### Riscos
- Usar o score como meta cega pode incentivar testes artificiais ou acoplados a detalhes internos.
- Elevar `break` sem baseline real pode criar bloqueios artificiais.
- Expandir cedo para API, Infrastructure ou solution inteira pode aumentar custo e ruido.

## Alternativas consideradas

1. **Executar mutation testing na solution inteira**
   - Daria visao ampla, mas aumentaria custo, ruido e tempo de feedback antes de haver baseline por alvo.

2. **Adicionar job obrigatorio no CI**
   - Criaria gate imediato, mas sem score inicial e sem triagem dos mutantes sobreviventes.

3. **Rodar apenas o alvo LedgerService.Application**
   - Manteria o escopo atual, mas deixaria o fluxo de saldos sem diagnostico equivalente.

## Proximos passos
- Rodar o Stryker localmente em `LedgerService.Application` e `BalanceService.Application`.
- Comparar os scores dos dois alvos.
- Identificar padroes recorrentes de mutantes sobreviventes.
- Melhorar testes onde houver comportamento relevante nao protegido.
- Avaliar um comando local agregado para os dois alvos.
- Avaliar job manual em pipeline remota futuramente.
- Definir threshold progressivo apenas com base no baseline real.
