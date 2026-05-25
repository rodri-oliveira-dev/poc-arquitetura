# Desenvolvimento local

Este guia concentra os passos para executar, validar e depurar a POC localmente.

## Pre-requisitos

Para a stack local:

- Docker-compatible API acessivel;
- CLI `docker` com suporte a `docker compose`;
- build de imagens habilitado no runtime local.

O projeto nao exige Docker Desktop como premissa. No Windows sem Docker Desktop, o ambiente recomendado e Rancher Desktop usando `moby/dockerd`, pois ele expoe uma API compativel com Docker. `containerd` puro com `nerdctl` nao deve ser tratado como ambiente suportado para os testes baseados em Testcontainers.

Para rodar no host:

- .NET SDK definido em `global.json`;
- PostgreSQL e Kafka acessiveis localmente;
- ferramentas locais restauradas com `dotnet tool restore`.

Ferramentas opcionais:

- `curl`, para exemplos HTTP;
- VS Code, para workspace, tasks e REST Client;
- Node.js 20+, para gerar a documentacao LikeC4 localmente.

## Escopo local e credenciais

Esta stack e local, descartavel e nao deve ser promovida para ambientes compartilhados, homologacao ou producao sem revisao de seguranca, secrets, transporte, imagens e observabilidade.

O compose usa defaults ficticios para desenvolvimento local. Para sobrescrever, copie `.env.example` para `.env` e ajuste os valores localmente. O arquivo `.env` e ignorado pelo Git e nao deve ser versionado. Os defaults atuais sao intencionalmente obvios e descartaveis:

- `POSTGRES_PASSWORD=local_dev_password`
- `BALANCE_DB_HOST=balance-db`
- `BALANCE_DB_PORT=5432`
- `BALANCE_DB_HOST_PORT=15433`
- `BALANCE_DB_NAME=dbBalance`
- `BALANCE_DB_USER=userBalance`
- `BALANCE_DB_PASSWORD=local_dev_password`
- `GRAFANA_ADMIN_PASSWORD=local_dev_password`
- `AUTH_POC_USERNAME=local_user`
- `AUTH_POC_PASSWORD=local_password`
- `AUTH_POC_SCOPE=ledger.write balance.read`

