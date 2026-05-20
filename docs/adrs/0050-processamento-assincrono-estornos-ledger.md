# ADR-0050: Processamento assincrono de estornos no LedgerService

## Status
Aceito

## Data
2026-05-06

## Contexto
A ADR-0049 introduziu a solicitacao assincrona de estorno via `POST /api/v1/lancamentos/{lancamentoId}/estornos`, com status inicial `Pending` e evento operacional `LancamentoEstornoSolicitado.v1` no Outbox. Aquela decisao nao implementava o processamento financeiro do estorno.

O processamento precisa manter o `LedgerService` como dono das regras de lancamento, preservar consistencia eventual via Outbox e impedir que o `BalanceService` decida se um estorno e valido. Tambem precisa ser idempotente para retries do worker e reexecucao do comando.

## Decisao
As solicitacoes de estorno serao processadas pelo proprio `LedgerService`, por meio de worker/background processing.

Nota de evolucao: apos a separacao fisica entre APIs e Workers, o `EstornoLancamentoProcessorService` passou a residir no projeto `LedgerService.Worker`; `LedgerService.Infrastructure` permanece com persistencia e repositories compartilhados.

O worker (`EstornoLancamentoProcessorService`) faz polling de solicitacoes `Pending` em `estornos_lancamentos` e delega cada item ao Mediator com `ProcessarEstornoLancamentoCommand`. O handler em `LedgerService.Application` executa o caso de uso em uma unidade transacional:

- marca a solicitacao como `Processing`;
- carrega o lancamento original;
- valida se ainda nao ha estorno concluido para o lancamento;
- cria um lancamento compensatorio no dominio, invertendo tipo e valor;
- grava o vinculo em `LancamentoCompensatorioId`;
- marca a solicitacao como `Completed`;
- registra no Outbox o evento financeiro final `LedgerEntryCreated.v1` do lancamento compensatorio.

`LancamentoEstornoSolicitado.v1` continua sendo mensagem operacional/intencao de processamento. O `BalanceService` nao consome esse evento como fato financeiro. O saldo e atualizado apenas quando o `BalanceService` consome `LedgerEntryCreated.v1` do lancamento compensatorio, usando a idempotencia ja existente por `ProcessedEvent`.

## Consequencias positivas
- Mantem regra de estorno no bounded context do Ledger.
- Evita regra de negocio do Ledger no `BalanceService`.
- Reutiliza o contrato financeiro ja consumido pelo Balance para atualizar saldos.
- Mantem endpoint HTTP sem processamento financeiro sincrono.
- Preserva consistencia eventual com Outbox transacional.
- Permite reprocessar o comando sem duplicar lancamento compensatorio nem evento final.
- Usa indice unico filtrado para reduzir duplicidade de compensatorios em concorrencia.

## Trade-offs
- O worker atual usa polling de tabela em vez de consumir o topico operacional de estorno.
- O evento final publicado e `LedgerEntryCreated.v1` do lancamento compensatorio, nao um novo contrato `LancamentoEstornado.v1`.
- A consistencia de saldo continua eventual: a solicitacao pode estar `Completed` no Ledger antes de o Outbox publicar e o Balance consumir o evento.
- Erros tecnicos definitivos sao marcados como `Failed`; retries operacionais futuros podem exigir politica mais rica de tentativas por solicitacao.

## Alternativas consideradas
1. Processar o estorno no controller.
   - Rejeitada porque aumentaria latencia HTTP e quebraria o fluxo assincrono definido na ADR-0049.

2. Fazer o `BalanceService` consumir `LancamentoEstornoSolicitado.v1` e aplicar saldo.
   - Rejeitada porque transferiria regra de negocio do Ledger para outro bounded context.

3. Criar um novo evento `LancamentoEstornado.v1` consumido pelo Balance.
   - Adiada porque o Balance ja possui contrato e idempotencia para `LedgerEntryCreated.v1`; publicar o lancamento compensatorio como fato financeiro final reduz mudanca de contrato.

4. Consumir o topico `ledger.lancamento.estorno.solicitado` dentro do proprio Ledger.
   - Adiada porque o repositorio ja possui persistencia da solicitacao e workers por polling; o polling evita dependencia circular de Kafka para processar uma intencao ja persistida localmente.

## Impacto nos testes
- Testes unitarios cobrem processamento valido, rejeicao de negocio, idempotencia e nao duplicidade.
- Testes de worker cobrem localizacao de pendentes, delegacao ao Mediator e cancelamento.
- Teste de integracao do Ledger valida endpoint, processamento via Mediator, persistencia do vinculo e Outbox final.
- Testes do Balance validam compensacao de credito/debito e rejeicao do evento operacional como evento financeiro.

## Impacto operacional
- Aplicar a nova migration do `LedgerService`.
- Configurar `Estornos:Processor` para habilitar/desabilitar o worker, intervalo de polling e tamanho do lote.
- Monitorar status `Pending`, `Processing`, `Completed`, `Rejected` e `Failed` em `estornos_lancamentos`.
- O saldo no Balance sera atualizado apos publicacao e consumo do `LedgerEntryCreated.v1` compensatorio.
