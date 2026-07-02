# PocArquitetura.HttpResilienceDefaults

Defaults compartilhados para configurar `HttpClient` resiliente em servicos .NET da POC `poc-arquitetura`.

Use este pacote quando um servico precisar aplicar a mesma politica de resiliencia em chamadas HTTP de saida, com configuracao por cliente em `HttpResilience:Clients`.

## Instalacao

```bash
dotnet add package PocArquitetura.HttpResilienceDefaults
```

## Uso basico

```csharp
using HttpResilienceDefaults;

services
    .AddHttpClient("Ledger", client =>
    {
        client.BaseAddress = new Uri("https://ledger.localhost");
    })
    .AddConfiguredHttpResilience(configuration, "Ledger");
```

Exemplo de configuracao:

```json
{
  "HttpResilience": {
    "Clients": {
      "Ledger": {
        "TotalTimeout": "00:00:30",
        "AttemptTimeout": "00:00:10",
        "RetryCount": 3,
        "RetryDelay": "00:00:02",
        "CircuitBreakerFailureRatio": 0.1,
        "CircuitBreakerMinimumThroughput": 100,
        "CircuitBreakerSamplingDuration": "00:00:30",
        "CircuitBreakerBreakDuration": "00:00:05"
      }
    }
  }
}
```

## Recursos

- Timeout total da requisicao.
- Timeout por tentativa.
- Retry configuravel.
- Circuit breaker configuravel.
- Metricas e logs para retries, timeouts e transicoes do circuit breaker.

Esta e uma biblioteca de estudo/POC. Licenca MIT.
