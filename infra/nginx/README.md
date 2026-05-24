# Nginx local opcional

Esta pasta contem a configuracao do Nginx usado como camada de borda opcional para desenvolvimento local. Ele nao substitui o `compose.yaml` principal e nao altera as APIs, que continuam ouvindo HTTP em `8080` dentro da rede Docker.

## Certificado local

O Nginx espera encontrar os arquivos abaixo, montados como volume:

- `infra/nginx/certs/localhost.crt`
- `infra/nginx/certs/localhost.key`

Nao versione certificados, chaves privadas, senhas ou material sensivel. A pasta `certs` ignora todo conteudo gerado localmente.

Opcao recomendada com `mkcert`:

```bash
mkcert -install
mkcert -cert-file infra/nginx/certs/localhost.crt -key-file infra/nginx/certs/localhost.key localhost ledger.localhost balance.localhost auth.localhost
```

Opcao com OpenSSL, sem instalar CA confiavel no sistema:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
  -keyout infra/nginx/certs/localhost.key \
  -out infra/nginx/certs/localhost.crt \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost,DNS:auth.localhost"
```

Com OpenSSL, o navegador deve alertar sobre certificado nao confiavel ate que a CA/certificado seja confiado localmente.

## Execucao

Depois de gerar o certificado, suba a stack com o overlay:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
```

Portal local:

- `https://localhost:7443`

Swaggers via Nginx:

- `https://ledger.localhost:7443/swagger`
- `https://balance.localhost:7443/swagger`
- `https://auth.localhost:7443/swagger`

O Nginx preserva `X-Correlation-Id` quando o cliente envia o header e gera um valor UUID-like a partir de `$request_id` quando o header esta ausente. O valor efetivo e encaminhado para as APIs, devolvido no response e registrado no access log.

Os access logs usam uma linha JSON por request em `/var/log/nginx/access.log`, com campos como `time`, `remote_addr`, `host`, `method`, `uri`, `status`, `request_time`, `upstream_response_time`, `correlation_id` e `user_agent`.

O Nginx normaliza `/swagger` para a Swagger UI de cada API. Nas portas HTTP diretas atuais, a UI fica em `/index.html` e os documentos OpenAPI ficam em `/swagger/v1/swagger.json`.

As URLs diretas das APIs continuam funcionando nas portas do `compose.yaml` principal.
