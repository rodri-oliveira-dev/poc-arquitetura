# ADR-0100: Substituir Auth.Api por Keycloak (OIDC) como IdP

## Status
Proposto

## Data
2026-02-16

## Contexto
Auth.Api é uma implementação customizada de emissão de JWT e JWKS, com escopo simplificado e credenciais fixas para PoC. Para evoluir segurança e governança, faz sentido adotar um provedor padrão OIDC.

Keycloak oferece endpoints OIDC e descoberta (well-known), JWKS e recursos avançados de autorização e gerenciamento de clientes/scopes. 

## Decisão
Migrar para Keycloak como Identity Provider:
- Emissão de tokens via fluxos OIDC padrão
- Descoberta via `/.well-known/openid-configuration` e uso do `jwks_uri`
- Modelar clients para `ledger-api` e `balance-api`
- Mapear scopes/roles de forma compatível com a política atual

## Consequências
- Reduz código custom de segurança e melhora aderência a padrões.
- Ganha recursos de gestão de usuários, clients, rotação de chaves, políticas, auditoria.
- Aumenta curva de configuração e dependência operacional do IdP.
- Exige revisão do modelo de scopes/audience e claims (ex.: padronizar `aud` em formato compatível com libs).

## Alternativas consideradas
- Continuar com Auth.Api e fortalecer internamente: mantém controle, porém reinventa OIDC.
- Usar outro IdP (Auth0, Cognito, etc.): possível, mas Keycloak atende bem ao cenário self-hosted.
