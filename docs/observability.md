# Observabilidade e operacao minima

Este documento define o inventario operacional minimo da POC para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`.

OpenTelemetry fica desabilitado por padrao. A correlacao via `X-Correlation-Id` permanece sempre ativa nas APIs e e usada para conectar logs, respostas HTTP e mensagens Kafka. A operacao local usa `docker compose`, PostgreSQL, Kafka, OpenTelemetry Collector e Jaeger conforme documentado em [desenvolvimento local](development/local-development.md).

## Baseline

- Logs: console logging do ASP.NET Core, com escopo de `CorrelationId` nos middlewares de correlacao, coletado centralmente por Grafana Alloy e consultavel no Loki no compose local.
- Traces: OpenTelemetry opcional para ASP.NET Core e `HttpClient`.
- Metricas: OpenTelemetry opcional para ASP.NET Core, `HttpClient` e runtime .NET.
- Exporters: console para validacao local e OTLP quando `OtlpEndpoint` estiver configurado.
- Correlacao: header HTTP `X-Correlation-Id`, campo `CorrelationId` em logs e `correlation_id` em eventos Kafka.
- Health: `GET /health` em `LedgerService.Api` e `BalanceService.Api`.
- Health simples tambem existe em `Auth.Api` para liveness do processo.
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

`Auth.Api` expoe `GET /health` como liveness simples. Ele nao expoe `GET /ready`; a validacao operacional minima do fluxo de autenticacao continua sendo feita por `POST /auth/login` e `GET /.well-known/jwks.json`.

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

Para enviar traces e metricas para um collector OTLP local no host:

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

O endpoint real deve ser fornecido pela plataforma de execucao ou secret/config store. Este repositorio provisiona apenas a stack local de desenvolvimento com OpenTelemetry Collector, Jaeger, Prometheus, Loki, Grafana Alloy, Alertmanager e Grafana. Tempo, notificacoes externas e stack produtiva equivalente continuam fora do escopo.

## Logs

Os logs usam o pipeline padrao do ASP.NET Core. As APIs habilitam `Logging:Console:IncludeScopes=true`, o que permite incluir o escopo de correlacao no console quando o provider exibe scopes.

O middleware de correlacao cria um scope estruturado e com representacao textual explicita:

```text
CorrelationId=<uuid> TraceId=<trace-id> SpanId=<span-id>
```

Esse formato evita que o console simples renderize apenas o tipo do estado do scope, como `System.Collections.Generic.Dictionary...`, e preserva os campos nomeados para providers que leem scopes estruturados.

Campos operacionais esperados:

- `CorrelationId`: valor do header `X-Correlation-Id`.
- `TraceId` e `SpanId`: adicionados ao logging scope em `LedgerService.Api` e `BalanceService.Api` quando ha `Activity` ativa.

Logs relevantes para operacao:

- pipeline HTTP, status codes e excecoes via handlers/middlewares das APIs;
- `OutboxKafkaPublisherService` no Ledger, incluindo polling, publicacao, falhas e retentativas;
- producer Kafka do Ledger, incluindo topico, particao e offset apos publicacao;
- consumer Kafka do Balance, incluindo processamento, commits, retries e envio para DLQ;
- erros de DLQ no Balance, especialmente quando a publicacao na DLQ falhar e o offset original nao for commitado.

### Logs centralizados com Loki e Alloy

O compose local adiciona Loki e Grafana Alloy para centralizar logs dos containers sem alterar o provider de logging das aplicacoes. As APIs continuam escrevendo no console; o Alloy le os logs dos containers pela Docker API e envia para o Loki.

Desenho local:

```text
Containers Docker -> Grafana Alloy -> Loki
Grafana -> Loki
```

O Alloy descobre apenas containers do projeto compose `poc-arquitetura` e aplica labels estaveis de baixa cardinalidade:

| Label | Origem | Exemplo |
| --- | --- | --- |
| `service` | label `com.docker.compose.service` | `ledger-service` |
| `container` | nome do container Docker | `poc-ledger-service` |
| `compose_project` | label `com.docker.compose.project` | `poc-arquitetura` |
| `environment` | valor fixo local | `local` |

No Loki local, a descoberta automatica de `service_name` e `detected_level` fica desabilitada para manter a lista de labels controlada pela POC.

`CorrelationId`, `TraceId`, `SpanId`, `event_id`, `outbox_message_id`, `merchant_id`, `idempotency_key`, payloads e mensagens de excecao nao sao labels do Loki. Esses valores podem ter alta cardinalidade e devem permanecer no conteudo pesquisavel do log. Isso preserva streams estaveis e evita explosao de cardinalidade no Loki.

Consultas LogQL uteis no Grafana Explore com datasource `Loki`:

```logql
{service="ledger-service"}
{service="balance-service"}
{container="poc-auth-api"}
{service="ledger-service"} |= "CorrelationId=<valor>"
{service="ledger-service"} |= "TraceId=<valor>"
{service="balance-service"} |= "CorrelationId=<valor>"
{service="ledger-service"} |= "fail"
{compose_project="poc-arquitetura", environment="local"}
```

Para uma janela recente, use o seletor de tempo do Grafana, por exemplo "Last 15 minutes". Pela API do Loki, a validacao basica pode usar `query_range`:

```bash
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service="ledger-service"}' \
  --data-urlencode 'limit=20'
