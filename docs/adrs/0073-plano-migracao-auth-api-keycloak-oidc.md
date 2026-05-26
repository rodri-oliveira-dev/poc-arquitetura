# ADR-0073 - Plano de migracao do Auth.Api para Keycloak/OIDC

## Status

Substituido por [ADR-0074](./0074-keycloak-como-identidade-principal.md)

## Contexto

A POC usa atualmente o `Auth.Api` como emissor local de JWT RS256 e como publicador de JWKS em `GET /.well-known/jwks.json`. As APIs de negocio (`LedgerService.Api` e `BalanceService.Api`) validam tokens offline por JWKS, sem introspeccao por request, conforme [ADR-0004](./0004-autenticacao-jwt-rs256-via-jwks.md).

A [ADR-0006](./0006-migrar-auth-api-para-keycloak.md) registrou a intencao de substituir o `Auth.Api` por Keycloak/OIDC. Desde entao, a POC tambem consolidou autorizacao por merchant via claim `merchant_id` na [ADR-0023](./0023-autorizacao-por-merchant.md) e endureceu temporariamente o `Auth.Api` na [ADR-0024](./0024-politica-autenticacao-auth-api-poc.md).

Esta ADR transforma a ADR-0006 em um plano tecnico executavel e rastreavel. A decisao final aplicada na stack principal foi registrada depois na [ADR-0074](./0074-keycloak-como-identidade-principal.md).

## Decisao

Manter a [ADR-0006](./0006-migrar-auth-api-para-keycloak.md) como registro historico da decisao inicial e criar esta ADR para guiar a execucao incremental da migracao. A ADR-0006 nao sera reescrita como estado atual.

A migracao deve substituir o `Auth.Api` por Keycloak/OIDC, mantendo as APIs de negocio com validacao offline de JWT assinado via JWKS. As APIs nao devem fazer introspeccao de token por request.

A primeira fase da migracao deve manter compatibilidade com as claims atuais para reduzir impacto nos endpoints, testes, scripts e cenarios k6:

- `iss`: deve ser o issuer do realm Keycloak configurado nas APIs, por exemplo o issuer exposto pelo discovery OIDC do realm local.
- `aud`: deve continuar representando a API consumidora, com `ledger-api` para `LedgerService.Api` e `balance-api` para `BalanceService.Api`.
- `scope`: deve continuar como string com scopes separados por espaco, preservando `ledger.write`, `ledger.read`, `outbox.admin` e `balance.read`.
- `merchant_id`: deve continuar como string com um ou mais merchants separados por espaco, preservando comparacao ordinal exata nas APIs.

Scripts e automacoes locais que precisam obter tokens devem usar o fluxo OAuth2 `client_credentials` no Keycloak, com clients configurados para os scopes e merchants necessarios aos cenarios automatizados.

O fluxo Direct Grant/password flow nao deve ser o fluxo preferencial da POC. Ele so deve ser considerado como excecao temporaria, documentada e restrita a um cenario local especifico, quando houver necessidade demonstrada de simular autenticacao interativa sem frontend.

## Plano de execucao

Cada etapa abaixo deve ser implementada separadamente, com validacao e commit proprios:

1. Modelar realm, clients, scopes e claims no Keycloak.
2. Adicionar Keycloak ao ambiente local, incluindo persistencia e configuracao reproduzivel do realm.
3. Ajustar configuracao JWT das APIs para consumir o discovery/JWKS do Keycloak preservando validacao offline. Implementado com `Jwt:JwksUrl` direto para o endpoint de certificados do realm.
4. Atualizar scripts locais de token para `client_credentials`. Implementado mantendo fallback `TOKEN_PROVIDER=auth-api`.
5. Atualizar documentacao operacional de autenticacao e exemplos de uso. Implementado para a configuracao local Keycloak.
6. Adaptar testes de integracao que dependem de emissao de token, mantendo asserts de issuer, audience, scopes e `merchant_id`.
7. Atualizar cenarios k6 e validacoes de seguranca quando os endpoints ou headers de autenticacao usados pelos cenarios forem afetados.
8. Remover o `Auth.Api` da stack local e da solution somente depois de a validacao com Keycloak cobrir o fluxo equivalente.

## Regras de contrato

Durante a primeira fase, os consumidores devem tratar o token Keycloak como equivalente funcional ao token atual do `Auth.Api`:

- issuer valido e configurado por ambiente;
- audience contendo a API de destino;
- scopes exigidos pelas policies existentes;
- `merchant_id` contendo exatamente os merchants autorizados para o token.

Nao deve haver dependencia runtime entre APIs de negocio e endpoint de introspeccao do Keycloak. A unica dependencia de autenticacao em runtime deve ser a obtencao e refresh das chaves publicas via discovery/JWKS, com cache conforme o modelo atual.

## Consequencias

### Beneficios

- Preserva baixa latencia e menor acoplamento das APIs de negocio.
- Reduz risco de migracao ao manter claims e policies atuais na primeira fase.
- Permite trocar o emissor de tokens sem redesenhar autorizacao por endpoint ou por merchant.
- Deixa a substituicao do `Auth.Api` rastreavel em etapas menores.

### Custos e trade-offs

- O Keycloak adiciona componente stateful, configuracao de realm e ciclo de vida operacional.
- Claims de `aud`, `scope` e `merchant_id` podem exigir mappers ou configuracao especifica de client scopes no Keycloak.
- Compatibilidade com o contrato atual pode atrasar adocao de modelos mais ricos de roles/grupos.
- Enquanto o `Auth.Api` e o Keycloak coexistirem, scripts e documentacao precisam deixar claro qual emissor esta ativo em cada etapa.

## Riscos

- Emitir tokens Keycloak sem `merchant_id` quebraria endpoints protegidos por autorizacao de merchant com `403`.
- Configurar audience generica demais pode enfraquecer isolamento entre `LedgerService.Api` e `BalanceService.Api`.
- Usar Direct Grant/password flow como padrao perpetuaria um fluxo inadequado para automacoes e mascararia a modelagem correta de clients.
- Remover `Auth.Api` antes de cobrir scripts, testes e k6 pode quebrar reproducibilidade local.
- Apontar APIs para introspeccao por request reintroduziria acoplamento e latencia rejeitados pela ADR-0004.

## Proximos passos historicos

- Definir o nome do realm local e os nomes dos clients sem alterar codigo nesta ADR.
- Especificar mappers Keycloak para `aud`, `scope` e `merchant_id`.
- Definir como os merchants autorizados serao configurados para clients de automacao local.
- Criar uma etapa de implementacao para compose/configuracao do Keycloak.
- Criar uma etapa de implementacao para scripts `client_credentials`.
- Revisar `docs/development/authentication.md` quando o fluxo Keycloak for implementado.

Esses passos foram fechados pela implementacao da migracao e pela [ADR-0074](./0074-keycloak-como-identidade-principal.md), que define Keycloak como identidade principal e deixa `Auth.Api` apenas como legado por overlay.
