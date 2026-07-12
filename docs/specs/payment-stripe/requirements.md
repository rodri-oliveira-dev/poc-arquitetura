# Specification SDD: PaymentService integrado a Stripe - requisitos

> Nota de estado atual (Prompt 9): este documento nasceu como specification
> inicial/documental. As etapas posteriores da branch `feature/novo-servico`
> implementaram o `PaymentService`, webhook, Inbox, Worker, integracao com
> Ledger, smokes e refund total com estorno no Ledger. As restricoes historicas
> de "nao implementar refund agora" permanecem como contexto da etapa inicial;
> no baseline final, continuam fora do escopo refund parcial, chargeback,
> dispute, payout, split, Stripe Connect, reconciliacao completa e UI
> administrativa.

## Contexto

O repositorio modela uma POC financeira distribuida com `LedgerService` como
fonte de verdade dos lancamentos financeiros, `BalanceService` como projecao
eventual dos eventos do Ledger, `TransferService` como orquestrador de Sagas
entre merchants, Kafka como provider padrao dos workers principais, Pub/Sub
apenas explicito/legado, Outbox, JWT/JWKS, autorizacao por scope e por merchant,
correlacao por `X-Correlation-Id`, OpenTelemetry opcional, DLQ, replay e testes
automatizados.

Esta specification define apenas a etapa SDD para introduzir um novo bounded
context conceitual `PaymentService`, responsavel pelo ciclo de vida interno de
pagamentos externos processados por Stripe. Nenhum codigo funcional, projeto
.NET, migration, endpoint, worker, compose, pacote ou integracao real com
Stripe deve ser implementado nesta etapa.

## Convencoes do repositorio a preservar

- Bounded contexts ficam em `src/<contexto>` e `tests/<contexto>`, com projetos
  `*.Api`, `*.Application`, `*.Domain`, `*.Infrastructure` e, quando houver
  processamento de background, `*.Worker`.
- `Api` recebe HTTP, aplica autenticacao, autorizacao, Swagger, health/readiness
  e composition root; controllers/endpoints devem permanecer finos.
- `Application` contem casos de uso, validacao de entrada, orquestracao,
  portas, idempotencia e transacoes logicas.
- `Domain` contem aggregates, value objects, invariantes e state machines sem
  EF Core, Kafka, Pub/Sub, HTTP, claims, SDKs externos ou nomes de topicos.
- `Infrastructure` contem EF Core, PostgreSQL, adapters externos, clientes HTTP
  e implementacoes concretas de portas.
- `Worker` hospeda `BackgroundService`/`IHostedService`, polling, claim, retry,
  DLQ, publicacao/consumo Kafka e clients service-to-service.
- O PostgreSQL local e compartilhado na POC, mas cada bounded context usa schema
  e roles proprios.
- `Idempotency-Key` em endpoints financeiros e UUID; replay com mesmo payload
  retorna resultado equivalente e payload diferente retorna `409 Conflict`.
- `X-Correlation-Id` e opcional nos requests de negocio, normalizado pela API e
  refletido no response.
- Autorizacao de negocio usa JWT RS256/JWKS, `scope` e claim `merchant_id`.
- Workers nao expoem HTTP por padrao; saude operacional vem de startup, logs,
  metricas e estados persistidos.
- Eventos internos/externos devem ter contrato versionado quando realmente
  publicados entre bounded contexts; nao criar eventos apenas por simetria.
- Kafka e default para mensageria interna nova; Pub/Sub nao deve ser usado em
  novos fluxos padrao sem ADR.
- Stripe, seus tipos de SDK e conceitos de transporte externo nao podem vazar
  para `Domain` ou contratos de outros bounded contexts.

## Problema

Clientes precisam solicitar pagamentos que serao processados por um gateway
externo. A resposta sincrona da criacao do pagamento no provider nao e prova
final do efeito financeiro. O sistema precisa receber webhooks da Stripe,
validar autenticidade, deduplicar eventos externos, processar de forma
assincrona, atualizar o estado interno do pagamento e, somente quando houver
confirmacao efetiva, solicitar ao `LedgerService` o lancamento financeiro
correspondente.

Sem desenho explicito, ha risco de:

- duplicar lancamentos financeiros por webhook duplicado, retry ou timeout;
- acoplar dominio interno a tipos da Stripe;
- processar webhook no controller e perder resiliencia;
- fazer PaymentService atualizar BalanceService diretamente;
- confundir sucesso no provider com sucesso financeiro end-to-end;
- criar tabelas, eventos ou workers prematuros sem criterio operacional.

