# Autenticacao e autorizacao

Este documento descreve o fluxo atual de JWT Bearer via JWKS e as regras de autorizacao usadas pelas APIs de negocio.

## Modelo atual

- O Keycloak local emite tokens JWT assinados com RS256 no realm `poc`.
- O Keycloak publica chaves publicas em `GET /realms/poc/protocol/openid-connect/certs`.
- `LedgerService.Api` e `BalanceService.Api` validam tokens por JWT Bearer e JWKS.
- As APIs nao fazem introspeccao por request; a configuracao de chaves usa cache e refresh.
- `Auth.Api` esta depreciado como emissor legado de POC, fora da stack principal, e so deve ser iniciado pelo overlay legado quando houver necessidade explicita de compatibilidade.

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
- clients de debug manual local: `poc-local-ledger-debug`, `poc-local-balance-debug` e `poc-local-admin-debug`;
- fluxo preferencial para scripts: `client_credentials`.

O client `poc-automation` usa um segredo local descartavel fornecido por `KEYCLOAK_CLIENT_SECRET` no ambiente do container. O import do realm usa placeholder resolvido pelo Keycloak no startup para manter o valor real fora do repositorio.

Os clients `poc-local-*-debug` sao publicos, habilitam Direct Grant apenas para facilitar debug manual local e nao possuem segredo. Eles nao substituem o fluxo `client_credentials` dos scripts automatizados.

### Usuarios locais de debug

O realm importado inclui tres usuarios descartaveis para quem precisa depurar autenticacao e autorizacao manualmente, por exemplo no Swagger, REST Client ou `curl`. Eles existem somente no realm local versionado, mas as senhas vêm de variaveis locais para evitar segredo hard-coded no import.

Use esses usuarios apenas em maquina de desenvolvimento local:

| Usuario | Senha | Client local | Finalidade | Scopes emitidos | `merchant_id` |
| --- | --- | --- | --- | --- | --- |
| `local_ledger_user` | `KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD` | `poc-local-ledger-debug` | Testar endpoints do LedgerService | `ledger.write ledger.read` | `tese m1` |
| `local_balance_user` | `KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD` | `poc-local-balance-debug` | Testar endpoints do BalanceService | `balance.read` | `tese m1` |
| `local_admin_user` | `KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD` | `poc-local-admin-debug` | Facilitar debug local completo | `ledger.write ledger.read balance.read outbox.admin` | `tese m1` |

Todos ficam habilitados no import e possuem senha nao temporaria para nao exigir troca no primeiro login. O realm marca os usuarios com atributos de debug local. As permissoes sao emitidas pela mesma estrategia ja adotada no realm: `clientScopes` default dos clients locais de debug incluem os scopes correspondentes, o client scope `poc-api-audience` adiciona `aud=ledger-api balance-api` e o client scope `poc-merchants` adiciona `merchant_id=tese m1`.

Exemplo para obter token manual do usuario Ledger:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=poc-local-ledger-debug" \
  -d "username=local_ledger_user" \
  -d "password=<KEYCLOAK_LOCAL_LEDGER_USER_PASSWORD>"
```

Exemplo de uso do token no LedgerService:

```bash
TOKEN="<access_token_do_comando_anterior>"
curl -i http://localhost:5226/api/v1/lancamentos \
  -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: 00000000-0000-0000-0000-000000000001" \
  -H "Content-Type: application/json" \
  -d '{"type":"CREDIT","merchantId":"m1","amount":10.00}'
```

Exemplo para obter token manual do usuario Balance:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=poc-local-balance-debug" \
  -d "username=local_balance_user" \
  -d "password=<KEYCLOAK_LOCAL_BALANCE_USER_PASSWORD>"
```

Exemplo de uso do token no BalanceService:

```bash
TOKEN="<access_token_do_comando_anterior>"
curl -i "http://localhost:5228/api/v1/consolidados/diario/2026-05-26?merchantId=m1" \
  -H "Authorization: Bearer $TOKEN"
```

O usuario `local_admin_user` pode ser usado do mesmo modo para endpoints de Ledger, Balance e Outbox local:

```bash
curl -s -X POST http://localhost:8081/realms/poc/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=poc-local-admin-debug" \
  -d "username=local_admin_user" \
  -d "password=<KEYCLOAK_LOCAL_ADMIN_USER_PASSWORD>"
```

Nao use esses usuarios em ambiente compartilhado, homologacao ou producao. Para automacoes locais, load tests, validadores e scanners, continue usando `scripts/get-token.*`, que permanecem no fluxo `client_credentials` com o client `poc-automation`.

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

## Auth.Api legado

O `Auth.Api` permanece no repositorio para rastreabilidade, testes de compatibilidade e rollback local controlado, mas nao faz parte da stack principal nem da borda Nginx padrao. Para iniciar o emissor legado:

```bash
docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth up -d --build auth-api
```

Para validar tokens emitidos por ele, sobrescreva a configuracao das APIs e use o provider legado somente nesse contexto:

