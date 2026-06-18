# TransferService API

Este documento resume os contratos HTTP iniciais do `TransferService.Api`.

## Solicitar transferencia

`POST /api/v1/transferencias`

Registra uma saga de transferencia entre merchants com status inicial `Pending` e grava o evento `TransferenciaSolicitada.v1` no Outbox na mesma transacao. O processamento financeiro ocorre de forma assincrona pelo `TransferService.Worker`.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `transfer.write`;
- `Idempotency-Key`: obrigatorio, em formato UUID. Repetir a mesma chave com o mesmo payload retorna a mesma resposta; repetir com payload diferente retorna `409 Conflict`;
- `X-Correlation-Id`: opcional. Se ausente, a API gera e devolve no response.

Request body:

```json
{
  "sourceMerchantId": "m1",
  "destinationMerchantId": "m2",
  "amount": 100.00,
  "description": "Transferencia entre carteiras",
  "externalReference": "pedido-123"
}
```

Regras de validacao:

| Campo | Regra |
| --- | --- |
| `sourceMerchantId` | Obrigatorio, ate 100 caracteres e autorizado na claim `merchant_id`. |
| `destinationMerchantId` | Obrigatorio, ate 100 caracteres e diferente do merchant de origem. |
| `amount` | Obrigatorio, maior que zero, ate 18 digitos e 2 casas decimais. |
| `description` | Opcional, ate 500 caracteres. |
| `externalReference` | Opcional, ate 200 caracteres. |
| `Idempotency-Key` | Obrigatorio e UUID valido. |

Resposta de sucesso:

```http
HTTP/1.1 202 Accepted
Location: /api/v1/transferencias/00000000-0000-0000-0000-000000000000
```

