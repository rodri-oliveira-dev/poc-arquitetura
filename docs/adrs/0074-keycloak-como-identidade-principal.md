# ADR-0074: Keycloak como identidade principal e Auth.Api legado

## Status

Aceito

## Data

2026-05-26

## Contexto

O Keycloak local ja emite JWT RS256 para o realm `poc`, publica JWKS e possui client de automacao local para `client_credentials`. `LedgerService.Api` e `BalanceService.Api` validam tokens offline via JWKS e ja usam Keycloak como issuer/JWKS padrao no compose.

O `Auth.Api` ainda existia na stack principal como emissor de POC por `POST /auth/login` e JWKS em `GET /.well-known/jwks.json`, mas esse caminho mantinha uma segunda origem operacional de identidade e exigia manutencao adicional em Nginx, scripts e documentacao.

## Decisao

Keycloak passa a ser o provedor principal de identidade da POC local.

O `Auth.Api` fica depreciado como legado:

- removido do `compose.yaml` principal;
- removido da borda Nginx principal;
- mantido no repositorio e na solution enquanto seus testes e contrato legado existirem;
- disponivel somente pelo overlay explicito `compose.auth-legacy.yaml`, profile `legacy-auth`.

Os testes de `Auth.Api` permanecem porque o projeto ainda existe e preserva rastreabilidade do emissor legado. A remocao completa do projeto, seus testes e referencias historicas deve ser uma etapa futura separada.

## Consequencias

- A stack principal sobe apenas Keycloak, Ledger, Balance, Workers, bancos, Kafka e observabilidade opcional.
- Scripts operacionais e k6 usam Keycloak por padrao para token.
- O fallback `TOKEN_PROVIDER=auth-api` continua possivel apenas quando o overlay legado estiver ativo e as APIs forem configuradas para o issuer/JWKS legado.
- O Nginx local deixa de expor `auth.localhost`.
- Documentacao e LikeC4 passam a representar Keycloak como identidade principal.

## Alternativas consideradas

1. Remover `Auth.Api` da solution e apagar testes agora.
   - Rejeitado porque ainda ha valor em manter o contrato legado testado e rastreavel durante a transicao.

2. Manter `Auth.Api` no compose principal com Keycloak como padrao.
   - Rejeitado porque manter dois emissores na stack minima aumenta superficie operacional e confunde o caminho recomendado.

3. Mover `Auth.Api` para overlay legado.
   - Aceito por reduzir impacto, preservar rollback local e deixar a decisao operacional explicita.
