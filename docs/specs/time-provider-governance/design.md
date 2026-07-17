# TimeProvider e governanca residual - design

## Workstream A - tempo e determinismo

O design adotado usa `TimeProvider` diretamente, sem criar nova interface. Cada composition root registra `TimeProvider.System`. Testes substituem esse registro por providers fixos derivados de `TimeProvider`.

Handlers e services deixam de aceitar parametros opcionais de relogio. Quando uma entidade ou aggregate precisa de instante para uma transicao, o instante e calculado na Application/Worker/Infrastructure e passado como argumento ao metodo de dominio.

Decisoes:

- `DateTimeOffset` permanece em contextos que ja modelam offset, como Balance, Transfer e Payment.
- `DateTime` UTC permanece no Ledger onde entidades e persistencia historica usam esse tipo.
- `TimeProvider.System.GetTimestamp()` permanece em gateways para medir duracao monotonicamente; essa medicao nao governa regra de negocio.
- Usos residuais de `DateTimeOffset.UtcNow` em adaptadores externos que materializam timestamps de provider ou fallback de payload ficam documentados como riscos residuais, nao como regra temporal de dominio.

## Workstream B - governanca

Arquivos de governanca ficam na raiz ou em `.github`, porque sao consumidos pelo GitHub e por novos contribuidores.

ADRs usam status canonicos:

- `Proposto`
- `Aceito`
- `Rejeitado`
- `Substituido`
- `Parcialmente substituido`
- `Parcialmente implementado`

A validacao `scripts/quality/validate-adrs.ps1` verifica padrao de nome e status. Ela e simples de proposito: evita adicionar dependencia nova e funciona no ambiente PowerShell ja usado pelo repositorio.