```

Se Loki ou Alloy ficarem indisponiveis, a aplicacao continua funcionando porque nao envia logs diretamente para Loki. O impacto esperado e apenas perda ou atraso na centralizacao dos logs enquanto a coleta/backend estiver indisponivel. Traces e metricas seguem pelo OpenTelemetry Collector; logs nao substituem traces nem metricas.

## Traces

Quando `Observability:OpenTelemetry:Enabled=true`, as APIs registram:

- spans de entrada HTTP via instrumentacao ASP.NET Core;
- spans de saida HTTP via instrumentacao `HttpClient`.

`LedgerService` e `BalanceService` tambem criam `Activity` em trechos de Kafka/Outbox instrumentados no codigo:

- `LedgerService.OutboxPublisher`, para publicacao do Outbox no Kafka;
- `BalanceService.KafkaConsumer`, para consumo Kafka;
- `BalanceService.Application`, para aplicacao do evento no consolidado;
- `BalanceService.Api`, para consultas de consolidado.

Quando ha `Activity` ativa no request HTTP, o Ledger persiste `traceparent`, `tracestate` e `baggage` em colunas opcionais da `outbox_messages`. O publisher restaura esse contexto antes de criar o span `outbox.publish` e publica os mesmos headers W3C no Kafka. O `BalanceService.Api` usa `traceparent`/`tracestate` como parent do span `kafka.consume` e reidrata o header `baggage` como baggage da `Activity` quando possivel.

O `CorrelationId` continua sendo um identificador operacional separado do `TraceId`. Mensagens antigas, ou criadas sem `Activity`, ficam sem os campos W3C e seguem pelo fallback atual: o processamento continua, e spans Kafka podem nascer como raiz quando houver listener OpenTelemetry.

## Metricas

Quando OpenTelemetry esta habilitado, as APIs registram metricas de:

- ASP.NET Core;
- `HttpClient`;
- runtime .NET.

As metricas sao exportadas para console quando `UseConsoleExporter=true` e para OTLP quando `OtlpEndpoint` esta preenchido.

As metricas automaticas sao tecnicas e geradas pela instrumentacao OpenTelemetry. No compose local, elas chegam ao OpenTelemetry Collector pelo mesmo endpoint OTLP das APIs. O Collector expoe essas series em formato Prometheus no endpoint interno `otel-collector:9464`, e o Prometheus faz scrape desse endpoint. O Grafana usa o Prometheus como datasource provisionado.

### Metricas customizadas

Metricas customizadas devem medir sinais tecnicos ou operacionais que nao aparecem nas instrumentacoes automaticas. Elas complementam traces e logs:

- metrica: serie temporal agregavel para volume, taxa, duracao ou estado observado;
- trace: linha do tempo de uma execucao especifica, com spans e causalidade;
- log: registro textual/estruturado de eventos pontuais para diagnostico.

A fundacao de metricas customizadas usa `System.Diagnostics.Metrics`. Cada servico ou componente tecnico deve declarar `Meter` pequeno e explicito perto da camada que conhece a operacao medida, sem criar framework interno. O `Meter` precisa ser registrado no pipeline OpenTelemetry Metrics com `AddMeter(...)` na API responsavel.

Convencoes:

- nomes em lowercase separados por ponto: `<service_or_domain>.<component>.<operation>.<measure>`;
- contadores usam plural quando representam ocorrencias, por exemplo `ledger.outbox.publish.attempts`;
- unidades seguem UCUM quando aplicavel; contadores de ocorrencias usam `1`;
- descricoes devem explicar o evento medido, o escopo e se a metrica e tecnica ou de negocio;
- instrumentos preferenciais nesta fase: `Counter<T>` para eventos/ocorrencias e, quando necessario em evolucoes futuras, histogramas para duracoes ou tamanhos.

Labels/tags permitidas devem ter baixa cardinalidade. Exemplos aceitos:

- `service`;
- `operation`;
- `event_type`;
- `topic`;
- `status`;
- `result`.

Labels proibidas por alta cardinalidade:

- `correlation_id`;
- `trace_id`;
- `span_id`;
- `event_id`;
- `outbox_message_id`;
- `merchant_id`;
- identificadores de usuario;
- documentos;
- payloads;
- valores unicos por requisicao.

A primeira metrica customizada foi `ledger.outbox.publish.attempts`, registrada pelo `Meter` `LedgerService.Outbox`. A etapa atual expande a instrumentacao operacional para Outbox, producer Kafka, consumer Kafka e DLQ usando `System.Diagnostics.Metrics`, sem alterar regra de negocio, contrato Kafka, payload, topicos ou stack local.

Meters customizados registrados no OpenTelemetry Metrics quando `Observability:OpenTelemetry:Enabled=true`:

- `LedgerService.Domain`, emitido pelo `LedgerService.Api`;
- `LedgerService.Outbox`, emitido pelo `LedgerService.Api`;
- `BalanceService.Domain`, emitido pelo `BalanceService.Api`;
- `BalanceService.Kafka`, emitido pelo `BalanceService.Api`.

Com OpenTelemetry desabilitado, os instrumentos continuam sendo chamados pela aplicacao, mas nao ha provider/exporter ativo coletando as series. O fluxo funcional permanece inalterado.

### Metricas de dominio

Metricas tecnicas medem comportamento da plataforma ou runtime, como HTTP, `HttpClient` e runtime .NET. Metricas operacionais medem componentes de entrega e confiabilidade, como Outbox, producer Kafka, consumer Kafka e DLQ. Metricas de dominio medem fatos de negocio observaveis pelos casos de uso, como lancamentos criados, estornos processados, reprocessamentos e atualizacao de projecoes de saldo.

As metricas de dominio sao emitidas em pontos de orquestracao da camada de aplicacao ou no processamento de mensagens que chama casos de uso. Elas nao fazem parte do dominio puro: entidades, value objects e regras de dominio nao dependem de `System.Diagnostics.Metrics`, OpenTelemetry, exporters ou infraestrutura de observabilidade.

Metricas de dominio nao devem carregar identificadores individuais nem valores de alta cardinalidade. Tags como `merchant_id`, `ledger_entry_id`, `event_id`, `correlation_id`, `trace_id`, `span_id`, `document`, `external_reference`, `idempotency_key`, valor monetario, descricao e mensagem de exception continuam proibidas. Tags como `result`, `reason`, `operation`, `entry_type`, `event_type` e `currency` devem usar conjuntos pequenos e estaveis. A tag `reason` deve ser classificacao estavel, nunca mensagem livre.

Metricas de dominio do `LedgerService.Api`:

| Metrica | Instrumento | Unidade | Tags permitidas | Significado | Interpretacao operacional |
| --- | --- | --- | --- | --- | --- |
| `ledger.entries.created` | Counter | `1` | `entry_type`, `currency`, `result` | Lancamentos financeiros criados pelo caso de uso de criacao. | Volume de entrada financeira aceita por tipo (`CREDIT`/`DEBIT`) e moeda. `result=success` indica persistencia e Outbox gravadas com sucesso. |
| `ledger.entries.rejected` | Counter | `1` | `reason` | Lancamentos rejeitados por classificacao estavel. | Ajuda a diferenciar rejeicoes de dominio ou idempotencia sem expor payload, documento, valor ou mensagem livre. |
| `ledger.reversals.requested` | Counter | `1` | `result` | Solicitacoes de estorno recebidas pelo Ledger. | Indica volume de solicitacoes aceitas, rejeitadas, nao encontradas ou falhas no fluxo de entrada do estorno. |
| `ledger.reversals.processed` | Counter | `1` | `result` | Solicitacoes de estorno processadas pelo worker/caso de uso. | Indica conclusao, rejeicao ou falha tecnica no processamento financeiro do estorno. |
| `ledger.reprocess.requests.created` | Counter | `1` | `result` | Solicitacoes de reprocessamento recebidas pelo Ledger. | Indica volume de pedidos de replay aceitos, rejeitados ou com falha no fluxo de entrada. |
| `ledger.reprocess.requests.processed` | Counter | `1` | `result` | Solicitacoes de reprocessamento processadas pelo Ledger. | Indica se o replay terminou como `completed`, `completed_with_warnings`, `rejected` ou `failed`. |
| `ledger.idempotency.hits` | Counter | `1` | `operation` | Replays atendidos por idempotencia no Ledger. | Sinaliza repeticao esperada de operacoes sem contar a `Idempotency-Key`; `operation` usa nomes estaveis como `create_entry`, `request_reversal` e `request_reprocess`. |

Metricas de dominio do `BalanceService.Api`:

| Metrica | Instrumento | Unidade | Tags permitidas | Significado | Interpretacao operacional |
| --- | --- | --- | --- | --- | --- |
| `balance.events.applied` | Counter | `1` | `event_type`, `result` | Eventos financeiros tratados pela aplicacao da projecao. | `success` indica evento aplicado, `duplicate` indica entrega repetida ignorada com seguranca e `failed` indica falha antes da conclusao. |
| `balance.events.duplicates` | Counter | `1` | `event_type` | Eventos duplicados ignorados pela idempotencia da projecao. | Mede duplicidade de fatos financeiros no nivel de dominio/projecao, separada da metrica operacional do consumer Kafka. |
| `balance.projections.updated` | Counter | `1` | `currency` | Projecoes de saldo diario atualizadas. | Volume de atualizacoes efetivas em `daily_balances`; nao conta eventos duplicados. |
| `balance.apply.duration` | Histogram | `ms` | `event_type`, `result` | Duracao da aplicacao do evento financeiro na projecao. | Latencia de dominio do update de saldo, separada da duracao operacional do processamento Kafka. |

Resultados padronizados usados nas metricas de dominio:

- `success`: operacao aceita e concluida no ponto medido;
- `rejected`: regra, autorizacao contextual ou conflito impediu a operacao;
- `failed`: falha tecnica ou impossibilidade de replay;
- `duplicate`: evento ou requisicao repetida tratada por idempotencia;
- `not_found`: recurso de dominio esperado nao foi localizado;
- `completed`: processamento assincrono concluido;
- `completed_with_warnings`: processamento concluido sem todos os efeitos esperados, por exemplo reprocessamento sem lancamentos elegiveis.

Metricas operacionais do `LedgerService.Api`:

| Metrica | Instrumento | Unidade | Tags permitidas | Interpretacao |
| --- | --- | --- | --- | --- |
| `ledger.outbox.messages.created` | Counter | `1` | `event_type` | Volume de mensagens gravadas na Outbox. Ajuda a comparar entrada da Outbox com publicacao. |
| `ledger.outbox.messages.published` | Counter | `1` | `event_type`, `topic`, `result` | Volume de publicacoes Outbox por resultado (`success` ou `failure`). |
| `ledger.outbox.publish.duration` | Histogram | `ms` | `event_type`, `topic`, `result` | Latencia tecnica da publicacao Outbox, incluindo confirmacao Kafka e marcacao de status. |
| `ledger.outbox.messages.pending` | ObservableGauge | `1` | `event_type` | Quantidade atual de mensagens `Pending` por tipo de evento. Indica backlog acumulado. |
| `ledger.outbox.messages.failed` | ObservableGauge | `1` | `event_type` | Quantidade atual de mensagens `Failed` por tipo de evento. Indica mensagens que exigem acao operacional. |
| `ledger.outbox.publish.attempts` | Counter | `1` | `event_type`, `result` | Tentativas finalizadas de publicacao Outbox por resultado. |
| `ledger.kafka.producer.messages.published` | Counter | `1` | `topic`, `event_type`, `result` | Resultado das chamadas do producer Kafka do Ledger. |
| `ledger.kafka.producer.publish.duration` | Histogram | `ms` | `topic`, `event_type`, `result` | Latencia da chamada `ProduceAsync` do producer Kafka. |
| `ledger.kafka.producer.errors` | Counter | `1` | `topic`, `event_type`, `error_type` | Erros do producer Kafka por tipo estavel de excecao. |

Metricas operacionais do `BalanceService.Api`:

| Metrica | Instrumento | Unidade | Tags permitidas | Interpretacao |
| --- | --- | --- | --- | --- |
| `balance.kafka.consumer.messages.consumed` | Counter | `1` | `topic`, `event_type`, `result` | Mensagens consumidas por resultado (`success`, `duplicate` ou `dlq`). |
| `balance.kafka.consumer.processing.duration` | Histogram | `ms` | `topic`, `event_type`, `result` | Duracao do processamento do evento Kafka ate sucesso, duplicidade ou DLQ. |
| `balance.kafka.consumer.errors` | Counter | `1` | `topic`, `event_type`, `error_type` | Falhas recuperaveis ou tecnicas do consumer que seguem para retry. |
| `balance.kafka.consumer.duplicates` | Counter | `1` | `topic`, `event_type` | Mensagens ignoradas pela idempotencia de `processed_events`. |
| `balance.kafka.dlq.messages.published` | Counter | `1` | `source_topic`, `event_type`, `reason` | Mensagens publicadas na DLQ por motivo classificado. |
| `balance.kafka.dlq.publish.errors` | Counter | `1` | `source_topic`, `event_type`, `error_type` | Falhas ao publicar na DLQ; nesse caso o offset original nao deve ser commitado. |

Os gauges da Outbox executam consultas agregadas simples por `status` e `event_type`; eles nao fazem consulta por mensagem nem participam do fluxo critico de publicacao. Em caso de falha na observacao do gauge, a coleta retorna vazia e nao bloqueia o processamento.

Tags proibidas em metricas customizadas:

- `correlation_id`;
- `trace_id`;
- `span_id`;
- `event_id`;
- `outbox_message_id`;
- `merchant_id`;
- offsets Kafka;
- particao Kafka especifica;
- payload;
- mensagem completa de exception.

Para erros, `error_type` usa o nome estavel da excecao, por exemplo `KafkaException`, `ProduceException` ou `TimeoutException`. Para DLQ, `reason` usa classificacoes estaveis: `deserialization_failed`, `validation_failed`, `non_recoverable_processing_failure` ou `unknown`.

Estas metricas ainda nao possuem dashboard especifico nem alertas proprios nesta etapa. No compose local, o OpenTelemetry Collector recebe metricas OTLP e as expoe no exporter Prometheus junto das metricas tecnicas automaticas. Prometheus, Alertmanager e Grafana foram adicionados para validacao tecnica local, nao para definir SLOs, alertas de negocio ou operacao produtiva.

Referencias relacionadas:

- Kafka, Outbox e DLQ: [docs/development/kafka-outbox.md](development/kafka-outbox.md)
- Decisao arquitetural: [ADR-0059](adrs/0059-metricas-customizadas-system-diagnostics.md)

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
- Kafka: `localhost:19092`;
- Jaeger UI: `http://localhost:16686`;
- Jaeger OTLP gRPC/HTTP: `localhost:4317` e `localhost:4318`, expostos para diagnostico direto;
- OpenTelemetry Collector OTLP gRPC/HTTP: `otel-collector:4317` e `otel-collector:4318` apenas na rede interna do compose;
- OpenTelemetry Collector Prometheus exporter: `otel-collector:9464` apenas na rede interna do compose;
- Prometheus: `http://localhost:9090`;
- Loki: `http://localhost:3100`;
- Grafana Alloy: `http://localhost:12345`;
- Alertmanager: `http://localhost:9093`;
- Grafana: `http://localhost:3000`.