## Objetivos

- Definir o bounded context `PaymentService` e suas responsabilidades.
- Definir uma Anti-Corruption Layer para Stripe por uma porta como
  `IPaymentGateway`.
- Definir a modelagem inicial do aggregate `Payment` e sua state machine.
- Definir contratos HTTP iniciais conceituais.
- Definir Inbox Pattern para webhooks externos com deduplicacao e claim seguro.
- Definir seguranca de webhook com assinatura, raw body e replay protection.
- Definir integracao Payment -> Ledger preservando idempotencia ponta a ponta.
- Definir eventos realmente necessarios e quais permanecem internos.
- Preparar o modelo para refund e, no baseline final, suportar refund total com
  estorno pelo Ledger.
- Definir matriz de falhas, observabilidade, testes e configuracoes futuras.
- Quebrar a implementacao futura em tarefas incrementais verificaveis.

## Nao objetivos

- Implementar codigo funcional.
- Criar projetos .NET, migrations, endpoints, tabelas, workers ou compose.
- Instalar pacote Stripe ou qualquer dependencia.
- Integrar com Stripe real ou sandbox.
- Alterar contratos OpenAPI versionados nesta etapa.
- Alterar Ledger, Balance, Transfer, Identity ou Audit.
- Criar producers/consumers Kafka do PaymentService nesta etapa.
- Implementar refund parcial, chargeback, dispute, payout, split, Stripe
  Connect, reconciliacao completa ou UI administrativa.
- Criar shared library de pagamentos ou contratos antes de necessidade real.
- Usar Stripe Sandbox como alvo de carga.

## Atores

| Ator | Papel |
| --- | --- |
| Cliente autenticado | Solicita pagamento e consulta status. |
| PaymentService.Api | Entrada HTTP de pagamentos e webhook Stripe. |
| Stripe | Provider externo de processamento e fonte dos eventos externos. |
| PaymentService.Worker | Processa Inbox, aplica state machine e integra com Ledger. |
| LedgerService.Api | Fonte de verdade do efeito financeiro. |
| BalanceService.Worker | Atualiza saldo por eventos financeiros do Ledger. |
| Operador | Investiga backlog, retries, poison messages e reconciliacoes. |

## Casos de uso

### Criar pagamento

1. Cliente chama `POST /api/v1/payments` com JWT, `payment.write`,
   `merchantId`, valor, moeda e referencia externa.
2. API valida scope, merchant, payload, `Idempotency-Key` e correlation id.
3. Application cria ou reusa `Payment` de forma idempotente.
4. Application chama a porta `IPaymentGateway` para criar a intencao externa
   com idempotencia externa deterministica.
5. Resultado interno persiste `Payment` como `Pending` ou `Processing`,
   guardando a referencia externa quando conhecida.
6. API responde `202 Accepted` ou `201 Created` conforme decisao de contrato da
   implementacao; esta spec recomenda `202 Accepted`, pois a confirmacao
   financeira depende do webhook e do Ledger.

### Consultar pagamento

1. Cliente chama `GET /api/v1/payments/{paymentId}` com `payment.read`.
2. API valida que o merchant do pagamento pertence a claim `merchant_id`.
3. Retorna estado interno, referencia externa, valores e estado do lancamento no
   Ledger quando existir.

### Receber webhook Stripe

1. Stripe chama `POST /api/v1/webhooks/stripe`.
2. API le raw body, valida `Stripe-Signature` com o secret do endpoint, aplica
   tolerancia temporal e limites.
3. API persiste evento na Inbox usando `(provider, providerEventId)` unico.
4. API responde `2xx` apos assinatura valida e persistencia da Inbox, nao apos
   processamento financeiro completo.
5. Worker processa Inbox posteriormente.

### Processar evento confirmado

1. Worker reclama item da Inbox com claim concorrente seguro.
2. Worker mapeia evento externo para comando interno sem expor tipos Stripe.
3. Application aplica state machine no `Payment`.
4. Quando o payment atinge `Succeeded` no provider e ainda nao possui lancamento
   no Ledger, Worker chama `LedgerService.Api` com `Idempotency-Key`
   deterministica.
5. Ledger cria ou reexecuta idempotentemente o lancamento `CREDIT`.
6. Payment armazena a referencia do lancamento e fica `Completed` quando o
   efeito financeiro foi aceito/criado pelo Ledger.

## Regras funcionais

- PaymentService e a fonte de verdade do estado interno do pagamento.
- Stripe e a fonte externa do estado reportado pelo provider, mas seus eventos
  precisam ser reconciliados e traduzidos para o modelo interno.
