# ADR-0052: Processamento assincrono de reprocessamento de lancamentos no LedgerService

## Status
Aceito

## Data
2026-05-07

## Contexto
A ADR-0051 introduziu `POST /api/v1/lancamentos/reprocessar`, a tabela `reprocessamentos_lancamentos` e o evento operacional `ReprocessamentoLancamentosSolicitado.v1` no Outbox, publicado no topico `ledger.lancamentos.reprocessamento.solicitado`.

Antes desta decisao, o topico existia e era produzido pelo `LedgerService`, mas nenhum componente consumia a solicitacao. O `BalanceService` possui consumer Kafka, mas ele e responsavel por fatos financeiros finais (`LedgerEntryCreated.v1`) e nao deve executar regra de negocio de reprocessamento do Ledger.

O dominio atual persiste `LedgerEntry` com os valores financeiros ja calculados no momento da escrita. Nao ha, nesta POC, uma regra separada de recalculo historico de valor nem um mecanismo de rebuild completo de projecao do Balance.

## Decisao
O processamento de `ReprocessamentoLancamentosSolicitado.v1` pertence ao proprio `LedgerService`, por meio de um consumer Kafka.

Nota de evolucao: apos a separacao fisica entre APIs e Workers, o `ReprocessamentoLancamentosConsumerService` e seu processor passaram a residir no projeto `LedgerService.Worker`; `LedgerService.Infrastructure` permanece com persistencia e repositories compartilhados.

O `ReprocessamentoLancamentosConsumerService` consome `ledger.lancamentos.reprocessamento.solicitado`, valida o header `event_type=ReprocessamentoLancamentosSolicitado.v1` e delega o trabalho ao Mediator com `ProcessarReprocessamentoLancamentosCommand`. O handler em `LedgerService.Application` executa o caso de uso em uma unidade transacional curta:

- localiza a solicitacao em `reprocessamentos_lancamentos`;
- ignora solicitacoes em estado final para suportar retry e reentrega Kafka;
- marca a solicitacao como `Processing`;
- busca `LedgerEntry` do mesmo `merchantId` dentro do periodo informado;
- registra no Outbox um `LedgerEntryCreated.v1` para cada lancamento elegivel;
- marca a solicitacao como `Completed` quando houver lancamentos;
- marca como `CompletedWithWarnings` quando nenhum lancamento for encontrado;
- marca como `Rejected` para erro de regra conhecido e `Failed` para erro tecnico inesperado.

Nesta etapa, "reprocessar valores" significa fazer replay dos fatos financeiros atuais do Ledger. O payload final usa o valor persistido em `LedgerEntry.Amount` e os demais campos ja materializados (`Type`, `OccurredAt`, `MerchantId`, `Currency`, `CorrelationId`). O identificador do evento financeiro permanece derivado do lancamento (`lan_{lancamentoId}`), preservando a idempotencia do `BalanceService` por `processed_events`.

O `BalanceService` continua consumindo somente `LedgerEntryCreated.v1`. Ele nao consome `ReprocessamentoLancamentosSolicitado.v1` como evento financeiro. Assim, saldos/consolidados sao atualizados apenas por fatos financeiros finais e de forma idempotente.

## Consequencias positivas
- Fecha o topico operacional de reprocessamento com um consumidor real.
- Mantem regra e ownership de reprocessamento no bounded context do Ledger.
- Evita acoplar o `BalanceService` a uma solicitacao operacional do Ledger.
- Reutiliza Outbox e contrato financeiro final ja existente.
- Preserva idempotencia em retries de Kafka e reexecucao do command.
- Mantem o request HTTP leve, retornando `202 Accepted` sem trabalho pesado sincrono.
- Permite que projecoes ausentes sejam corrigidas por replay de eventos financeiros.

## Trade-offs / custos
- O reprocessamento atual faz replay de fatos persistidos; ele nao recalcula um valor por uma nova formula historica inexistente no dominio.
- Se o Balance ja processou um evento com regra antiga incorreta, o replay com o mesmo identificador sera ignorado por idempotencia e nao recompoe automaticamente o saldo.
- Nao foi criado um novo contrato `LancamentoReprocessado.v1`; o evento final continua sendo `LedgerEntryCreated.v1`.
- O consumer depende de Kafka para iniciar o processamento da solicitacao, diferente do estorno atual que usa polling de tabela.
- O controle de progresso por item ainda e simples: a solicitacao registra status final e motivo/erro, mas nao uma tabela de itens reprocessados.

## Alternativas consideradas
1. Fazer o `BalanceService` consumir `ReprocessamentoLancamentosSolicitado.v1`.
   - Rejeitada porque colocaria regra operacional do Ledger no servico de projecao e trataria uma solicitacao como fato financeiro final.

2. Processar o reprocessamento no controller HTTP.
   - Rejeitada porque violaria o contrato `202 Accepted`, aumentaria latencia e poderia criar transacoes longas.

3. Criar um worker por polling da tabela `reprocessamentos_lancamentos`.
   - Rejeitada nesta etapa porque o topico operacional ja existe e a tarefa era tornar `ledger.lancamentos.reprocessamento.solicitado` consumido.

4. Criar um evento financeiro novo `LancamentoReprocessado.v1`.
   - Adiada porque o Balance ja possui contrato, idempotencia e projecao para `LedgerEntryCreated.v1`; novo evento exigiria semantica adicional de substituicao/correcao de saldo.

5. Reconstruir integralmente `daily_balances` no Balance.
   - Adiada porque exige outro caso de uso, outra autorizacao operacional e controle cuidadoso de janela/progresso.

## Impacto nos testes
- Testes unitarios cobrem processamento de solicitacao `Pending`, transicao para `Processing`, replay de eventos, `Completed`, `CompletedWithWarnings`, `Failed` e idempotencia.
- Testes unitarios do processor Kafka cobrem mensagem valida, payload invalido, topico incorreto e `event_type` incorreto.
- Teste de integracao do Ledger cobre criacao do job, processamento via Mediator, filtro por merchant/periodo, Outbox final e reexecucao sem duplicidade.
- Teste do Balance garante que `ReprocessamentoLancamentosSolicitado.v1` nao e processado como evento financeiro.

## Impacto operacional
- Aplicar a migration que adiciona campos de tracking de status em `reprocessamentos_lancamentos`.
- Configurar `Reprocessamentos:Consumer` no `LedgerService.Api`.
- Monitorar `Pending`, `Processing`, `Completed`, `CompletedWithWarnings`, `Rejected` e `Failed`.
- Monitorar o Outbox para publicacao dos `LedgerEntryCreated.v1` gerados pelo replay.
- Para recomposicao completa de saldos ja projetados incorretamente, criar decisao e fluxo especificos.