O compose sobrescreve configuracoes por variaveis de ambiente para usar os nomes internos `ledger-db`, `balance-db`, `kafka` e `otel-collector`. Aplique migrations manualmente antes de usar as APIs em banco vazio.

### Validacao local com Jaeger, Prometheus, Loki e Grafana

O compose local inclui OpenTelemetry Collector, Jaeger all-in-one com OTLP habilitado, Prometheus, Loki, Grafana Alloy e Grafana. O desenho local passa a ser:

```text
Aplicacoes -> OpenTelemetry Collector -> Jaeger
Aplicacoes -> OpenTelemetry Collector -> Prometheus endpoint
Prometheus -> OpenTelemetry Collector
Prometheus -> Alertmanager
Containers Docker -> Grafana Alloy -> Loki
Grafana -> Prometheus
Grafana -> Loki
```

`Auth.Api`, `LedgerService.Api` e `BalanceService.Api` sobem com OpenTelemetry habilitado e exportam para `http://otel-collector:4317` dentro da rede do compose. As APIs continuam apontando somente para o Collector, nao para Prometheus ou Grafana.

O Collector recebe OTLP via gRPC em `4317` e HTTP em `4318`, aplica `batch`, encaminha traces para `jaeger:4317` usando o exporter `otlp_grpc` e expoe metricas no exporter `prometheus` em `0.0.0.0:9464`. Essa porta nao e publicada no host; o Prometheus acessa `otel-collector:9464` pela rede Docker.

