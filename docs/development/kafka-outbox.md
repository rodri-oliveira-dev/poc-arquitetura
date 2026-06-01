# Kafka, Outbox e DLQ

Este documento concentra a referencia de mensageria entre `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Worker` e `BalanceService.Api`.

Kafka permanece como provider completo de mensageria nesta POC. O boundary dos workers usa portas neutras para publicacao, consumo e DLQ, enquanto os adapters Kafka concentram detalhes como topicos, partitions, offsets, keys e commit. O `LedgerService.Worker` tambem suporta Pub/Sub como provider alternativo para publicar a Outbox. O consumer e a DLQ Pub/Sub do Balance ja possuem adapters, mas ainda nao foram ativados na composition root nesta etapa.

Em termos de desenho, a aplicacao trata `OrderingKey` como conceito logico de ordenacao por agregado/entidade. No adapter Kafka, esse valor e materializado como message key e influencia o particionamento; no producer Pub/Sub, a ordering key opcional usa o `AggregateId`. Ack/nack, subscription e delivery attempt devem ser tratados como semantica propria do provider quando os consumers Pub/Sub forem implementados, sem simular partition, offset ou commit dentro dos processors neutros.

Configuracao neutra:

```json
{
  "Messaging": {
    "Provider": "Kafka"
  }
}
```

`Messaging:Provider` usa `Kafka` como default quando ausente. A configuracao existente `Kafka:Enabled=false` continua suportada para desligar os hosted services de mensageria em testes e cenarios locais especificos.

Para a publicacao Pub/Sub do Ledger, use `Messaging:Provider=PubSub`. A configuracao `PubSub:Enabled=false` desliga os hosted services relacionados a Pub/Sub de forma equivalente ao flag Kafka.

## Fluxo

1. `LedgerService.Api` cria um lancamento, registra uma solicitacao de estorno ou registra uma solicitacao de reprocessamento.
2. A mesma transacao grava a mensagem em `outbox_messages`.
3. `LedgerService.Worker` hospeda `OutboxPublisherService`, que le mensagens pendentes e publica pela porta de mensageria configurada: Kafka ou Pub/Sub.
4. `LedgerService.Worker` hospeda `EstornoLancamentoProcessorService`, que processa solicitacoes `Pending` e cria lancamentos compensatorios.
5. O estorno concluido grava `LedgerEntryCreated.v1` do lancamento compensatorio no Outbox.
6. `LedgerService.Worker` hospeda `ReprocessamentoLancamentosConsumerService`, que consome `ledger.lancamentos.reprocessamento.solicitado`, chama o caso de uso de reprocessamento e registra eventos financeiros finais no Outbox quando houver lancamentos elegiveis.
7. `BalanceService.Worker` consome apenas `LedgerEntryCreated.v1` e atualiza a projecao `daily_balances`.
8. Mensagens invalidas ou nao recuperaveis do fluxo consumido pelo Balance sao publicadas pela porta neutra de DLQ; nesta POC, a DLQ concreta e um topico Kafka.

No consumo Kafka do Balance, o fluxo interno esperado e:

```text
KafkaLedgerEventsConsumer
  -> KafkaReceivedMessageMapper
  -> ReceivedMessage
  -> LedgerEntryCreatedMessageProcessor
  -> Application Handler
```

O adapter Pub/Sub equivalente, ainda nao ativado na composition root, usa:

```text
LedgerEventsPubSubConsumer
  -> PubSubReceivedMessageMapper
  -> ReceivedMessage
  -> LedgerEntryCreatedMessageProcessor
  -> Application Handler
```

Quando o processor retorna `true`, o consumer responde com `ack`. Quando retorna `false` ou ocorre falha recuperavel, responde com `nack`.

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

O `BalanceService.Worker` deve ignorar/rejeitar `LancamentoEstornoSolicitado.v1` como evento financeiro. Saldos so mudam com `LedgerEntryCreated.v1`, inclusive quando esse evento representa o lancamento compensatorio de um estorno.

O contrato formal, o exemplo valido e a politica de compatibilidade de `LedgerEntryCreated.v1` ficam em [docs/contracts/events/LedgerEntryCreated.v1.md](../contracts/events/LedgerEntryCreated.v1.md). `currency` nao faz parte do payload atual: o Ledger nao recebe nem persiste moeda e o Balance usa `BRL` como limitacao conhecida da POC.

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

No producer Pub/Sub do Ledger, os mesmos metadados logicos sao publicados como attributes.

