# OWASP ZAP local

Este guia documenta a execucao local versionada do OWASP ZAP contra as APIs HTTP da POC. O fluxo usa container Docker, assume que a stack ja esta no ar e salva relatorios em `zap-reports/<timestamp>/`.

Os scripts usam por padrao a imagem oficial `ghcr.io/zaproxy/zaproxy:stable`, conforme a documentacao Docker do ZAP: <https://www.zaproxy.org/docs/docker/>. O scan usa `zap-api-scan.py` importando OpenAPI/Swagger em `/swagger/v1/swagger.json`, conforme a documentacao de API Scan do ZAP: <https://www.zaproxy.org/docs/docker/api-scan/>.

Tambem existe o workflow `.github/workflows/owasp-zap.yml` para executar o mesmo baseline em GitHub Actions sob demanda ou automaticamente apos sucesso do CI da `main`. Ele nao roda em `pull_request` e nao e gate obrigatorio nesta etapa.

## Pre-requisitos

- Docker-compatible API acessivel.
- CLI `docker` com suporte a `docker compose`.
- Stack local ja iniciada com `./scripts/local/start-stack.*` ou stack completa com Nginx iniciada com `./scripts/local/start-full-stack.*`.
- Para Linux/macOS, `curl` e `python3` disponiveis no host.

Os scripts nao sobem a stack principal automaticamente, nao executam build, testes .NET nem k6, e nao removem containers da aplicacao. Eles criam/removem apenas o container temporario `poc-arquitetura-zap`.

Quando quiser um comando unico para ambiente local ainda parado, use `-StartStack` ou `--start-stack`. Esse modo chama os scripts oficiais `scripts/local/start-stack.*` antes dos health checks. Ele e explicito porque pode restaurar ferramentas, aplicar migrations e subir containers da POC.

Por padrao o scan importa OpenAPI sem autenticacao. Quando quiser exercitar endpoints protegidos com Bearer token, use `-UseAuthentication` ou `--use-authentication`; o runner chama `scripts/validation/get-token.*`, que usa Keycloak local por padrao.

## Alvos

Por padrao, os scripts validam as URLs HTTP diretas das APIs de negocio:

- LedgerService.Api: `http://localhost:5226`
- BalanceService.Api: `http://localhost:5228`

Com Nginx local, use as URLs HTTPS:

- LedgerService.Api: `https://ledger.localhost:7443`
- BalanceService.Api: `https://balance.localhost:7443`

Antes do scan, cada script chama `GET /health` em todas as APIs. Se alguma API estiver indisponivel, a execucao falha com a URL usada e uma sugestao para subir a stack local ou a stack completa.

Depois do health check, o alvo analisado pelo ZAP e o documento OpenAPI de cada API:

- LedgerService.Api: `/swagger/v1/swagger.json`
- BalanceService.Api: `/swagger/v1/swagger.json`

Ha duas perspectivas de rede diferentes no runner:

- o host acessa as APIs pelas portas publicadas em `127.0.0.1`, por exemplo `http://localhost:5226/health`;
- o container temporario do ZAP acessa as APIs pela rede Docker quando `--docker-network` e `--ledger-zap-url`/`--balance-zap-url` sao informados;
- quando esses parametros nao sao informados, o comportamento local legado continua convertendo `localhost` para `host.docker.internal`.

O `compose.yaml` mantem as portas das APIs publicadas somente em `127.0.0.1` para evitar exposicao acidental na interface de rede da maquina de desenvolvimento ou do runner. Em GitHub Actions, como o ZAP roda em outro container, ele entra na mesma rede Compose das APIs e usa os nomes internos de servico:

- LedgerService.Api: `http://ledger-service:8080`
- BalanceService.Api: `http://balance-service:8080`

Antes de chamar `zap-api-scan.py`, o runner valida que a rede Docker informada existe e que um container consegue baixar e interpretar cada `/swagger/v1/swagger.json` como JSON OpenAPI valido. Falhas de conectividade, HTTP de erro ou documento invalido continuam sendo falhas operacionais.

Por padrao, o runner aguarda ate 90 segundos por API, com tentativas a cada 3 segundos. Ajuste esse comportamento quando a maquina local estiver mais lenta:

```powershell
./scripts/security/run-owasp-zap.ps1 -StartStack -HealthTimeoutSeconds 180
```