O Jaeger local continua sendo o backend de visualizacao de traces. O Prometheus coleta o Collector pelo job `otel-collector` a cada `15s` e tambem coleta `prometheus` e `alertmanager` para sinais tecnicos da propria stack de alerting. O Alloy coleta logs dos containers do compose local e envia para o Loki. O Grafana recebe datasources provisionados para Prometheus, com uid `prometheus`, apontando para `http://prometheus:9090` e marcado como default, e Loki, com uid `loki`, apontando para `http://loki:3100`.

O Alertmanager local recebe alertas do Prometheus pelo endereco interno `alertmanager:9093`. Ele usa receiver local sem envio externo; a UI fica disponivel em `http://localhost:9093`.

As metricas desta etapa continuam sendo tecnicas automaticas da instrumentacao OpenTelemetry, como ASP.NET Core, `HttpClient` e runtime .NET. A centralizacao de logs adiciona coleta e consulta textual no Loki, sem criar metricas customizadas, dashboards complexos, SLOs ou notificacoes externas.

### Dashboards Grafana provisionados

Os dashboards locais ficam versionados em `observability/grafana/dashboards/` e sao carregados por provisioning em `observability/grafana/provisioning/dashboards/dashboards.yml`. Os datasources ficam em `observability/grafana/provisioning/datasources/datasources.yml`. O `compose.yaml` monta esses arquivos no container Grafana em modo somente leitura, entao nao ha configuracao manual pos-subida.

Dashboards criados:

- `APIs - Visão Geral`: visao tecnica minima das APIs usando metricas automaticas HTTP.
- `Runtime .NET - Visão Geral`: visao tecnica minima do runtime .NET usando metricas automaticas `System.Runtime`.

Metricas usadas nos dashboards:

- `http_server_request_duration_seconds_count`: volume e taxa de requisicoes HTTP por servico, status, metodo e rota normalizada.
- `http_server_request_duration_seconds_bucket`: percentil 95 de duracao HTTP por servico.
- `dotnet_process_memory_working_set_bytes`: memoria do processo por servico.
- `dotnet_gc_collections_total`: coletas de GC por geracao.
- `dotnet_thread_pool_queue_length_total`: tamanho da fila do ThreadPool.
- `dotnet_exceptions_total`: excecoes observadas pelo runtime por tipo estavel.

