# Specification SDD: PaymentService integrado a Stripe - design

## Decisoes principais

| Tema | Decisao | Classificacao |
| --- | --- | --- |
| Bounded context | Criar `PaymentService` em etapa futura | Necessaria para requisito explicito e fronteira de dominio. |
| Stripe | Integrar por ACL em `Infrastructure` via `IPaymentGateway` | Necessaria para proteger o modelo interno. |
| Webhook | Persistir Inbox antes de processamento | Necessaria para confiabilidade e replay. |
| Ledger | Chamar `LedgerService.Api`, nunca banco/tabelas | Necessaria para preservar ownership. |
| Balance | Nao integrar diretamente | Necessaria para preservar CQRS/projecao. |
| Kafka interno | Adiar ate existir consumidor real | Util, mas opcional no MVP de pagamentos. |
| Refund | Preparar modelo, nao implementar | Util para evitar decisao incompativel. |

## Bounded context

Nome recomendado: `PaymentService`.

Motivo: o repositorio usa nomes em ingles para contexts recentes
(`TransferService`, `IdentityService`, `AuditService`) e nomes de projeto
`<Context>Service.<Layer>`. O termo `Payment` e mais especifico que `Stripe`
e evita nomear o bounded context pelo fornecedor externo.

Estrutura futura:

```text
src/payment/
  PaymentService.Api/
  PaymentService.Application/
  PaymentService.Domain/
  PaymentService.Infrastructure/
  PaymentService.Worker/
tests/payment/
  PaymentService.UnitTests/
  PaymentService.IntegrationTests/
  PaymentService.Worker.Tests/
```

Solution contextual futura: `PaymentService.slnx`, alem de inclusao na
`PocArquitetura.slnx` quando o contexto existir.

## Responsabilidades por camada e processo

### PaymentService.Api

- Expor `POST /api/v1/payments` e `GET /api/v1/payments/{paymentId}`.
- Expor `POST /api/v1/webhooks/stripe` como endpoint de integracao externa.
- Validar JWT, scopes e merchant nos endpoints de negocio.
- Validar assinatura Stripe no webhook antes de persistir Inbox.
- Capturar raw body do webhook sem mutacao.
- Aplicar limite de body, rate limit adequado e correlation id.
- Retornar respostas HTTP e ProblemDetails.
- Nao chamar Ledger no controller.
- Nao processar state machine complexa no webhook.

### PaymentService.Application

- Orquestrar criacao de pagamento, consulta e processamento de eventos da Inbox.
- Definir portas: `IPaymentGateway`, `IPaymentRepository`,
  `IPaymentInboxRepository`, `ILedgerEntryClient`, `IClock`, `IUnitOfWork`.
- Aplicar idempotencia de `POST /payments`.
- Mapear eventos externos ja traduzidos para comandos internos.
- Aplicar state machine do aggregate.
- Decidir quando solicitar lancamento ao Ledger.
- Classificar erros de provider e Ledger em transitorios/definitivos.
- Nao conhecer Stripe SDK, Kafka, EF Core, HTTP controllers ou claims.

### PaymentService.Domain

- Modelar `Payment` como aggregate root.
- Proteger invariantes de valor, moeda, merchant, provider, referencias e
  transicoes de estado.
- Representar value objects: `PaymentId`, `MerchantId`, `Money`,
  `PaymentProvider`, `ExternalPaymentReference`, `ExternalReference`,
  `LedgerEntryReference`.
- Registrar fatos internos de dominio quando uteis para a Application.
- Nao referenciar Stripe, Ledger client, Kafka, Inbox, Outbox ou EF Core.

### PaymentService.Infrastructure

- Implementar EF Core/PostgreSQL para schema `payment`.
- Implementar repositories, unit of work, constraints e claim de Inbox.
- Implementar Stripe adapter atras de `IPaymentGateway`.
- Implementar HTTP client resiliente para Stripe se a SDK permitir controle
  adequado ou adapter sobre SDK caso ela seja escolhida futuramente.
