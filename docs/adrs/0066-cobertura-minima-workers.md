# ADR-0066: Cobertura minima dedicada para workers

## Status

Aceito

## Contexto

Os hosts `LedgerService.Worker` e `BalanceService.Worker` foram separados das APIs HTTP. A politica anterior validava 80% de cobertura consolidada da solution inteira, mas esse numero global podia mascarar baixa cobertura nos processos de background.

Os workers concentram composicao de HostedServices, consumers Kafka, publishers Outbox e validacao de options operacionais. Esses pontos precisam ser testaveis de forma isolada e visivel.

## Decisao

A validacao oficial de cobertura continua usando `coverlet.runsettings`, `XPlat Code Coverage` e ReportGenerator.

Alem do gate global de 80% de cobertura de linhas, os fluxos locais, `pre-push` e CI passam a exigir pelo menos 80% de cobertura de linhas para os assemblies:

- `LedgerService.Worker`;
- `BalanceService.Worker`.

Os testes especificos desses hosts ficam em projetos dedicados:

- `tests/LedgerService.Worker.Tests`;
- `tests/BalanceService.Worker.Tests`.

## Consequencias

- A baixa cobertura de um worker falha mesmo quando a cobertura global da solution estiver acima do minimo.
- Testes de APIs e testes de workers ficam separados por fronteira operacional.
- O repositorio mantem uma unica estrategia de coleta e relatorio de cobertura.
- Novos pontos de composicao dos workers devem ser acompanhados por testes nos projetos dedicados.