O `BalanceService.Worker` exige `event_type=LedgerEntryCreated.v1`, usa `event_id` para rastreabilidade e idempotencia quando presente, restaura `traceparent`/`tracestate` como parent do span `kafka.consume`, reidrata `baggage` quando possivel e preserva headers relevantes ao enviar mensagens para a DLQ. Mensagens antigas sem headers W3C continuam validas; nesse caso, o consumo segue pelo fallback funcional e pode criar um span raiz quando OpenTelemetry estiver habilitado.

## Outbox

A Outbox e neutra em relacao ao provider de mensageria: ela persiste a intencao de publicacao e o worker usa `IOutboxMessagePublisher` para entregar a mensagem pelo adapter configurado. No Ledger, esse adapter pode ser Kafka ou Pub/Sub.

Estados esperados:

- `Pending`: mensagem criada e aguardando publicacao;
- `Processing`: mensagem reclamada por um publisher com lock temporario;
- `Processed`: mensagem publicada com sucesso;
- `DeadLetter`: mensagem excedeu o limite de retries.

Mensagens `DeadLetter` exigem investigacao antes de qualquer nova tentativa. O requeue operacional recoloca somente mensagens `DeadLetter` em `Pending`; mensagens `Processed` nao sao reprocessadas e mensagens `Processing` validas continuam sob responsabilidade do lock do publisher.

A Outbox tambem persiste metadados opcionais de propagacao distribuida em `traceparent`, `tracestate` e `baggage`. Esses campos nao fazem parte do payload do evento, nao mudam contrato de negocio e servem apenas para reconstruir a arvore W3C entre o request HTTP original, o polling da Outbox, Kafka e o consumer do Balance. Quando OpenTelemetry esta desligado ou nao existe `Activity.Current`, esses campos ficam nulos e o fluxo continua usando `correlation_id`.

Configuracoes principais em `Outbox:Publisher`:

- `PollingIntervalSeconds`;
- `BatchSize`;
- `MaxParallelism`;
- `MaxAttempts`;
- `BaseBackoffSeconds`;
- `LockDurationSeconds`.

O claim ocorre antes da publicacao paralela e cada mensagem e validada novamente contra `lock_owner` e `locked_until` antes de publicar. O adapter Kafka limita a espera de publicacao por `Kafka:Producer:MessageTimeoutMs` (default de 30 segundos); `Outbox:Publisher:LockDurationSeconds` deve permanecer maior que o pior tempo esperado de publicacao para reduzir reclaim durante uma tentativa em andamento. O publisher nao renova locks durante a publicacao nesta etapa. Se adapters ou tempos de entrega futuros puderem ultrapassar essa janela com frequencia, renovacao periodica de lock deve ser avaliada como melhoria separada, preservando a semantica at-least-once.

## DLQ em banco e requeue operacional

Use o requeue quando a causa da falha ja tiver sido corrigida ou classificada como transiente, por exemplo indisponibilidade temporaria de Kafka, credenciais/ACL corrigidas, topico recriado ou configuracao de producer ajustada. Nao use para mascarar erro permanente de contrato, payload invalido, topico incorreto ou incompatibilidade de consumidor.

Endpoint administrativo:

- `GET /api/v1/outbox/dead-letters?page=1&pageSize=50`;
- `POST /api/v1/outbox/dead-letters/{id}/requeue`;
- exige JWT valido com scope `outbox.admin`;
- o requeue exige `reason`;
- altera apenas mensagens com status `DeadLetter`.

Exemplo controlado por id:

```bash
curl -i -X POST http://localhost:5226/api/v1/outbox/dead-letters/00000000-0000-0000-0000-000000000001/requeue \
  -H "Authorization: Bearer <TOKEN_COM_OUTBOX_ADMIN>" \
  -H "Content-Type: application/json" \
  -d '{
    "reason": "Kafka recuperado apos indisponibilidade temporaria"
  }'
```

Exemplo de inspecao:

```bash
curl -i "http://localhost:5226/api/v1/outbox/dead-letters?page=1&pageSize=50" \
  -H "Authorization: Bearer <TOKEN_COM_OUTBOX_ADMIN>"
```

Cada mensagem requeued registra `requeue_count`, `last_requeued_at`, `last_requeued_by` e `last_requeue_reason`. O `OutboxPublisherService` publica depois pelo fluxo normal de polling, preservando headers, correlacao, retry/backoff e idempotencia at-least-once.

Procedimento recomendado:

1. Identifique a causa do `DeadLetter` em logs, `last_error` e configuracao Kafka.
2. Corrija a causa raiz antes do requeue.
3. Prefira `outboxMessageId` para recuperacao pontual; use filtros por `eventType` e data apenas para incidentes conhecidos.
4. Execute o endpoint com `reason` claro e limite pequeno.
5. Aguarde o polling e confirme transicao para `Processed`.
6. Confirme no Balance que a projecao foi atualizada ou que `processed_events` manteve idempotencia em caso de reentrega.

