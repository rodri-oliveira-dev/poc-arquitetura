# PetShop.Observability

Building blocks de propagação para futuros módulos, APIs, workers e serviços da plataforma.

## Responsabilidades

A solução é dividida em dois assemblies:

- `PetShop.Observability`: núcleo agnóstico de ASP.NET e da mensageira;
- `PetShop.Observability.AspNetCore`: middleware de entrada para APIs.

O núcleo suporta:

- `X-Correlation-Id` em chamadas HTTP de saída;
- `correlation_id` em mensagens;
- `tenant_id` em mensagens e jobs;
- `traceparent`, `tracestate` e `baggage` no formato W3C;
- captura de contexto para persistência em Outbox;
- criação de `Activity` dos tipos `Producer` e `Consumer`;
- adapters para qualquer broker baseado em `Dictionary<string, string>`.

A library não configura exporter, collector, backend APM ou sampling. Cada serviço continua responsável por configurar seu próprio `service.name`, instrumentações e exportação OpenTelemetry.

## Registro

```csharp
using PetShop.Observability.DependencyInjection;

builder.Services.AddPetShopObservabilityPropagation();
```

Para um `HttpClient`:

```csharp
builder.Services
    .AddHttpClient<AppointmentClient>()
    .AddCorrelationIdPropagation();
```

O `CorrelationIdDelegatingHandler` propaga apenas `X-Correlation-Id`. A propagação HTTP de `traceparent`, `tracestate` e `baggage` deve ser realizada pela instrumentação padrão do `HttpClient` do OpenTelemetry.

O tenant HTTP continua vindo da claim autenticada `tenant_id`. A library não envia `tenant_id` como header HTTP de autoridade.

## ASP.NET Core

```csharp
using PetShop.Observability.AspNetCore.Extensions;

app.UseAuthentication();
app.UsePetShopObservabilityContext();
app.UseAuthorization();
```

O middleware deve ser executado depois de `UseAuthentication`, pois lê a claim `tenant_id` do principal autenticado. Ele:

- valida ou gera `X-Correlation-Id` como GUID;
- devolve o mesmo header na resposta;
- adiciona correlation e tenant ao contexto de execução;
- enriquece a `Activity` e o scope de logging;
- não substitui as políticas de autenticação ou autorização multitenant.

## Publicação direta em mensageria

```csharp
using System.Diagnostics;

using PetShop.Observability.Messaging;

private static readonly ActivitySource ActivitySource =
    new("SchedulingService.Messaging");

using Activity? activity = propagation.StartProducerActivity(
    ActivitySource,
    "appointment.created publish",
    "kafka",
    "appointments.created.v1");

PropagationContextSnapshot outgoing = propagation.CaptureCurrent();
var headers = new Dictionary<string, string>();
propagation.Inject(headers, outgoing);

// O adapter converte os valores string para os headers nativos do broker.
await publisher.PublishAsync(payload, headers, cancellationToken);
```

A captura deve ocorrer depois que a Activity de producer for criada, para que o header `traceparent` represente o span de publicação.

## Consumo de mensagem

```csharp
PropagationContextSnapshot received = propagation.Extract(message.Headers);

using Activity? activity = propagation.StartConsumerActivity(
    ActivitySource,
    "appointment.created process",
    "kafka",
    message.Topic,
    received);

using IDisposable executionScope = executionContextAccessor.Push(received);
await processor.ProcessAsync(message, cancellationToken);
```

O adapter da mensageira é responsável apenas por transformar headers nativos em pares de string e vice-versa.

## Outbox

Para não quebrar o trace entre a transação original e o relay assíncrono:

1. durante o caso de uso, chame `CaptureCurrent()`;
2. persista `correlation_id`, `tenant_id`, `traceparent`, `tracestate` e `baggage` na Outbox;
3. o relay restaura esse snapshot como parent de uma Activity `Producer`;
4. depois de iniciar a Activity de producer, capture novamente o contexto;
5. injete o novo snapshot nos headers do broker.

Exemplo resumido do relay:

```csharp
PropagationContextSnapshot persisted = outboxMessage.PropagationContext;

using Activity? activity = propagation.StartProducerActivity(
    ActivitySource,
    "outbox publish",
    "kafka",
    outboxMessage.Destination,
    persisted);

PropagationContextSnapshot outgoing = propagation.CaptureCurrent(
    persisted.CorrelationId,
    persisted.TenantId);

var headers = new Dictionary<string, string>();
propagation.Inject(headers, outgoing);
await publisher.PublishAsync(outboxMessage.Payload, headers, cancellationToken);
```

## Adapters de broker

A library não referencia Kafka, Pub/Sub, RabbitMQ ou Azure Service Bus.

Um adapter Kafka pode converter os headers assim:

```csharp
var transportHeaders = new Confluent.Kafka.Headers();
foreach ((string key, string value) in headers)
{
    transportHeaders.Add(key, Encoding.UTF8.GetBytes(value));
}
```

No Pub/Sub, o mesmo dicionário pode ser mapeado diretamente para attributes.

## Regras de segurança e cardinalidade

- Não coloque token, senha, e-mail, telefone ou payload completo em baggage.
- Não use `correlation_id`, `tenant_id`, IDs de tutor, pet ou agendamento como labels de métricas.
- Tenant e correlation podem aparecer em logs e traces quando necessários para diagnóstico.
- Headers recebidos nunca substituem autorização.
- Mensagens tenant-owned devem possuir `tenant_id`; ausência deve ser tratada pelo consumidor conforme o contrato.
- DLQ, retry e replay devem preservar todos os headers de propagação.

## Integração futura

Quando uma mensageira for adotada, crie o adapter no projeto de infraestrutura correspondente. Não adicione dependência do broker a estes building blocks.
