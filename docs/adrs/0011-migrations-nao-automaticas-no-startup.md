# ADR-0011: Não aplicar migrations automaticamente no startup

## Status
Aceito

## Data
2026-02-17

## Contexto
Em ambientes com container/compose, é comum automatizar `database update` no startup. Porém isso pode:

- esconder comportamento (side effects) dentro do boot da aplicação;
- causar corrida entre múltiplas instâncias aplicando migrations ao mesmo tempo;
- gerar falhas difíceis de diagnosticar (app não sobe porque migration travou);
- criar risco em produção (migrations executadas sem controle).

Na PoC, queremos que a infra seja explícita e reprodutível, sem “mágica”.

## Decisão
Não aplicar migrations automaticamente no startup dos microserviços.

As migrations são aplicadas de forma explícita via `dotnet-ef database update`, conforme documentado no `README.md`.

Se houver necessidade de automação no futuro, preferimos:

- um **job/sidecar** explícito no compose/CI;
- controles (lock, retries, observabilidade) separados do runtime da API.

## Consequências

### Benefícios
- Boot mais previsível (API sobe/derruba por motivos de runtime, não por migração).
- Controle explícito do momento de mudança de schema.
- Evita corrida em scale-out.

### Trade-offs / custos
- Um passo manual/extra em ambiente local.
- Maior disciplina para manter docs e scripts atualizados.

## Alternativas consideradas

1) **Auto-migrate no startup**
   - Prós: menos passos manuais.
   - Contras: riscos de corrida e side effects; menos previsível.