- Implementar client HTTP para `LedgerService.Api` atras de porta interna.
- Implementar validacao de assinatura do webhook se ficar como servico tecnico
  injetado na API.
- Manter tipos Stripe confinados nesta camada.

### PaymentService.Worker

- Reclamar eventos da Inbox com lease/ownership.
- Processar eventos pendentes com retry persistido e backoff.
- Aplicar state machine chamando Application.
- Chamar Ledger quando um pagamento confirmado ainda nao tiver efeito
  financeiro.
- Marcar Inbox como processada, falha recuperavel ou dead-letter logico.
- Emitir logs, metricas e traces do processamento.
- Nao expor controllers, Swagger, CORS ou superficie HTTP.

## Boundaries entre contexts

| Contexto | Responsabilidade | Fora do contexto |
| --- | --- | --- |
| PaymentService | Ciclo de vida do pagamento externo e traducao Stripe -> modelo interno. | Lancamentos contabeis finais e saldo. |
| Stripe Adapter | Criar intencao externa, validar/interpretar eventos Stripe, traduzir erros. | Regras de dominio internas e persistencia de Payment. |
| Inbox | Entrada confiavel de eventos externos e deduplicacao. | Efeito financeiro e atualizacao de saldo. |
| LedgerService | Criar lancamento financeiro, idempotencia de lancamentos e Outbox financeira. | Estado externo Stripe e state machine de Payment. |
| BalanceService | Projetar saldo a partir de eventos do Ledger. | Criacao de pagamentos, webhook, Ledger command. |

## Anti-Corruption Layer da Stripe

### Porta conceitual

```text
IPaymentGateway
  CreatePaymentIntentAsync(CreateExternalPaymentRequest, CancellationToken)
  GetPaymentAsync(ExternalPaymentReference, CancellationToken) [futuro/reconciliacao]
  CreateRefundAsync(CreateExternalRefundRequest, CancellationToken) [futuro]
```

Responsabilidade:

- Receber modelos internos e retornar modelos internos.
- Esconder SDK, endpoints, retries especificos, `PaymentIntent`, `Charge`,
  `Event`, `Refund` e erros nativos.
- Aplicar idempotencia externa usando chave deterministica definida pela
  Application.
- Classificar falhas:
  - transitorias: timeout, rede, 408, 429, 5xx, indisponibilidade;
  - definitivas: autenticacao/configuracao invalida, parametro invalido,
    metodo de pagamento recusado de forma definitiva, moeda nao suportada;
  - desconhecidas: resposta perdida ou timeout apos envio.
- Preservar cancellation token e timeouts configurados.
- Emitir spans/metricas sem labels de alta cardinalidade.

### Modelos internos sugeridos

`CreateExternalPaymentRequest`:

| Campo | Descricao |
| --- | --- |
| `paymentId` | Id interno usado em metadata/idempotencia. |
| `merchantId` | Merchant dono do pagamento. |
| `amount` | Valor positivo. |
| `currency` | Moeda ISO, inicialmente `BRL` ou conforme regra futura. |
| `externalReference` | Referencia do cliente/checkout. |
| `idempotencyKey` | Chave externa deterministica. |
| `correlationId` | Correlacao operacional. |

`CreateExternalPaymentResult`:

| Campo | Descricao |
| --- | --- |
| `provider` | `Stripe`. |
| `externalPaymentReference` | Id do PaymentIntent ou equivalente. |
| `providerStatus` | Status externo traduzido. |
| `clientSecret` | Opcional, se o fluxo de checkout precisar retornar ao cliente. |
| `requiresAction` | Indica acao adicional do cliente, se aplicavel. |
| `rawStatus` | String externa para diagnostico, sem expor tipo SDK. |

`PaymentGatewayFailure`:

