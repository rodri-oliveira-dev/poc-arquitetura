# SonarQube local

Este fluxo sobe um SonarQube local para analise estatica do projeto. O SonarQube nao recebe telemetria runtime da aplicacao. Ele recebe, via scanner, o resultado da analise estatica, testes e cobertura.

## Subir o SonarQube

```bash
docker compose -f compose.sonar.yaml --profile quality up -d
```

O compose inicia os servicos `sonar-db` e `sonarqube`. Acesse:

```text
http://localhost:9000
```

No primeiro acesso, conclua a configuracao local do SonarQube e crie um token de usuario ou de projeto. Nao versione tokens.

## Configurar o token

No Bash:

```bash
export SONAR_TOKEN="<token-criado-no-sonarqube>"
```

No PowerShell:

```powershell
$env:SONAR_TOKEN="<token-criado-no-sonarqube>"
```

Opcionalmente, ajuste a URL caso use outra porta:

```bash
export SONAR_HOST_URL="http://localhost:9000"
```

## Executar a analise

```bash
bash scripts/sonar-analyze.sh
```

O script restaura as tools locais, inicia o SonarScanner for .NET, compila `LedgerService.slnx`, executa os testes com `coverlet.runsettings` e finaliza o envio para o SonarQube.

A cobertura para o SonarQube usa o formato OpenCover, consumido por `sonar.cs.opencover.reportsPaths`. O mesmo `coverlet.runsettings` tambem gera Cobertura, preservando o formato usado pelo fluxo de cobertura existente do CI.

## Parar ou remover

Parar os containers sem apagar volumes:

```bash
docker compose -f compose.sonar.yaml --profile quality stop
```

Remover os containers sem apagar volumes:

```bash
docker compose -f compose.sonar.yaml --profile quality down
```

Remover containers e volumes do SonarQube local:

```bash
docker compose -f compose.sonar.yaml --profile quality down -v
```

Use a remocao de volumes apenas quando quiser descartar banco, configuracoes, extensoes e historico local do SonarQube.
