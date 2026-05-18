# Observabilidade e operacao minima

Este documento define o inventario operacional minimo da POC para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`.

OpenTelemetry fica desabilitado por padrao. A correlacao via `X-Correlation-Id` permanece sempre ativa nas APIs e e usada para conectar logs, respostas HTTP e mensagens Kafka. A operacao local usa `docker compose`, PostgreSQL e Kafka conforme documentado em [desenvolvimento local](development/local-development.md).

## Baseline

- Logs: console logging do ASP.NET Core, com escopo de `CorrelationId` nos middlewares de correlacao.
- Traces: OpenTelemetry opcional para ASP.NET Core e `HttpClient`.
- Metricas: OpenTelemetry opcional para ASP.NET Core, `HttpClient` e runtime .NET.
- Exporters: console para validacao local e OTLP quando `OtlpEndpoint` estiver configurado.
- Correlacao: header HTTP `X-Correlation-Id`, campo `CorrelationId` em logs e `correlation_id` em eventos Kafka.
- Health: `GET /health` em `LedgerService.Api` e `BalanceService.Api`.
- Readiness: `GET /ready` em `LedgerService.Api` e `BalanceService.Api`.
- Mensageria: Kafka com topico principal `ledger.ledgerentry.created` e DLQ `ledger.ledgerentry.created.dlq`.
- Outbox: publicacao assincrona do Ledger com polling, lock, tentativas e backoff configuraveis.

## Endpoints operacionais

### Health

`LedgerService.Api` e `BalanceService.Api` expoem:

- `GET /health`
- publico nesta POC;
- fora do rate limit;
- resposta esperada: HTTP 200 com body `ok`;
- nao verifica PostgreSQL nem Kafka;
- uso esperado: liveness simples do processo HTTP.

`Auth.Api` nao expoe endpoint dedicado de health/readiness nesta POC. A validacao operacional minima do Auth e feita por `POST /auth/login` e `GET /.well-known/jwks.json`.

### Readiness

`LedgerService.Api` e `BalanceService.Api` expoem:

- `GET /ready`
- publico nesta POC;
- fora do rate limit;
- resposta 200 quando o servico esta pronto;
- resposta 503 quando alguma dependencia obrigatoria esta indisponivel;
- body esperado em sucesso: `status=ready` e `checks`;
- body esperado em falha: `status=not_ready` e `checks`.

Checks atuais:

- `db`: valida conexao com o PostgreSQL do respectivo servico.
- `kafka`: valida metadados dos topicos Kafka quando `Kafka:Enabled=true`.
- `kafka=disabled`: indica que o Kafka foi explicitamente desabilitado por configuracao.

No `LedgerService.Api`, readiness valida os topicos resolvidos por `Kafka:Producer:TopicMap` ou `Kafka:Producer:DefaultTopic`. No `BalanceService.Api`, readiness valida `Kafka:Consumer:Topics` e `Kafka:Consumer:DeadLetterTopic`.

## Configuracao

As APIs usam a secao `Observability:OpenTelemetry`:

```json
{
  "Observability": {
    "OpenTelemetry": {
      "Enabled": false,
      "UseConsoleExporter": false,
      "OtlpEndpoint": "",
      "ServiceName": "LedgerService.Api"
    }
  }
}
```

Variaveis de ambiente equivalentes:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__UseConsoleExporter = "true"
$env:Observability__OpenTelemetry__OtlpEndpoint = "http://localhost:4317"
$env:Observability__OpenTelemetry__ServiceName = "LedgerService.Api"
```

Use `ServiceName` conforme o servico:

- `Auth.Api`
- `LedgerService.Api`
- `BalanceService.Api`

## Ambientes

### Development ou Local

Para validar sem backend de observabilidade, habilite o exporter de console:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__UseConsoleExporter = "true"
```

Para enviar traces e metricas para um collector OTLP local:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__OtlpEndpoint = "http://localhost:4317"
```

### Test

Mantenha OpenTelemetry desabilitado, salvo em testes especificos de instrumentacao. Isso evita ruido e dependencia de collector externo.

### Ambientes compartilhados ou produtivos

Habilite OpenTelemetry por configuracao de ambiente, nunca alterando o default versionado:

```powershell
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__OtlpEndpoint = "https://otel-collector.example:4317"
$env:Observability__OpenTelemetry__UseConsoleExporter = "false"
```

O endpoint real deve ser fornecido pela plataforma de execucao ou secret/config store. Este repositorio nao provisiona collector, dashboard, Jaeger, Tempo, Prometheus ou stack equivalente.

## Logs