As variaveis `BALANCE_DB_*` sao a origem local para o PostgreSQL do Balance no compose, para a connection string de `BalanceService.Api` e `BalanceService.Worker` dentro da rede Docker, e para os scripts que aplicam migrations ou executam load tests. Em volumes PostgreSQL existentes, alterar `.env` ou `compose.yaml` nao altera automaticamente a senha ja gravada no banco. Se houver divergencia, veja [troubleshooting](../troubleshooting.md#password-authentication-failed-for-user-userbalance).

Nao reutilize esses valores fora da maquina local. Em ambientes compartilhados ou produtivos, use um mecanismo proprio de secret/config store e credenciais rotacionaveis.

## Stack local com compose

O `compose.yaml` sobe por padrao a stack minima de desenvolvimento:

- `Auth.Api`;
- `LedgerService.Api`;
- `LedgerService.Worker`;
- `BalanceService.Api`;
- `BalanceService.Worker`;
- PostgreSQL Ledger;
- PostgreSQL Balance;
- Kafka single node em KRaft;
- job de inicializacao dos topicos Kafka.

Componentes opcionais ficam em profiles:

- profile `observability`: OpenTelemetry Collector, Jaeger, Prometheus, Loki, Grafana Alloy, Alertmanager e Grafana;
- profile `k6`: container k6 definido em `compose.k6.yaml`.

Tambem existe um overlay opcional `compose.nginx.yaml` para adicionar uma borda local com Nginx e HTTPS em desenvolvimento. Ele nao faz parte da stack minima e nao altera as APIs, que continuam rodando internamente em HTTP com `ASPNETCORE_URLS=http://+:8080`. Quando o overlay e usado, o Nginx cria um upstream local `ledger_api` com duas instancias da `LedgerService.Api` e algoritmo `least_conn`.

A observabilidade completa inclui:

- OpenTelemetry Collector como entrada local de telemetria OTLP;
- Jaeger all-in-one como backend local de visualizacao de traces;
- Prometheus para coletar metricas tecnicas expostas pelo Collector;
- Loki para armazenar logs centralizados dos containers;
- Grafana Alloy para coletar logs dos containers via Docker API, isolado no profile `observability`;
- Alertmanager local para visualizar alertas tecnicos basicos sem envio externo;
- Grafana com datasources Prometheus, Loki e Jaeger e dashboards minimos provisionados.

Subir a stack minima com migrations:

```powershell
./scripts/start-local-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-local-stack.sh
```

Esse fluxo sobe bancos, Kafka e `Auth.Api`, aplica migrations pelo host e depois inicia `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`.

Os scripts `start-local-stack.*` usam Docker Compose, restauram tools .NET, aplicam migrations pelo host e nao removem volumes. Eles nao executam testes automatizados, k6 nem scanners.

Para subir tambem observabilidade e habilitar exportacao OTLP nas aplicacoes:

```powershell
./scripts/start-local-stack.ps1 -Observability
```

No Linux/macOS:

```bash
OBSERVABILITY=true ./scripts/start-local-stack.sh
```

Para subir somente o compose, sem aplicar migrations:

```bash
docker compose up -d --build
```

Esse comando inicia apenas a stack minima. Para habilitar observabilidade completa pelo compose, incluindo coleta local de logs via Docker API, use:

```bash
OTEL_ENABLED=true docker compose --profile observability up -d --build
```

`OTEL_ENABLED=true` habilita as aplicacoes a exportarem traces e metricas para `otel-collector:4317`. Sem essa variavel, os backends de observabilidade podem subir, mas as aplicacoes permanecem com OpenTelemetry desabilitado para manter a stack minima leve.

O socket Docker, mesmo montado como somente leitura, e uma superficie sensivel. Use o profile `observability` apenas em maquina local confiavel; nao use em ambiente compartilhado ou produtivo sem redesenhar a coleta de logs e revisar permissoes.

### Stack completa com observabilidade e Nginx

Use `start-full-stack.*` quando quiser um ambiente local completo para demonstracao ou validacao manual integrada:

- stack minima com APIs, workers, bancos, Kafka e init de topicos;
- migrations aplicadas pelo host, reaproveitando o fluxo de `start-local-stack.*`;
- profile `observability` ativo com `OTEL_ENABLED=true`;
- overlay `compose.nginx.yaml` com `nginx-edge`, `ledger-service-1` e `ledger-service-2`;
- validacoes leves de `docker compose ps`, `/health` direto e via Nginx, Grafana, Jaeger, Prometheus e Alertmanager.

Pre-requisitos adicionais:

- certificados locais em `infra/nginx/certs/localhost.crt` e `infra/nginx/certs/localhost.key`;
- portas da stack minima, observabilidade e Nginx livres no host;
- `curl` disponivel no Linux/macOS para as validacoes HTTP.

No Windows:

```powershell
./scripts/start-full-stack.ps1
```

No Linux/macOS:

```bash
./scripts/start-full-stack.sh
```

Para evitar rebuild de imagens:

```powershell
./scripts/start-full-stack.ps1 -NoBuild
```

```bash
./scripts/start-full-stack.sh --no-build
```

Para pular apenas as chamadas HTTP de verificacao pos-subida:

```powershell
./scripts/start-full-stack.ps1 -SkipHealthChecks
```

```bash
./scripts/start-full-stack.sh --skip-health-checks
```

Antes de subir, o script valida portas usadas pela stack completa e verifica se ha containers do overlay Nginx ou rede local do projeto em estado anterior. Quando encontra recursos locais do proprio projeto que podem prender a subida, ele pergunta se pode executar uma limpeza nao destrutiva com `docker compose down --remove-orphans`, sem `-v`. Essa limpeza para/remove containers e redes locais do projeto, mas preserva volumes, bancos locais, imagens e certificados.

Para autorizar essa limpeza previamente em fluxo nao interativo:

```powershell
./scripts/start-full-stack.ps1 -Cleanup
```

```bash
./scripts/start-full-stack.sh --cleanup
```

Se uma porta estiver ocupada por processo externo ou por container que nao pertence ao projeto, o script para e informa a porta/servico afetado. Nesse caso, libere o recurso manualmente antes de tentar novamente.

O script para com mensagem clara se os certificados do Nginx nao existirem e nao tenta gera-los automaticamente. Ele nao remove volumes, nao apaga bancos locais, nao executa testes automatizados, nao executa k6 e nao executa scanners de seguranca.

Para parar a stack completa sem remover containers, redes, volumes, bancos locais, imagens ou certificados:

```powershell
./scripts/stop-full-stack.ps1
```

```bash
./scripts/stop-full-stack.sh
```

Esse fluxo para primeiro `nginx-edge`, `ledger-service-1` e `ledger-service-2` pelo overlay `compose.nginx.yaml`, e depois para a stack base com o profile `observability`.

### Borda local HTTPS com Nginx

O Nginx local e opcional e serve como entrada HTTPS para desenvolvimento e demonstracao de load balance local do Ledger. Use-o quando quiser validar navegacao, Swagger via TLS e distribuicao de chamadas para duas instancias da `LedgerService.Api`, sem mudar contrato HTTP nem substituir a stack minima.

Antes de subir o overlay, gere ou disponibilize um certificado local em:

- `infra/nginx/certs/localhost.crt`
- `infra/nginx/certs/localhost.key`

Esses arquivos nao devem ser versionados. A opcao recomendada para certificado confiavel no host e `mkcert`:

```bash
mkcert -install
mkcert -cert-file infra/nginx/certs/localhost.crt -key-file infra/nginx/certs/localhost.key localhost ledger.localhost balance.localhost auth.localhost
```

Alternativa com OpenSSL:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
  -keyout infra/nginx/certs/localhost.key \
  -out infra/nginx/certs/localhost.crt \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,DNS:ledger.localhost,DNS:balance.localhost,DNS:auth.localhost"
```

Com OpenSSL, o navegador pode exibir alerta de certificado nao confiavel ate que o certificado seja confiado localmente.

Suba primeiro a stack local pelo fluxo normal, principalmente em banco novo para aplicar migrations. Para iniciar a borda com duas instancias do Ledger atras do Nginx, use o overlay:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
```

Para subir tudo diretamente pelo compose sem o script de migrations:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build
```

No overlay, o servico direto `ledger-service` fica no profile `direct-ledger`. Assim, uma execucao limpa do comando acima sobe `ledger-service-1` e `ledger-service-2` para o Nginx, sem publicar porta HTTP direta do Ledger no host. O `compose.yaml` principal continua expondo `http://localhost:5226/` quando usado sem o overlay.

Se a stack minima ja estiver rodando com `ledger-service`, ela pode permanecer ativa para compatibilidade com scripts existentes; o Nginx, porem, distribui trafego somente para `ledger-service-1` e `ledger-service-2`. Para observar exatamente duas instancias do Ledger no ambiente, pare a instancia direta antes de subir o overlay:

```bash
docker compose stop ledger-service
docker compose -f compose.yaml -f compose.nginx.yaml up -d --build nginx-edge
```

Portal HTTPS:

- `https://localhost:7443`

Swaggers via Nginx:

- `https://ledger.localhost:7443/swagger`
- `https://balance.localhost:7443/swagger`
- `https://auth.localhost:7443/swagger`

Os subdominios `.localhost` evitam configurar `PathBase` nas APIs e preservam o Swagger em `/swagger`. As URLs HTTP diretas continuam disponiveis nas portas atuais e sao o alvo dos scripts e testes de carga existentes.

No overlay, o Nginx normaliza `/swagger` para a Swagger UI de cada API. Nas portas HTTP diretas atuais, a UI fica em `/index.html` e os documentos OpenAPI ficam em `/swagger/v1/swagger.json`.

TLS na borda local aceita somente `TLSv1.2` e `TLSv1.3`, desabilitando implicitamente SSLv2, SSLv3, TLSv1.0 e TLSv1.1. O overlay local nao aplica nem repassa HSTS e nao deve emitir `Strict-Transport-Security`; em `localhost`, subdominios `.localhost` e certificados autoassinados, HSTS pode ser cacheado pelo navegador e atrapalhar navegacao, rollback e alternancia entre fluxos HTTP/HTTPS de desenvolvimento. HSTS deve ser decidido apenas para ambientes apropriados fora deste fluxo local.

O Nginx adiciona uma politica basica de headers de seguranca nas respostas da borda local: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` bloqueando camera, microphone e geolocation, e `X-XSS-Protection: 0`. Para evitar duplicidade, a borda remove esses mesmos headers quando vierem das APIs internas e aplica a politica unica do proxy. O valor `0` desativa o filtro legado de XSS de navegadores antigos, evitando comportamento inconsistente; a protecao efetiva fica em controles modernos como CSP e escaping das aplicacoes. A pagina do portal tambem recebe `Content-Security-Policy` restrita ao proprio host, com `style-src 'unsafe-inline'` apenas para preservar o CSS inline estatico do portal. Os hosts dos Swaggers nao recebem CSP adicional no Nginx para evitar bloquear assets e scripts da Swagger UI.

Os hosts de API via Nginx (`ledger.localhost`, `balance.localhost` e `auth.localhost`) recebem `Cache-Control: no-store` em todas as respostas proxied. Essa politica evita que respostas sensiveis de autenticacao, autorizacao, ledger e saldo sejam armazenadas por navegadores, clientes ou proxies intermediarios. A borda tambem remove headers de cache vindos das APIs internas e envia `Pragma: no-cache` e `Expires: 0` por compatibilidade com clientes legados. O portal estatico em `https://localhost:7443` nao recebe essa regra; os assets do Swagger servidos pelos hosts de API recebem `no-store`, o que preserva funcionamento e favorece seguranca no ambiente local.

Para reduzir fingerprinting, a borda local usa uma imagem local baseada em Alpine com Nginx e o modulo `headers-more`. A configuracao usa `server_tokens off`, remove o header `Server` emitido pela borda e remove headers de tecnologia vindos das APIs internas quando presentes, como `X-Powered-By`, `X-AspNet-Version`, `X-AspNetMvc-Version` e `X-Swagger-UI-Version`.

O Nginx local tambem aplica limites defensivos basicos para reduzir abuso acidental ou malicioso antes que a chamada alcance o ASP.NET:

- `client_max_body_size 1m`, alinhado ao limite padrao `ApiLimits:MaxRequestBodySizeBytes` das APIs Ledger e Balance;
- `client_body_timeout 10s` e `client_header_timeout 10s`;
- `keepalive_timeout 30s`, `send_timeout 30s`, `proxy_connect_timeout 5s`, `proxy_send_timeout 30s` e `proxy_read_timeout 30s`;
- `large_client_header_buffers 4 8k`;
- `limit_conn` por IP em 20 conexoes simultaneas;
- `limit_req` por IP em 10 requisicoes por segundo, com `burst=40` e retorno `429 Too Many Requests`.

Payload acima de 1 MiB deve ser rejeitado na borda com `413 Payload Too Large`. Headers grandes demais podem ser rejeitados pelo Nginx antes de qualquer contrato de negocio da API. Esses limites sao uma protecao local demonstravel e nao substituem WAF, protecao DDoS, limites por usuario/merchant/client id nem dimensionamento de producao.

Validacao da politica de cache via Nginx:

```bash
curl -k -I https://localhost:7443
curl -k -I https://ledger.localhost:7443/swagger
curl -k -I https://balance.localhost:7443/swagger
curl -k -I https://auth.localhost:7443/swagger
curl -k -I https://ledger.localhost:7443/health
```

As respostas dos hosts de API devem conter `Cache-Control: no-store`, `Pragma: no-cache` e `Expires: 0`. As respostas via Nginx nao devem conter `Server`, `X-Powered-By` nem `X-Swagger-UI-Version`.

O Nginx tambem atua como ponto de entrada de correlacao local:

- se o cliente enviar `X-Correlation-Id`, o valor e preservado e encaminhado para a API;
- se o cliente omitir `X-Correlation-Id`, a borda gera um identificador, encaminha para a API e devolve no response;
- o access log do Nginx e emitido como JSON por linha e inclui `correlation_id`.
- para `ledger.localhost`, o access log tambem inclui `upstream_addr` e `upstream_status`, e o response inclui `X-Upstream-Addr` para diagnostico local.

Validacao manual sem correlation id explicito:

```bash
curl -k -i https://ledger.localhost:7443/health
```

Validacao preservando um correlation id explicito:

```bash
curl -k -i -H "X-Correlation-Id: 11111111-1111-4111-8111-111111111111" https://ledger.localhost:7443/health
```

Em ambos os casos, confira o header `X-Correlation-Id` no response e o campo `correlation_id` nos logs:

```bash
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
```

Validacao de payload acima do limite:

```bash
dd if=/dev/zero of=/tmp/payload-maior-que-1m.bin bs=1024 count=1100
curl -k -i -X POST https://ledger.localhost:7443/health --data-binary @/tmp/payload-maior-que-1m.bin
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Em PowerShell:

```powershell
$payload = New-TemporaryFile
[System.IO.File]::WriteAllBytes($payload.FullName, (New-Object byte[] (1100 * 1024)))
curl.exe -k -i -X POST https://ledger.localhost:7443/health --data-binary "@$($payload.FullName)"
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
Remove-Item $payload.FullName
```

O status esperado e `413`. Como a rejeicao ocorre na borda, a rota escolhida nao precisa aceitar POST em uso normal.

Validacao de excesso de requisicoes:

```bash
for i in $(seq 1 80); do curl -k -s -o /dev/null -w "%{http_code}\n" https://ledger.localhost:7443/health & done; wait
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Em PowerShell:

```powershell
1..80 | ForEach-Object {
  Start-Job { curl.exe -k -s -o NUL -w "%{http_code}`n" https://ledger.localhost:7443/health } | Out-Null
}
Get-Job | Receive-Job -Wait -AutoRemoveJob
docker compose -f compose.yaml -f compose.nginx.yaml exec nginx-edge tail -n 80 /var/log/nginx/access.log
```

Parte das chamadas pode retornar `429` quando o burst local for excedido. Chamadas normais de Swagger, portal e health devem continuar retornando os status esperados. Para diagnosticar, use os campos `status`, `request_time`, `upstream_status`, `upstream_addr` e `correlation_id` do access log JSON.

Validacao de distribuicao entre as instancias Ledger:

```bash
for i in $(seq 1 20); do curl -k -s -o /dev/null -D - https://ledger.localhost:7443/health | grep -i X-Upstream-Addr; done
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge | grep upstream_addr
docker compose -f compose.yaml -f compose.nginx.yaml logs ledger-service-1 ledger-service-2
```

Em PowerShell:

```powershell
1..20 | ForEach-Object {
  (Invoke-WebRequest -SkipCertificateCheck https://ledger.localhost:7443/health).Headers["X-Upstream-Addr"]
}
docker compose -f compose.yaml -f compose.nginx.yaml logs nginx-edge
docker compose -f compose.yaml -f compose.nginx.yaml logs ledger-service-1 ledger-service-2
```

O Nginx open source usa uma lista estatica de upstreams nesta POC. Ele demonstra balanceamento local, mas nao implementa autoscaling real, descoberta dinamica avancada, circuit breaker ou reconfiguracao automatica quando o numero de replicas muda. Para producao, a topologia deve ser redesenhada com orquestrador, health checks e estrategia operacional propria.

As APIs executam `UseForwardedHeaders` no inicio do pipeline para reconhecer `X-Forwarded-For`, `X-Forwarded-Proto` e `X-Forwarded-Host` enviados pelo Nginx. Isso permite que componentes ASP.NET Core vejam o scheme externo `https` e o host publico `.localhost` quando a chamada entra pelo proxy, sem mudar o trafego HTTP interno entre containers.

Parar a stack:

```bash
docker compose down
```

Para a stack completa iniciada por `start-full-stack.*`, prefira:

```powershell
./scripts/stop-full-stack.ps1
```

```bash
./scripts/stop-full-stack.sh
```

Use `docker compose down` apenas quando quiser remover containers e redes da stack minima manualmente. Nao use `docker compose down -v` salvo quando a intencao for remover volumes e descartar dados locais.

Ver status e logs:

```bash
docker compose ps
docker compose logs -f ledger-service
docker compose -f compose.yaml -f compose.nginx.yaml logs -f ledger-service-1
docker compose -f compose.yaml -f compose.nginx.yaml logs -f ledger-service-2
docker compose logs -f ledger-worker
docker compose logs -f balance-service
docker compose logs -f balance-worker
docker compose -f compose.yaml -f compose.nginx.yaml logs -f nginx-edge
```

Portas expostas no host:

| Componente | URL ou porta |
| --- | --- |
| Auth.Api | `http://localhost:5030/` |
| LedgerService.Api | `http://localhost:5226/` |
| BalanceService.Api | `http://localhost:5228/` |
| PostgreSQL Ledger | `localhost:15432` |
| PostgreSQL Balance | `localhost:15433` |
| Kafka | `localhost:19092` |
| Jaeger UI | `http://localhost:16686/` com profile `observability` |
| Jaeger OTLP | `localhost:4317` e `localhost:4318` com profile `observability`, para diagnostico direto |
| OpenTelemetry Collector OTLP | `otel-collector:4317` e `otel-collector:4318` na rede interna do compose, com profile `observability` |
| OpenTelemetry Collector metrics | `otel-collector:9464` na rede interna do compose, com profile `observability` |
| Prometheus | `http://localhost:9090/` com profile `observability` |
| Loki | `http://localhost:3100/` com profile `observability` |
| Grafana Alloy | `http://localhost:12345/` quando o profile `observability` estiver ativo |
| Alertmanager | `http://localhost:9093/` com profile `observability` |
| Grafana | `http://localhost:3000/` com profile `observability` |
| Portal Nginx HTTPS | `https://localhost:7443/` com `compose.nginx.yaml` |
| LedgerService.Api via Nginx | `https://ledger.localhost:7443/` com `compose.nginx.yaml` |
| BalanceService.Api via Nginx | `https://balance.localhost:7443/` com `compose.nginx.yaml` |
| Auth.Api via Nginx | `https://auth.localhost:7443/` com `compose.nginx.yaml` |

O compose sobrescreve configuracoes por variaveis de ambiente para usar hosts internos como `ledger-db`, `balance-db`, `kafka` e `otel-collector`. Quando `OTEL_ENABLED=true` e o profile `observability` esta ativo, as APIs e workers enviam OTLP somente para o Collector. O Collector encaminha traces para o Jaeger e expoe metricas em formato Prometheus para scrape interno. Prometheus coleta o Collector; Alloy coleta logs dos containers e envia para Loki. Grafana consulta Prometheus, Loki e Jaeger. O Grafana carrega automaticamente a pasta `Observability` com os dashboards `APIs - Visao Geral` e `Runtime .NET - Visao Geral`, versionados em `observability/grafana/dashboards/`. O datasource Loki possui derived field para abrir traces no datasource interno Jaeger a partir de logs com `TraceId=<valor>`. O ambiente local do compose roda como `Development`.

Prometheus tambem carrega regras locais em `observability/prometheus/rules/` e envia alertas para o Alertmanager local. A UI do Alertmanager fica em `http://localhost:9093/` e nao possui integracao externa configurada.

## Migrations via compose

O compose nao aplica migrations automaticamente. Na primeira execucao com banco vazio, e sempre que houver mudanca de schema, aplique as migrations pelo host usando as portas expostas.

LedgerService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15432;Database=appdb;Username=appuser;Password=local_dev_password"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\LedgerService.Infrastructure\LedgerService.Infrastructure.csproj `
  -s src\LedgerService.Api\LedgerService.Api.csproj `
  -c AppDbContext
```

BalanceService:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=15433;Database=dbBalance;Username=userBalance;Password=local_dev_password"
dotnet tool restore
dotnet tool run dotnet-ef -- database update `
  -p src\BalanceService.Infrastructure\BalanceService.Infrastructure.csproj `
  -s src\BalanceService.Api\BalanceService.Api.csproj `
  -c BalanceDbContext
```

## Execucao no host

Use este modo quando PostgreSQL e Kafka ja estiverem disponiveis e voce quiser rodar ou depurar os processos no host.

Restaure as ferramentas:

```bash
dotnet tool restore
```

Suba as APIs:

```bash
dotnet run --project src\Auth.Api\Auth.Api.csproj
dotnet run --project src\LedgerService.Api\LedgerService.Api.csproj
dotnet run --project src\LedgerService.Worker\LedgerService.Worker.csproj
dotnet run --project src\BalanceService.Api\BalanceService.Api.csproj
dotnet run --project src\BalanceService.Worker\BalanceService.Worker.csproj
```

As portas padrao sao:

- Auth.Api: `http://localhost:5030/`;
- LedgerService.Api: `http://localhost:5226/`;
- BalanceService.Api: `http://localhost:5228/`.

`LedgerService.Worker` e `BalanceService.Worker` nao expoem porta HTTP; acompanhe pelo console ou logs dos containers.

## Configuracao

Configuracoes versionadas ficam nos `appsettings*.json` dos projetos de API e do Worker. Para sobrescrever valores localmente, use variaveis de ambiente com `__` como separador de secoes:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5432;Database=appdb;Username=appuser;Password=__REDACTED__"
$env:Kafka__Producer__BootstrapServers = "127.0.0.1:9092"
```

Nao versione segredos. Em ambientes compartilhados ou produtivos, JWKS via HTTP e Kafka `Plaintext` nao devem ser usados.

## Politica local de imagens

O compose local usa imagens com tags versionadas e nao usa `latest`. Essa escolha reduz manutencao para a POC multi-plataforma e preserva a ergonomia local.

Ambientes de CI, homologacao, producao ou qualquer ambiente compartilhado devem aplicar pinagem por digest ou scan de imagens antes da promocao. Atualizacoes de imagem precisam ser intencionais, revisaveis e registradas em diff. Se uma imagem sem digest for promovida para ambiente compartilhado, a promocao deve ser bloqueada ou justificada formalmente.

## Testcontainers e Docker-compatible API

Alguns testes de integracao usam Testcontainers com PostgreSQL real. O Testcontainers depende de uma Docker-compatible API acessivel, nao de Docker Desktop especificamente e nem da CLI usada para a stack local.

Esses testes iniciam e descartam containers PostgreSQL automaticamente durante a execucao, usando connection string dinamica e porta publicada dinamicamente pelo runtime de containers. Nao e necessario ter PostgreSQL instalado localmente e nao e necessario executar `docker compose up` antes dos testes.

Os testes PostgreSQL ficam em collections xUnit especificas, compartilham um container por collection e limpam as tabelas afetadas entre cenarios para evitar estado residual.

Validacao rapida do ambiente:

```powershell
docker version
docker ps
dotnet test
```

No Windows sem Docker Desktop, a recomendacao local e Rancher Desktop com `moby/dockerd`:

```powershell
rdctl set --container-engine.name=moby
```

Em geral, nao defina `DOCKER_HOST` de forma persistente. Com Rancher Desktop em `moby/dockerd`, a CLI `docker` e o Testcontainers devem localizar a Docker-compatible API pelo contexto/padrao do ambiente.

Nao configure `DOCKER_HOST` de forma permanente no codigo da aplicacao. Essa configuracao pertence ao ambiente local do desenvolvedor.

### Troubleshooting - Testcontainers no Windows sem Docker Desktop

Se os testes com Testcontainers falharem por nao localizar o Docker daemon:

1. Confirme que o Rancher Desktop esta usando `moby/dockerd`.
2. Confirme que a Docker API esta acessivel:

   ```powershell
   docker version
   docker ps
   ```

3. Confirme o `DOCKER_HOST`:

   ```powershell
   echo $env:DOCKER_HOST
   ```

4. Valor recomendado no Windows:

   ```text
   <vazio>
   ```

5. Se o ambiente estiver com `DOCKER_HOST=npipe:////./pipe/docker_engine`, a CLI `docker` pode funcionar, mas o Docker.DotNet usado pelo Testcontainers pode falhar com erro semelhante a `npipe:////pipe/docker_engine is not a valid npipe URI`. Remova a variavel persistente do usuario e reabra o terminal ou IDE:

   ```powershell
   [Environment]::SetEnvironmentVariable("DOCKER_HOST", $null, "User")
   ```

6. Para validar na sessao atual sem reabrir o terminal:

   ```powershell
   Remove-Item Env:DOCKER_HOST -ErrorAction SilentlyContinue
   docker ps
   dotnet test
   ```

7. Se algum runtime especifico exigir `DOCKER_HOST` para o processo de teste, use override apenas na sessao atual do terminal:

   ```powershell
   $env:DOCKER_HOST = "npipe://./pipe/docker_engine"
   dotnet test
   ```

   Evite persistir esse valor como variavel de usuario, pois ele pode quebrar a CLI `docker` em alguns ambientes Windows.

8. Feche e abra novamente o terminal ou IDE apos alterar variaveis de ambiente persistentes.

## Swagger e endpoints operacionais

Swagger/OpenAPI fica habilitado por padrao somente em `Development`. Fora desse ambiente, a exposicao exige `Swagger:Enabled=true`.

Endpoints operacionais:

- `GET /health`: liveness simples, publico nesta POC, sem depender de DB ou Kafka.
- `GET /ready`: readiness operacional, publico nesta POC. No `LedgerService.Api` e no `BalanceService.Api`, valida o banco necessario para aceitar trafego HTTP.

O compose usa healthchecks nativos para PostgreSQL e Kafka. As imagens runtime `mcr.microsoft.com/dotnet/aspnet:10.0` usadas pelas APIs nao trazem `curl`, `wget` ou `busybox`; por isso os healthchecks HTTP das APIs nao sao declarados no compose nesta etapa para evitar instalar dependencias apenas para sondas locais. Valide as APIs por `GET /health` no host ou pelos scripts/workflows que ja fazem essa chamada.

Detalhes de operacao ficam em [observabilidade e operacao minima](../observability.md).

## Limites operacionais

`LedgerService.Api` e `BalanceService.Api` possuem limites configuraveis:

- `ApiLimits:MaxRequestBodySizeBytes`;
- `ApiLimits:RateLimitPermitLimit`;
- `ApiLimits:RateLimitWindowSeconds`;
- `ApiLimits:RateLimitQueueLimit`;
- `ApiLimits:MaxBalancePeriodDays`.

Em variaveis de ambiente, use `ApiLimits__MaxRequestBodySizeBytes`, `ApiLimits__MaxBalancePeriodDays` e os demais nomes equivalentes.

## VS Code

O repositorio inclui:

- `poc-arquitetura.code-workspace`;
- `.vscode/extensions.json`;
- `.vscode/settings.json`;
- `.vscode/launch.json`;
- `.vscode/tasks.json`;
- `.vscode/rest-client.env.json`.

Para abrir:

1. Use `File > Open Workspace from File...`.
2. Selecione `poc-arquitetura.code-workspace`.
3. Instale as extensoes sugeridas.

As configuracoes do VS Code sao opcionais e apenas facilitam comandos que continuam funcionando pelo terminal. A solution padrao e `LedgerService.slnx`; as exclusoes do workspace escondem diretorios gerados como `bin`, `obj`, `TestResults`, `artifacts/k6`, relatorios de cobertura e `StrykerOutput`.

Tasks uteis:

- `dotnet: tool restore`;
- `dotnet: restore solution`;
- `dotnet: build solution`;
- `dotnet: test solution`;
- `test: coverage gate`;
- `local stack: start`;
- `local stack: start with observability`;
- `test: load smoke`.

As tasks de stack e k6 chamam os scripts versionados (`scripts/start-local-stack.*` e `scripts/run-loadtests.*`) para evitar duplicar logica. Elas nao executam teardown destrutivo nem migrations fora do fluxo ja definido pelos scripts.

As configuracoes de debug rodam processos no host em `Development` para `Auth.Api`, `LedgerService.Api`, `LedgerService.Worker`, `BalanceService.Api` e `BalanceService.Worker`. Os nomes indicam que dependencias locais podem ser necessarias quando banco, Kafka ou JWKS forem usados. Se a stack completa do compose estiver em execucao, pare o container equivalente antes de depurar o mesmo processo no host para evitar conflito de porta ou processamento duplicado.

O arquivo `src/LedgerService.Api/LedgerService.Api.http` pode ser usado com a extensao REST Client. Nao coloque segredos em `.vscode/rest-client.env.json`.

## Load tests com k6

Os testes de carga ficam em `loadtests/k6` e rodam dentro da rede do compose.

Pre-requisitos:

1. Suba a stack local com `./scripts/start-local-stack.ps1` ou `./scripts/start-local-stack.sh`.
2. Mantenha `Auth.Api`, `LedgerService.Api`, `BalanceService.Api`, `LedgerService.Worker` e `BalanceService.Worker` em execucao quando validar cenarios que dependem de efeitos assincronos.

Windows:

```powershell
./scripts/run-loadtests.ps1 -Mode smoke
./scripts/run-loadtests.ps1 -Mode balance50
./scripts/run-loadtests.ps1 -Mode resilience
```

Linux/macOS:

```bash
./scripts/run-loadtests.sh smoke
./scripts/run-loadtests.sh balance50
./scripts/run-loadtests.sh resilience
```

Arquivos gerados em `artifacts/k6` e `.env.k6.auto` nao sao versionados.

Os runners aplicam `compose.k6.yaml` e recriam os containers HTTP alvo antes do k6 para manter os testes apontando para as APIs e garantir que overrides de ambiente entrem em vigor. Os workers continuam sem endpoint HTTP nos cenarios de carga. Antes de obter token e executar o k6, os runners validam uma conexao real no PostgreSQL do Balance usando `BALANCE_DB_USER`, `BALANCE_DB_NAME` e `BALANCE_DB_PASSWORD`; se a senha do volume local divergir da configuracao, o fluxo falha cedo com diagnostico e nenhuma acao destrutiva.

Para validar manualmente a configuracao efetiva do k6:

```bash
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config --services
```

## OWASP ZAP local

Os scripts versionados de ZAP executam DAST local em container contra `Auth.Api`, `LedgerService.Api` e `BalanceService.Api`. Eles assumem que a stack ja esta rodando, validam `GET /health` antes do scan, importam `/swagger/v1/swagger.json` de cada API, salvam relatorios em `zap-reports/<timestamp>/` e removem apenas o container temporario `poc-arquitetura-zap` ao final.

URLs diretas:

```powershell
./scripts/run-owasp-zap.ps1
```

```bash
./scripts/run-owasp-zap.sh
```

Para subir a stack local direta antes do scan, use:

```powershell
./scripts/run-owasp-zap.ps1 -StartStack
```

```bash
./scripts/run-owasp-zap.sh --start-stack
```

Via Nginx local:

```powershell
./scripts/run-owasp-zap.ps1 -UseNginx
```

```bash
./scripts/run-owasp-zap.sh --use-nginx
```

O modo padrao usa `zap-api-scan.py -f openapi -S` e nao falha a execucao apenas por encontrar alertas. Active scan e falha por alertas exigem parametros explicitos. Detalhes ficam em [OWASP ZAP local](owasp-zap.md).

## Migrations de referencia

As migrations ficam nos projetos `Infrastructure`.

LedgerService:

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\LedgerService.Infrastructure\\LedgerService.Infrastructure.csproj \
  -s src\\LedgerService.Api\\LedgerService.Api.csproj \
  -c AppDbContext
```

BalanceService:

```bash
dotnet tool run dotnet-ef -- migrations list \
  -p src\\BalanceService.Infrastructure\\BalanceService.Infrastructure.csproj \
  -s src\\BalanceService.Api\\BalanceService.Api.csproj \
  -c BalanceDbContext
```

Para criar, aplicar ou reverter migrations, use os mesmos projetos e contexts acima. Nao altere migrations antigas apenas para organizar.