```bash
./scripts/security/run-owasp-zap.sh --start-stack --health-timeout 180
```

## PowerShell

URLs diretas:

```powershell
./scripts/security/run-owasp-zap.ps1
```

Subindo a stack local antes do scan:

```powershell
./scripts/security/run-owasp-zap.ps1 -StartStack
```

Sem rebuild de imagens ao subir a stack:

```powershell
./scripts/security/run-owasp-zap.ps1 -StartStack -NoBuild
```

Via Nginx local:

```powershell
./scripts/security/run-owasp-zap.ps1 -UseNginx
```

Sobrescrevendo URLs ou imagem:

```powershell
./scripts/security/run-owasp-zap.ps1 `
  -LedgerUrl http://localhost:5226 `
  -BalanceUrl http://localhost:5228 `
  -ZapImage ghcr.io/zaproxy/zaproxy:stable
```

Sobrescrevendo o caminho do documento OpenAPI:

```powershell
./scripts/security/run-owasp-zap.ps1 -SwaggerPath /swagger/v1/swagger.json
```

Com token Bearer obtido pelo provider local configurado:

```powershell
./scripts/security/run-owasp-zap.ps1 -UseAuthentication
```

Com token manual, quando voce ja obteve um JWT valido:

```powershell
./scripts/security/run-owasp-zap.ps1 -UseAuthentication -Token "<TOKEN>"
```

## Bash

URLs diretas:

```bash
./scripts/security/run-owasp-zap.sh
```

Subindo a stack local antes do scan:

```bash
./scripts/security/run-owasp-zap.sh --start-stack
```

Sem rebuild de imagens ao subir a stack:

```bash
./scripts/security/run-owasp-zap.sh --start-stack --no-build
```

Via Nginx local:

```bash
./scripts/security/run-owasp-zap.sh --use-nginx
```

Sobrescrevendo URLs ou imagem:

```bash
./scripts/security/run-owasp-zap.sh \
  --ledger-url http://localhost:5226 \
  --balance-url http://localhost:5228 \
  --zap-image ghcr.io/zaproxy/zaproxy:stable
```

Usando uma rede Docker e URLs internas para o container ZAP:

```bash
./scripts/security/run-owasp-zap.sh \
  --ledger-url http://localhost:5226 \
  --balance-url http://localhost:5228 \
  --docker-network poc-arquitetura_poc-net \
  --ledger-zap-url http://ledger-service:8080 \
  --balance-zap-url http://balance-service:8080
```

Sobrescrevendo o caminho do documento OpenAPI:

```bash
./scripts/security/run-owasp-zap.sh --swagger-path /swagger/v1/swagger.json
```

Com token Bearer obtido pelo provider local configurado:

```bash
./scripts/security/run-owasp-zap.sh --use-authentication
```

Com token manual, quando voce ja obteve um JWT valido:

```bash
./scripts/security/run-owasp-zap.sh --use-authentication --token "<TOKEN>"
```

## Relatorios

Cada execucao cria uma subpasta com timestamp no formato `yyyyMMdd-HHmmss`, por exemplo:

```text
zap-reports/20260525-153045/
```

Arquivos esperados:

- `ledger-service-api.html`, `ledger-service-api.json`, `ledger-service-api.md`
- `balance-service-api.html`, `balance-service-api.json`, `balance-service-api.md`
- `summary.md`

O `summary.md` registra data/hora, imagem ZAP, URLs analisadas, alvo visto pelo container, arquivos gerados e status final por API. A pasta `zap-reports/` e ignorada pelo Git; relatorios gerados nao devem ser versionados.

Quando o modo autenticado estiver ativo, o summary registra apenas que `Authorization: Bearer` foi injetado via ZAP Replacer. O token nao e gravado no summary.

## GitHub Actions

O workflow `owasp-zap-baseline` e executado manualmente em `Actions > owasp-zap-baseline > Run workflow` ou automaticamente por `workflow_run` quando o workflow `main-dotnet-ci` conclui com sucesso na branch `main`.

Na execucao automatica, o workflow faz checkout explicito de `${{ github.event.workflow_run.head_sha }}`, o mesmo SHA validado pelo CI. Se o CI falhar ou for cancelado, o job automatico fica pulado.

Ele sobe no runner uma stack HTTP controlada com:

- `postgres-db`;
- `keycloak`;
- `ledger-service`;
- `balance-service`.

O workflow aplica migrations dos bancos antes de iniciar as APIs e aguarda `/health` em:

- `http://localhost:5226/health`;
- `http://localhost:5228/health`.

