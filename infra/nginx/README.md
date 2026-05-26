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
mkcert -cert-file infra/nginx/certs/localhost.crt -key-file infra/nginx/certs/localhost.key localhost ledger.localhost balance.localhost
```

Opcao com OpenSSL, sem instalar CA confiavel no sistema:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
  -keyout infra/nginx/certs/localhost.key \
  -out infra/nginx/certs/localhost.crt \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost"
```

Com OpenSSL, o navegador deve alertar sobre certificado nao confiavel ate que a CA/certificado seja confiado localmente.

## Execucao

Depois de gerar o certificado, suba a stack com o overlay:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
```

Para subir a stack completa da POC com observabilidade, migrations pelo host e este overlay Nginx, use:

```powershell
./scripts/start-full-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-full-stack.sh
```

Para parar esse fluxo completo sem remover volumes ou certificados:

```powershell
./scripts/stop-full-stack.ps1
```

No Linux/macOS:

```bash
./scripts/stop-full-stack.sh
```

Se containers antigos do overlay ou a rede local do projeto ficarem presos, `start-full-stack.*` detecta o estado antes da subida e pergunta se pode executar uma limpeza nao destrutiva sem `-v`. Use `-Cleanup` no PowerShell ou `--cleanup` no shell para autorizar essa limpeza sem prompt.

O container `nginx-edge` usa uma imagem local definida em `infra/nginx/Dockerfile`, baseada em Alpine, com Nginx e o modulo `headers-more`. O modulo permite remover o header `Server` emitido pelo proprio Nginx, algo que `server_tokens off` sozinho nao faz.

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

## Headers de seguranca

O Nginx adiciona `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` restringindo camera, microphone e geolocation, e `X-XSS-Protection: 0` em respostas da borda local. Para evitar duplicidade, o proxy remove esses mesmos headers quando vierem das APIs internas e aplica a politica unica da borda. O `X-XSS-Protection: 0` desativa um filtro legado de navegadores antigos para evitar comportamento inconsistente.

Somente o portal em `https://localhost:7443` recebe `Content-Security-Policy` na borda. Os hosts dos Swaggers nao recebem CSP adicional no Nginx para evitar bloquear scripts e estilos da Swagger UI.

Os hosts de API via Nginx (`ledger.localhost` e `balance.localhost`) recebem `Cache-Control: no-store` em todas as respostas proxied para evitar armazenamento indevido de payloads sensiveis de autorizacao, ledger e saldo por navegadores, clientes ou proxies intermediarios. A borda tambem envia `Pragma: no-cache` e `Expires: 0` por compatibilidade com clientes legados e remove headers de cache vindos das APIs internas antes de aplicar a politica unica do proxy. O portal local estatico em `https://localhost:7443` nao recebe essa politica para manter a regra focada nas APIs; os assets do Swagger via hosts de API tambem recebem `no-store`, o que preserva funcionamento da UI e prioriza seguranca no ambiente local.

O Nginx usa `server_tokens off` e o modulo `headers-more` para remover o header `Server` das respostas da borda local. Tambem remove headers de tecnologia vindos das APIs internas quando presentes, como `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version` e `X-Swagger-UI-Version`. Esse hardening reduz fingerprinting, sem substituir controles de seguranca da aplicacao.

## Limites defensivos locais

A borda local aplica limites defensivos basicos antes de encaminhar chamadas para as APIs:

- `client_max_body_size 1m`, alinhado ao limite padrao `ApiLimits:MaxRequestBodySizeBytes` das APIs Ledger e Balance;
- `client_body_timeout 10s` e `client_header_timeout 10s` para reduzir conexoes lentas na leitura de body ou headers;
- `keepalive_timeout 30s`, `send_timeout 30s`, `proxy_connect_timeout 5s`, `proxy_send_timeout 30s` e `proxy_read_timeout 30s`;
- `large_client_header_buffers 4 8k`, para aceitar headers normais de autenticacao/correlacao sem permitir headers excessivos;
- `limit_conn` por IP em 20 conexoes simultaneas;
- `limit_req` por IP em 10 requisicoes por segundo, com `burst=40`, retornando `429` quando excedido.

Payload acima de 1 MiB e rejeitado pelo Nginx com `413 Payload Too Large`, antes de chegar ao ASP.NET. Excesso de requisicoes ou conexoes pela borda local retorna `429 Too Many Requests`. Esses controles sao demonstrativos e defensivos para desenvolvimento local; protecao real de producao exige dimensionamento, observabilidade, regras de ambiente e controles adicionais fora desta POC.

Portal local:

- `https://localhost:7443`

Swaggers via Nginx:

- `https://ledger.localhost:7443/swagger`
- `https://balance.localhost:7443/swagger`

O Nginx preserva `X-Correlation-Id` quando o cliente envia o header e gera um valor UUID-like a partir de `$request_id` quando o header esta ausente. O valor efetivo e encaminhado para as APIs, devolvido no response e registrado no access log.

Os access logs usam uma linha JSON por request em `/var/log/nginx/access.log`, com campos como `time`, `remote_addr`, `host`, `method`, `uri`, `status`, `request_time`, `upstream_response_time`, `correlation_id` e `user_agent`.

Para validar o balanceamento local do Ledger, faça algumas chamadas e observe `X-Upstream-Addr` ou o campo `upstream_addr` no access log:

```bash
for i in $(seq 1 20); do curl -k -s -o /dev/null -D - https://ledger.localhost:7443/health | grep -i X-Upstream-Addr; done
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge | grep upstream_addr
```

Validacao rapida dos limites defensivos:

```bash
dd if=/dev/zero of=/tmp/payload-maior-que-1m.bin bs=1024 count=1100
curl -k -i -X POST https://ledger.localhost:7443/health --data-binary @/tmp/payload-maior-que-1m.bin
for i in $(seq 1 80); do curl -k -s -o /dev/null -w "%{http_code}\n" https://ledger.localhost:7443/health & done; wait
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

O primeiro comando deve retornar `413` quando o arquivo ultrapassar 1 MiB. O segundo pode retornar `429` em parte das chamadas quando o burst local for excedido. Os logs JSON do Nginx registram `status`, `request_time`, `upstream_status` e `correlation_id`, ajudando a diferenciar bloqueios na borda de respostas das APIs.

O Nginx open source nesta POC usa upstreams estaticos. Isso demonstra balanceamento local, mas nao representa autoscaling real, service discovery dinamico avancado ou circuit breaker de producao.

O Nginx normaliza `/swagger` para a Swagger UI de cada API. Nas portas HTTP diretas atuais, a UI fica em `/index.html` e os documentos OpenAPI ficam em `/swagger/v1/swagger.json`.

As URLs diretas das APIs continuam funcionando nas portas do `compose.yaml` principal.

Validacao rapida da politica de cache:

```bash
curl -k -I https://localhost:7443
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
curl -k -I https://ledger.localhost:7443/health
```

As respostas dos hosts de API devem conter `Cache-Control: no-store`, `Pragma: no-cache` e `Expires: 0`. As respostas via Nginx nao devem conter `Server`, `X-Powered-By` nem `X-Swagger-UI-Version`.
