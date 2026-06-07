# Rebuild de projecao do Balance

Este runbook descreve a abordagem segura para reconstruir a projecao de saldos
do Balance em modo paralelo logico, com relatorio de divergencia antes de
qualquer alteracao destrutiva.

O mecanismo disponivel nao cria endpoint publico, nao apaga dados, nao troca
tabela automaticamente e nao corrige saldos por conta propria. Ele e um caso de
uso interno de `BalanceService.Application` para apoiar uma decisao operacional
posterior.

## Abordagem escolhida

A primeira implementacao usa relatorio calculado sem persistencia.

Alternativas avaliadas:

| Alternativa | Avaliacao |
| --- | --- |
| Tabela temporaria | Boa para volumes maiores, mas exige schema operacional, transacao mais longa e cuidados extras com conexao. |
| Estrutura em memoria para teste | Segura e simples para a POC, mas precisa ler a projecao atual para comparar. |
| Tabela de rebuild | Boa para auditoria persistente e troca controlada futura, mas cria migration e superficie operacional maior. |
| Relatorio calculado sem persistencia | Menor abordagem segura agora. Reusa a fonte existente, valida contratos, simula saldo em memoria e nao altera `daily_balances` nem `processed_events`. |

Decisao atual: usar relatorio calculado sem persistencia. Uma tabela de rebuild
ou troca controlada deve ser decisao futura e explicita.

## Caso de uso interno

O caso de uso interno e:

- `ProjectionRebuildDivergenceReportCommand`;
- `ProjectionRebuildDivergenceReportHandler`;
- `ProjectionRebuildDivergenceReportResult`;
- `ProjectionRebuildDivergenceItem`;
- `ProjectionRebuildEventItemResult`.

A execucao deve ocorrer por uma superficie operacional controlada que resolva
`ISender` no mesmo composition root do `BalanceService.Worker` ou de uma
ferramenta interna. Nao existe endpoint administrativo publico para este fluxo.

Exemplo conceitual:

```csharp
ProjectionRebuildDivergenceReportResult report = await sender.Send(
    new ProjectionRebuildDivergenceReportCommand(
        new PartialProjectionRebuildFilter(
            MerchantId: "merchant-001",
            OccurredFrom: DateTimeOffset.Parse("2026-06-06T00:00:00Z"),
            OccurredUntil: DateTimeOffset.Parse("2026-06-07T00:00:00Z"),
            EventVersion: "v2"),
        "avaliacao operacional antes de rebuild"),
    cancellationToken);
```

## Fonte logica

O relatorio usa a mesma porta da reconstrucao parcial:

- `IFilteredEventReplaySource`;
- implementacao atual `OutboxFilteredEventReplaySource`;
- fonte concreta atual `ledger.outbox_messages`;
- evento logico `LedgerEntryCreated`;
- status da Outbox `Processed`.

A fonte le payload, `event_type`, `occurred_at`, `merchantId`, `accountId`
quando existir, correlacao e tracing. Ela nao altera a Outbox, nao requeue
mensagens e nao confirma entregas de Pub/Sub ou Kafka.

Pub/Sub e Kafka continuam sendo providers de entrega. Eles nao sao removidos e
nao definem a regra de rebuild. Se uma fonte futura ler broker ou DLQ, ela deve
traduzir os dados para o mesmo contrato de aplicacao antes do calculo.

## Protecoes

O relatorio:

- valida `eventName`, `eventVersion` e payload com o catalogo de JSON Schemas;
- rejeita versao nao suportada;
- rejeita payload invalido ou fora do filtro;
- deduplica eventos por `payload.id` dentro do lote;
- preserva idempotencia porque nao cria novo identificador;
- calcula o saldo esperado em memoria;
- compara contra a projecao atual lida de `daily_balances`;
- nao chama delete, insert, update, `SaveChanges` ou commit;
- retorna `Mutated=false`.

Duplicatas dentro do lote aparecem como `DuplicateInBatch` e nao entram no
saldo reconstruido.

## Formato do relatorio

O resultado possui contadores globais:

- `ReportId`;
- `Mutated`;
- `FilterDescription`;
- `TotalFound`;
- `TotalValid`;
- `TotalInvalid`;
- `TotalDuplicates`;
- `TotalCompared`;
- `HasDivergences`.

Cada item de comparacao contem:

- `AccountId`, quando todos os eventos daquele saldo carregarem a mesma conta;
- `MerchantId`;
- `Date`;
- `Currency`;
- `CurrentBalance`;
- `RebuiltBalance`;
- `Difference`, calculada como saldo reconstruido menos saldo atual;
- `EventsAnalyzed`;
- `InvalidEvents`;
- `DuplicateEventsIgnored`.

Eventos individuais tambem aparecem em `Events` com:

- `SourceId`;
- `EventId`;
- `EventName`;
- `EventVersion`;
- `AccountId`, quando a fonte possuir esse campo;
- `Status`;
- `ErrorMessage`.

Eventos invalidos podem nao gerar item de saldo, porque a data, moeda ou valor
podem nao ser confiaveis. Mesmo assim, eles ficam visiveis em `Events` e entram
em `TotalInvalid`.

## Interpretacao

Quando `HasDivergences=false`, o saldo atual e o saldo reconstruido coincidem
para as chaves comparadas.

Quando `HasDivergences=true`, registre o relatorio, investigue a causa raiz e
defina uma etapa operacional separada. O relatorio por si so nao autoriza
correcao automatica.

Uma diferenca positiva indica que o saldo reconstruido ficou maior que o saldo
atual. Uma diferenca negativa indica que o saldo reconstruido ficou menor que o
saldo atual.

## Limites atuais

- O filtro por `accountId` nao e usado para rebuild da projecao atual, porque
  `LedgerEntryCreated.v1` e `LedgerEntryCreated.v2` nao possuem `accountId` no
  contrato logico e `daily_balances` agrega por merchant, data e moeda.
- O relatorio nao persiste auditoria dedicada. O operador deve anexar o
  resultado ao registro operacional.
- O relatorio nao troca tabela, nao promove tabela paralela e nao aplica
  correcao financeira.
- O relatorio nao tenta inferir dias sem eventos quando a fonte retorna lote
  vazio.
- O limite de lote continua limitado entre 1 e 1000 itens.

## Etapa futura explicita

Qualquer troca, promocao, truncamento, correcao automatica ou escrita em uma
tabela de rebuild precisa ser planejada como etapa futura e explicita. Essa
decisao deve definir fonte de verdade, auditoria persistente, janela, criterio
de parada, rollback operacional, seguranca, autorizacao e validacoes antes de
alterar a projecao atual.
