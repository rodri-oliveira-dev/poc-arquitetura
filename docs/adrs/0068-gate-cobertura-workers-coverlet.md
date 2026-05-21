# ADR-0068: Gate de cobertura dos workers com emissao explicita no Coverlet

## Status

Aceito

## Contexto

O gate dedicado de cobertura dos workers dependia dos assemblies `LedgerService.Worker` e `BalanceService.Worker` aparecerem no relatorio consolidado. Durante a validacao, os testes dos workers executavam, mas o Coverlet nao emitia esses assemblies porque falhava ao resolver dependencias transitivas do host `Microsoft.NET.Sdk.Worker` durante a instrumentacao.

## Decisao

- Manter `coverlet.runsettings`, `XPlat Code Coverage` e ReportGenerator como fluxo oficial.
- Preservar o contexto de compilacao nos projetos `LedgerService.Worker` e `BalanceService.Worker` para permitir a instrumentacao dos assemblies executaveis.
- Aplicar gate minimo de 85% de cobertura de linhas na solution e nos assemblies `LedgerService.Worker` e `BalanceService.Worker`.
- Tratar assembly Worker ausente no relatorio como falha explicita, tanto localmente quanto em `pre-push` e workflows.

## Consequencias

- A coleta passa a evidenciar cobertura real dos workers, em vez de depender apenas de Application/Infrastructure.
- Baixa cobertura ou ausencia de assembly Worker falha o gate.
- Testes de composicao, options e configuracao tecnica dos workers devem continuar sem depender de Kafka, PostgreSQL, Docker ou timing real.
