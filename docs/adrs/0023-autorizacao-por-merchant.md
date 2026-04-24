# ADR-0023 - Autorizacao por merchant

## Status

Aceito

## Contexto

As APIs de negocio recebem `merchantId` em pontos controlados pelo cliente:

- `POST /api/v1/lancamentos`, no body.
- `GET /v1/consolidados/diario/{date}`, na query string.
- `GET /v1/consolidados/periodo`, na query string.

Antes desta decisao, os endpoints validavam autenticacao JWT, audience e scopes, mas nao havia verificacao explicita de que o token estava autorizado ao `merchantId` solicitado.

## Problema

Scopes como `ledger.write` e `balance.read` autorizam uma capacidade da API, mas nao delimitam o objeto de negocio. Um token valido poderia solicitar dados ou criar lancamentos para outro merchant se conhecesse ou tentasse outro identificador, caracterizando Broken Object Level Authorization.

## Decisao

As APIs LedgerService.Api e BalanceService.Api passam a exigir autorizacao por merchant para todo endpoint que receba `merchantId`.

O token deve conter a claim `merchant_id` com um ou mais merchants separados por espaco. O valor solicitado em body/query deve bater exatamente com um dos valores da claim, usando comparacao ordinal.

Comportamento esperado:

- token ausente, invalido, expirado, issuer/audience/assinatura invalidos: `401`;
- token valido sem scope exigido: `403`;
- token valido com scope, mas sem `merchant_id`: `403`;
- token valido com scope e `merchant_id` diferente do solicitado: `403`;
- token valido com scope e `merchant_id` compatível com o solicitado: segue o fluxo normal.

O Auth.Api da POC emite `merchant_id` a partir de `Auth:AuthorizedMerchants` para o usuario fixo.

## Alternativas Consideradas

- Usar apenas scopes por merchant, como `ledger.write:m1`: rejeitado por acoplar capacidade e tenancy, aumentando explosao de scopes.
- Validar merchant no Application/Domain: rejeitado porque a decisao depende de claims HTTP/JWT, detalhe da camada Api.
- Fazer introspeccao no Auth.Api a cada request: rejeitado para preservar o modelo atual de validacao offline via JWKS.
- Usar wildcard de merchant: rejeitado nesta POC para evitar permissao ampla implicita.

## Impacto no Contrato

Nao ha mudanca no formato de requests ou responses de sucesso.

Ha mudanca no contrato de autorizacao: tokens usados nos endpoints com `merchantId` devem incluir `merchant_id`. A falta da claim ou um valor divergente passa a resultar em `403 Forbidden`.

## Claims Esperadas

- `scope`: string com scopes separados por espaco, por exemplo `ledger.write balance.read`.
- `merchant_id`: string com merchants separados por espaco, por exemplo `m1 tese`.
- `iss`: issuer configurado no servico.
- `aud`: audience do servico, como `ledger-api` ou `balance-api`.

## Trade-offs

Esta decisao e simples e compativel com a arquitetura atual, mas ainda e uma representacao estatica de tenancy no token. Alteracoes de vinculo entre usuario e merchant so entram em vigor apos emissao de novo token ou expiracao do token antigo.

## Proximos Passos

- Evoluir o Auth.Api ou o provedor OIDC real para buscar merchants autorizados em fonte confiavel.
- Avaliar suporte a roles administrativas com regra explicita e auditavel.
- Adicionar auditoria de negacoes por merchant quando houver infraestrutura de seguranca/observabilidade dedicada.