As queries agrupam principalmente por `exported_job`, que representa `Auth.Api`, `LedgerService.Api` ou `BalanceService.Api` nas series exportadas pelo Collector. O painel por rota usa `http_route` somente porque a instrumentacao ASP.NET Core exporta rotas normalizadas, como `/health`, `/ready` e templates de rota, evitando identificadores unicos.

Limitacoes conhecidas:

- dashboards de Outbox, Kafka, DLQ e dominio dependem de metricas customizadas especificas e ficam para etapas futuras;
- os dashboards nao definem SLO, alerta, regra de negocio, retencao ou operacao produtiva;
- se a versao futura da instrumentacao alterar nomes ou labels das metricas automaticas, os JSONs devem ser revisados junto com a validacao no Prometheus.

### Alertas tecnicos Prometheus

As regras locais ficam em `observability/prometheus/rules/technical-alerts.yml` e sao carregadas pelo Prometheus por `rule_files`. O Prometheus envia os alertas para o Alertmanager local configurado em `observability/alertmanager/alertmanager.yml`.

Alertas criados:

| Alerta | Objetivo | Expressao PromQL | Severidade | `for` |
| --- | --- | --- | --- | --- |
| `CollectorDown` | Detectar indisponibilidade do OpenTelemetry Collector como target de metricas. | `up{job="otel-collector"} == 0` | `critical` | `1m` |
| `AlertmanagerDown` | Detectar indisponibilidade do Alertmanager local. | `up{job="alertmanager"} == 0` | `critical` | `1m` |
| `PrometheusConfigReloadFailed` | Detectar falha de reload da configuracao do Prometheus. | `prometheus_config_last_reload_successful == 0` | `critical` | `1m` |
| `HighHttp5xxRate` | Detectar taxa elevada de respostas HTTP 5xx nas APIs. | `sum by (exported_job) (rate(http_server_request_duration_seconds_count{exported_job=~"Auth.Api\|LedgerService.Api\|BalanceService.Api", http_response_status_code=~"5.."}[5m])) > 0.1` | `warning` | `2m` |
| `HighHttpRequestDuration` | Detectar p95 de latencia HTTP elevado nas APIs. | `histogram_quantile(0.95, sum by (exported_job, le) (rate(http_server_request_duration_seconds_bucket{exported_job=~"Auth.Api\|LedgerService.Api\|BalanceService.Api"}[5m]))) > 2` | `warning` | `5m` |
| `ReadinessEndpointFailing` | Detectar respostas 5xx observadas em `GET /ready` no Ledger ou Balance. | `sum by (exported_job) (increase(http_server_request_duration_seconds_count{exported_job=~"LedgerService.Api\|BalanceService.Api", http_route="/ready", http_response_status_code=~"5.."}[5m])) > 0` | `warning` | `1m` |
| `HighDotnetExceptionRate` | Detectar taxa elevada de excecoes .NET observadas pelo runtime. | `sum by (exported_job) (rate(dotnet_exceptions_total{exported_job=~"Auth.Api\|LedgerService.Api\|BalanceService.Api"}[5m])) > 1` | `warning` | `5m` |

Esses alertas usam somente labels de baixa cardinalidade ja presentes nas metricas tecnicas atuais, como `job`, `instance`, `service`, `exported_job`, `http_response_status_code` e `http_route`. Eles nao usam `CorrelationId`, `TraceId`, `SpanId`, `event_id`, `merchant_id` ou identificadores por requisicao.

Alertas evitados nesta etapa:

- `ServiceDown` generico e `PrometheusTargetMissing`: ficariam redundantes com alertas especificos para `CollectorDown` e `AlertmanagerDown` na topologia atual.
- `JaegerDown` e `GrafanaDown`: Jaeger e Grafana nao sao targets Prometheus configurados nesta etapa.
- Alertas de Outbox, Kafka, DLQ, dominio ou SLO: ficam fora do escopo enquanto nao houver criterio operacional e metricas adequadas para alerta.
- Alertas de readiness por sonda ativa: o Prometheus atual nao faz probe HTTP direto dos endpoints; `ReadinessEndpointFailing` depende de chamadas reais a `GET /ready` gerarem metricas HTTP.

Para visualizar as regras, acesse `http://localhost:9090/alerts`. Para ver o estado entregue ao Alertmanager, acesse `http://localhost:9093/#/alerts`. Silences locais podem ser criados pela UI do Alertmanager durante investigacoes manuais; nao ha roteamento para e-mail, Slack, Teams, PagerDuty ou webhook externo.

Suba a stack:

```bash
docker compose up -d --build
```

Acesse a UI do Jaeger em `http://localhost:16686`.

Para conferir os componentes de observabilidade:

```bash
docker compose logs otel-collector
docker compose logs jaeger
docker compose logs prometheus
docker compose logs loki
docker compose logs alloy
docker compose logs alertmanager
docker compose logs grafana
```

Para gerar traces HTTP simples, sem Kafka, Outbox ou autenticacao, chame os endpoints operacionais:

```bash
curl http://localhost:5226/health
curl http://localhost:5226/ready
curl http://localhost:5228/health
curl http://localhost:5228/ready
curl http://localhost:5030/health
```

Na UI do Jaeger, use o seletor de servico para procurar:

- `Auth.Api`
- `LedgerService.Api`
- `BalanceService.Api`

Ao consultar traces, o esperado e visualizar spans de entrada HTTP gerados pela instrumentacao ASP.NET Core para `GET /health` e `GET /ready` nos servicos que expoem esses endpoints. A validacao confirma apenas o caminho minimo de traces HTTP; ela nao depende de eventos Kafka, Outbox, endpoints autenticados, spans customizados ou metricas customizadas.

No Prometheus, acesse `http://localhost:9090/targets` e confirme que os targets `otel-collector:9464`, `alertmanager:9093` e `localhost:9090` estao `UP`. Em `http://localhost:9090/alerts`, confirme que o grupo `technical-alerts` foi carregado. Depois gere chamadas HTTP para as APIs e pesquise metricas tecnicas automaticas. Os nomes podem variar conforme a versao dos pacotes OpenTelemetry e do runtime .NET, mas normalmente incluem series relacionadas a HTTP server, HTTP client e runtime/processo .NET. Prometheus nao deve ter targets diretos para `Auth.Api`, `LedgerService.Api` ou `BalanceService.Api` nesta etapa.