Limitacoes e riscos:

- O requeue nao altera payload nem contrato de evento.
- Requeue de `LedgerEntryCreated.v1` pode gerar reentrega Kafka, esperada no modelo at-least-once; o Balance deve permanecer idempotente.
- Se a mensagem voltar para `DeadLetter`, nao repita indefinidamente: investigue contrato, topico, ACL, serializacao e disponibilidade do broker.
- A auditoria persistente guarda o ultimo requeue e o contador; logs da API/publisher complementam a linha do tempo operacional.

## DLQ

Mensagens com falha de desserializacao, contrato invalido, payload invalido ou falha nao recuperavel sao publicadas pela porta `IDeadLetterPublisher`. Nesta POC, o adapter Kafka publica em `ledger.ledgerentry.created.dlq`.

Politica de commit:

- processamento normal com sucesso: commita o offset original;
- publicacao na DLQ com sucesso: commita o offset original;
- falha ao publicar na DLQ: nao commita o offset original.

O envelope neutro da DLQ preserva payload original quando disponivel, origem logica, provider original, tipo de evento, attributes/headers relevantes, motivo, tipo da excecao, timestamp e metadados de transporte. No adapter Kafka, os metadados de transporte incluem topico, particao, offset e key quando disponivel; esses valores continuam sendo propagados nos headers Kafka da DLQ como `original_topic`, `original_partition` e `original_offset`. No adapter Pub/Sub, os metadados originais usam attributes com prefixo neutro `original_metadata_`, sem tornar topico, particao ou offset obrigatorios. Processors neutros nao devem depender de offset, partition ou commit.

## Metricas operacionais

A mensageria publica metricas customizadas via `System.Diagnostics.Metrics` quando OpenTelemetry Metrics esta habilitado no processo host correspondente. A instrumentacao nao altera payloads, headers, topicos, contratos de evento, politica de retry ou politica de DLQ. Sem OpenTelemetry habilitado, as chamadas aos instrumentos continuam seguras, mas nao ha coleta/exportacao.

