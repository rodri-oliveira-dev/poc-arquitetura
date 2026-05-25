# OWASP ZAP local

Este guia documenta a execucao local versionada do OWASP ZAP contra as APIs HTTP da POC. O fluxo usa container Docker, assume que a stack ja esta no ar e salva relatorios em `zap-reports/<timestamp>/`.

Os scripts usam por padrao a imagem oficial `ghcr.io/zaproxy/zaproxy:stable`, conforme a documentacao Docker do ZAP: <https://www.zaproxy.org/docs/docker/>.

## Pre-requisitos

- Docker-compatible API acessivel.
- CLI `docker` com suporte a `docker compose`.
- Stack local ja iniciada com `./scripts/start-local-stack.*` ou stack completa com Nginx iniciada com `./scripts/start-full-stack.*`.
- Para Linux/macOS, `curl` e `python3` disponiveis no host.

Os scripts nao sobem a stack principal automaticamente, nao executam build, testes .NET nem k6, e nao removem containers da aplicacao. Eles criam/removem apenas o container temporario `poc-arquitetura-zap`.

## Alvos

Por padrao, os scripts validam as URLs HTTP diretas:

- Auth.Api: `http://localhost:5030`
- LedgerService.Api: `http://localhost:5226`
- BalanceService.Api: `http://localhost:5228`

Com Nginx local, use as URLs HTTPS:

- Auth.Api: `https://auth.localhost:7443`
- LedgerService.Api: `https://ledger.localhost:7443`
- BalanceService.Api: `https://balance.localhost:7443`

Antes do scan, cada script chama `GET /health` em todas as APIs. Se alguma API estiver indisponivel, a execucao falha com a URL usada e uma sugestao para subir a stack local ou a stack completa.

## PowerShell

URLs diretas:

```powershell
./scripts/run-owasp-zap.ps1
```

Via Nginx local:

```powershell
./scripts/run-owasp-zap.ps1 -UseNginx
```

Sobrescrevendo URLs ou imagem:

```powershell
./scripts/run-owasp-zap.ps1 `
  -AuthUrl http://localhost:5030 `
  -LedgerUrl http://localhost:5226 `
  -BalanceUrl http://localhost:5228 `
  -ZapImage ghcr.io/zaproxy/zaproxy:stable
```

## Bash

URLs diretas:

```bash
./scripts/run-owasp-zap.sh
```

Via Nginx local:

```bash
./scripts/run-owasp-zap.sh --use-nginx
```

Sobrescrevendo URLs ou imagem:

```bash
./scripts/run-owasp-zap.sh \
  --auth-url http://localhost:5030 \
  --ledger-url http://localhost:5226 \
  --balance-url http://localhost:5228 \
  --zap-image ghcr.io/zaproxy/zaproxy:stable
```

## Relatorios

Cada execucao cria uma subpasta com timestamp no formato `yyyyMMdd-HHmmss`, por exemplo:

```text
zap-reports/20260525-153045/
```

Arquivos esperados:

- `auth-api.html`, `auth-api.json`, `auth-api.md`
- `ledger-service-api.html`, `ledger-service-api.json`, `ledger-service-api.md`
- `balance-service-api.html`, `balance-service-api.json`, `balance-service-api.md`
- `summary.md`

O `summary.md` registra data/hora, imagem ZAP, URLs analisadas, alvo visto pelo container, arquivos gerados e status final por API. A pasta `zap-reports/` e ignorada pelo Git; relatorios gerados nao devem ser versionados.

## Tipo de scan

O padrao e `zap-baseline.py`, que executa spider limitado e analise passiva. Esse modo e o recomendado para desenvolvimento local porque evita active scan agressivo por padrao.

Active scan fica disponivel apenas por parametro explicito:

```powershell
./scripts/run-owasp-zap.ps1 -ActiveScan
```

```bash
./scripts/run-owasp-zap.sh --active-scan
```

Use active scan somente em ambiente local descartavel e autorizado. Ele pode gerar trafego mais invasivo que o baseline.

## Criterio de falha

Por padrao, o script conclui e salva relatorios mesmo quando o ZAP encontra alertas. O exit code representa falha operacional do runner ou do ZAP, nao necessariamente a presenca de achados.

Para propagar alertas como falha:

```powershell
./scripts/run-owasp-zap.ps1 -FailOnAlerts
```

```bash
./scripts/run-owasp-zap.sh --fail-on-alerts
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
