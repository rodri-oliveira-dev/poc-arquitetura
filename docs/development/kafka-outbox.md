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
- `traceparent`, quando houver contexto W3C persistido na Outbox ou `Activity` atual;
- `tracestate`, quando houver contexto W3C com tracestate;
- `baggage`, quando houver baggage persistido na Outbox ou `Activity` atual.

O `BalanceService.Api` exige `event_type=LedgerEntryCreated.v1`, usa `event_id` para rastreabilidade e idempotencia quando presente, restaura `traceparent`/`tracestate` como parent do span `kafka.consume`, reidrata `baggage` quando possivel e preserva headers relevantes ao enviar mensagens para a DLQ. Mensagens antigas sem headers W3C continuam validas; nesse caso, o consumo segue pelo fallback funcional e pode criar um span raiz quando OpenTelemetry estiver habilitado.

## Outbox

Estados esperados:

- `Pending`: mensagem criada e aguardando publicacao;
- `Processing`: mensagem reclamada por um publisher com lock temporario;
- `Sent`: mensagem publicada com sucesso;
- `Failed`: mensagem excedeu o limite de tentativas.

Mensagens `Failed` exigem investigacao antes de qualquer nova tentativa. O requeue operacional recoloca somente mensagens `Failed` em `Pending`; mensagens `Sent` nao sao reprocessadas e mensagens `Processing` validas continuam sob responsabilidade do lock do publisher.

A Outbox tambem persiste metadados opcionais de propagacao distribuida em `traceparent`, `tracestate` e `baggage`. Esses campos nao fazem parte do payload do evento, nao mudam contrato de negocio e servem apenas para reconstruir a arvore W3C entre o request HTTP original, o polling da Outbox, Kafka e o consumer do Balance. Quando OpenTelemetry esta desligado ou nao existe `Activity.Current`, esses campos ficam nulos e o fluxo continua usando `correlation_id`.

Configuracoes principais em `Outbox:Publisher`:

- `PollingIntervalSeconds`;
- `BatchSize`;
- `MaxParallelism`;
- `MaxAttempts`;
- `BaseBackoffSeconds`;
- `LockDurationSeconds`.

## Requeue operacional de Outbox Failed

Use o requeue quando a causa da falha ja tiver sido corrigida ou classificada como transiente, por exemplo indisponibilidade temporaria de Kafka, credenciais/ACL corrigidas, topico recriado ou configuracao de producer ajustada. Nao use para mascarar erro permanente de contrato, payload invalido, topico incorreto ou incompatibilidade de consumidor.

Endpoint administrativo:

- `POST /api/v1/outbox/failed/requeue`;
- exige JWT valido com scope `ledger.outbox.requeue`;
- exige `reason` e ao menos um filtro: `outboxMessageId`, `eventType`, `occurredFrom` ou `occurredUntil`;
- `limit` padrao: `50`; maximo: `100`;
- altera apenas mensagens com status `Failed`.

Exemplo controlado por id:

```bash
curl -i -X POST http://localhost:5226/api/v1/outbox/failed/requeue \
  -H "Authorization: Bearer <TOKEN_COM_LEDGER_OUTBOX_REQUEUE>" \
  -H "Content-Type: application/json" \
  -d '{
    "outboxMessageId": "00000000-0000-0000-0000-000000000001",
    "reason": "Kafka recuperado apos indisponibilidade temporaria"
  }'
```

Exemplo por tipo de evento e janela:

```bash
curl -i -X POST http://localhost:5226/api/v1/outbox/failed/requeue \
  -H "Authorization: Bearer <TOKEN_COM_LEDGER_OUTBOX_REQUEUE>" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "LedgerEntryCreated.v1",
    "occurredFrom": "2026-05-08T00:00:00",
    "occurredUntil": "2026-05-08T23:59:59",
    "limit": 25,
    "reason": "Requeue apos correcao de ACL do producer"
  }'
```

Cada mensagem requeued registra `requeue_count`, `last_requeued_at`, `last_requeued_by` e `last_requeue_reason`. O `OutboxKafkaPublisherService` publica depois pelo fluxo normal de polling, preservando headers, correlacao, retry/backoff e idempotencia at-least-once.

Procedimento recomendado:

1. Identifique a causa do `Failed` em logs, `last_error` e configuracao Kafka.
2. Corrija a causa raiz antes do requeue.
3. Prefira `outboxMessageId` para recuperacao pontual; use filtros por `eventType` e data apenas para incidentes conhecidos.
4. Execute o endpoint com `reason` claro e limite pequeno.
5. Aguarde o polling e confirme transicao para `Sent`.
6. Confirme no Balance que a projecao foi atualizada ou que `processed_events` manteve idempotencia em caso de reentrega.

Limitacoes e riscos:

- O requeue nao altera payload nem contrato de evento.
- Requeue de `LedgerEntryCreated.v1` pode gerar reentrega Kafka, esperada no modelo at-least-once; o Balance deve permanecer idempotente.
- Se a mensagem voltar para `Failed`, nao repita indefinidamente: investigue contrato, topico, ACL, serializacao e disponibilidade do broker.
- A auditoria persistente guarda o ultimo requeue e o contador; logs da API/publisher complementam a linha do tempo operacional.

## DLQ

