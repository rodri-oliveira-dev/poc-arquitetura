# LedgerService API

Este documento resume os contratos HTTP de escrita do `LedgerService.Api`.

## Criar lancamento

`POST /api/v1/lancamentos`

Cria um lancamento `CREDIT` ou `DEBIT`, persiste o lancamento e grava um evento `LedgerEntryCreated.v1` no Outbox na mesma transacao.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

## Solicitar estorno de lancamento

`POST /api/v1/lancamentos/{lancamentoId}/estornos`

Solicita o estorno de um lancamento existente. O endpoint apenas registra a intencao de estorno com status inicial `Pending`, persiste a solicitacao e grava o evento operacional `LancamentoEstornoSolicitado.v1` no Outbox. O processamento financeiro efetivo nao acontece no request HTTP; ele e executado de forma assincrona por worker do proprio `LedgerService`.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID. Repetir a mesma chave com o mesmo payload retorna o mesmo resultado; repetir com payload diferente retorna `409 Conflict`;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

Request body:

```json
{
  "motivo": "Erro operacional no lancamento original"
}
```

Resposta de sucesso:

```http
HTTP/1.1 202 Accepted
Location: /api/v1/lancamentos/estornos/00000000-0000-0000-0000-000000000000
```

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Pending",
  "statusUrl": "/api/v1/lancamentos/estornos/00000000-0000-0000-0000-000000000000"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `202 Accepted` | Solicitacao aceita e persistida como `Pending`. |
| `400 Bad Request` | Payload ou headers invalidos. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant do lancamento original. |
| `404 Not Found` | Lancamento original inexistente. |
| `409 Conflict` | Chave de idempotencia reutilizada com payload diferente ou solicitacao ativa duplicada. |
| `413 Payload Too Large` | Body acima de `ApiLimits:MaxRequestBodySizeBytes`. |
| `429 Too Many Requests` | Rate limit excedido. |

## Consultar status de estorno

`GET /api/v1/lancamentos/estornos/{estornoId}`

Consulta o estado atual de uma solicitacao de estorno criada por `POST /api/v1/lancamentos/{lancamentoId}/estornos`. A consulta usa Mediator na camada Application e retorna somente campos publicos do contrato HTTP, sem expor entidade de dominio, tabela ou detalhes de persistencia.

Parametros de rota:

| Parametro | Descricao |
| --- | --- |
| `estornoId` | Identificador da solicitacao de estorno retornado no `202 Accepted` do endpoint de criacao. Deve ser um UUID valido. |

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.read`.

Exemplo de request:

```http
GET /api/v1/lancamentos/estornos/00000000-0000-0000-0000-000000000000
Authorization: Bearer <token>
```

Resposta `Pending`:

```http
HTTP/1.1 200 OK
```

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Pending",
  "motivo": "Erro operacional no lancamento original",
  "solicitadoEm": "2026-05-06T10:30:00"
}
```

