# Kafka, Outbox e DLQ

Este documento concentra a referencia de mensageria entre `LedgerService.Api` e `BalanceService.Api`.

## Fluxo

1. `LedgerService.Api` cria um lancamento, registra uma solicitacao de estorno ou registra uma solicitacao de reprocessamento.
2. A mesma transacao grava a mensagem em `outbox_messages`.
3. `OutboxKafkaPublisherService` le mensagens pendentes e publica no Kafka.
4. `EstornoLancamentoProcessorService`, no proprio Ledger, processa solicitacoes `Pending` e cria lancamentos compensatorios.
5. O estorno concluido grava `LedgerEntryCreated.v1` do lancamento compensatorio no Outbox.
6. `ReprocessamentoLancamentosConsumerService`, no proprio Ledger, consome `ledger.lancamentos.reprocessamento.solicitado`, chama o caso de uso de reprocessamento e registra eventos financeiros finais no Outbox quando houver lancamentos elegiveis.
7. `BalanceService.Api` consome apenas `LedgerEntryCreated.v1` e atualiza a projecao `daily_balances`.
8. Mensagens invalidas ou nao recuperaveis do fluxo consumido pelo Balance sao publicadas na DLQ.

## Topicos e evento

| Item | Valor |
| --- | --- |
| Evento de lancamento | `LedgerEntryCreated.v1` |
| Topico de lancamento | `ledger.ledgerentry.created` |
| Evento de solicitacao de estorno | `LancamentoEstornoSolicitado.v1` |
| Topico de solicitacao de estorno | `ledger.lancamento.estorno.solicitado` |
| Evento de solicitacao de reprocessamento | `ReprocessamentoLancamentosSolicitado.v1` |
| Topico de solicitacao de reprocessamento | `ledger.lancamentos.reprocessamento.solicitado` |
| DLQ | `ledger.ledgerentry.created.dlq` |
| Mapeamentos | `LedgerEntryCreated.v1` -> `ledger.ledgerentry.created`; `LancamentoEstornoSolicitado.v1` -> `ledger.lancamento.estorno.solicitado`; `ReprocessamentoLancamentosSolicitado.v1` -> `ledger.lancamentos.reprocessamento.solicitado` |

`LancamentoEstornoSolicitado.v1` e gravado pelo Ledger no Outbox e publicado pelo mesmo worker. Ele representa a intencao operacional de estorno, nao um fato financeiro final. O processamento financeiro nao depende do `BalanceService`: o proprio Ledger processa a solicitacao persistida e, ao concluir, registra um `LedgerEntryCreated.v1` para o lancamento compensatorio.

O `BalanceService.Api` deve ignorar/rejeitar `LancamentoEstornoSolicitado.v1` como evento financeiro. Saldos so mudam com `LedgerEntryCreated.v1`, inclusive quando esse evento representa o lancamento compensatorio de um estorno.

`ReprocessamentoLancamentosSolicitado.v1` tambem e evento operacional/intencao interna. Ele nao representa conclusao nem alteracao direta de saldo. O `LedgerService` e o dono do processamento: o consumer de reprocessamento le esse topico, localiza a solicitacao persistida, muda o status e republica `LedgerEntryCreated.v1` para os lancamentos elegiveis como evento financeiro final. O `BalanceService` nao consome a solicitacao operacional.

O compose cria os topicos no startup local. O consumer do Balance usa `AllowAutoCreateTopics=false`.

## Headers

Headers publicados pelo producer:

- `event_id`;
- `event_type`;
- `correlation_id`, quando existir;
- `traceparent`, quando houver `Activity`;
- `baggage`, quando houver `Activity`.

O `BalanceService.Api` exige `event_type=LedgerEntryCreated.v1`, usa `event_id` para rastreabilidade e idempotencia quando presente e preserva headers relevantes ao enviar mensagens para a DLQ.

## Outbox

Estados esperados:

- `Pending`: mensagem criada e aguardando publicacao;
- `Processing`: mensagem reclamada por um publisher com lock temporario;
- `Sent`: mensagem publicada com sucesso;
- `Failed`: mensagem excedeu o limite de tentativas.

Configuracoes principais em `Outbox:Publisher`:

- `PollingIntervalSeconds`;
- `BatchSize`;
- `MaxParallelism`;
- `MaxAttempts`;
- `BaseBackoffSeconds`;
- `LockDurationSeconds`.

## DLQ

Mensagens com falha de desserializacao, contrato invalido, payload invalido ou falha nao recuperavel sao publicadas em `ledger.ledgerentry.created.dlq`.

Politica de commit:

- processamento normal com sucesso: commita o offset original;
- publicacao na DLQ com sucesso: commita o offset original;
- falha ao publicar na DLQ: nao commita o offset original.

