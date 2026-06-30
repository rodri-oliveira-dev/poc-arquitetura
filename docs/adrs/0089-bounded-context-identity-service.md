# ADR-0089: Novo bounded context IdentityService

## Status
Aceito

## Data
2026-06-26

## Contexto
O projeto passou a ter um bounded context dedicado para identidade em
`src/identity`, com testes em `tests/identity`. Esse modulo e independente dos
bounded contexts financeiros existentes e deve evoluir sem misturar cadastro de
usuarios com regras de lancamento, saldo ou transferencia.

O `Auth.Api` permanece no repositorio apenas como legado de autenticacao de POC.
Ele nao deve receber novas responsabilidades de cadastro, gestao de usuarios ou
integracao com provider de identidade principal.

O `IdentityService` precisa seguir a mesma separacao arquitetural dos demais
servicos principais: `Api`, `Application`, `Domain` e `Infrastructure`, com
testes proprios e fronteiras explicitas.

## Decisao
Criar o bounded context `IdentityService` como modulo independente em
`src/identity`, com testes em `tests/identity`.

O modulo e composto por:

- `IdentityService.Api`, responsavel pela superficie HTTP, autenticacao,
  autorizacao, Swagger, health/readiness e composition root;
- `IdentityService.Application`, responsavel pelos casos de uso, portas,
  validacao de entrada da aplicacao e orquestracao;
- `IdentityService.Domain`, responsavel por entidades, value objects,
  aggregate roots, invariantes e domain events;
- `IdentityService.Infrastructure`, responsavel por EF Core, PostgreSQL,
  Keycloak Admin API, dispatch de domain events e adaptadores de e-mail.

O `IdentityService` usa o schema PostgreSQL `identity` e roles proprias para
runtime e migrations. Ele nao compartilha tabelas com Ledger, Balance,
Transfer ou Auth legado.

O `Auth.Api` continua legado e fora da stack principal. Qualquer integracao nova
de identidade deve usar Keycloak e o `IdentityService`, nao o `Auth.Api`.

## Consequencias

### Beneficios
- Isola cadastro e vinculo local de usuarios em um bounded context proprio.
- Preserva os bounded contexts financeiros sem acopla-los a detalhes de
  identidade.
- Mantem o `Auth.Api` rastreavel como legado, mas sem expandir sua superficie.
- Permite testar identidade de forma independente em `tests/identity`.
- Facilita evolucao futura de fluxos de usuario, e-mail, auditoria e integracao
  com provider de identidade.

### Custos e limitacoes
- A stack local passa a subir mais uma API e suas dependencias especificas.
- O contexto exige configuracao propria de Keycloak, PostgreSQL e e-mail.
- A operacao local precisa garantir que o client administrativo do Keycloak
  esteja configurado antes do `IdentityService.Api` iniciar.

### Impactos operacionais
- O compose local sobe `IdentityService.Api`, PostgreSQL, Keycloak,
  `keycloak-identity-admin-init` e Mailpit no fluxo padrao.
- O `IdentityService.Api` expoe Swagger proprio e contrato OpenAPI em
  `docs/openapi/identity.v1.json`.
- As migrations do contexto usam o `IdentityDbContext` e o schema `identity`.

## Fora do escopo
- Remover o `Auth.Api` legado.
- Transformar o `IdentityService` em emissor de tokens.
- Mover regras de autorizacao dos demais servicos para o `IdentityService`.
- Criar mensageria de identidade nesta etapa.