Resposta `Completed`:

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Completed",
  "motivo": "Erro operacional no lancamento original",
  "solicitadoEm": "2026-05-06T10:30:00"
}
```

Resposta `Failed`:

```json
{
  "estornoId": "00000000-0000-0000-0000-000000000000",
  "lancamentoOriginalId": "11111111-1111-1111-1111-111111111111",
  "status": "Failed",
  "motivo": "Erro operacional no lancamento original",
  "solicitadoEm": "2026-05-06T10:30:00"
}
```

Estados possiveis:

| Status | Significado |
| --- | --- |
| `Pending` | Solicitacao registrada e ainda nao processada. |
| `Processing` | Processamento iniciado por fluxo assincrono futuro. |
| `Completed` | Estorno concluido pelo worker do Ledger. |
| `Failed` | Processamento falhou por erro tecnico ou inesperado. |
| `Rejected` | Solicitacao rejeitada por regra de negocio no processamento. |

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `200 OK` | Solicitacao encontrada e autorizada para o merchant do token. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant do estorno. |
| `404 Not Found` | Solicitacao inexistente ou rota com `estornoId` fora do formato UUID. |
| `429 Too Many Requests` | Rate limit excedido. |

Como o processamento financeiro e assincrono, a API registra a intencao e inicialmente retorna `Pending`. Estados posteriores refletem a evolucao do worker e devem ser interpretados com consistencia eventual: uma consulta logo apos o `POST` pode retornar `Pending` ate que o worker processe a solicitacao.

## Outbox, processamento assincrono e saldo

A solicitacao de estorno grava uma linha em `estornos_lancamentos` e outra em `outbox_messages` na mesma transacao. O evento usa:

- `EventType`: `LancamentoEstornoSolicitado.v1`;
- topico Kafka mapeado: `ledger.lancamento.estorno.solicitado`;
- `AggregateType`: `LancamentoEstorno`;
- `AggregateId`: identificador da solicitacao de estorno.

Esse evento representa uma mensagem operacional/intencao interna. Ele nao e fato financeiro final e nao deve ser consumido pelo `BalanceService` para alterar saldo.

O processamento efetivo ocorre no `EstornoLancamentoProcessorService`, em `LedgerService.Infrastructure`. O worker:

1. reclama solicitacoes `Pending` de forma atomica, mudando-as para `Processing` com lock por linha no PostgreSQL;
2. delega cada `estornoId` ao Mediator com `ProcessarEstornoLancamentoCommand`;
3. o handler recarrega a solicitacao com `SELECT ... FOR UPDATE` quando usa PostgreSQL;
4. o dominio cria um lancamento compensatorio invertendo tipo e valor do lancamento original;
5. o handler vincula o compensatorio em `LancamentoCompensatorioId`;
6. a solicitacao termina como `Completed`;
7. o Outbox registra `LedgerEntryCreated.v1` para o lancamento compensatorio.

Estados:

| Status | Significado |
| --- | --- |
| `Pending` | Solicitacao registrada, ainda nao processada. |
| `Processing` | Worker iniciou o processamento da solicitacao. |
| `Completed` | Lancamento compensatorio persistido e evento final registrado no Outbox. |
| `Rejected` | Estorno recusado por regra de negocio, sem lancamento compensatorio. |
| `Failed` | Falha tecnica ou inesperada no processamento. |

Idempotencia:

- reexecutar `ProcessarEstornoLancamentoCommand` para uma solicitacao `Completed` nao cria outro lancamento;
- apenas uma solicitacao `Pending` ou `Processing` pode existir por `lancamento_original_id`, por indice unico filtrado;
- requisicoes concorrentes com `Idempotency-Key` diferentes para o mesmo lancamento retornam no maximo um `202 Accepted`; a outra recebe `409 Conflict`;
- o lancamento compensatorio usa `external_reference=estorno:{lancamentoOriginalId}` para detectar duplicidade;
- ha indice unico filtrado para `external_reference` de estornos, reduzindo duplicidade em execucao concorrente;
- o evento final `LedgerEntryCreated.v1` so e registrado quando o estorno e concluido na mesma transacao.

O `BalanceService` consome apenas o evento financeiro final `LedgerEntryCreated.v1`. Para estorno de credito, o compensatorio e `DEBIT` com valor negativo; para estorno de debito, o compensatorio e `CREDIT` com valor positivo. Assim o saldo liquido e compensado pelo mesmo fluxo de consolidacao ja usado para lancamentos normais.

## Solicitar reprocessamento de lancamentos

`POST /api/v1/lancamentos/reprocessar`

Solicita o reprocessamento de lancamentos de um merchant em um periodo controlado. O endpoint apenas registra a solicitacao com status inicial `Pending`, persiste a linha em `reprocessamentos_lancamentos` e grava o evento operacional `ReprocessamentoLancamentosSolicitado.v1` no Outbox. O reprocessamento efetivo nao acontece no request HTTP; ele e executado de forma assincrona pelo consumer de reprocessamento do proprio `LedgerService`.

O contrato usa `merchantId` em vez de `contaId` porque o dominio atual do Ledger e segmentado por merchant e a autorizacao tambem usa a claim `merchant_id`.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID. Repetir a mesma chave com o mesmo payload retorna o mesmo resultado; repetir com payload diferente retorna `409 Conflict`;
- `X-Correlation-Id`: opcional, em formato UUID. Se ausente, a API gera e devolve no response.

Request body:

```json
{
  "merchantId": "m1",
  "dataInicial": "2026-05-01",
  "dataFinal": "2026-05-06",
  "motivo": "Correcao de regra de consolidacao"
}
```

Regras de validacao:

| Campo | Regra |
| --- | --- |
| `merchantId` | Obrigatorio e deve estar autorizado no token. |
| `dataInicial` | Obrigatoria. |
| `dataFinal` | Obrigatoria, maior ou igual a `dataInicial`. |
| periodo | Maximo inclusivo de 31 dias nesta POC. |
| `motivo` | Obrigatorio, entre 10 e 500 caracteres. |
| `Idempotency-Key` | Obrigatorio e UUID valido. |

Resposta de sucesso:

```http
HTTP/1.1 202 Accepted
Location: /api/v1/lancamentos/reprocessamentos/00000000-0000-0000-0000-000000000000
```

```json
{
  "reprocessamentoId": "00000000-0000-0000-0000-000000000000",
  "merchantId": "m1",
  "dataInicial": "2026-05-01",
  "dataFinal": "2026-05-06",
  "status": "Pending",
  "statusUrl": "/api/v1/lancamentos/reprocessamentos/00000000-0000-0000-0000-000000000000"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `202 Accepted` | Solicitacao aceita e persistida como `Pending`. |
| `400 Bad Request` | Payload, periodo ou headers invalidos. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant informado. |
| `409 Conflict` | Chave de idempotencia reutilizada com payload diferente. |
| `413 Payload Too Large` | Body acima de `ApiLimits:MaxRequestBodySizeBytes`. |
| `429 Too Many Requests` | Rate limit excedido. |

Estados modelados:

| Status | Significado |
| --- | --- |
| `Pending` | Solicitacao registrada, ainda nao processada. |
| `Processing` | Consumer iniciou o processamento da solicitacao. |
| `Completed` | Reprocessamento concluido sem avisos. |
| `CompletedWithWarnings` | Reprocessamento concluido com avisos operacionais. |
| `Failed` | Processamento falhou por erro tecnico ou inesperado. |
| `Rejected` | Solicitacao rejeitada por regra de negocio no processamento. |
| `Canceled` | Solicitacao cancelada antes da conclusao. |

## Consultar status de reprocessamento

`GET /api/v1/lancamentos/reprocessamentos/{reprocessamentoId}`

Consulta a solicitacao registrada por `POST /api/v1/lancamentos/reprocessar`. Como o processamento e assincrono, uma consulta logo apos o `POST` pode retornar `Pending`; depois que o consumer processa a mensagem, o status evolui para `Processing` e entao para um estado final.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `ledger.read`.

Resposta `Pending`:

```json
{
  "reprocessamentoId": "00000000-0000-0000-0000-000000000000",
  "merchantId": "m1",
  "dataInicial": "2026-05-01",
  "dataFinal": "2026-05-06",
  "status": "Pending",
  "motivo": "Correcao de regra de consolidacao",
  "solicitadoEm": "2026-05-07T08:30:00"
}
```

## Outbox e processamento de reprocessamento

A solicitacao de reprocessamento grava uma linha em `reprocessamentos_lancamentos` e uma mensagem em `outbox_messages` na mesma transacao. O evento usa:

- `EventType`: `ReprocessamentoLancamentosSolicitado.v1`;
- topico Kafka mapeado: `ledger.lancamentos.reprocessamento.solicitado`;
- `AggregateType`: `ReprocessamentoLancamentos`;
- `AggregateId`: identificador da solicitacao de reprocessamento.

Esse evento representa uma intencao operacional interna. Ele nao e fato financeiro final e nao deve ser consumido pelo `BalanceService` para alterar saldo.

O processamento efetivo ocorre no `ReprocessamentoLancamentosConsumerService`, em `LedgerService.Infrastructure`. O consumer:

1. consome `ledger.lancamentos.reprocessamento.solicitado`;
2. valida `event_type=ReprocessamentoLancamentosSolicitado.v1`;
3. chama `ProcessarReprocessamentoLancamentosCommand` via Mediator;
4. o handler marca a solicitacao como `Processing`;
5. busca lancamentos do mesmo `merchantId` dentro do periodo informado;
6. registra no Outbox um `LedgerEntryCreated.v1` para cada lancamento elegivel;
7. conclui como `Completed` ou `CompletedWithWarnings`.

Nesta POC, "reprocessar valores" significa republicar os fatos financeiros ja persistidos no Ledger usando o valor atual do `LedgerEntry` (`Amount`, `Type`, `OccurredAt`, `MerchantId`, `Currency` etc.). O `BalanceService` continua consumindo apenas `LedgerEntryCreated.v1`; como o identificador do evento financeiro e o mesmo (`lan_{lancamentoId}`), a idempotencia por `processed_events` evita duplicidade em retry/reentrega e permite aplicar lancamentos que ainda nao tinham sido projetados.

Limitacao conhecida: o fluxo atual corrige projecoes ausentes por replay, mas nao refaz uma projecao ja materializada com regra historica errada. Uma recomposicao completa de saldo/consolidado deve ser tratada por decisao futura se for necessaria.
