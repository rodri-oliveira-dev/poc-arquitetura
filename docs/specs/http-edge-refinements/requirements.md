# Refinamentos de borda HTTP

## Diagnostico

As APIs passavam hosts locais como argumentos de `AddApiDefaults`, por exemplo
`ledger.localhost` e `localhost`. Como esses valores eram adicionados
programaticamente a `ForwardedHeaders:AllowedHosts`, ambientes nao locais podiam
parecer configurados mesmo sem host publico explicitamente informado.

A policy `swagger` de rate limiting existia no Shared, mas Swagger e registrado
por middleware antes de `UseRateLimiter()` e nao recebia metadata de
`RequireRateLimiting`. A policy era nominal.

A chave autenticada de rate limiting priorizava `client_id`, depois `azp`, antes
de `sub`. Em tokens de usuario final com `azp` e `sub`, usuarios distintos do
mesmo client poderiam compartilhar bucket.

No catalogo arquitetural, `StripeConceptLayers` e `HasApi` eram metadados sem
regra consumidora.

## Requisitos

- Hosts locais devem existir somente em configuracao local ou de
  desenvolvimento.
- O Shared nao deve conhecer dominios locais nem produtivos.
- Ambientes fora de `Development` e `Local` devem exigir proxy/rede confiavel e
  host encaminhado nao local.
- `localhost`, subdominios `.localhost` e loopback nao podem satisfazer
  validacao produtiva.
- Swagger deve ficar sem policy nominal ou passar efetivamente pelo rate
  limiter; nao manter configuracao morta.
- A precedencia de claims deve ser explicita, estavel, protegida por hash e sem
  identificadores em labels de metricas.
- Tokens autenticados sem identidade utilizavel devem usar fallback por IP, sem
  particao global vazia.
- Metadados arquiteturais devem existir somente quando alguma regra os consome.

## Fora do escopo

- Redesenhar autenticacao JWT, scopes ou autorizacao por merchant.
- Introduzir rate limiting distribuido, Redis ou novo componente externo.
- Alterar CORS, Swashbuckle, contratos HTTP ou OpenAPI.
- Refatorar bounded contexts alem dos pontos de composicao das APIs.