O envelope da DLQ preserva payload original quando disponivel, topico, particao, offset, headers relevantes, motivo, tipo da excecao e timestamp.

## Configuracao

Configuracoes ficam nos `appsettings*.json` dos projetos de API.

Ledger:

- `Kafka:Producer`;
- `Outbox:Publisher`.
- `Reprocessamentos:Consumer`.

Balance:

- `Kafka:Consumer`;
- `Kafka:DeadLetterProducer`.

`SecurityProtocol=Plaintext` existe apenas para execucao local (`Development`/`Local`) e para o ambiente `Test`. Em ambientes compartilhados ou produtivos, configure `SSL` ou `SASL_SSL` com os parametros operacionais por variaveis de ambiente ou secret store.

## Validacao rapida

1. Suba a stack local.
2. Aplique migrations.
3. Obtenha um token conforme [autenticacao e autorizacao](authentication.md).
4. Crie um lancamento em `POST /api/v1/lancamentos`.
5. Verifique no banco Ledger uma linha em `outbox_messages` com `Pending`.
6. Aguarde o polling e confirme transicao para `Sent`.
7. Consulte o Balance para confirmar atualizacao da projecao.

Em falha do Kafka, o servico nao deve cair: ele registra erro, incrementa tentativas e agenda `next_attempt_at` com backoff.

## Processamento de estornos

O worker `EstornoLancamentoProcessorService` usa polling da tabela `estornos_lancamentos`:

- `Pending`: selecionado para processamento;
- `Processing`: caso de uso iniciado;
- `Completed`: lancamento compensatorio persistido e evento final no Outbox;
- `Rejected`: regra de negocio impediu o estorno;
- `Failed`: falha tecnica ou inesperada registrada.

Configuracao:

- `Estornos:Processor:Enabled`;
- `Estornos:Processor:PollingIntervalSeconds`;
- `Estornos:Processor:BatchSize`.

A idempotencia e garantida por status final, verificacao de estorno ja concluido por lancamento original, busca do lancamento compensatorio por `external_reference=estorno:{lancamentoOriginalId}` e indice unico filtrado para essa referencia. Reprocessar uma solicitacao concluida nao duplica lancamento nem evento final.

## Solicitacoes de reprocessamento

`POST /api/v1/lancamentos/reprocessar` persiste solicitacoes em `reprocessamentos_lancamentos` e grava `ReprocessamentoLancamentosSolicitado.v1` no Outbox na mesma transacao. O status inicial e `Pending`, com periodo maximo inclusivo de 31 dias e idempotencia por `merchantId` + `Idempotency-Key`.

O processamento efetivo ocorre no `ReprocessamentoLancamentosConsumerService`, em `LedgerService.Infrastructure`. O hosted service usa as configuracoes de `Reprocessamentos:Consumer`, assina o topico `ledger.lancamentos.reprocessamento.solicitado`, valida `event_type=ReprocessamentoLancamentosSolicitado.v1` e delega o trabalho ao Mediator com `ProcessarReprocessamentoLancamentosCommand`.

O handler:

1. localiza a solicitacao por `reprocessamentoId`;
2. ignora solicitacoes ja finais para suportar retry e reentrega Kafka;
3. marca a solicitacao como `Processing`;
4. busca `LedgerEntry` do mesmo `merchantId` no periodo inclusivo informado;
5. registra no Outbox um `LedgerEntryCreated.v1` para cada lancamento elegivel;
6. marca como `Completed` quando houver lancamentos ou `CompletedWithWarnings` quando nenhum lancamento for encontrado;
7. marca como `Rejected` em erro de regra conhecido e `Failed` em erro tecnico inesperado.

O reprocessamento de valores, nesta POC, e feito por replay idempotente dos fatos financeiros persistidos no Ledger. O payload final usa os campos atuais do `LedgerEntry` (`Amount`, `Type`, `OccurredAt`, `MerchantId`, `Currency` etc.) e o mesmo identificador de evento financeiro derivado do lancamento (`lan_{id}`). Assim, se o Balance ja aplicou aquele lancamento, a tabela `processed_events` evita duplicidade; se o evento estava ausente na projecao, o consumer aplica o saldo/consolidado pelo fluxo normal.

Limitacao conhecida: esse fluxo corrige projecoes ausentes por replay de eventos do Ledger, mas nao reconstroi saldos ja materializados com regra historica incorreta. Uma recomposicao completa de projecao ou evento especifico de correcao de valor deve ser modelado em decisao futura se essa necessidade aparecer.

## Governanca

Mudancas em topicos, headers, tipos de evento, payload ou politica de DLQ podem afetar produtores, consumidores e projecoes. Atualize testes, documentacao e ADRs quando houver decisao nova ou mudanca de contrato.
