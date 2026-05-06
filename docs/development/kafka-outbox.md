# Kafka, Outbox e DLQ

Este documento concentra a referencia de mensageria entre `LedgerService.Api` e `BalanceService.Api`.

## Fluxo

1. `LedgerService.Api` cria um lancamento ou registra uma solicitacao de estorno.
2. A mesma transacao grava a mensagem em `outbox_messages`.
3. `OutboxKafkaPublisherService` le mensagens pendentes e publica no Kafka.
4. `BalanceService.Api` consome `LedgerEntryCreated.v1` e atualiza a projecao `daily_balances`.
5. Mensagens invalidas ou nao recuperaveis do fluxo consumido pelo Balance sao publicadas na DLQ.

## Topicos e evento

| Item | Valor |
| --- | --- |
| Evento de lancamento | `LedgerEntryCreated.v1` |
| Topico de lancamento | `ledger.ledgerentry.created` |
| Evento de solicitacao de estorno | `LancamentoEstornoSolicitado.v1` |
| Topico de solicitacao de estorno | `ledger.lancamento.estorno.solicitado` |
| DLQ | `ledger.ledgerentry.created.dlq` |
| Mapeamentos | `LedgerEntryCreated.v1` -> `ledger.ledgerentry.created`; `LancamentoEstornoSolicitado.v1` -> `ledger.lancamento.estorno.solicitado` |

`LancamentoEstornoSolicitado.v1` e gravado pelo Ledger no Outbox e publicado pelo mesmo worker. Nesta etapa nao ha consumidor de estorno implementado; o evento registra a intencao para processamento assincrono futuro.

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

## Governanca

Mudancas em topicos, headers, tipos de evento, payload ou politica de DLQ podem afetar produtores, consumidores e projecoes. Atualize testes, documentacao e ADRs quando houver decisao nova ou mudanca de contrato.
