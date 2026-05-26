# Autenticacao e autorizacao

Este documento descreve o fluxo atual de JWT Bearer via JWKS e as regras de autorizacao usadas pelas APIs de negocio.

## Modelo atual

- O Keycloak local emite tokens JWT assinados com RS256 no realm `poc`.
- O Keycloak publica chaves publicas em `GET /realms/poc/protocol/openid-connect/certs`.
- `LedgerService.Api` e `BalanceService.Api` validam tokens por JWT Bearer e JWKS.
- As APIs nao fazem introspeccao por request; a configuracao de chaves usa cache e refresh.
- `Auth.Api` continua disponivel como emissor legado de POC e pode ser reativado por configuracao.

## Keycloak local

O compose local disponibiliza um Keycloak acessivel em `http://localhost:8081/`. Ele e o emissor padrao para os scripts locais de token e para a configuracao JWT das APIs de negocio.

`LedgerService.Api` e `BalanceService.Api` continuam usando `Jwt:JwksUrl` direto, sem discovery OIDC nesta etapa. Para Keycloak, esse valor deve apontar para o endpoint de certificados do realm. Dentro da rede Docker, as APIs usam o JWKS interno `http://keycloak:8080/realms/poc/protocol/openid-connect/certs`, enquanto o issuer validado permanece o issuer publico dos tokens locais: `http://localhost:8081/realms/poc`.

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

As APIs de negocio continuam autorizando exclusivamente pelo contrato acima. Roles nativas do Keycloak, grupos ou `resource_access` nao sao usados como substitutos para `scope` ou `merchant_id`; o realm local deve mapear os valores necessarios para essas claims explicitas, sem wildcard de merchant.

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

## Fallback Auth.Api local

O `Auth.Api` permanece no compose para transicao e testes de compatibilidade. Para validar tokens emitidos por ele, sobrescreva a configuracao das APIs:

| Variavel | Valor Auth.Api local |
| --- | --- |
| `JWT_ISSUER` | `https://auth-api` |
| `JWT_JWKS_URL` | `http://auth-api:8080/.well-known/jwks.json` |
| `JWT_REQUIRE_HTTPS_METADATA` | `false` |
| `TOKEN_PROVIDER` | `auth-api` |

O catalogo atual do `Auth.Api` aceita somente estes scopes no `POST /auth/login`:

- `ledger.write`
- `outbox.admin`
- `balance.read`

Os endpoints de consulta de status do `LedgerService.Api` exigem `ledger.read`, mas esse scope ainda nao esta no catalogo emitido pelo `Auth.Api` local. Esse e um desalinhamento operacional conhecido da POC: os testes de integracao cobrem esses endpoints com tokens gerados pelas factories de teste; os scripts operacionais locais validam os estados de estorno/reprocessamento pelo banco enquanto usam o token padrao `ledger.write balance.read`.

O realm Keycloak local inclui `ledger.read` junto com `ledger.write`, `balance.read` e `outbox.admin`.
O client `poc-automation` declara esses scopes como default client scopes, adiciona as audiences `ledger-api` e `balance-api` no access token e emite `merchant_id=tese m1` pelo mapper `poc-merchants`.

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

Fora de `Development`, `Local` e `Test`, `Jwt:JwksUrl` deve usar HTTPS.

`Jwt:RequireHttpsMetadata=false` e JWKS via HTTP sao aceitos apenas para execucao local. O ambiente `Test` e usado por testes automatizados com `WebApplicationFactory`.

Configuracoes de resiliencia do fetch de JWKS:

- `Jwt:JwksTimeoutSeconds`;
- `Jwt:JwksRetryCount`;
- `Jwt:JwksRetryBaseDelayMilliseconds`.

## Scripts de token locais

Os scripts `scripts/get-token.ps1` e `scripts/get-token.sh` imprimem somente o token em `stdout`. Mensagens de erro vao para `stderr` e nao exibem segredo de client nem senha.

