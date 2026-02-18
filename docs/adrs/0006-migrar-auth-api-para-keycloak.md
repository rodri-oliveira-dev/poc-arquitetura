# ADR-0006: (Ponto de melhoria) Substituir Auth.Api por Keycloak (OIDC)

## Status
Proposto

## Data
2026-02-17

## Contexto
Atualmente, a PoC usa um microserviço próprio `Auth.Api` para:

- emitir JWT (RS256);
- expor JWKS público (`/.well-known/jwks.json`) para validação offline pelas APIs.

Esse design atende a PoC, mas traz limitações típicas de um auth “caseiro”:

- funcionalidades de IAM (usuários/roles, MFA, federation, gestão de clientes, rotação de chaves) não existem ou são simplificadas;
- manutenção de segurança vira responsabilidade do time (hardening, CVEs, features);
- fluxos OIDC completos (authorization code, refresh tokens, logout, etc.) não são priorizados.

Além disso, existe o desejo de evoluir para uma solução padrão de mercado.

> Observação importante: o termo do enunciado cita `auth.service`, mas no repositório o serviço existente se chama `Auth.Api`. Este ADR assume que o “auth.service” mencionado é o `Auth.Api` atual.

## Decisão
Planejar a migração futura de `Auth.Api` para **Keycloak**, adotando **OpenID Connect (OIDC)** como provider de identidade.

Diretriz de evolução:

- Manter o padrão das APIs de negócio de **validar JWT offline** via **JWKS**.
- Trocar a origem do JWKS de `Auth.Api` para o endpoint de JWKS do Keycloak (ex.: `.../realms/<realm>/.well-known/openid-configuration` -> `jwks_uri`).
- Substituir o fluxo `POST /auth/login` (credenciais fixas) por um fluxo compatível com OIDC (ex.: `client_credentials` para testes automatizados ou `password` somente se aceitável no contexto).

## Consequências

### Benefícios
- Reduz risco e esforço de manter um auth próprio.
- Ganha recursos “prontos”: gestão de usuários, rotação de chaves, clients, roles, políticas, MFA, etc.
- Melhora aderência a padrões (OIDC/OAuth2), facilitando integração com outras ferramentas e times.

### Trade-offs / custos
- Aumenta a complexidade operacional (novo componente/stateful) e configuração inicial.
- Necessidade de decidir estratégia de ambientes e persistência (DB do Keycloak, backups, upgrades).
- Possíveis mudanças de contratos/claims (issuer/audience/scope/roles), exigindo compatibilidade e migração.

## Alternativas consideradas

1) **Manter Auth.Api indefinidamente**
   - Prós: controle total e simplicidade na PoC.
   - Contras: vira dívida técnica; difícil evoluir com segurança.

2) **Outro provider OIDC gerenciado** (Auth0, Azure AD, Cognito)
   - Prós: reduz operação.
   - Contras: custo e dependência de cloud/fornecedor (pode não ser desejável).

## Próximos passos (não implementados)

- TODO: definir modelo de claims (audiences, scopes vs roles) e mapear para as policies existentes.
- TODO: ajustar documentação e scripts de obtenção de token (`scripts/get-token.*`) para o fluxo escolhido.
- TODO: ajustar `Jwt__JwksUrl` (ou evoluir para ler `openid-configuration`) e atualizar compose/infra.