| Campo | Descricao |
| --- | --- |
| `category` | `Transient`, `Definitive`, `UnknownResult`. |
| `code` | Codigo normalizado. |
| `message` | Mensagem segura e sem segredo. |
| `retryAfter` | Opcional para 429/backoff. |

## HTTP inicial

### `POST /api/v1/payments`

Finalidade: registrar pedido de pagamento e criar intencao externa.

Autenticacao/autorizacao:

- JWT Bearer Keycloak/JWKS.
- Audience futura: `payment-api`.
- Scope: `payment.write`.
- `merchantId` do body deve existir na claim `merchant_id`.

Headers:

- `Authorization: Bearer <token>`;
- `Idempotency-Key`: obrigatorio, UUID;
- `X-Correlation-Id`: opcional, UUID normalizado.

Request conceitual:

```json
{
  "merchantId": "m1",
  "amount": 100.00,
  "currency": "BRL",
  "description": "Pagamento do pedido 123",
  "externalReference": "order-123"
}
```

Response recomendado:

```http
HTTP/1.1 202 Accepted
Location: /api/v1/payments/00000000-0000-0000-0000-000000000000
```

```json
{
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "status": "Pending",
  "merchantId": "m1",
  "amount": 100.00,
  "currency": "BRL",
  "externalReference": "order-123",
  "statusUrl": "/api/v1/payments/00000000-0000-0000-0000-000000000000"
}
```

Status:

| Status | Quando |
| --- | --- |
| `202 Accepted` | Payment registrado; confirmacao financeira assincrona. |
| `400 Bad Request` | Header, JSON ou payload invalido. |
| `401 Unauthorized` | Token ausente/invalido. |
| `403 Forbidden` | Scope ou merchant insuficiente. |
| `409 Conflict` | Idempotency key com payload diferente ou operacao em processamento. |
| `413 Payload Too Large` | Body acima do limite. |
| `422 Unprocessable Entity` | Violacao de regra de dominio. |
| `429 Too Many Requests` | Rate limit excedido. |
| `502/503/504` | Falha externa quando a criacao nao puder ser aceita localmente. |

Replay idempotente: mesma chave e mesmo payload retorna resposta equivalente,
sem criar novo Payment nem nova intencao externa.

### `GET /api/v1/payments/{paymentId}`

Finalidade: consultar estado interno e diagnostico publico do pagamento.

Autorizacao:

- JWT Bearer.
- Scope: `payment.read`.
- Merchant do Payment deve estar na claim `merchant_id`.

Response conceitual:

```json
{
  "paymentId": "00000000-0000-0000-0000-000000000000",
  "status": "Completed",
  "providerStatus": "succeeded",
  "merchantId": "m1",
  "amount": 100.00,
  "currency": "BRL",
  "externalReference": "order-123",
  "externalPaymentReference": "pi_...",
  "ledgerEntryId": "11111111-1111-1111-1111-111111111111",
  "createdAt": "2026-07-08T10:00:00Z",
  "updatedAt": "2026-07-08T10:01:00Z"
}
```

`Succeeded` significa sucesso confirmado no provider. `Completed` significa
efeito financeiro aceito/criado no Ledger. Para o cliente, "concluido" deve ser
`Completed` quando a experiencia exigir saldo refletido no fluxo interno; pode
ser exibido como "pagamento aprovado, contabilizacao pendente" enquanto estiver
`LedgerPending`.

### `POST /api/v1/webhooks/stripe`

Finalidade: receber eventos Stripe.

Autenticacao/autorizacao:

- Nao usa JWT normal, porque Stripe nao envia token Keycloak.
- Autenticidade deve ser validada pela assinatura criptografica do header
  `Stripe-Signature`, usando o secret do endpoint.

Headers:

- `Stripe-Signature`: obrigatorio.
- `Content-Type: application/json`.
- `X-Correlation-Id`: opcional; se ausente, API gera correlation id proprio da
  entrega. O correlation id do pagamento pode ser recuperado por metadata quando
  existir, mas webhook e uma nova request externa.

Regras:

- Usar raw body inalterado para validacao.
- Rejeitar assinatura invalida antes de persistir.
- Persistir Inbox com deduplicacao antes de responder sucesso.
- Responder `2xx` apos assinatura valida e persistencia, nao apos processamento
  completo.
- Nao chamar Ledger nem atualizar Payment diretamente no controller.

Status:

| Status | Quando |
| --- | --- |
| `200 OK` ou `202 Accepted` | Assinatura valida e evento persistido ou duplicado reconhecido. |
| `400 Bad Request` | Payload ilegivel ou header ausente/malformado. |
| `401 Unauthorized` | Assinatura invalida. |
| `413 Payload Too Large` | Body acima do limite. |
| `429 Too Many Requests` | Abuso fora do comportamento esperado. |
| `500/503` | Inbox indisponivel; Stripe deve tentar novamente. |

Decisao: responder sucesso apos validacao de assinatura e persistencia na Inbox.
Processamento completo no request aumentaria timeout, acoplamento com Ledger e
risco de repeticoes desnecessarias da Stripe.

## Inbox Pattern

Tabela conceitual futura: `payment.inbox_messages`.

Campos:

| Campo | Descricao |
| --- | --- |
| `id` | UUID interno. |
| `provider` | `Stripe`. |
| `provider_event_id` | Id externo do evento (`evt_...`). |
| `event_type` | Tipo externo normalizado. |
| `payload` | JSON bruto validado, preferencialmente `jsonb` ou string preservada. |
| `payload_sha256` | Hash para diagnostico sem logar payload. |
| `received_at_utc` | Recebimento. |
| `processed_at_utc` | Conclusao. |
| `status` | `Pending`, `Processing`, `Processed`, `RetryScheduled`, `DeadLetter`. |
| `attempt_count` | Tentativas. |
| `next_retry_at_utc` | Proxima tentativa. |
| `last_error` | Erro sanitizado. |
| `processing_started_at_utc` | Inicio da tentativa atual. |
| `lock_owner` | Instancia que reclamou. |
| `locked_until_utc` | Lease. |
| `correlation_id` | Correlacao da entrega. |

Constraint unica: `(provider, provider_event_id)`.

Dois requests simultaneos com o mesmo `providerEventId`:

1. Ambos validam assinatura.
2. Ambos tentam inserir Inbox.
3. Um vence a unique constraint.
4. O outro detecta duplicidade, incrementa metrica `inbox_duplicate_total` e
   retorna `2xx` sem criar novo item.
5. Worker processa apenas a linha vencedora; state machine e idempotencia do
   Ledger continuam como segunda e terceira linhas de defesa.

Claim concorrente:

- Usar update atomico com filtro por status e `locked_until_utc` expirado,
  preferencialmente `FOR UPDATE SKIP LOCKED` ou equivalente.
- Definir `lock_owner`, `locked_until_utc`, `Processing` e incremento de
  tentativa na mesma transacao.
- Multiplas instancias podem processar em paralelo sem reclamar a mesma linha.
- Se o worker cair, o lease expira e outro worker pode reprocessar.

Retry/backoff:

- Falhas transitorias reagemendam `next_retry_at_utc`.
- Backoff exponencial com jitter e `MaxRetryCount`.
- Falhas definitivas vao para `DeadLetter` logico.
- Reprocessamento deve ser operacional, com motivo e limites.

Retencao:

- Eventos processados podem ser retidos por janela configuravel para auditoria e
  deduplicacao.
- Payload bruto deve ter retencao curta ou politica explicita, pois pode conter
  dados sensiveis.

## Webhook security

Criterios futuros:

- Validar `Stripe-Signature` usando raw body UTF-8 exatamente como recebido.
- Usar o signing secret correto do endpoint; secret de Stripe CLI e secret do
  Dashboard sao diferentes.
- Validar timestamp assinado com tolerancia temporal, default recomendado pela
  Stripe e 5 minutos.
