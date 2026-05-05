# ADR-0042: Mutation testing local com Stryker no LedgerService.Application

## Status
Aceito

## Data
2026-05-05

## Contexto
O repositorio ja valida testes automatizados e cobertura consolidada da solution, mas cobertura tradicional indica apenas que o codigo foi executado. Ela nao responde se os testes detectariam pequenas alteracoes indevidas de comportamento.

Mutation testing complementa esse diagnostico ao gerar mutantes e verificar se os testes falham diante dessas alteracoes. Como a execucao pode ser custosa, a adocao inicial deve ser local, incremental e sem bloquear pull requests.

## Decisao
Adotar Stryker.NET como ferramenta local versionada em `dotnet-tools.json`, com configuracao inicial em `tests/LedgerService.UnitTests/stryker-config.json`.

O alvo inicial e somente `src/LedgerService.Application`, selecionado por `project: "LedgerService.Application.csproj"` e por padroes `mutate` restritos a essa camada.

A configuracao usa `mutation-level: "Standard"`, `coverage-analysis: "perTest"`, reporters `progress`, `html` e `json`, e thresholds com `break: 0`.

A execucao permanece opcional e local. Nenhum workflow remoto passa a executar mutation testing como quality gate nesta decisao.

A documentacao operacional fica em `docs/development/mutation-testing-stryker.md`.

## Consequencias

### Beneficios
- Complementa a cobertura tradicional com um diagnostico de forca dos testes.
- Mantem a adocao incremental focada em regras de aplicacao do Ledger.
- Evita aumentar o tempo de PR e CI neste primeiro momento.
- Cria uma base reprodutivel para comparar scores futuros.

### Trade-offs / custos
- A execucao local pode ser mais lenta que testes comuns.
- O score inicial pode revelar mutantes sobreviventes que exigem triagem manual.
- Mutantes equivalentes ou irrelevantes precisam de criterio para nao gerar testes artificiais.

### Riscos
- Usar o score como meta cega pode incentivar testes acoplados a detalhes internos.
- Elevar `break` sem baseline real pode criar bloqueios artificiais.
- Expandir para a solution inteira cedo demais pode aumentar custo e ruido.

## Alternativas consideradas

1. **Executar mutation testing na solution inteira**
   - Daria visao ampla, mas aumentaria custo, ruido e tempo de feedback antes de haver baseline.

2. **Adicionar job obrigatorio no CI**
   - Criaria gate imediato, mas sem score inicial e sem triagem dos mutantes sobreviventes.

3. **Nao versionar configuracao**
   - Seria menos invasivo, mas deixaria a execucao local dependente de comandos manuais e suscetivel a drift entre maquinas.

## Proximos passos
- Rodar o Stryker localmente e registrar o score inicial.
- Melhorar testes para mutantes sobreviventes relevantes.
- Avaliar `BalanceService.Application` como segundo alvo.
- Avaliar um job manual no CI depois que o fluxo local estabilizar.
- Definir threshold progressivo apenas com base no baseline real.