```json
{
  "transferenciaId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "sourceMerchantId": "m1",
  "destinationMerchantId": "m2",
  "amount": 100.00,
  "createdAt": "2026-06-17T10:30:00Z",
  "statusUrl": "/api/v1/transferencias/00000000-0000-0000-0000-000000000000"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `202 Accepted` | Solicitacao aceita e persistida como `Pending`, incluindo replay idempotente com mesma chave e mesmo payload. |
| `400 Bad Request` | Payload, JSON ou headers invalidos. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para o merchant de origem. |
| `409 Conflict` | Chave de idempotencia reutilizada com payload diferente. |
| `413 Payload Too Large` | Body acima de `ApiLimits:MaxRequestBodySizeBytes`. |
| `422 Unprocessable Entity` | Violacao de regra de dominio. |
| `429 Too Many Requests` | Rate limit excedido. |

## Consultar status de transferencia

`GET /api/v1/transferencias/{transferenciaId}`

Consulta o estado atual da saga registrada pelo POST. A consulta usa Mediator na camada Application e retorna contrato HTTP proprio da API, sem expor entidade de dominio ou persistencia.

Headers relevantes:

- `Authorization: Bearer <token>` com scope `transfer.read`.

Resposta:

```json
{
  "transferenciaId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "sourceMerchantId": "m1",
  "destinationMerchantId": "m2",
  "amount": 100.00,
  "createdAt": "2026-06-17T10:30:00Z",
  "updatedAt": "2026-06-17T10:30:00Z"
}
```

Respostas esperadas:

| Status | Quando ocorre |
| --- | --- |
| `200 OK` | Saga encontrada e token autorizado para o merchant de origem ou destino. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Scope insuficiente ou token sem autorizacao para os merchants da transferencia. |
| `404 Not Found` | Saga inexistente ou rota com `transferenciaId` fora do formato UUID. |
| `429 Too Many Requests` | Rate limit excedido. |

Estados modelados inicialmente:

| Status | Significado |
| --- | --- |
| `Pending` | Saga registrada, ainda nao processada. |
| `Processing` | Worker iniciou o processamento. |
| `DebitCreating` | Criacao do debito em andamento. |
| `DebitCreated` | Debito criado. |
| `CreditCreating` | Criacao do credito em andamento. |
| `Completed` | Transferencia concluida. |
| `CompensationRequested` | Compensacao solicitada apos falha posterior ao debito. |
| `Compensated` | Transferencia compensada. |
| `Failed` | Falha tecnica ou inesperada. |
| `Rejected` | Transferencia rejeitada antes de debito efetivo. |

## Processamento assincrono da Saga

O `TransferService.Worker` possui duas responsabilidades separadas:

- processar Sagas pendentes ou elegiveis para retry;
- publicar a Outbox do TransferService no Kafka.

O fluxo usa Kafka como transporte explicito dos eventos da Saga. Pub/Sub nao e configurado nem usado pelo TransferService.

Fluxo feliz:

1. A API grava a Saga como `Pending` e o evento `TransferenciaSolicitada.v1`.
2. O Worker reivindica a Saga e marca o processamento.
3. O Worker chama o `LedgerService.Api` para criar o debito:
   - `merchantId = sourceMerchantId`;
   - `type = DEBIT`;
   - `amount` negativo;
   - `Idempotency-Key = transferencia:{transferenciaId}:debit`.
4. Ao confirmar o debito, salva `debitLancamentoId`, marca `DebitCreated` e grava `TransferenciaDebitoCriado.v1`.
5. O Worker chama o `LedgerService.Api` para criar o credito:
   - `merchantId = destinationMerchantId`;
   - `type = CREDIT`;
   - `amount` positivo;
   - `Idempotency-Key = transferencia:{transferenciaId}:credit`.
6. Ao confirmar o credito, salva `creditLancamentoId`, marca `Completed` e grava `TransferenciaCreditoCriado.v1` e `TransferenciaConcluida.v1`.
7. O publisher da Outbox publica cada evento no topico Kafka correspondente usando `transferenciaId` como message key.

Falha compensavel:

1. Se o credito falhar depois do debito criado, o Worker solicita estorno do debito no `LedgerService.Api`.
2. A compensacao usa `Idempotency-Key = transferencia:{transferenciaId}:compensate-debit`.
3. Quando a solicitacao de estorno e registrada com sucesso, a Saga fica `CompensationRequested`, salva `compensationEstornoId` e grava `TransferenciaCompensacaoSolicitada.v1`.
4. O modelo atual considera sucesso a solicitacao/registro do estorno no Ledger; confirmacao assincrona posterior pode evoluir para `Compensated` sem mudar o contrato HTTP.

Falhas antes de debito efetivo seguem retry controlado por estado da Saga. Ao esgotar `MaxRetryCount` ou receber erro definitivo, a Saga fica `Failed` e grava `TransferenciaFalhou.v1`.

## Kafka e Outbox

| Evento | Topico |
| --- | --- |
| `TransferenciaSolicitada.v1` | `transfer.transferencia.solicitada` |
| `TransferenciaDebitoCriado.v1` | `transfer.transferencia.debito-criado` |
| `TransferenciaCreditoCriado.v1` | `transfer.transferencia.credito-criado` |
| `TransferenciaConcluida.v1` | `transfer.transferencia.concluida` |
| `TransferenciaCompensacaoSolicitada.v1` | `transfer.transferencia.compensacao-solicitada` |
| `TransferenciaCompensada.v1` | `transfer.transferencia.compensada` |
| `TransferenciaFalhou.v1` | `transfer.transferencia.falhou` |

A DLQ de aplicacao e `transfer.transferencia.dlq`. Mensagens publicadas nao sao republicadas. Erros temporarios de Kafka mantem a mensagem pendente para retry; erro definitivo ou payload invalido envia a mensagem para DLQ e marca a Outbox como `DeadLetter`.

## Smoke tests e carga local

Os smoke tests e testes de carga do `TransferService.Api` seguem o padrao k6 do repositorio em `loadtests/k6` e sao executados pelos runners `scripts/run-loadtests.*`. Nao ha collection Postman/Newman versionada para este bounded context porque o repositorio ja centraliza smoke/load HTTP em k6.

Smoke local:

```powershell
./scripts/run-loadtests.ps1 -Mode transfer-smoke-kafka
```

```bash
./scripts/run-loadtests.sh transfer-smoke-kafka
```

O modo `transfer-smoke-kafka` valida:

- obtencao de token pelo fluxo local padrao de `scripts/get-token.*`;
- `POST /api/v1/transferencias` com `Idempotency-Key` e `X-Correlation-Id`;
- resposta `202 Accepted`, body com `transferenciaId`, status `Pending`, `statusUrl` e header `Location`;
- `GET /api/v1/transferencias/{transferenciaId}` com `200 OK`;
- replay idempotente com mesma chave e mesmo payload;
- conflito `409 Conflict` com mesma chave e payload diferente;
- `404 Not Found` para id inexistente;
- `401 Unauthorized` sem token;
- `403 Forbidden` para merchant de origem fora da claim `merchant_id`;
- `400 Bad Request` para `Idempotency-Key` ausente, `amount <= 0` e origem igual ao destino.

Carga moderada local:

```powershell
./scripts/run-loadtests.ps1 -Mode transfer-load-kafka
```

```bash
./scripts/run-loadtests.sh transfer-load-kafka
```

O modo `transfer-load-kafka` executa POST/GET com ramping ate 10 VUs por padrao, gerando `Idempotency-Key`, `X-Correlation-Id` e `externalReference` unicos por iteracao. Ele nao exige conclusao full-stack em 100% das iteracoes para evitar instabilidade artificial em teste de carga HTTP. Os thresholds iniciais sao `http_req_failed{service:transfer} < 2%`, `checks >= 99%`, `transfer_post_success >= 99%`, `transfer_get_success >= 99%`, `dropped_iterations == 0`, p95 menor que 1000ms e p99 menor que 2000ms para as operacoes de criacao e consulta.

Variaveis uteis:

| Variavel | Default local | Uso |
| --- | --- | --- |
| `BASE_URL_TRANSFER` | inferida como `http://transfer-service:8080` dentro do compose | Base URL usada pelo k6. |
| `TRANSFER_PATH` | `/api/v1/transferencias` | Rota versionada de transferencias. |
| `SOURCE_MERCHANT_ID` | `m1` | Merchant de origem autorizado pelo realm local. |
| `DESTINATION_MERCHANT_ID` | `m2` | Merchant de destino usado nos cenarios. |
| `TRANSFER_HTTP_REQ_DURATION_P95_MS` | `500` no smoke, `1000` no load | Override do threshold p95. |
| `TRANSFER_HTTP_REQ_DURATION_P99_MS` | `1000` no smoke, `2000` no load | Override do threshold p99. |

