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

O overlay cria duas instancias explicitas da `LedgerService.Api`, `ledger-service-1` e `ledger-service-2`, sem publicar portas HTTP no host. O Nginx encaminha `ledger.localhost:7443` para o upstream estatico `ledger_api` com `least_conn`:

```nginx
upstream ledger_api {
  least_conn;
  server ledger-service-1:8080;
  server ledger-service-2:8080;
}
```

O `compose.yaml` principal continua independente do Nginx e preserva a porta direta `http://localhost:5226/` quando executado sem este overlay.

## TLS local

A borda local aceita somente `TLSv1.2` e `TLSv1.3`. Protocolos antigos como SSLv2, SSLv3, TLSv1.0 e TLSv1.1 ficam fora da configuracao do Nginx.

O ambiente local nao configura nem repassa o header `Strict-Transport-Security`. HSTS pode ficar em cache no navegador e dificultar rollback em `localhost`, subdominios `.localhost` ou certificados autoassinados. Essa politica deve ser tratada apenas em ambiente apropriado fora deste fluxo local.

Portal local:

- `https://localhost:7443`

Swaggers via Nginx:

- `https://ledger.localhost:7443/swagger`
- `https://balance.localhost:7443/swagger`
- `https://auth.localhost:7443/swagger`

O Nginx preserva `X-Correlation-Id` quando o cliente envia o header e gera um valor UUID-like a partir de `$request_id` quando o header esta ausente. O valor efetivo e encaminhado para as APIs, devolvido no response e registrado no access log.

Os access logs usam uma linha JSON por request em `/var/log/nginx/access.log`, com campos como `time`, `remote_addr`, `host`, `method`, `uri`, `status`, `request_time`, `upstream_response_time`, `correlation_id` e `user_agent`.

Para validar o balanceamento local do Ledger, faça algumas chamadas e observe `X-Upstream-Addr` ou o campo `upstream_addr` no access log:

```bash
for i in $(seq 1 20); do curl -k -s -o /dev/null -D - https://ledger.localhost:7443/health | grep -i X-Upstream-Addr; done
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge | grep upstream_addr
```

O Nginx open source nesta POC usa upstreams estaticos. Isso demonstra balanceamento local, mas nao representa autoscaling real, service discovery dinamico avancado ou circuit breaker de producao.

O Nginx normaliza `/swagger` para a Swagger UI de cada API. Nas portas HTTP diretas atuais, a UI fica em `/index.html` e os documentos OpenAPI ficam em `/swagger/v1/swagger.json`.

As URLs diretas das APIs continuam funcionando nas portas do `compose.yaml` principal.