- LedgerService e a fonte de verdade do efeito financeiro.
- BalanceService deve ser atualizado apenas pelos eventos financeiros do Ledger.
- PaymentService nunca grava tabelas de Ledger ou Balance.
- PaymentService nunca atualiza BalanceService diretamente.
- Webhook nao executa processamento financeiro complexo no controller.
- Webhook valido deve ser persistido antes do processamento assincrono.
- Eventos externos duplicados devem ser idempotentes.
- Eventos atrasados ou fora de ordem nao podem regredir estado final.
- Um Payment pode gerar no maximo um credito financeiro no Ledger.
- Retry de chamada ao Ledger deve usar idempotency key deterministica.
- Tipos Stripe (`PaymentIntent`, `Charge`, `Event`, `Refund`) nao atravessam a
  Infrastructure.
- Refund total usa estorno no Ledger no baseline final; refund parcial continua
  fora do MVP porque o contrato publico atual do Ledger suporta estorno total.

## Requisitos nao funcionais

- Seguranca: assinatura Stripe validada com raw body, secret correto, tolerancia
  temporal, comparacao segura e logs sem secrets.
- Resiliencia: Inbox persistida, retry duravel, backoff, claim com lease e
  dead-letter logico para poison messages.
- Consistencia: modelo at-least-once com idempotencia em Payment, Inbox e
  Ledger.
- Observabilidade: logs estruturados, correlation id, trace context quando
  possivel e metricas de baixa cardinalidade.
- Testabilidade: dominio testavel sem Stripe, HTTP ou EF Core; adapters testados
  com fake/WireMock.Net ou equivalente.
- Operacao local: provider fake deve permitir testes automatizados sem conta
  externa.
- Performance: endpoint de webhook deve responder rapido apos persistencia da
  Inbox, sem depender de Ledger ou Stripe no caminho de retorno.

## Criterios de aceite da specification

- Os cinco documentos da pasta `docs/specs/payment-stripe/` existem.
- ADRs propostas registram as decisoes novas sem duplicar ADRs existentes.
- A state machine deixa explicito estado final, regressao, duplicidade, eventos
  atrasados e refund futuro.
- A integracao com Ledger define idempotencia para o cenario de resposta HTTP
  perdida depois do lancamento persistido.
- A Inbox define constraint unica e claim seguro para multiplas instancias.
- A seguranca de webhook explica raw body, assinatura, replay e resposta `2xx`.
- Tasks futuras sao pequenas, ordenadas e verificaveis.

## Restricoes

- Esta etapa e somente documental.
- Nao versionar secrets, chaves de API ou exemplos com valores reais.
- Nao criar OpenAPI enquanto endpoints nao existirem.
- Nao criar JSON Schema de eventos que ainda nao serao publicados.
- Nao alterar LikeC4 como se o PaymentService ja existisse em runtime; a
  atualizacao do modelo arquitetural deve ocorrer na fatia que criar a estrutura
  ou na ADR de implementacao correspondente.

## Riscos

| Risco | Impacto | Mitigacao na spec |
| --- | --- | --- |
| Duplicidade de webhook | Credito duplicado | Inbox unica + state machine + idempotencia Ledger. |
| Timeout de Stripe com resultado desconhecido | Payment externo criado sem referencia local | Idempotencia externa e reconciliacao futura. |
| Timeout do Ledger apos persistir lancamento | Retry cria duplicidade se chave variar | Chave deterministica por `paymentId` e operacao. |
| Webhook com assinatura invalida | Fraude ou payload adulterado | Rejeitar antes de persistir Inbox. |
| Evento fora de ordem | Regressao de estado | Ranking de estados e regras de ignorar regressao. |
| Poison message | Loop infinito | Falha definitiva e dead-letter logico. |
| Overengineering de eventos | Complexidade sem uso | Publicar somente eventos com consumidor real. |

## Premissas

- Stripe sera o primeiro provider externo, mas o modelo nao deve impedir outro
  provider futuro.
- O MVP prioriza pagamento confirmado; refund e reconciliacao completa entram em
  fases posteriores.
- O contrato atual do Ledger para `POST /api/v1/lancamentos` com
  `Idempotency-Key` UUID sera usado para criar o credito financeiro.
- O worker do PaymentService podera obter token service-to-service para
  `ledger.write`, seguindo o padrao do TransferService.
- A assinatura Stripe deve seguir a documentacao oficial: raw body inalterado,
  header `Stripe-Signature`, secret do endpoint e tolerancia temporal.
