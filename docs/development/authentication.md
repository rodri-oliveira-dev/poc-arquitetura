# Autenticacao e autorizacao

Este documento descreve o fluxo atual de JWT Bearer via JWKS e as regras de autorizacao usadas pelas APIs de negocio.

## Modelo atual

- `Auth.Api` emite tokens JWT assinados com RS256.
- `Auth.Api` publica chaves publicas em `GET /.well-known/jwks.json`.
- `LedgerService.Api` e `BalanceService.Api` validam tokens por JWT Bearer e JWKS.
- As APIs nao fazem introspeccao por request; a configuracao de chaves usa cache e refresh.

## Claims e validacoes

Claims relevantes:

- `scope`: string com scopes separados por espaco.
- `merchant_id`: string com um ou mais merchants separados por espaco.

Validacoes esperadas:

- `iss` deve bater com `Jwt:Issuer`;
- `aud` deve conter a audience do servico;
- endpoints protegidos exigem scope compativel;
- endpoints que recebem `merchantId` no body ou query exigem que o valor exista na claim `merchant_id`.

Audiences atuais:

| Servico | Audience |
| --- | --- |
| LedgerService.Api | `ledger-api` |
| BalanceService.Api | `balance-api` |

Nesta POC, o `Auth.Api` pode emitir `aud` como string com audiences separadas por espaco, como `ledger-api balance-api`. As APIs tratam esse formato tokenizando por espaco.

## Scopes por endpoint

| Endpoint | Scope |
| --- | --- |
| `POST /api/v1/lancamentos` | `ledger.write` |
| `GET /v1/consolidados/diario/{date}` | `balance.read` |
| `GET /v1/consolidados/periodo` | `balance.read` |

## Transporte e JWKS

Fora de `Development` e `Local`, `Jwt:JwksUrl` deve usar HTTPS.

`Jwt:RequireHttpsMetadata=false` e JWKS via HTTP sao aceitos apenas para execucao local. O ambiente `Test` e usado por testes automatizados com `WebApplicationFactory`.

Configuracoes de resiliencia do fetch de JWKS:

- `Jwt:JwksTimeoutSeconds`;
- `Jwt:JwksRetryCount`;
- `Jwt:JwksRetryBaseDelayMilliseconds`.

## Como obter token localmente

Suba o `Auth.Api` via compose ou `dotnet run` e solicite um token:

```bash
curl -s -X POST http://localhost:5030/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "poc-usuario",
    "password": "Poc#123",
    "scope": "ledger.write balance.read"
  }'
```

Em `Development`, o repositorio traz valores de exemplo em `src/Auth.Api/appsettings.Development.json`. Em outros ambientes, use variaveis de ambiente ou secret store.

Use o `access_token` retornado:

```bash
curl -i http://localhost:5226/api/v1/lancamentos \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Idempotency-Key: 00000000-0000-0000-0000-000000000001" \
  -H "Content-Type: application/json" \
  -d '{"type":"CREDIT","merchantId":"tese","amount":10.00}'
```

O contrato do `Auth.Api` retorna `access_token`. Alguns scripts aceitam `accessToken` apenas como fallback de compatibilidade.

## Cuidados

- Nao relaxe issuer, audience, scopes ou validacao de merchant sem decisao explicita.
- Nao use credenciais de POC em ambientes compartilhados ou produtivos.
- Se a autenticacao evoluir para usuarios reais, refresh tokens, revogacao ou federacao, reavalie a arquitetura conforme ADR existente sobre Keycloak/OIDC.
