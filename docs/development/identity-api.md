# IdentityService API

Este documento descreve o contrato HTTP atual do cadastro de usuarios no
`IdentityService`. O contrato versionado fica em
[`docs/openapi/identity.v1.json`](../openapi/identity.v1.json).

## POST /api/v1/users

Cadastra um usuario no Keycloak, persiste o vinculo local no schema PostgreSQL
`identity`, gera `merchantId` automaticamente e dispara o e-mail de boas-vindas
apos o commit local.

O endpoint exige token Bearer com scope `identity.write`.

Headers:

| Header | Obrigatorio | Uso |
| --- | --- | --- |
| `Authorization` | sim | Token JWT Bearer emitido pelo Keycloak local ou emissor configurado. |
| `Content-Type` | sim | `application/json`. |
| `Idempotency-Key` | nao | Chave opaca para retry seguro do cadastro. |

`Idempotency-Key` aceita de 1 a 128 caracteres e apenas letras, numeros, ponto,
underscore, dois-pontos e hifen: `^[A-Za-z0-9._:-]{1,128}$`. Valores invalidos
retornam `400 Bad Request`.

Payload:

```json
{
  "username": "local.identity.user",
  "name": "Local Identity User",
  "email": "local.identity.user@example.com",
  "password": "SenhaLocal123!",
  "document": "12345678900"
}
```

Resposta de sucesso:

```http
HTTP/1.1 201 Created
Location: /api/v1/users/11111111-1111-1111-1111-111111111111
Content-Type: application/json
```

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "keycloakUserId": "kc-123",
  "merchantId": "mrc_123",
  "username": "local.identity.user",
  "email": "local.identity.user@example.com"
}
```

A senha e enviada somente ao Keycloak. Ela nao e persistida no banco local, nao
entra no hash de idempotencia e nao aparece na resposta.

## Primeira chamada com Idempotency-Key

```bash
curl -i -X POST "http://localhost:5232/api/v1/users" \
  -H "Authorization: Bearer <ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: create-user-local.identity.user-001" \
  -d '{
    "username": "local.identity.user",
    "name": "Local Identity User",
    "email": "local.identity.user@example.com",
    "password": "SenhaLocal123!",
    "document": "12345678900"
  }'
```

Resultado esperado: `201 Created`. A resposta final fica registrada para replay
enquanto a chave estiver dentro do TTL.

## Retry com mesma chave e mesmo payload

Reenviar a mesma chamada com a mesma `Idempotency-Key` e o mesmo payload logico
retorna novamente `201 Created` com a resposta armazenada.

No replay:

- Keycloak nao e chamado novamente;
- novo `MerchantId` nao e gerado;
- usuario local nao e persistido novamente;
- e-mail de boas-vindas nao e reenviado.

Alterar apenas `password` em um retry com a mesma chave nao muda o hash logico e
nao troca a senha no Keycloak. A chamada continua sendo tratada como replay da
primeira execucao.

## Retry com payload diferente

Se a mesma `Idempotency-Key` for reutilizada com payload logico diferente, a API
retorna `409 Conflict`:

```json
{
  "title": "Idempotency key conflict",
  "status": 409,
  "detail": "Idempotency key already used with a different logical payload."
}
```

Esse conflito tambem vale quando a primeira chamada ainda esta em processamento
e a segunda usa payload logico diferente.

## Operacao em andamento

Se uma segunda chamada chegar com a mesma `Idempotency-Key` e mesmo payload
enquanto a primeira ainda esta em `Processing`, a API retorna `409 Conflict`:

```json
{
  "title": "Idempotency key is still processing",
  "status": 409,
  "detail": "Idempotency key is still processing."
}
```

O endpoint nao bloqueia aguardando a primeira chamada terminar. O cliente deve
tentar novamente depois.

## Respostas documentadas

| Status | Quando ocorre |
| --- | --- |
| `201 Created` | Cadastro concluido ou replay de resposta concluida. |
| `400 Bad Request` | Payload invalido ou `Idempotency-Key` invalido. |
| `401 Unauthorized` | Token ausente ou invalido. |
| `403 Forbidden` | Token sem scope `identity.write`. |
| `409 Conflict` | Payload diferente para a mesma chave ou operacao ainda em andamento. |
| `422 Unprocessable Entity` | Validacao de negocio quando aplicavel. |
| `502 Bad Gateway` | Falha traduzida do provider externo quando aplicavel. |

## Limitacoes

- A idempotencia cobre apenas `POST /api/v1/users`.
- O TTL atual da chave e de 24 horas; limpeza automatica de registros expirados
  ainda nao faz parte desta etapa.
- Falha de e-mail apos o commit nao desfaz cadastro e nao e corrigida por retry
  HTTP. Evolucao futura de e-mail duravel esta na
  [ADR-0095](../adrs/0095-evolucao-futura-email-identity-service.md).
- Falha de compensacao no Keycloak pode exigir recuperacao operacional antes de
  liberar novo retry seguro.
- Qualquer falha conhecida depois da criacao confirmada do usuario no Keycloak
  e antes da confirmacao local aciona compensacao com timeout configuravel em
  `IdentityService:CreateUserConsistency:CompensationTimeout`; isso inclui
  cancelamento da requisicao, geracao de `MerchantId`, construcao dos value
  objects, criacao do agregado e persistencia local. Falhas antes do efeito
  externo nao tentam remover usuario no Keycloak. Cancelamento dentro do client
  administrativo apos criacao externa e antes da senha usa
  `IdentityProvider:Keycloak:CompensationTimeout`.