Precedencia:

- `TOKEN` informado por variavel de ambiente e retornado diretamente;
- `TOKEN_PROVIDER=keycloak`, valor padrao, usa Keycloak local por `client_credentials`;
- `TOKEN_PROVIDER=auth-api` usa o fallback legado do `Auth.Api` em `POST /auth/login`.

Variaveis Keycloak:

| Variavel | Default local |
| --- | --- |
| `KEYCLOAK_BASE_URL` | `http://localhost:<KEYCLOAK_HOST_PORT>` |
| `KEYCLOAK_HOST_PORT` | `8081` |
| `KEYCLOAK_REALM` | `poc` |
| `KEYCLOAK_TOKEN_URL` | `/realms/<realm>/protocol/openid-connect/token` |
| `KEYCLOAK_CLIENT_ID` | `poc-automation` |
| `KEYCLOAK_CLIENT_SECRET` | `local_dev_client_secret` |
| `KEYCLOAK_SCOPE` | vazio, usando os default client scopes do realm |

Variaveis legadas preservadas para `TOKEN_PROVIDER=auth-api`:

| Variavel | Default local |
| --- | --- |
| `AUTH_BASE_URL` | `http://localhost:5030` |
| `TOKEN_URL` | `/auth/login` |
| `AUTH_POC_USERNAME` ou `USERNAME` | `local_user` |
| `AUTH_POC_PASSWORD` ou `PASSWORD` | `local_password` |
| `AUTH_POC_SCOPE` ou `SCOPE` | `ledger.write balance.read` |

O contrato de resposta continua aceitando `access_token` como campo principal e `accessToken` como fallback temporario.

Scripts operacionais que precisam de token devem chamar `scripts/get-token.*` em vez de duplicar login. Esse e o caso dos validadores locais (`validate-*.ps1`), dos runners k6 (`run-loadtests.*`) e do modo autenticado do OWASP ZAP (`run-owasp-zap.* -UseAuthentication` ou `--use-authentication`). Assim, Keycloak e o fallback temporario para `Auth.Api` seguem uma unica configuracao.

### Keycloak local

Suba o Keycloak:

```bash
docker compose up -d keycloak
```

Obtenha o token:

```bash
./scripts/get-token.sh
```

No Windows:

```powershell
./scripts/get-token.ps1
```

O fluxo usa `client_credentials` contra o token endpoint do realm:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=poc-automation" \
  -d "client_secret=local_dev_client_secret"
```

O access token emitido pelo realm local deve conter `iss=http://localhost:8081/realms/poc`, audiences `ledger-api` e `balance-api`, scopes `ledger.write ledger.read balance.read outbox.admin` e `merchant_id=tese m1`.

Para APIs rodando em container, mantenha `Jwt:JwksUrl` apontando para `http://keycloak:8080/realms/poc/protocol/openid-connect/certs`. Para APIs rodando no host, use `http://localhost:8081/realms/poc/protocol/openid-connect/certs`.

### Fallback Auth.Api

Suba o `Auth.Api` via compose ou `dotnet run` e solicite um token pelo provider legado:

```bash
TOKEN_PROVIDER=auth-api ./scripts/get-token.sh
```

No Windows:

```powershell
$env:TOKEN_PROVIDER = "auth-api"
./scripts/get-token.ps1
```

Chamada equivalente:

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

O contrato do `Auth.Api` retorna `access_token`. Os scripts aceitam `accessToken` apenas como fallback de compatibilidade.

## Cuidados

- Nao relaxe issuer, audience, scopes ou validacao de merchant sem decisao explicita.
- Nao use credenciais de POC em ambientes compartilhados ou produtivos.
- Se a autenticacao evoluir para usuarios reais, refresh tokens, revogacao ou federacao, reavalie a arquitetura conforme ADR existente sobre Keycloak/OIDC.
