# ADR-0019: Endurecer seguranca das APIs conforme OWASP

## Status
Proposto

## Contexto

As APIs de negocio ja possuem controles importantes: JWT Bearer via JWKS, policies por scope, fallback policy autenticada, rate limiting, security headers, ProblemDetails, correlation id e readiness. A analise OWASP identificou, porem, diferencas entre APIs e riscos ainda relevantes:

- `Auth.Api` usa usuario/senha fixos de POC;
- `Auth.Api` nao tem o mesmo hardening de headers, rate limit, HTTPS/HSTS e erro padronizado das APIs de negocio;
- Swagger fica publico nas APIs de negocio e pode ser habilitado no Auth por configuracao;
- a autorizacao atual valida scopes, mas nao vincula `merchantId` ao sujeito do token.

## Decisao proposta

Padronizar um baseline de seguranca para todas as APIs:

- aplicar hardening equivalente no Auth.Api ou substituir Auth.Api por um IdP OIDC;
- definir exposicao de Swagger por ambiente/config;
- exigir autorizacao de objeto/tenant para `merchantId`;
- manter scopes por endpoint, mas complementar com regras de ownership;
- revisar fluxos sensiveis com rate limits especificos.

## Alternativas consideradas

- Manter controles atuais por serem suficientes para POC.
- Endurecer apenas Ledger e Balance.
- Migrar diretamente para Keycloak/OIDC antes de qualquer hardening adicional.

## Consequencias positivas

- Reduz riscos OWASP API1, API2, API5, API6, API8 e API9.
- Evita que Auth.Api seja o ponto mais fraco da topologia.
- Deixa a POC mais proxima de uma base reutilizavel.

## Consequencias negativas / trade-offs

- Exige modelar tenancy/ownership, hoje ausente.
- Pode alterar contratos de token e testes.
- Pode aumentar friccao local se nao houver perfis claros de desenvolvimento.

## Riscos

- Implementar checagem de merchant no controller em vez de uma regra reutilizavel.
- Bloquear fluxos de POC sem oferecer caminho local simples.
- Criar divergencia entre Swagger documentado e autorizacao real.

## Proximos passos sugeridos

- Definir claims de merchant/tenant ou modelo equivalente.
- Criar testes de acesso negado entre merchants.
- Decidir se Auth.Api sera mantido ou substituido conforme ADR-0006.
- Definir politica de Swagger para local, CI, staging e producao.
