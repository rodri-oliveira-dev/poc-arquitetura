# ADR-0024 - Politica de autenticacao do Auth.Api na POC

## Status

Aceito

## Contexto

O Auth.Api e um emissor JWT local para a POC. Ele publica JWKS e permite validar o fluxo de autenticacao/autorizacao dos servicos LedgerService.Api e BalanceService.Api sem depender de um provedor externo.

Antes desta decisao, o endpoint `POST /auth/login` mantinha credenciais de POC hardcoded no codigo e concedia todos os scopes suportados quando `scope` vinha vazio/nulo.

## Problema

Credenciais hardcoded e concessao implicita de scopes amplos aumentam o risco de uso indevido, especialmente se a configuracao de POC for reaproveitada fora do ambiente local. A concessao automatica de todos os scopes tambem mascara erros de cliente e viola o principio de menor privilegio.

## Decisao

O Auth.Api passa a exigir credenciais configuradas em `Auth:DevelopmentUser:Username` e `Auth:DevelopmentUser:Password`. Nao ha fallback em `appsettings.json` para producao; a aplicacao falha na inicializacao se esses valores nao forem configurados.

O campo `scope` deve ser explicito no request de login. Quando `scope` vier vazio/nulo ou contiver apenas espacos, o endpoint retorna `400 invalid_scope` e nao emite token. Scopes informados continuam sendo validados contra o catalogo fixo da POC.

O endpoint `POST /auth/login` passa a ter rate limit proprio por IP remoto, configuravel por `Auth:LoginRateLimit:PermitLimit` e `Auth:LoginRateLimit:WindowSeconds`, retornando `429 Too Many Requests` ao exceder o limite.

Logs basicos de auditoria registram sucesso, credencial invalida e scope ausente/invalido sem registrar senha.

## Limites da Solucao Atual

- O Auth.Api continua sendo um emissor simplificado de POC, nao um IdP real.
- A credencial configurada e estatica por ambiente.
- Nao ha refresh token, revogacao, MFA, politicas de senha ou bloqueio persistente de conta.
- O rate limit e em memoria, adequado para POC/local, mas nao coordenado entre replicas.
- O catalogo de scopes permanece fixo no codigo da POC.

## Recomendacao Futura

Substituir o Auth.Api por um IdP/OIDC real, como Keycloak ou provedor gerenciado, mantendo validacao offline via JWKS nos servicos consumidores. O IdP deve assumir autenticacao, politicas de senha, rotacao de credenciais, MFA, revogacao, auditoria e emissao de claims/scopes.

## Politica de Scopes

- Clientes devem solicitar scopes explicitamente.
- Scopes vazios/nulos nao geram token.
- O Auth.Api so emite scopes reconhecidos no catalogo da POC.
- Endpoints consumidores continuam aplicando policy-based authorization por scope.

## Riscos Aceitos Temporariamente

Aceitamos manter um usuario local configuravel para preservar a reprodutibilidade da POC. Esse usuario deve ser usado apenas em desenvolvimento/testes locais e nao substitui um IdP/OIDC real.

Aceitamos rate limit em memoria por simplicidade, sabendo que ambientes com multiplas instancias exigiriam armazenamento distribuido ou protecao no gateway/ingress.

## Impacto no Contrato

O contrato de sucesso de `POST /auth/login` permanece igual.

Ha mudanca no comportamento de erro: requests sem `scope` explicito agora recebem `400 invalid_scope` em vez de token com todos os scopes. O endpoint tambem pode retornar `429 Too Many Requests`.