Mensagens com falha de desserializacao, contrato invalido, payload invalido ou falha nao recuperavel sao publicadas em `ledger.ledgerentry.created.dlq`.

Politica de commit:

- processamento normal com sucesso: commita o offset original;
- publicacao na DLQ com sucesso: commita o offset original;
- falha ao publicar na DLQ: nao commita o offset original.

O envelope da DLQ preserva payload original quando disponivel, topico, particao, offset, headers relevantes, motivo, tipo da excecao e timestamp.

## Metricas operacionais

A mensageria publica metricas customizadas via `System.Diagnostics.Metrics` quando OpenTelemetry Metrics esta habilitado na API. A instrumentacao nao altera payloads, headers, topicos, contratos de evento, politica de retry ou politica de DLQ. Sem OpenTelemetry habilitado, as chamadas aos instrumentos continuam seguras, mas nao ha coleta/exportacao.

Metricas de dominio de lancamentos, estornos, reprocessamentos e projecoes de saldo ficam documentadas em [observabilidade](../observability.md#metricas-de-dominio). Este documento lista apenas metricas operacionais de mensageria para evitar duplicacao de vocabulario.

Metricas do `LedgerService.Api`:

- Outbox: `ledger.outbox.messages.created`, `ledger.outbox.messages.published`, `ledger.outbox.publish.duration`, `ledger.outbox.messages.pending`, `ledger.outbox.messages.failed`, `ledger.outbox.publish.attempts`.
- Kafka Producer: `ledger.kafka.producer.messages.published`, `ledger.kafka.producer.publish.duration`, `ledger.kafka.producer.errors`.

Metricas do `BalanceService.Api`:

- Kafka Consumer: `balance.kafka.consumer.messages.consumed`, `balance.kafka.consumer.processing.duration`, `balance.kafka.consumer.errors`, `balance.kafka.consumer.duplicates`.
- DLQ: `balance.kafka.dlq.messages.published`, `balance.kafka.dlq.publish.errors`.

Tags permitidas:

- Outbox: `event_type`, `topic`, `result`.
- Kafka Producer: `topic`, `event_type`, `result`, `error_type`.
- Kafka Consumer: `topic`, `event_type`, `result`, `error_type`.
- DLQ: `source_topic`, `event_type`, `reason`, `error_type`.

Tags proibidas por alta cardinalidade: `correlation_id`, `trace_id`, `span_id`, `event_id`, `outbox_message_id`, `merchant_id`, offsets, particoes especificas, payload e mensagem completa de exception. Para DLQ, a tag `reason` usa classificacao estavel (`deserialization_failed`, `validation_failed`, `non_recoverable_processing_failure`, `unknown`), nao texto livre.

Interpretacao rapida:

- backlog crescente: observe `ledger.outbox.messages.pending` por `event_type`;
- mensagens travadas: observe `ledger.outbox.messages.failed` e `ledger.outbox.messages.published{result="failure"}`;
- falhas de producer: observe `ledger.kafka.producer.errors`;
- consumo saudavel: observe `balance.kafka.consumer.messages.consumed{result="success"}`;
- duplicidade esperada por at-least-once: observe `balance.kafka.consumer.duplicates`;
- desvio para DLQ: observe `balance.kafka.dlq.messages.published` por `reason`;
- falha critica de DLQ: observe `balance.kafka.dlq.publish.errors`, pois o offset original nao deve ser commitado.

Estas metricas ainda nao possuem dashboard, alertas, Prometheus, Grafana ou OpenTelemetry Collector nesta etapa.

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

Para o roteiro operacional completo Auth -> Ledger -> Outbox -> Kafka -> Balance, incluindo `X-Correlation-Id`, logs, consultas SQL, Balance e Jaeger, use a secao [Validacao Auth -> Ledger -> Outbox -> Kafka -> Balance](../observability.md#validacao-auth---ledger---outbox---kafka---balance). O script recomendado e:

```powershell
./scripts/validate-auth-ledger-trace.ps1
```

## Processamento de estornos

O worker `EstornoLancamentoProcessorService` usa polling da tabela `estornos_lancamentos`, mas a selecao nao e apenas uma leitura. Cada ciclo reclama pendentes com `UPDATE ... FOR UPDATE SKIP LOCKED ... RETURNING`, mudando as linhas para `Processing` antes de delegar ao Mediator. Isso permite workers concorrentes sem processar o mesmo estorno ao mesmo tempo.

- `Pending`: selecionado para processamento;
- `Processing`: caso de uso iniciado;
- `Completed`: lancamento compensatorio persistido e evento final no Outbox;
- `Rejected`: regra de negocio impediu o estorno;
- `Failed`: falha tecnica ou inesperada registrada.

Configuracao:

- `Estornos:Processor:Enabled`;
- `Estornos:Processor:PollingIntervalSeconds`;
- `Estornos:Processor:BatchSize`.

A idempotencia e garantida por indice unico filtrado para uma solicitacao ativa por `lancamento_original_id`, claim atomico de pendentes, lock real por linha no processamento, status final, verificacao de estorno ja concluido por lancamento original, busca do lancamento compensatorio por `external_reference=estorno:{lancamentoOriginalId}` e indice unico filtrado para essa referencia. Reprocessar uma solicitacao concluida nao duplica lancamento nem evento final.

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