- Nao usar tolerancia zero, pois isso desativa a checagem de recencia.
- Comparar HMAC em tempo constante quando a validacao for manual.
- Aceitar apenas esquema de assinatura `v1`.
- Permitir rotacao de secret com mais de uma assinatura valida durante janela
  controlada.
- Rejeitar payload adulterado, assinatura invalida e timestamp fora da janela.
- Aplicar limite de body antes de alocar payload grande.
- Rate limit deve proteger abuso, mas sem bloquear indevidamente retries
  legitimos da Stripe.
- Logs nunca devem registrar secret, assinatura completa, payload completo,
  dados de cartao ou PII desnecessaria.
- Metricas de rejeicao devem usar labels estaveis: `reason`, `provider`.

Referencias oficiais usadas na specification:

- Stripe recomenda verificar eventos com `Stripe-Signature`, raw body e endpoint
  secret: <https://docs.stripe.com/webhooks/signature>.
- A documentacao de webhooks da Stripe orienta responder rapidamente `2xx`
  antes de logica complexa: <https://docs.stripe.com/webhooks>.
- A tolerancia default das bibliotecas Stripe para timestamp e 5 minutos:
  <https://docs.stripe.com/webhooks>.

## Integracao com LedgerService

Evento/estado que habilita Ledger: pagamento confirmado no provider e transicao
interna para `Succeeded`, desde que `ledgerEntryId` ainda esteja ausente.

Fluxo:

1. Inbox event indica sucesso do provider.
2. Payment aplica transicao para `Succeeded`.
3. Worker chama `POST /api/v1/lancamentos` no Ledger:
   - `merchantId`: merchant do Payment;
   - `type`: `CREDIT`;
   - `amount`: valor positivo;
   - `description`: descricao segura do pagamento;
   - `externalReference`: `payment:{paymentId}` ou referencia equivalente;
   - `Idempotency-Key`: UUID deterministico derivado de
     `paymentId + "ledger-credit"`;
   - `X-Correlation-Id`: correlation id do Payment/evento.
4. Se Ledger retorna `201`, Payment salva `ledgerEntryId` e vai para
   `Completed`.
5. Se timeout/resposta desconhecida, Worker agenda retry com a mesma chave.
6. Se Ledger ja persistiu o lancamento, o retry retorna replay idempotente.

Tratamento HTTP:

| Classe | Tratamento |
| --- | --- |
| `400`, `401`, `403`, `404`, `422` | Definitivo; marcar falha operacional e exigir correcao. |
| `409` | Se conflito de payload para mesma key, falha critica; investigar determinismo. |
| `429` | Transitorio com backoff e respeito a `Retry-After` se existir. |
| `5xx`, timeout, rede, circuit open | Transitorio; retry persistido. |

Indisponibilidade prolongada do Ledger: Payment fica `LedgerPending` ou
`Succeeded` com `ledgerStatus=Pending`, metricas de backlog e alertas. Nao
reverter sucesso externo automaticamente.

## Eventos e contratos

Eventos de dominio internos sugeridos:

| Evento | Publicar externamente? | Justificativa |
| --- | --- | --- |
| `PaymentRequested` | Nao no MVP | Uso interno para clareza/testes. |
| `PaymentProviderSucceeded` | Nao no MVP | Estado interno acionado por Inbox. |
| `PaymentProviderFailed` | Nao no MVP | Sem consumidor externo inicial. |
| `PaymentLedgerEntryRequested` | Nao no MVP | Pode ser log/estado, nao contrato. |
| `PaymentLedgerEntryCreated` | Nao no MVP | Ledger ja publica fato financeiro. |

Eventos de integracao do PaymentService so devem ser criados quando houver
consumidor real ou necessidade operacional clara. O Balance nao deve consumir
eventos de Payment. Se no futuro auditoria funcional consumir eventos de
Payment, usar contrato canonico de auditoria ou evento dedicado versionado com
JSON Schema e Kafka, conforme governanca existente.

