# SonarQube local

Este fluxo sobe um SonarQube local para analise estatica do projeto. O SonarQube nao recebe telemetria runtime da aplicacao. Ele recebe, via scanner, o resultado da analise estatica, testes e cobertura.

## Subir o SonarQube

```bash
docker compose -f compose.sonar.yaml --profile quality up -d
```

O compose inicia os servicos `sonar-db` e `sonarqube`. O banco, dados principais e extensoes permanecem persistentes nos volumes `sonar-postgres-data`, `sonarqube-data` e `sonarqube-extensions`; logs locais do SonarQube ficam em `tmpfs` por padrao com tamanho configuravel por `SONAR_LOGS_TMPFS_SIZE` (`256m` por default). Acesse:

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
./scripts/quality/sonar-analyze.sh
```

O script restaura as tools locais, inicia o SonarScanner for .NET, compila `LedgerService.slnx`, executa os testes com `coverlet.runsettings` e finaliza o envio para o SonarQube.

A cobertura para o SonarQube usa o formato OpenCover, consumido por `sonar.cs.opencover.reportsPaths`. O mesmo `coverlet.runsettings` tambem gera Cobertura, preservando o formato usado pelo fluxo de cobertura existente do CI.

O script mantem a deteccao geral de credenciais hard-coded ativa, mas ignora issues somente em linhas cujo contexto pareca credencial e cujo valor seja um placeholder uppercase de secret entre `<...>`, como `Password=<LEDGER_DB_PASSWORD>` ou `KEYCLOAK_CLIENT_SECRET=<KEYCLOAK_CLIENT_SECRET>`. Valores reais ou literais, como `Password=postgres`, `Password=123456`, `Password=localpassword` ou `Password=my-secret`, continuam fora desse padrao.

## Tratativas de erro

### `vm.max_map_count` baixo

Se o container `sonarqube` iniciar e parar logo depois, consulte os logs:

```bash
docker logs poc-sonarqube
```

Quando aparecer erro semelhante a este:

```text
vm.max_map_count [65530] is too low, increase to at least [262144]
```

ajuste o parametro no ambiente Docker local e suba novamente o compose.

No Windows com Rancher Desktop em WSL:

```powershell
wsl.exe -d rancher-desktop -u root -- sysctl -w vm.max_map_count=262144
docker compose -f compose.sonar.yaml --profile quality up -d
```

Em Linux:

```bash
sudo sysctl -w vm.max_map_count=262144
docker compose -f compose.sonar.yaml --profile quality up -d
```

Esse ajuste pode precisar ser repetido apos reiniciar o WSL, Rancher Desktop ou a maquina.

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

Use a remocao de volumes apenas quando quiser descartar banco, configuracoes, extensoes e historico local do SonarQube. Os logs em `tmpfs` sao descartados ao parar/remover o container.