No Alertmanager, acesse `http://localhost:9093/#/alerts` para visualizar alertas recebidos do Prometheus. A configuracao local nao envia notificacoes para sistemas externos.

No Loki, acesse `http://localhost:3100/ready` e espere resposta `ready`. Para validar ingestion sem depender do Grafana:

```bash
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service="auth-api"}' \
  --data-urlencode 'limit=20'
```

No Alloy, acesse `http://localhost:12345` para diagnostico local do agente. O container precisa ter acesso somente leitura a `/var/run/docker.sock`; sem esse socket, a coleta de logs falha, mas as APIs continuam funcionando.

No Grafana, acesse `http://localhost:3000` com as credenciais locais de POC `admin`/`admin`. Em `Connections` ou `Data sources`, confirme os datasources `Prometheus` apontando para `http://prometheus:9090` e `Loki` apontando para `http://loki:3100`. Em `Dashboards`, abra a pasta `Observability` e confirme que os dashboards `APIs - Visão Geral` e `Runtime .NET - Visão Geral` foram carregados automaticamente. Para validar metricas, use Explore com uma das metricas tecnicas listadas acima. Para validar logs, use Explore com o datasource `Loki` e a query `{service="ledger-service"}`.

### Validacao Auth -> Ledger -> Outbox -> Kafka -> Balance

Para validar o fluxo distribuido completo com chamada autenticada, Outbox, Kafka, Balance, logs e Jaeger, use `Auth.Api` para obter um JWT RS256 e chame o endpoint protegido `POST /api/v1/lancamentos` no `LedgerService.Api`.

Esse endpoint foi escolhido porque:

- exige `Authorization: Bearer <token>` com scope `ledger.write`;
- exige `Idempotency-Key`;
- aceita e devolve `X-Correlation-Id`;
- usa `merchantId` no contrato real e valida esse valor contra a claim `merchant_id` emitida pelo `Auth.Api`;
- grava `LedgerEntryCreated.v1` em `outbox_messages` na mesma transacao da escrita;
- aciona o `OutboxKafkaPublisherService`, que publica no topico `ledger.ledgerentry.created`;
- alimenta o `BalanceService.Api`, que atualiza `processed_events` e `daily_balances`.

Pre-requisitos:

- Docker-compatible API disponivel;
- stack local com migrations aplicadas;
- portas do compose livres: `5030`, `5226`, `5228`, `15432`, `15433`, `16686`, `19092`, `4317`, `4318`, `9090`, `9093` e `3000`;
- OpenTelemetry habilitado pelo compose para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`.

Suba a stack local completa. O script aplica migrations antes de iniciar Ledger e Balance:

```powershell
./scripts/start-local-stack.ps1
```

Se preferir validar apenas a sintaxe efetiva do compose:

```powershell
docker compose config
```

Payload real do login, conforme `src/Auth.Api/Contracts/LoginRequest.cs`:

```json
{
  "username": "poc-usuario",
  "password": "Poc#123",
  "scope": "ledger.write balance.read"
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
5. valida `201 Created` e o `X-Correlation-Id` devolvido;
6. consulta `outbox_messages` no PostgreSQL do Ledger ate encontrar o evento como `Sent`;
7. consulta `processed_events` e `daily_balances` no PostgreSQL do Balance;
8. chama `GET /v1/consolidados/diario/{date}?merchantId={merchantId}` no Balance com o mesmo `X-Correlation-Id`;
9. consulta traces recentes no Jaeger;
10. tenta localizar o `CorrelationId` nos logs recentes de `ledger-service` e `balance-service`.

O script usa polling curto configuravel por `-PollingTimeoutSeconds` e `-PollingIntervalSeconds`. Ele nao usa sleeps longos para mascarar consistencia eventual; se o Outbox ou o consumer nao avancarem dentro do timeout, a validacao falha com o ultimo estado observado.

### Validacao local de estorno e reprocessamento

Fluxos assincronos derivados do Ledger possuem scripts dedicados para validacao operacional local:

```powershell
./scripts/validate-ledger-reversal-flow.ps1
./scripts/validate-ledger-reprocess-flow.ps1
```

Se a politica local do PowerShell bloquear a execucao direta de scripts, execute de forma pontual com:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/validate-ledger-reversal-flow.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/validate-ledger-reprocess-flow.ps1
```

Pre-requisitos:

- stack local completa iniciada, preferencialmente com `./scripts/start-local-stack.ps1`;
- migrations aplicadas pelo startup local;
- Docker-compatible API disponivel para `docker compose exec -T ... psql`;
- portas do compose livres e acessiveis: `5030`, `5226`, `5228`, `16686`, `9090`, `9093` e `3000`;
- OpenTelemetry habilitado pelo compose para consulta de traces no Jaeger.

Os dois scripts usam `scripts/get-token.ps1`, enviam `Authorization`, `Idempotency-Key` e `X-Correlation-Id` explicito, fazem polling curto configuravel e falham com erro quando um estado esperado nao aparece.

`validate-ledger-reversal-flow.ps1` valida:

- criacao de um lancamento base em `POST /api/v1/lancamentos`;
- chegada do evento base `LedgerEntryCreated.v1` a `outbox_messages.status=Sent`;
- processamento inicial no Balance via `processed_events` e `daily_balances`;
- solicitacao real de estorno em `POST /api/v1/lancamentos/{lancamentoId}/estornos`;
- persistencia em `estornos_lancamentos`;
- publicacao do evento operacional `LancamentoEstornoSolicitado.v1`;
- evolucao do estorno ate `Completed`;
- criacao do lancamento compensatorio com `external_reference=estorno:{lancamentoOriginalId}`;
- publicacao do `LedgerEntryCreated.v1` compensatorio;
- consumo do compensatorio pelo Balance e delta compensatorio no consolidado diario;
- traces recentes no Jaeger e presenca do `CorrelationId` em logs recentes de `ledger-service` e `balance-service`.

`validate-ledger-reprocess-flow.ps1` valida:

- criacao de um lancamento base em `POST /api/v1/lancamentos`;
- fluxo normal ate Outbox `Sent`, `processed_events` e `daily_balances`;
- solicitacao real de reprocessamento em `POST /api/v1/lancamentos/reprocessar`;
- persistencia em `reprocessamentos_lancamentos`;
- publicacao do evento operacional `ReprocessamentoLancamentosSolicitado.v1`;
- evolucao do reprocessamento ate `Completed`;
- republicacao de `LedgerEntryCreated.v1` para o lancamento elegivel;
- idempotencia do Balance mantendo uma unica linha em `processed_events` para o mesmo evento financeiro;
- consolidado diario inalterado apos a reentrega idempotente;
- traces recentes no Jaeger e presenca do `CorrelationId` em logs recentes de `ledger-service` e `balance-service`.

Limitacoes conhecidas:

- os scripts assumem o compose local e os nomes de servico `ledger-db`, `balance-db`, `ledger-service` e `balance-service`;
- os scripts consultam bancos diretamente para validar estados assincronos e identificadores internos que nao fazem parte do contrato publico de criacao de lancamento;
- a validacao do Jaeger confirma traces recentes por servico, nao uma busca por `CorrelationId` na UI;
- a politica local do PowerShell pode exigir `-ExecutionPolicy Bypass` na invocacao do processo;
- o token local padrao usa os scopes aceitos pelo `Auth.Api` nesta POC (`ledger.write balance.read`), por isso os scripts validam estados de estorno/reprocessamento pelo banco em vez dos endpoints de status protegidos por `ledger.read`.

Tambem e possivel executar manualmente com `curl`:

```bash
TOKEN="$(curl -sS -X POST http://localhost:5030/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"poc-usuario","password":"Poc#123","scope":"ledger.write balance.read"}' \
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

Guarde o `id` retornado no body, por exemplo `lan_12345678`, e a data de `occurredAt`.

Consultas SQL uteis pelo compose:

```bash
docker compose exec -T ledger-db psql -U appuser -d appdb \
  -c "SELECT id, event_type, status, attempts, correlation_id, traceparent, tracestate, baggage, processed_at FROM outbox_messages WHERE correlation_id = '11111111-1111-4111-8111-111111111111' ORDER BY occurred_at DESC LIMIT 5;"
```

Durante uma janela curta, a mensagem pode aparecer como `Pending` ou `Processing`. Depois do polling do `OutboxKafkaPublisherService`, o esperado e `Sent`. Se Kafka estiver indisponivel, acompanhe `attempts`, `next_attempt_at`, `last_error` e eventual `Failed`.

No Balance, confirme o efeito funcional:

```bash
docker compose exec -T balance-db psql -U userBalance -d dbBalance \
  -c "SELECT event_id, merchant_id, processed_at FROM processed_events WHERE event_id = '<ID_RETORNADO_PELO_LEDGER>';"

docker compose exec -T balance-db psql -U userBalance -d dbBalance \
  -c "SELECT merchant_id, date, currency, total_credits, total_debits, net_balance FROM daily_balances WHERE merchant_id = 'tese' ORDER BY updated_at DESC LIMIT 5;"
```

Tambem e possivel consultar a API de leitura, usando o mesmo token:

```bash
curl -i "http://localhost:5228/v1/consolidados/diario/<YYYY-MM-DD>?merchantId=tese" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Correlation-Id: ${CORRELATION_ID}"
```

Logs:

```bash
docker compose logs ledger-service --since 10m | grep 11111111-1111-4111-8111-111111111111
docker compose logs balance-service --since 10m | grep 11111111-1111-4111-8111-111111111111
```

Logs centralizados no Loki:

```bash
curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service="ledger-service"} |= "CorrelationId=11111111-1111-4111-8111-111111111111"' \
  --data-urlencode 'limit=20'

curl -G "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service="balance-service"} |= "CorrelationId=11111111-1111-4111-8111-111111111111"' \
  --data-urlencode 'limit=20'
```

Para buscar pelo `TraceId`, copie o valor da UI/API do Jaeger ou do logging scope e pesquise no conteudo do log:

```logql
{service="ledger-service"} |= "TraceId=<trace-id>"
{service="balance-service"} |= "TraceId=<trace-id>"
```

Resultado esperado:

- `POST /auth/login` retorna `access_token`;
- `POST /api/v1/lancamentos` retorna `201 Created`;
- o header `X-Correlation-Id` do response preserva o UUID enviado;
- a tabela `outbox_messages` contem `LedgerEntryCreated.v1` com o mesmo `correlation_id` e status final `Sent`;
- os logs do Ledger mostram o `CorrelationId` na requisicao e/ou no publisher;
- o Loki retorna logs do Ledger com o `CorrelationId` dentro do conteudo do log;
- `processed_events` contem o `id` do evento financeiro retornado no payload do Ledger;
- `daily_balances` reflete o credito ou debito criado;
- `GET /v1/consolidados/diario/{date}` retorna o consolidado atualizado;
- os logs do Balance mostram o mesmo `CorrelationId` durante o consumo;
- o Loki retorna logs do Balance com o `CorrelationId` dentro do conteudo do log;
- a UI do Jaeger em `http://localhost:16686` mostra traces recentes para `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`.

Na UI do Jaeger:

1. selecione `Auth.Api` e procure `POST /auth/login`;
2. selecione `LedgerService.Api` e procure `POST /api/v1/lancamentos` e spans `outbox.publish`;
3. selecione `BalanceService.Api` e procure spans `kafka.consume`, `balance.apply` e a consulta `GET /v1/consolidados/diario/{date}`;
4. use o `TraceID` para analise temporal e o `CorrelationId` nos logs/SQL para conectar a operacao de negocio.

Trace distribuido no fluxo autenticado:

- `POST /auth/login` e `POST /api/v1/lancamentos` sao chamadas HTTP separadas; o token JWT nao carrega trace context.
- O Ledger persiste `correlation_id`, `traceparent`, `tracestate` e `baggage` na tabela `outbox_messages`.
- O trace HTTP do `POST /api/v1/lancamentos` pode aparecer como a mesma arvore do processamento assincrono Outbox/Kafka/Balance quando OpenTelemetry esta habilitado e existe `Activity` ativa no request.
- O span `outbox.publish` usa o contexto salvo na Outbox como parent e o producer publica os headers W3C no Kafka.
- O consumer do Balance usa esse parent quando `traceparent` esta presente; se o contexto W3C estiver ausente ou invalido, o consumo continua com fallback operacional.

### Diagnostico de propagacao Kafka

Estado atual auditado no fluxo `LedgerService.Api` -> Outbox -> Kafka -> `BalanceService.Api`:

- O `CorrelationIdMiddleware` das APIs resolve `X-Correlation-Id`, gera UUID quando o header esta ausente ou invalido, injeta o valor no request/response HTTP e cria scope de log com `CorrelationId`, `TraceId` e `SpanId` quando ha `Activity` ativa.
- O caso de uso de criacao de lancamento grava o `correlationId` no payload `LedgerEntryCreated.v1` e na coluna `outbox_messages.correlation_id`.
- Quando existe `Activity.Current`, o caso de uso tambem grava `traceparent`, `tracestate` e `baggage` nas colunas opcionais da Outbox.
- O `OutboxKafkaPublisherService` restaura o parent W3C salvo na Outbox ao criar a `Activity` `outbox.publish` com `ActivityKind.Producer`.
- O `OutboxKafkaProducer` publica `event_id`, `event_type`, `correlation_id`, `traceparent`, `tracestate` e `baggage` quando esse contexto existe na Outbox ou na `Activity.Current`.
- O `BalanceService` le headers Kafka em dicionario case-insensitive, valida `event_type=LedgerEntryCreated.v1`, usa `event_id` do header quando presente e faz fallback para o `id` do payload.
- O consumer tenta restaurar o parent do span `kafka.consume` com `ActivityContext.TryParse(traceparent, tracestate)`. Se o `traceparent` estiver ausente ou invalido, o consumo continua com uma nova Activity raiz quando OpenTelemetry esta habilitado.
- O `correlation_id` do header Kafka nao e usado como fonte primaria no Balance; a correlacao operacional vem do campo `correlationId` do payload. Por isso, mensagens sem header `correlation_id`, mas com payload valido e `event_type`, continuam sendo processadas.
- O header `baggage` recebido e reidratado como baggage da `Activity` quando possivel. O consumer tambem adiciona `correlation_id` como baggage local.
- Mensagens com `event_type`, payload invalido ou falha nao recuperavel sao enviadas para DLQ. A DLQ preserva `event_id`, `event_type`, `correlation_id`, `traceparent`, `tracestate` e `baggage` quando esses headers existirem.

Com OpenTelemetry desligado:

- a correlacao por `X-Correlation-Id`, payload `correlationId`, coluna `outbox_messages.correlation_id`, logs e header Kafka `correlation_id` continua funcionando;
- as `ActivitySource` customizadas nao possuem listener e normalmente nao criam `Activity`; consequentemente, novas mensagens nao persistem nem publicam `traceparent`, `tracestate` e `baggage`;
- o Balance continua processando mensagens Kafka validas, mas nao ha arvore exportada de spans customizados.

Riscos e limites conhecidos:

- A continuidade HTTP -> Outbox -> Kafka -> Balance depende de `Activity` ativa no request de origem. Sem OpenTelemetry/listener, nao ha contexto W3C novo a persistir.
- Sem `traceparent`, o Balance pode gerar um span raiz para `kafka.consume`, o que e correto operacionalmente, mas representa trace quebrado para analise temporal ponta a ponta.
- O `CorrelationId` esta separado do `TraceId`; isso evita acoplamento indevido, mas tambem significa que consultas por correlation id dependem de logs, SQL, tags ou payload, nao da identidade nativa do trace.
- A formatacao/parsing de `baggage` e pequena e cobre o formato usado pela POC; valores com metadados W3C sao aceitos descartando metadados no baggage reidratado.
- A logica W3C foi centralizada em helpers pequenos por servico para reduzir drift sem criar framework interno.

O `X-Correlation-Id` e sempre refletido no response pelo middleware de correlacao. Em traces HTTP gerados pela instrumentacao ASP.NET Core, ele nao deve ser tratado como substituto de `traceID`; para fluxos Kafka/Outbox, o correlation id tambem e persistido no dominio e propagado em mensagens como `correlation_id`.

Para inspecionar headers no Kafka de forma pontual, use o console consumer do container Kafka. Esse comando pode consumir mensagens do topico principal; use em ambiente local/controlado:

```bash
docker compose exec -T kafka /opt/kafka/bin/kafka-console-consumer.sh \
  --bootstrap-server kafka:9092 \
  --topic ledger.ledgerentry.created \
  --from-beginning \
  --max-messages 5 \
  --property print.headers=true
```

Procure headers `event_id`, `event_type`, `correlation_id` e, quando tracing estiver ativo no publisher, `traceparent`.

Se o Collector ou o Jaeger ficar temporariamente indisponivel depois que a aplicacao ja iniciou, o exporter OTLP pode registrar falhas de exportacao, mas o processamento HTTP deve continuar. Nesse periodo os traces podem ser perdidos ou aparecer com atraso ate o backend voltar a receber dados.

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
- o valor entra no logging scope como `CorrelationId=<uuid>`;
- eventos Kafka usam `correlation_id` quando o fluxo possui esse valor.

`CorrelationId` nao substitui trace distribuido. Ele e um identificador estavel de operacao para suporte e auditoria leve, controlado pelo header HTTP e propagado para responses e eventos. `TraceId` e `SpanId` identificam a arvore temporal de spans da `Activity`; quando OpenTelemetry esta desabilitado, a correlacao por `X-Correlation-Id` continua funcionando.

## Validacao rapida

1. Suba a stack ou a API desejada.
2. Execute `GET /health` e `GET /ready` em `LedgerService.Api` ou `BalanceService.Api`.
3. Habilite OpenTelemetry com console exporter quando quiser validar traces e metricas.
4. Execute uma chamada HTTP enviando ou omitindo `X-Correlation-Id`.
5. Verifique:
   - header `X-Correlation-Id` no response;
   - logs com `CorrelationId`;
   - spans e metricas no console, quando `UseConsoleExporter=true`;
   - chegada de traces no Collector e visualizacao no Jaeger, quando `OtlpEndpoint` estiver configurado.
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