Esses health checks continuam usando o acesso do host pelas portas publicadas. Para o scan, o workflow descobre programaticamente a rede `poc-net` associada aos containers do Compose, conecta o container temporario do ZAP a essa rede e importa os contratos por:

- `http://ledger-service:8080/swagger/v1/swagger.json`;
- `http://balance-service:8080/swagger/v1/swagger.json`.

Esse desenho evita trocar os bindings locais para `0.0.0.0`, preserva o isolamento do Compose local e impede que falhas como rede inexistente, OpenAPI inacessivel ou importacao invalida sejam mascaradas como sucesso.

Workers, Kafka, Pub/Sub emulator legado e Nginx local ficam fora do escopo padrao porque o baseline atual analisa a superficie HTTP descrita por `/swagger/v1/swagger.json` das APIs principais. Se uma evolucao futura passar a validar endpoints autenticados, fluxos assincronos ponta a ponta ou Nginx, o workflow deve ser ajustado junto com o criterio de falha e a documentacao.

Depois do scan, o workflow publica o artifact `owasp-zap-baseline-reports` com retencao de 7 dias. O artifact inclui, quando gerados pelo ZAP:

- relatorios HTML;
- relatorios JSON;
- relatorios Markdown;
- summaries de texto ou Markdown.

Por padrao, alertas do ZAP nao falham o job. O job falha para problemas operacionais, como erro ao subir a stack, migrations com falha, APIs indisponiveis, falha operacional do container ZAP ou erro do runner. Ao disparar manualmente, o input `fail_on_alerts=true` pode ser usado para propagar alertas como falha, mas isso continua sendo uma decisao manual e nao altera os checks obrigatorios de PR.

Na execucao automatica pos-CI, alertas permanecem consultivos e `fail_on_alerts` nao e aplicado. Nao use `owasp-zap-baseline` como required check enquanto essa politica consultiva estiver vigente; branch protection deve continuar focada no check `Build and test` e nos checks de seguranca definidos como bloqueantes.

Esse criterio existe porque o baseline ainda pode gerar falsos positivos ou achados dependentes do ambiente local da POC. Ele deve evoluir para gate apenas quando houver ambiente alvo estavel, politica de triagem, allowlist ou baseline aceito, severidades bloqueantes definidas e baixo ruido operacional.

## Tipo de scan

O padrao e `zap-api-scan.py -f openapi -S`, que importa o contrato Swagger/OpenAPI e executa o API scan em modo seguro. Esse modo e o recomendado para desenvolvimento local porque melhora a descoberta dos endpoints sem executar active scan por padrao.

Active scan fica disponivel apenas por parametro explicito:

```powershell
./scripts/security/run-owasp-zap.ps1 -ActiveScan
```

```bash
./scripts/security/run-owasp-zap.sh --active-scan
```

Use active scan somente em ambiente local descartavel e autorizado. Ele pode gerar trafego mais invasivo que o baseline.

## Criterio de falha

Por padrao, o script conclui e salva relatorios mesmo quando o ZAP encontra alertas. O exit code representa falha operacional do runner ou do ZAP, nao necessariamente a presenca de achados.

Para propagar alertas como falha:

```powershell
./scripts/security/run-owasp-zap.ps1 -FailOnAlerts
```

```bash
./scripts/security/run-owasp-zap.sh --fail-on-alerts
```

## TLS local

Ao usar Nginx local com HTTPS e certificado autoassinado, os scripts validam `/health` com tolerancia ao certificado local e executam o ZAP com `connection.sslAcceptAll=true`. Essa configuracao existe para o fluxo local da POC e nao deve ser tratada como padrao para ambientes compartilhados ou produtivos.

## Interpretacao

Os relatorios ZAP ajudam a identificar sinais dinamicos de exposicao, configuracao insegura e headers ausentes no ambiente local. Eles nao substituem pentest, threat modeling, revisao de arquitetura, SAST, dependency review, scan de imagens ou validacao em ambiente representativo.

Quando houver achados, priorize:

- severidade e confianca do alerta;
- endpoint e superficie exposta;
- reproducibilidade local;
- diferenca entre risco aceito da POC local e requisito para ambiente compartilhado/produtivo.
