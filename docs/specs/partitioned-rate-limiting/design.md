# Design

## Policies

As policies ficam centralizadas em `ApiDefaults.RateLimiting.ApiRateLimitPolicies`:

- `authenticated-read`: consultas autenticadas.
- `authenticated-write`: comandos autenticados.
- `administrative`: operacoes administrativas, hoje Outbox DLQ/requeue.
- `anonymous-webhook`: webhooks anonimos, hoje Stripe.
- `fixed`: alias legado para escrita autenticada, mantido para compatibilidade
  durante migracoes.

As APIs nao aplicam mais uma policy unica em `MapControllers()`. Cada action ou
controller recebe `EnableRateLimiting` conforme a operacao. O endpoint minimal
de Identity aplica `RequireRateLimiting(ApiRateLimitPolicies.AuthenticatedWrite)`.

## Chave de particao autenticada

A chave segue um modelo misto com predominancia de usuario final quando o token
possui subject:

1. `sub` ou `ClaimTypes.NameIdentifier`;
2. `client_id` ou `azp`, como componente adicional quando presente;
3. `client_id` ou `azp` sozinho, para tokens machine-to-machine sem subject.

Quando `merchant_id` existe, seus valores sao normalizados, ordenados e
incluidos na composicao. Isso permite que tokens para merchants diferentes
tenham buckets independentes sem depender de merchant enviado no body, query ou
path.

Quando as claims de cliente/subject nao existem, a chave usa fallback por IP
remoto normalizado. Esse fallback evita uma particao vazia compartilhada por
todos os autenticados incompletos, mas e menos preciso que claims corretas.

A chave final e hash SHA-256 da composicao interna. O hash reduz risco de
identificadores aparecerem em diagnosticos de objetos, embora o limiter continue
local e em memoria.

## Chave de particao anonima

Webhooks anonimos usam `HttpContext.Connection.RemoteIpAddress`. Esse valor deve
ser consumido somente depois de `UseForwardedHeaders()`, que ja valida proxies,
redes confiaveis e `ForwardLimit = 1`.

O codigo nao le `X-Forwarded-For` diretamente. Se o proxy remoto nao for
confiavel, o middleware ignora o header e a particao permanece no IP da conexao
direta.

## Pipeline

`UseRateLimiter()` precisa executar depois de `UseAuthentication()` e
`UseAuthorization()` para que policies autenticadas vejam `HttpContext.User` com
claims. As APIs seguem a ordem:

1. `UseForwardedHeaders()`;
2. `UseApiDefaults()`;
3. Swagger quando habilitado;
4. `UseAuthentication()`;
5. `UseAuthorization()`;
6. `UseRateLimiter()`;
7. endpoints.

Swagger e registrado por middleware antes de `UseRateLimiter()` e nao possui
policy nominal. A protecao de Swagger em desenvolvimento local fica na borda
Nginx quando o overlay e usado; fora de `Development`, a exposicao continua
dependendo de `Swagger:Enabled=true`.

## Configuracao

A secao `ApiLimits` mantem os defaults antigos:

```json
{
  "ApiLimits": {
    "RateLimitPermitLimit": 100,
    "RateLimitWindowSeconds": 60,
    "RateLimitQueueLimit": 10
  }
}
```

Cada policy pode sobrescrever os defaults:

```json
{
  "ApiLimits": {
    "AuthenticatedReadRateLimit": {
      "PermitLimit": 300,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    "AuthenticatedWriteRateLimit": {
      "PermitLimit": 100,
      "WindowSeconds": 60,
      "QueueLimit": 10
    },
    "AdministrativeRateLimit": {
      "PermitLimit": 30,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    "AnonymousWebhookRateLimit": {
      "PermitLimit": 120,
      "WindowSeconds": 60,
      "QueueLimit": 0
    }
  }
}
```

`QueueLimit` e configurado conscientemente por policy. Para endpoints sensiveis
ou webhooks, `0` evita acumular trabalho na memoria da replica. Defaults atuais
preservam compatibilidade com a configuracao anterior quando a policy nao
sobrescreve o valor.

## Observabilidade

`ApiRateLimitMetrics` publica o meter `ApiDefaults.RateLimiting` e o contador
`api.rate_limiting.rejected_requests`.

Labels permitidas:

- `policy`;
- `partition_type`.

Labels proibidas:

- subject;
- client ID;
- merchant ID;
- IP bruto;
- path dinamico ou payload.

## Limites do modelo

O limiter e local e em memoria por replica. Em escala horizontal, cada replica
mantem sua propria janela e seus proprios contadores. Um cliente distribuido por
N replicas pode consumir aproximadamente N vezes o limite configurado, dependendo
do balanceamento.

Uma evolucao futura deve avaliar rate limiting distribuido com storage externo
ou gateway/API management quando houver requisito de limite global por tenant,
cliente ou merchant.
