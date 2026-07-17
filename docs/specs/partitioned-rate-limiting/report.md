# Relatorio

## Resultado

A politica global `fixed` foi substituida por policies particionadas do ASP.NET
Core, todas locais a cada replica. A implementacao permanece em memoria e nao
introduz Redis, banco, cache distribuido ou novo componente de infraestrutura.

## Policies criadas

- `authenticated-read`: consultas autenticadas.
- `authenticated-write`: comandos autenticados.
- `administrative`: operacoes administrativas.
- `anonymous-webhook`: webhook anonimo Stripe.
- `fixed`: alias legado para escrita autenticada.

## Estrategia de chave

Endpoints autenticados:

- priorizam `sub` ou `ClaimTypes.NameIdentifier` quando ha subject;
- adicionam `client_id` ou `azp` a chave quando presente;
- usam `client_id` ou `azp` sozinho para tokens machine-to-machine sem subject;
- incluem `merchant_id` autorizado quando presente no token;
- ordenam merchants para manter chave estavel;
- usam fallback por IP remoto normalizado quando claims de cliente/subject
  estao ausentes;
- aplicam hash SHA-256 na composicao interna;
- nao usam body, query ou path como fonte de confianca para merchant.

Endpoints anonimos:

- usam `RemoteIpAddress` apos `UseForwardedHeaders()`;
- nao leem `X-Forwarded-For` diretamente;
- dependem da configuracao existente de proxies/redes confiaveis.

## Claims utilizadas

- `sub`;
- `ClaimTypes.NameIdentifier`;
- `client_id`;
- `azp`;
- `merchant_id`;
- `scope` permanece somente na autorizacao existente, sem mudanca de regra.

## Observabilidade

Foi adicionada a metrica `api.rate_limiting.rejected_requests` no meter
`ApiDefaults.RateLimiting`.

Labels:

- `policy`;
- `partition_type`.

Nao ha subject, merchant, client ID ou IP bruto como label de metrica.

## Limitacoes

O limite e por replica. Com multiplas replicas, cada uma possui janelas e
contadores proprios. O limite efetivo global pode crescer proporcionalmente ao
numero de replicas atingidas por um cliente.

Recomendacao futura: avaliar rate limiting distribuido em gateway/API
management ou storage externo dedicado quando o produto exigir limite global por
cliente, tenant ou merchant. Essa etapa nao implementa essa recomendacao.
