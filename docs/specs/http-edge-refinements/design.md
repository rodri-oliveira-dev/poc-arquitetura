# Design

## Forwarded hosts

`AddApiDefaults` passa a ler apenas `ForwardedHeaders` da configuracao. Os
argumentos programaticos de hosts encaminhados foram removidos, evitando que o
pacote Shared injete `localhost` ou qualquer dominio especifico de servico.

Cada API declara seus hosts locais em `appsettings.Development.json`. Em
ambientes nao locais, a configuracao externa deve informar:

- `ForwardedHeaders:TrustedProxies` ou `ForwardedHeaders:TrustedNetworks`;
- `ForwardedHeaders:AllowedHosts` com host publico real.

O validator rejeita, fora de `Development` e `Local`, `localhost`,
subdominios `.localhost` e enderecos de loopback.

## Swagger

Foi escolhida a alternativa 1: remover a policy nominal.

Justificativa:

- Swagger e habilitado por padrao apenas em `Development`;
- fora de `Development`, depende de `Swagger:Enabled=true`;
- o middleware Swagger fica antes de `UseRateLimiter()`;
- aplicar rate limiting exigiria mudar a forma de roteamento da documentacao sem
  ganho concreto para a POC;
- a borda Nginx local ja possui controles proprios de limite para demonstracao.

## Rate limiting autenticado

O modelo e misto:

- usuario final quando o token possui `sub` ou `ClaimTypes.NameIdentifier`;
- machine-to-machine quando o token possui somente `client_id` ou `azp`.

Precedencia e composicao:

1. `sub` ou `ClaimTypes.NameIdentifier`;
2. `client_id` ou `azp` como componente adicional quando presente;
3. `client_id` ou `azp` sozinho quando nao ha subject;
4. merchants autorizados da claim `merchant_id`, normalizados e ordenados;
5. fallback por IP remoto normalizado quando nao ha identidade utilizavel.

A composicao interna continua protegida por SHA-256. As metricas continuam com
labels de baixa cardinalidade: `policy` e `partition_type`.

## Catalogo arquitetural

`AllowedMessagingProviders`, `HasPersistence`,
`AllowedInternalLayerReferences`, `AllowedSharedProjectReferences` e
`DomainForbiddenTypeNameTerms` permanecem porque sao consumidos por regras de
arquitetura.

`StripeConceptLayers` e `HasApi` foram removidos porque nao eram consumidos por
nenhuma regra. A governanca de Stripe permanece na regra que restringe conceitos
Stripe ao `PaymentService`.