Topicos Kafka: nenhum no MVP. Futuro, se necessario, usar nomes como
`payment.payment.succeeded` e message key `paymentId`, com headers
`event_id`, `event_type`, `correlation_id`, `traceparent`, `tracestate`,
`baggage`, `causation_id`.

## Refund futuro

Entrada futura: `POST /api/v1/payments/{paymentId}/refunds` ou comando interno
equivalente, autenticado por `payment.write` e merchant autorizado.

Desenho:

1. PaymentService registra solicitacao de refund em estado intermediario.
2. Adapter Stripe chama provider com idempotencia externa.
3. Webhook de refund confirma resultado externo.
4. Payment aplica estado de refund.
5. Quando refund externo for confirmado, PaymentService solicita estorno ou
   lancamento compensatorio ao Ledger com idempotency key deterministica.
6. Refund parcial deve registrar valor acumulado reembolsado e nao permitir
   exceder valor capturado.

Nao decidir agora se refund sera entidade interna do aggregate ou aggregate
separado. Para o MVP, o `Payment` deve guardar dados minimos que evitem bloqueio:
valor original, moeda, referencia externa do provider, ledger entry id e saldo
refundavel conceitual.

## Falhas distribuidas

| Cenario | Comportamento esperado | Protecao | Risco residual | Metrica | Retry/DLQ/acao |
| --- | --- | --- | --- | --- | --- |
| Stripe indisponivel na criacao | Retornar erro transitorio ou aceitar Payment sem provider ref se implementado explicitamente | Timeout/circuit breaker | Resultado desconhecido se request chegou | `payment_provider_failure_total` | Retry da criacao apenas com idempotencia externa |
| Timeout ao criar PaymentIntent | Marcar resultado desconhecido e permitir retry com mesma key externa | Idempotencia externa | Provider criou sem resposta local | `payment_provider_request_total{result="timeout"}` | Retry/reconciliacao |
| Resposta Stripe perdida apos sucesso | Retry retorna mesma intencao quando key externa preservada | Idempotencia externa | Necessita reconciliacao se key nao recuperar | `payment_provider_failure_total` | Reconciliacao futura |
| Webhook duplicado | Retornar `2xx`, nao duplicar Inbox | Unique `(provider,eventId)` | Nenhum se constraint ok | `inbox_duplicate_total` | Sem retry |
| Webhook fora de ordem | Ignorar regressao ou manter pendente conforme state machine | Ranking de estados | Diagnostico de sequencia incompleta | `inbox_processing_total{result="ignored"}` | Sem DLQ se esperado |
| Webhook atrasado | Processar se ainda aplicavel; ignorar regressao | State machine | Pode exigir reconciliacao | `webhook_received_total` | Sem retry |
| Assinatura invalida | Rejeitar, nao persistir | HMAC/raw body/timestamp | Falso negativo por secret errado | `webhook_invalid_signature_total` | Acao operacional |
| Inbox indisponivel | Retornar `5xx` para Stripe tentar novamente | Persistencia obrigatoria | Perda se Stripe esgotar retries | `webhook_received_total{result="inbox_unavailable"}` | Retry do provider |
| API salva Inbox e cai antes de responder | Stripe reenvia; duplicado retorna `2xx` | Unique constraint | Reentrega esperada | `inbox_duplicate_total` | Sem acao |
| Worker cai apos claim | Lease expira e outro worker processa | `locked_until` | Reprocessamento parcial | `inbox_retry_total` | Retry |
| Worker muda Payment e cai antes de Inbox processed | Reprocessa; state machine idempotente | Transacao/estado final | Logs duplicados | `inbox_retry_total` | Retry |
| Payment confirmado, Ledger indisponivel | Payment fica pendente de Ledger | Retry persistido/circuit breaker | Backlog financeiro | `payment_ledger_pending_total` | Retry/alerta |
| Ledger grava e resposta perde | Retry mesma key retorna replay | Idempotency-Key deterministica | Conflito se payload variar | `ledger_integration_failure_total` | Retry |
| Retry concorrente de workers | Um claim por Inbox/Payment; Ledger idempotente | Lease + locks + key | Concorrencia por bug | `inbox_processing_failure_total` | Investigacao |
| Evento externo desconhecido | Persistir e marcar ignored ou dead-letter conforme risco | Catalogo de tipos aceitos | Perda de novo evento relevante | `webhook_received_total{event_type="unknown"}` | Acao se recorrente |
| Payload incompativel | Dead-letter logico apos classificacao definitiva | Validacao | Evolucao de contrato Stripe | `inbox_processing_failure_total` | DLQ/acao |
| Poison message | Dead-letter logico | Max attempts | Backlog se nao isolado | `inbox_backlog` | DLQ/operador |
| Restart com backlog | Worker retoma por `Pending`/lease expirado | Estado persistido | Drenagem lenta | `inbox_backlog` | Operacao |