| Variavel | Valor Auth.Api local |
| --- | --- |
| `JWT_ISSUER` | `https://auth-api` |
| `JWT_JWKS_URL` | `http://auth-api:8080/.well-known/jwks.json` |
| `JWT_REQUIRE_HTTPS_METADATA` | `false` |
| `TOKEN_PROVIDER` | `auth-api` |

O catalogo do `Auth.Api` legado aceita somente estes scopes no `POST /auth/login`:

- `ledger.write`
- `outbox.admin`
- `balance.read`

Os endpoints de consulta de status do `LedgerService.Api` exigem `ledger.read`, mas esse scope nao esta no catalogo emitido pelo `Auth.Api` legado. Esse desalinhamento e aceito apenas enquanto o projeto legado existir; o fluxo operacional local deve usar Keycloak, cujo realm inclui `ledger.read`.

O realm Keycloak local inclui `ledger.read` junto com `ledger.write`, `balance.read`, `transfer.write`, `transfer.read` e `outbox.admin`.
O client `poc-automation` declara esses scopes como default client scopes, adiciona as audiences `ledger-api`, `balance-api` e `transfer-api` no access token e emite `merchant_id=tese m1 m2` pelo mapper `poc-merchants`.
Os clients `poc-local-*-debug` adicionam as mesmas audiences e emitem `scope` pelos seus `clientScopes` default e `merchant_id` pelo mapper `poc-merchants`, sem usar roles nativas do Keycloak como contrato das APIs.

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
| BalanceService.Api | `GET /api/v1/consolidados/diario/{date}` | `balance.read` | Valida `merchantId` da query string contra `merchant_id`. |
| BalanceService.Api | `GET /api/v1/consolidados/periodo` | `balance.read` | Valida `merchantId` da query string contra `merchant_id`. |
| TransferService.Api | `POST /api/v1/transferencias` | `transfer.write` | Valida `sourceMerchantId` do body contra `merchant_id`. |
| TransferService.Api | `GET /api/v1/transferencias/{transferenciaId}` | `transfer.read` | Valida o merchant da transferencia persistida contra `merchant_id`. |

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
- `TOKEN_PROVIDER=auth-api` usa o `Auth.Api` legado em `POST /auth/login` e exige que o overlay `compose.auth-legacy.yaml` esteja em execucao.

Variaveis Keycloak:

| Variavel | Default local |
| --- | --- |
| `KEYCLOAK_BASE_URL` | `http://localhost:<KEYCLOAK_HOST_PORT>` |
| `KEYCLOAK_HOST_PORT` | `8081` |
| `KEYCLOAK_REALM` | `poc` |
| `KEYCLOAK_TOKEN_URL` | `/realms/<realm>/protocol/openid-connect/token` |
| `KEYCLOAK_CLIENT_ID` | `poc-automation` |
| `KEYCLOAK_CLIENT_SECRET` | `<KEYCLOAK_CLIENT_SECRET>` |
| `KEYCLOAK_SCOPE` | vazio, usando os default client scopes do realm |

Variaveis legadas preservadas para `TOKEN_PROVIDER=auth-api`:

| Variavel | Default local |
| --- | --- |
| `AUTH_BASE_URL` | `http://localhost:5030` |
| `TOKEN_URL` | `/auth/login` |
| `AUTH_POC_USERNAME` ou `USERNAME` | `local_user` |
| `AUTH_POC_PASSWORD` ou `PASSWORD` | `<AUTH_POC_PASSWORD>` |
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
  -d "client_secret=<KEYCLOAK_CLIENT_SECRET>"
```

O access token emitido pelo realm local deve conter `iss=http://localhost:8081/realms/poc`, audiences `ledger-api`, `balance-api` e `transfer-api`, scopes `ledger.write ledger.read balance.read transfer.write transfer.read outbox.admin` e `merchant_id=tese m1 m2`.

Para debug manual com usuario/senha, use o client publico de debug correspondente diretamente no token endpoint com `grant_type=password`. Os scripts versionados nao usam esse fluxo por padrao para evitar transformar Direct Grant em automacao oficial.

Para APIs rodando em container, mantenha `Jwt:JwksUrl` apontando para `http://keycloak:8080/realms/poc/protocol/openid-connect/certs`. Para APIs rodando no host, use `http://localhost:8081/realms/poc/protocol/openid-connect/certs`.

### Auth.Api legado

Suba o `Auth.Api` pelo overlay legado ou via `dotnet run` e solicite um token pelo provider legado:

```bash
docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth up -d --build auth-api
TOKEN_PROVIDER=auth-api ./scripts/get-token.sh
```

No Windows:

```powershell
docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth up -d --build auth-api
$env:TOKEN_PROVIDER = "auth-api"
./scripts/get-token.ps1
```

Chamada equivalente:

```bash
curl -s -X POST http://localhost:5030/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "local_user",
    "password": "<AUTH_POC_PASSWORD>",
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
