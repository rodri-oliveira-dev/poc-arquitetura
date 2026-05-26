# Autenticacao e autorizacao

Este documento descreve o fluxo atual de JWT Bearer via JWKS e as regras de autorizacao usadas pelas APIs de negocio.

## Modelo atual

- `Auth.Api` emite tokens JWT assinados com RS256.
- `Auth.Api` publica chaves publicas em `GET /.well-known/jwks.json`.
- `LedgerService.Api` e `BalanceService.Api` validam tokens por JWT Bearer e JWKS.
- As APIs nao fazem introspeccao por request; a configuracao de chaves usa cache e refresh.

## Keycloak local lado a lado

O compose local tambem disponibiliza um Keycloak opcional no profile `identity`, acessivel em `http://localhost:8081/`. Ele existe para experimentacao e preparacao da migracao planejada na ADR-0006, mas ainda nao substitui o `Auth.Api`.

Nesta etapa, `LedgerService.Api` e `BalanceService.Api` continuam configurados para validar tokens emitidos pelo `Auth.Api` e obter JWKS em `http://auth-api:8080/.well-known/jwks.json` dentro da rede Docker. O Keycloak ja possui realm versionado para emitir tokens compativeis com o contrato atual, mas as APIs ainda nao foram alteradas para consumir o JWKS do Keycloak.

Configuracao versionada do realm local:

- arquivo de import: `infra/keycloak/realm-poc.json`;
- realm: `poc`;
- issuer local: `http://localhost:8081/realms/poc`;
- discovery OIDC: `http://localhost:8081/realms/poc/.well-known/openid-configuration`;
- JWKS: `http://localhost:8081/realms/poc/protocol/openid-connect/certs`;
- client de automacao local: `poc-automation`;
- fluxo preferencial para scripts: `client_credentials`.

O client `poc-automation` usa um segredo local descartavel no import versionado: `local_dev_client_secret`. Esse valor existe apenas para desenvolvimento local e nao deve ser reutilizado em ambientes compartilhados ou produtivos.

## Claims e validacoes

Claims relevantes:

- `scope`: string com scopes separados por espaco.
- `merchant_id`: string com um ou mais merchants separados por espaco.

Validacoes esperadas:

- `iss` deve bater com `Jwt:Issuer`;
- `aud` deve conter a audience do servico;
- endpoints protegidos exigem scope compativel;
- endpoints que recebem `merchantId` no body ou query exigem que o valor exista na claim `merchant_id`.
- endpoints que inferem o merchant a partir de um recurso persistido, como status de estorno e reprocessamento, validam o merchant do recurso contra a claim `merchant_id`.

Audiences atuais:

| Servico | Audience |
| --- | --- |
| LedgerService.Api | `ledger-api` |
| BalanceService.Api | `balance-api` |

Nesta POC, o `Auth.Api` pode emitir `aud` como string com audiences separadas por espaco, como `ledger-api balance-api`. As APIs tratam esse formato tokenizando por espaco.

## Scopes emitidos pelo Auth.Api local

O catalogo atual do `Auth.Api` aceita somente estes scopes no `POST /auth/login`:

- `ledger.write`
- `outbox.admin`
- `balance.read`

Os endpoints de consulta de status do `LedgerService.Api` exigem `ledger.read`, mas esse scope ainda nao esta no catalogo emitido pelo `Auth.Api` local. Esse e um desalinhamento operacional conhecido da POC: os testes de integracao cobrem esses endpoints com tokens gerados pelas factories de teste; os scripts operacionais locais validam os estados de estorno/reprocessamento pelo banco enquanto usam o token padrao `ledger.write balance.read`.

O realm Keycloak local ja inclui `ledger.read` junto com `ledger.write`, `balance.read` e `outbox.admin`, preparando a migracao sem alterar o `Auth.Api`.

## Scopes por endpoint

| Servico | Endpoint | Scope | Validacao de merchant |
| --- | --- | --- | --- |
| LedgerService.Api | `POST /api/v1/lancamentos` | `ledger.write` | Valida `merchantId` do body contra `merchant_id`. |
| LedgerService.Api | `POST /api/v1/lancamentos/{lancamentoId}/estornos` | `ledger.write` | Valida o merchant do lancamento original contra `merchant_id`. |
| LedgerService.Api | `POST /api/v1/lancamentos/reprocessar` | `ledger.write` | Valida `merchantId` do body contra `merchant_id`. |
| LedgerService.Api | `GET /api/v1/lancamentos/estornos/{estornoId}` | `ledger.read` | Valida o merchant do estorno persistido contra `merchant_id`. |
| LedgerService.Api | `GET /api/v1/lancamentos/reprocessamentos/{reprocessamentoId}` | `ledger.read` | Valida o merchant do reprocessamento persistido contra `merchant_id`. |
| LedgerService.Api | `GET /api/v1/outbox/dead-letters` | `outbox.admin` | Nao se aplica. Endpoint administrativo da Outbox. |
| LedgerService.Api | `POST /api/v1/outbox/dead-letters/{id}/requeue` | `outbox.admin` | Nao se aplica. Endpoint administrativo da Outbox. |
| BalanceService.Api | `GET /v1/consolidados/diario/{date}` | `balance.read` | Valida `merchantId` da query string contra `merchant_id`. |
| BalanceService.Api | `GET /v1/consolidados/periodo` | `balance.read` | Valida `merchantId` da query string contra `merchant_id`. |

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
    "username": "local_user",
    "password": "local_password",
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

## Como obter token Keycloak localmente

Suba o Keycloak com o profile `identity`:

```bash
docker compose --profile identity up -d keycloak
```

Solicite um token por `client_credentials`:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=poc-automation" \
  -d "client_secret=local_dev_client_secret"
```

O access token emitido pelo realm local deve conter:

- `iss`: `http://localhost:8081/realms/poc`;
- `aud`: `ledger-api` e `balance-api`;
- `scope`: `ledger.write ledger.read balance.read outbox.admin`, alem de scopes tecnicos que o Keycloak possa incluir;
- `merchant_id`: `tese m1`.

Enquanto `LedgerService.Api` e `BalanceService.Api` continuarem apontando para `Auth.Api`, use tokens Keycloak apenas para validacao manual do realm, discovery, JWKS e contrato de claims.

## Cuidados

- Nao relaxe issuer, audience, scopes ou validacao de merchant sem decisao explicita.
- Nao use credenciais de POC em ambientes compartilhados ou produtivos.
- Se a autenticacao evoluir para usuarios reais, refresh tokens, revogacao ou federacao, reavalie a arquitetura conforme ADR existente sobre Keycloak/OIDC.