## Observabilidade

Metricas propostas, baixa cardinalidade:

- `payment_created_total`;
- `payment_provider_request_total`;
- `payment_provider_failure_total`;
- `payment_provider_request_duration`;
- `webhook_received_total`;
- `webhook_invalid_signature_total`;
- `inbox_duplicate_total`;
- `inbox_processing_total`;
- `inbox_processing_failure_total`;
- `inbox_retry_total`;
- `inbox_backlog`;
- `ledger_integration_failure_total`;
- `payment_ledger_pending_total`.

Labels permitidas: `provider`, `operation`, `status`, `result`,
`failure_category`, `event_type` normalizado e de baixa cardinalidade.

Labels proibidas: `payment_id`, `merchant_id`, `provider_event_id`,
`correlation_id`, `trace_id`, `idempotency_key`, payload e mensagem de excecao.

Logs:

- Incluir `CorrelationId`, `paymentId`, `provider`, `providerEventId`,
  `eventType`, `status`, `attemptCount`, `failureCategory`.
- Nao registrar secrets, assinatura completa, client secret, API key Stripe,
  payload completo ou dados sensiveis desnecessarios.

Tracing:

- Request inicial cria trace HTTP.
- Chamada Stripe usa span `payment.provider.create`.
- Webhook inicia nova arvore; quando metadata externa contiver `paymentId` ou
  correlation id original, registrar link/atributo para causalidade, nao assumir
  mesmo trace.
- Worker cria spans para claim Inbox, processamento, state machine e chamada ao
  Ledger.
- Chamada ao Ledger propaga `X-Correlation-Id`; Ledger preserva Outbox -> Kafka
  -> Balance conforme padrao existente.

## Estrategia de testes futura

Unitarios:

- `Payment` aggregate, value objects, invariantes e transicoes.
- Eventos duplicados, atrasados e fora de ordem.
- Geracao deterministica de idempotency keys internas e externas.

Aplicacao:

- Handlers de criacao, consulta e processamento de Inbox.
- Mapeamento de provider event para comando interno.
- Decisao de chamar Ledger apenas uma vez.
- Classificacao de falhas Stripe/Ledger.

Integracao:

- PostgreSQL real com Testcontainers.
- Unique constraint da Inbox.
- Claim concorrente com multiplas instancias logicas.
- Retry persistido, lease expirado e restart.
- API + banco para webhooks com assinatura fake controlada.

Stripe adapter:

- Nao usar Stripe real na suite principal.
- Avaliar WireMock.Net ou fake HTTP/SDK wrapper.
- Validar request, response, timeout, 429, 5xx e erros definitivos.

Webhook:

- Assinatura valida/invalida.
- Body adulterado.
- Evento duplicado simultaneo.
- Evento desconhecido e payload incompativel.

Smoke:

```text
Payment request
  -> provider fake ou sandbox controlado
  -> webhook
  -> Inbox
  -> Worker
  -> Ledger
  -> Kafka
  -> Balance
```

k6:

- Nao usar Stripe Sandbox como alvo de carga.
- Usar provider fake local.
- Medir `POST /payments`, webhook fake, backlog/drenagem da Inbox e consulta.

## Configuracao futura

Chaves conceituais:

| Chave | Uso |
| --- | --- |
| `PaymentGateway:Provider` | `Stripe` ou `Fake`. |
| `Stripe:ApiKey` | Secret, nunca versionado. |
| `Stripe:WebhookSigningSecret` | Secret do endpoint. |
| `Stripe:ApiBaseUrl` | Override para fake/testes, se aplicavel. |
| `Stripe:WebhookSignatureToleranceSeconds` | Tolerancia temporal. |
| `PaymentService:Worker:Inbox:BatchSize` | Tamanho do lote. |
| `PaymentService:Worker:Inbox:PollingIntervalSeconds` | Polling. |
| `PaymentService:Worker:Inbox:LeaseTimeoutSeconds` | Lease. |
| `PaymentService:Worker:Inbox:MaxRetryCount` | Limite retry. |
| `PaymentService:Worker:Inbox:BaseBackoffSeconds` | Backoff. |
| `HttpResilience:Clients:Stripe:*` | Timeout/retry/circuit breaker. |
| `HttpResilience:Clients:Ledger:*` | Chamada ao Ledger. |

Secrets devem vir de user-secrets, `.env.local` gerado, variaveis de ambiente
ou secret store futuro. Arquivos versionados devem conter apenas placeholders.

Desenvolvimento local:

- Usar Stripe CLI opcionalmente para encaminhar webhooks, com secret local
  proprio.
- Usar provider fake para testes automatizados e k6.
- Nunca exigir conta externa para suite principal.

## Questoes arquiteturais respondidas

| Pergunta | Resposta |
| --- | --- |
| Fonte de verdade do estado do pagamento | `PaymentService`. |
| Fonte de verdade do efeito financeiro | `LedgerService`. |
| Divergencia temporaria com Stripe | Estado interno fica pendente/reconciliavel; webhooks e reconciliacao futura convergem. |
| Webhook duplicado | Unique Inbox + state machine idempotente + Ledger idempotente. |
| Webhook fora de ordem | Ignorar regressao ou registrar pendencia conforme state machine. |
| Evento desconhecido | Persistir e classificar como ignored/dead-letter conforme risco. |
| Poison Message | Max retries e dead-letter logico. |
| No maximo um credito | `ledgerEntryId` no Payment + idempotency key deterministica no Ledger. |
| Confirmado externamente sem Ledger | Fica `LedgerPending`; worker retenta e reconciliacao futura lista pendencias. |
| Reconciliacao Stripe | Futura rotina consulta provider por periodo/referencia e compara estado interno. |
| Reconciliacao Payment-Ledger | Relatorio de Payments `Succeeded/LedgerPending` e chamadas idempotentes ao Ledger. |
| `Succeeded` significa o que | Sucesso no provider. `Completed` significa efeito financeiro aceito/criado no Ledger. |
| Quando concluir para cliente | Preferir `Completed`; se UX aceitar, expor "aprovado, contabilizacao pendente". |
| Timeout desconhecido | Retry com mesma idempotency key e reconciliacao. |
| Concorrencia de Workers | Claim com lease, locks por Payment e idempotencia. |
| Replay seguro | Reprocessa Inbox sem duplicar por state machine e Ledger idempotente. |
| Multiplos providers | Porta `IPaymentGateway` e `PaymentProvider` enum simples, sem framework generico prematuro. |

## Decisoes abertas

- Moedas suportadas no MVP (`BRL` apenas ou ISO amplo).
- Se `POST /payments` retornara `clientSecret` para fluxo client-side Stripe.
- Quais tipos de evento Stripe serao aceitos inicialmente.
- Retencao exata de payload bruto da Inbox.
- Se reconciliacao Stripe entra no primeiro release funcional ou em fase
  posterior.