Metricas de dominio de lancamentos, estornos, reprocessamentos e projecoes de saldo ficam documentadas em [observabilidade](../observability.md#metricas-de-dominio). Este documento lista apenas metricas operacionais de mensageria para evitar duplicacao de vocabulario.

Metricas do `LedgerService.Api`:

- Outbox criada pela escrita HTTP: `ledger.outbox.messages.created`.

Metricas do `LedgerService.Worker`:

- Outbox publisher: `ledger.outbox.messages.published`, `ledger.outbox.publish.duration`, `ledger.outbox.messages.pending`, `ledger.outbox.messages.dead_letter`, `ledger.outbox.publish.attempts`.
- Kafka Producer: `ledger.kafka.producer.messages.published`, `ledger.kafka.producer.publish.duration`, `ledger.kafka.producer.errors`.

Metricas do `BalanceService.Worker`:

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
- mensagens travadas: observe `ledger.outbox.messages.dead_letter` e `ledger.outbox.messages.published{result="failure"}`;
- falhas de producer: observe `ledger.kafka.producer.errors`;
- consumo saudavel: observe `balance.kafka.consumer.messages.consumed{result="success"}`;
- duplicidade esperada por at-least-once: observe `balance.kafka.consumer.duplicates`;
- desvio para DLQ: observe `balance.kafka.dlq.messages.published` por `reason`;
- falha critica de DLQ: observe `balance.kafka.dlq.publish.errors`, pois o offset original nao deve ser commitado.

Estas metricas nao possuem dashboard especifico nem alertas nesta etapa. No compose local, o OpenTelemetry Collector recebe metricas OTLP e as expoe no exporter Prometheus em `otel-collector:9464`; o Prometheus faz scrape apenas do Collector e o Grafana usa o datasource Prometheus provisionado. A validacao operacional do fluxo distribuido continua focada em traces no Jaeger e estados funcionais, sem alterar contratos Kafka, payloads, topicos ou politica de DLQ.

## Configuracao

Configuracoes de mensageria do Ledger ficam em `src/LedgerService.Worker/appsettings.json`. A API do Ledger mantem apenas configuracoes HTTP, JWT, hardening, observabilidade da API e banco.

Ledger:

- `Messaging:Provider`;
- `Kafka:Producer`;
- `PubSub:Enabled`;
- `PubSub:Producer`;
- `Outbox:Publisher`.
- `Reprocessamentos:Consumer`.

Configuracoes de mensageria do Balance ficam em `src/BalanceService.Worker/appsettings.json`. A API do Balance mantem apenas configuracoes HTTP, JWT, hardening, observabilidade da API e banco.

Balance:

- `Messaging:Provider`;
- `Kafka:Consumer`;
- `Kafka:Consumer:DeadLetterTopic`.
- `PubSub:Consumer` (adapter implementado, ainda nao ativado na composition root).

O `LedgerService.Worker` aceita `Kafka` e `PubSub` em `Messaging:Provider`; qualquer outro valor falha no startup com erro explicito de provider nao suportado. O `BalanceService.Worker` continua aceitando apenas `Kafka` nesta etapa.

`SecurityProtocol=Plaintext` existe apenas para execucao local (`Development`/`Local`) e para o ambiente `Test`. Em ambientes compartilhados ou produtivos, configure `SSL` ou `SASL_SSL` com os parametros operacionais por variaveis de ambiente ou secret store.

## Validacao rapida

1. Suba a stack local.
2. Aplique migrations.
3. Obtenha um token conforme [autenticacao e autorizacao](authentication.md).
4. Crie um lancamento em `POST /api/v1/lancamentos`.
5. Verifique no banco Ledger uma linha em `outbox_messages` com `Pending`.
6. Aguarde o polling e confirme transicao para `Processed`.
7. Consulte o Balance para confirmar atualizacao da projecao.

Em falha do Kafka, as APIs HTTP nao devem cair por causa do processamento assincrono. O Worker registra erro, incrementa `retry_count` e agenda `next_retry_at` com backoff exponencial e jitter. Ao atingir `MaxAttempts`, a mensagem vira `DeadLetter` e sai do processamento automatico ate requeue administrativo.

Para o roteiro operacional completo Keycloak -> Ledger -> Outbox -> Kafka -> Balance, incluindo `X-Correlation-Id`, logs, consultas SQL, Balance e Jaeger, use a secao [Validacao Keycloak -> Ledger -> Outbox -> Kafka -> Balance](../observability.md#validacao-keycloak---ledger---outbox---kafka---balance). O script recomendado e:

```powershell
./scripts/validate-auth-ledger-trace.ps1
```

Para validar os fluxos derivados de estorno e reprocessamento com os mesmos componentes operacionais, use a secao [Validacao local de estorno e reprocessamento](../observability.md#validacao-local-de-estorno-e-reprocessamento). Os scripts dedicados sao:

```powershell
./scripts/validate-ledger-reversal-flow.ps1
./scripts/validate-ledger-reprocess-flow.ps1
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

O processamento efetivo ocorre no `ReprocessamentoLancamentosConsumerService`, em `LedgerService.Worker`. O hosted service usa as configuracoes de `Reprocessamentos:Consumer`, assina o topico `ledger.lancamentos.reprocessamento.solicitado`, valida `event_type=ReprocessamentoLancamentosSolicitado.v1` e delega o trabalho ao Mediator com `ProcessarReprocessamentoLancamentosCommand`.

O handler:

1. localiza a solicitacao por `reprocessamentoId`;
2. ignora solicitacoes ja finais para suportar retry e reentrega Kafka;
3. marca a solicitacao como `Processing`;
4. busca `LedgerEntry` do mesmo `merchantId` no periodo inclusivo informado;
5. registra no Outbox um `LedgerEntryCreated.v1` para cada lancamento elegivel;
6. marca como `Completed` quando houver lancamentos ou `CompletedWithWarnings` quando nenhum lancamento for encontrado;
7. marca como `Rejected` em erro de regra conhecido e `Failed` em erro tecnico inesperado.

O reprocessamento de valores, nesta POC, e feito por replay idempotente dos fatos financeiros persistidos no Ledger. O payload final usa os campos atuais do `LedgerEntry` (`Amount`, `Type`, `OccurredAt`, `MerchantId` etc.) e o mesmo identificador de evento financeiro derivado do lancamento (`lan_{id}`). Assim, se o Balance ja aplicou aquele lancamento, a tabela `processed_events` evita duplicidade; se o evento estava ausente na projecao, o consumer aplica o saldo/consolidado pelo fluxo normal.

Limitacao conhecida: esse fluxo corrige projecoes ausentes por replay de eventos do Ledger, mas nao reconstroi saldos ja materializados com regra historica incorreta. Uma recomposicao completa de projecao ou evento especifico de correcao de valor deve ser modelado em decisao futura se essa necessidade aparecer.

## Governanca

Mudancas em topicos, headers, tipos de evento, payload ou politica de DLQ podem afetar produtores, consumidores e projecoes. Mudancas no provider de mensageria tambem exigem testes de contrato e integracao para Outbox, consumo idempotente, DLQ e tracing. Atualize testes, documentacao e ADRs quando houver decisao nova ou mudanca de contrato.