Esses testes nao validam a conclusao da Saga, chamadas ao Ledger, publicacao Kafka, DLQ ou compensacao. Para validar o fluxo completo com `TransferService.Worker`, suba a stack Kafka local e acompanhe a Saga conforme a secao de processamento assincrono deste documento.

Smoke full-stack Kafka:

```powershell
./scripts/run-loadtests.ps1 -Mode transfer-fullstack-kafka
```

```bash
./scripts/run-loadtests.sh transfer-fullstack-kafka
```

O modo `transfer-fullstack-kafka` e manual e valida API + Worker + LedgerService + Outbox + Kafka. Ele usa o compose padrao com Kafka, garante que Kafka e `transfer-worker` estejam em execucao, executa uma transferencia com `TRANSFER_CORRELATION_ID` controlado, consulta o status com polling ate `Completed` e valida pelo runner que os topicos `transfer.transferencia.solicitada`, `transfer.transferencia.debito-criado`, `transfer.transferencia.credito-criado` e `transfer.transferencia.concluida` receberam novas mensagens. A amostra Kafka do fluxo precisa ter `message key = transferenciaId`, payload com o `correlationId` esperado e a DLQ `transfer.transferencia.dlq` nao pode crescer no fluxo feliz.

Esse smoke nao usa Pub/Sub e nao depende de `BalanceService` para decidir a Saga. O `BalanceService` continua sendo uma projecao eventual fora da decisao de debito, credito e compensacao.

Use o provider padrao `TOKEN_PROVIDER=keycloak` ou informe `TOKEN` manualmente com `transfer.write`, `transfer.read`, audience `transfer-api` e `merchant_id` contendo os merchants testados. O `Auth.Api` legado nao e o caminho recomendado para esses modos porque seu catalogo historico nao cobre os scopes do TransferService.