Os logs usam o pipeline padrao do ASP.NET Core. `LedgerService.Api` e `BalanceService.Api` habilitam `Logging:Console:IncludeScopes=true`, o que permite incluir `CorrelationId` no console quando o provider exibe scopes.

Campos operacionais esperados:

- `CorrelationId`: valor do header `X-Correlation-Id`.
- `TraceId` e `SpanId`: adicionados ao logging scope em `LedgerService.Api` e `BalanceService.Api` quando ha `Activity` ativa.

Logs relevantes para operacao:

- pipeline HTTP, status codes e excecoes via handlers/middlewares das APIs;
- `OutboxKafkaPublisherService` no Ledger, incluindo polling, publicacao, falhas e retentativas;
- producer Kafka do Ledger, incluindo topico, particao e offset apos publicacao;
- consumer Kafka do Balance, incluindo processamento, commits, retries e envio para DLQ;
- erros de DLQ no Balance, especialmente quando a publicacao na DLQ falhar e o offset original nao for commitado.

## Traces

Quando `Observability:OpenTelemetry:Enabled=true`, as APIs registram:

- spans de entrada HTTP via instrumentacao ASP.NET Core;
- spans de saida HTTP via instrumentacao `HttpClient`.

`LedgerService` e `BalanceService` tambem criam `Activity` em trechos de Kafka/Outbox ja instrumentados no codigo. Quando ha `Activity`, headers W3C como `traceparent` e `baggage` sao propagados nas mensagens.

## Metricas

Quando OpenTelemetry esta habilitado, as APIs registram metricas de:

- ASP.NET Core;
- `HttpClient`;
- runtime .NET.

As metricas sao exportadas para console quando `UseConsoleExporter=true` e para OTLP quando `OtlpEndpoint` esta preenchido.

Metricas atuais sao tecnicas e geradas pela instrumentacao OpenTelemetry. O repositorio nao define metricas de negocio customizadas, dashboards, alertas ou Prometheus scrape config.

## Kafka

Kafka e usado como barramento entre escrita e leitura:

- produtor: `LedgerService.Api` via Outbox publisher;
- consumidor: `BalanceService.Api`;
- topico principal: `ledger.ledgerentry.created`;
- evento atual: `LedgerEntryCreated.v1`;
- DLQ do Balance: `ledger.ledgerentry.created.dlq`;
- compose local: Kafka single node em KRaft, exposto no host em `localhost:19092` e na rede interna como `kafka:9092`.

Headers relevantes:

- `event_id`;
- `event_type`;
- `correlation_id`;
- `traceparent`;
- `tracestate`;
- `baggage`.

O `BalanceService.Api` exige `event_type=LedgerEntryCreated.v1`. Mensagens com contrato invalido, payload invalido ou falha nao recuperavel sao desviadas para a DLQ.

## DLQ

A DLQ atual e `ledger.ledgerentry.created.dlq`.

Politica operacional:

- se o processamento normal termina com sucesso, o offset original e commitado;
- se a publicacao na DLQ termina com sucesso, o offset original e commitado;
- se a publicacao na DLQ falha, o offset original nao e commitado para permitir nova tentativa;
- o envelope da DLQ preserva payload original quando disponivel, topico, particao, offset, headers relevantes, motivo, tipo da excecao e timestamp.

Validacao minima:

1. Subir a stack local.
2. Garantir que o topico principal e a DLQ existem.
3. Enviar ou produzir uma mensagem invalida no topico principal em ambiente controlado.
4. Confirmar log de envio para DLQ.
5. Confirmar existencia da mensagem em `ledger.ledgerentry.created.dlq`.

## Outbox

O `LedgerService.Api` grava mensagens em `outbox_messages` na mesma transacao da escrita de lancamento. O hosted service `OutboxKafkaPublisherService` publica mensagens pendentes no Kafka e marca o status conforme o resultado.

Estados esperados:

- `Pending`: mensagem criada e aguardando publicacao;
- `Processing`: mensagem reclamada por um publisher com lock temporario;
- `Sent`: mensagem publicada com sucesso no Kafka;
- `Failed`: mensagem excedeu o limite configurado de tentativas.

Mensagens em `Failed` podem ser recuperadas por requeue administrativo protegido em `POST /api/v1/outbox/failed/requeue`, com scope `ledger.outbox.requeue`, motivo obrigatorio e filtros por id, tipo de evento ou janela de ocorrencia. O requeue registra contador, operador, data e motivo na linha da mensagem e recoloca somente mensagens `Failed` como `Pending`; mensagens `Sent` e `Processing` validas nao sao alteradas.

Configuracoes principais em `Outbox:Publisher`:

- `PollingIntervalSeconds`;
- `BatchSize`;
- `MaxParallelism`;
- `MaxAttempts`;
- `BaseBackoffSeconds`;
- `LockDurationSeconds`.

Validacao minima:

1. Aplicar migrations.
2. Subir `LedgerService.Api` com PostgreSQL e Kafka acessiveis.
3. Criar um lancamento em `POST /api/v1/lancamentos`.
4. Verificar linha em `outbox_messages` com `Pending`.
5. Aguardar o polling e verificar transicao para `Sent`.
6. Em falha de Kafka, verificar incremento de tentativas e agendamento de `next_attempt_at`.
7. Se a mensagem atingir `Failed`, corrigir a causa raiz e usar o requeue administrativo documentado em `docs/development/kafka-outbox.md`.

## Configuracao local

### Compose

O caminho recomendado para a stack completa e:

```bash
docker compose up -d --build
```

Portas expostas no host:

- Auth.Api: `http://localhost:5030/`;
- LedgerService.Api: `http://localhost:5226/`;
- BalanceService.Api: `http://localhost:5228/`;
- PostgreSQL Ledger: `localhost:15432`;
- PostgreSQL Balance: `localhost:15433`;
- Kafka: `localhost:19092`.

O compose sobrescreve configuracoes por variaveis de ambiente para usar os nomes internos `ledger-db`, `balance-db` e `kafka`. Aplique migrations manualmente antes de usar as APIs em banco vazio.

### Validacao local com Jaeger

O compose local inclui Jaeger all-in-one com OTLP habilitado. `LedgerService.Api` e `BalanceService.Api` sobem com OpenTelemetry habilitado e exportam para `http://jaeger:4317` dentro da rede do compose.

Suba a stack:

```bash
docker compose up -d --build
```

Acesse a UI do Jaeger em `http://localhost:16686`.

Para gerar traces HTTP simples, sem Kafka, Outbox ou autenticacao, chame os endpoints operacionais:

```bash
curl http://localhost:5226/health
curl http://localhost:5226/ready
curl http://localhost:5228/health
curl http://localhost:5228/ready
```

Na UI do Jaeger, use o seletor de servico para procurar:

- `LedgerService.Api`
- `BalanceService.Api`

Ao consultar traces, o esperado e visualizar spans de entrada HTTP gerados pela instrumentacao ASP.NET Core para `GET /health` e `GET /ready`. A validacao confirma apenas o caminho minimo de traces HTTP; ela nao depende de eventos Kafka, Outbox, endpoints autenticados, spans customizados ou metricas customizadas.

### Validacao Auth -> Ledger com JWT

Para validar o fluxo HTTP autenticado com trace no Jaeger, use `Auth.Api` para obter um JWT RS256 e chame o endpoint protegido `POST /api/v1/lancamentos` no `LedgerService.Api`.

Esse endpoint foi escolhido porque:

- exige `Authorization: Bearer <token>` com scope `ledger.write`;
- exige `Idempotency-Key`;
- aceita e devolve `X-Correlation-Id`;
- usa `merchantId` no contrato real e valida esse valor contra a claim `merchant_id` emitida pelo `Auth.Api`;
- gera uma requisicao HTTP de negocio visivel como trace em `LedgerService.Api`.

Payload real do login, conforme `src/Auth.Api/Contracts/LoginRequest.cs`:

```json
{
  "username": "poc-usuario",
  "password": "Poc#123",
  "scope": "ledger.write"
}
```

O compose local configura essas credenciais de POC em `auth-api` e o `Auth.Api` versionado autoriza os merchants `tese` e `m1`. Para criar um lancamento, use um desses merchants. O contrato real de criacao de lancamento fica em `src/LedgerService.Api/Contracts/CreateLancamentoRequest.cs`; `CREDIT` exige `amount` maior que zero e `DEBIT` exige `amount` menor que zero.

No Windows/PowerShell, o fluxo completo pode ser executado com:

```powershell
./scripts/validate-auth-ledger-trace.ps1
```

O script:

1. chama `POST /auth/login` em `http://localhost:5030`;
2. extrai `access_token`;
3. chama `POST /api/v1/lancamentos` em `http://localhost:5226`;
4. envia `Authorization`, `Idempotency-Key` e `X-Correlation-Id` explicito;
5. imprime o status HTTP e o `X-Correlation-Id` devolvido;
6. consulta traces recentes de `LedgerService.Api` no Jaeger;
7. tenta localizar o correlation id nos logs recentes de `ledger-service`.

Tambem e possivel executar manualmente com `curl`:

```bash
TOKEN="$(curl -sS -X POST http://localhost:5030/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"poc-usuario","password":"Poc#123","scope":"ledger.write"}' \
  | sed -nE 's/.*"access_token"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p')"

CORRELATION_ID="11111111-1111-4111-8111-111111111111"
IDEMPOTENCY_KEY="$(uuidgen)"

curl -i -X POST http://localhost:5226/api/v1/lancamentos \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Idempotency-Key: ${IDEMPOTENCY_KEY}" \
  -H "X-Correlation-Id: ${CORRELATION_ID}" \
  -H "Content-Type: application/json" \
  -d '{
    "merchantId": "tese",
    "type": "CREDIT",
    "amount": 10.00,
    "description": "Validacao local Auth -> Ledger com OpenTelemetry",
    "externalReference": "local-auth-ledger-manual"
  }'
```

Resultado esperado:

- `POST /auth/login` retorna `access_token`;
- `POST /api/v1/lancamentos` retorna `201 Created`;
- o header `X-Correlation-Id` do response preserva o UUID enviado;
- `docker compose logs ledger-service --since 10m` mostra o correlation id no escopo de logs quando o provider imprime scopes;
- a UI do Jaeger em `http://localhost:16686` mostra trace recente para o servico `LedgerService.Api`, associado ao `POST /api/v1/lancamentos`.

Na UI do Jaeger:

1. selecione `LedgerService.Api` em `Service`;
2. clique em `Find Traces`;
3. procure uma entrada recente de `POST /api/v1/lancamentos`.

O `X-Correlation-Id` e sempre refletido no response pelo middleware de correlacao. Em traces HTTP gerados pela instrumentacao ASP.NET Core, ele nao deve ser tratado como substituto de `traceID`; para fluxos Kafka/Outbox, o correlation id tambem e persistido no dominio e propagado em mensagens como `correlation_id`.

Se o Jaeger ficar temporariamente indisponivel depois que a aplicacao ja iniciou, o exporter OTLP pode registrar falhas de exportacao, mas o processamento HTTP deve continuar. Nesse periodo os traces podem ser perdidos ou aparecer com atraso ate o backend voltar a receber dados.

### Host

Para execucao fora de container, configure PostgreSQL e Kafka locais e use as configuracoes dos `appsettings.json` como baseline. Use variaveis de ambiente para sobrescrever valores por ambiente:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=app123"
$env:Kafka__Producer__BootstrapServers = "127.0.0.1:9092"
$env:Observability__OpenTelemetry__Enabled = "true"
$env:Observability__OpenTelemetry__UseConsoleExporter = "true"
```

Nao versionar segredos. Em ambientes compartilhados ou produtivos, Kafka `Plaintext` e JWKS via HTTP nao devem ser usados; configure transporte seguro por variaveis de ambiente ou secret/config store.

## Correlation id

O header padrao e `X-Correlation-Id`:

- se vier ausente ou invalido, a API gera um UUID;
- o valor efetivo e devolvido no response;
- o valor entra no logging scope como `CorrelationId`;
- eventos Kafka usam `correlation_id` quando o fluxo possui esse valor.

`CorrelationId` nao substitui trace distribuido. Ele e um identificador estavel de operacao para suporte e auditoria leve; traces e spans continuam sendo a fonte para analise temporal detalhada quando OpenTelemetry esta habilitado.

## Validacao rapida

1. Suba a stack ou a API desejada.
2. Execute `GET /health` e `GET /ready` em `LedgerService.Api` ou `BalanceService.Api`.
3. Habilite OpenTelemetry com console exporter quando quiser validar traces e metricas.
4. Execute uma chamada HTTP enviando ou omitindo `X-Correlation-Id`.
5. Verifique:
   - header `X-Correlation-Id` no response;
   - logs com `CorrelationId`;
   - spans e metricas no console, quando `UseConsoleExporter=true`;
   - chegada de traces e metricas no collector, quando `OtlpEndpoint` estiver configurado.
6. Para fluxos Kafka, crie um lancamento e confirme publicacao Outbox, consumo pelo Balance e ausencia de mensagens inesperadas na DLQ.

## Governanca

Este documento deve ser atualizado quando houver mudanca em:

- endpoint operacional;
- readiness ou health check;
- campos de log, correlacao, tracing ou metricas;
- topicos, headers, contratos Kafka ou DLQ;
- politica de Outbox, retry, lock, backoff ou estados;
- configuracao local, compose, portas ou variaveis relevantes;
- decisao arquitetural registrada em ADR que afete operacao.

Mudancas arquiteturais relacionadas a observabilidade, operacao, mensageria, resiliencia ou setup local devem atualizar tambem a ADR correspondente ou criar nova ADR quando houver decisao nova.
