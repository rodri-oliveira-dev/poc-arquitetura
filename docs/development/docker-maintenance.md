# Manutencao Docker local

Este guia documenta diagnostico e limpeza de disco da stack local. Nenhum comando aqui deve ser executado de forma automatica em CI ou hooks sem revisao, porque volumes Docker podem conter bancos, filas, buckets, indices e chaves locais.

## Diagnostico de uso

Use estes comandos para entender o consumo antes de limpar:

```bash
docker system df -v
docker buildx du
docker volume ls
./scripts/docker/disk-report.sh
```

No PowerShell:

```powershell
docker system df -v
docker buildx du
docker volume ls
./scripts/docker/disk-report.ps1
```

Containers sao processos e writable layers. Imagens sao artefatos de runtime/build. Volumes guardam dados fora do ciclo de vida do container. Cache de build pertence ao builder Docker/BuildKit. Bind mounts apontam para arquivos do workspace ou da maquina host e nao sao removidos por `docker volume rm`.

## Cache de build limitado a 5GB

Limpeza imediata do cache mantendo ate 5GB:

```bash
docker builder prune -a --keep-storage 5GB -f
docker buildx prune -a --keep-storage 5GB -f
```

Isso limpa cache do builder, nao volumes do Compose. Para validar builders customizados e consumo detalhado:

```bash
docker buildx ls
docker buildx du
```

O limite permanente e configuracao do Docker daemon/builder, nao do `docker-compose.yml`. Em Docker Desktop ou `daemon.json`, use:

```json
{
  "builder": {
    "gc": {
      "enabled": true,
      "defaultKeepStorage": "5GB"
    }
  }
}
```

Depois de alterar configuracao do daemon, reinicie o runtime Docker.

## Limpeza segura padrao

Para remover containers e redes do projeto sem apagar volumes:

```bash
./scripts/docker/clean-safe.sh
```

No PowerShell:

```powershell
./scripts/docker/clean-safe.ps1
```

Esse fluxo executa `docker compose down --remove-orphans` sem `-v` e pergunta antes de limpar cache de build e imagens dangling.

## Volumes com retencao de 7 dias

Docker nao possui TTL nativo para volumes. Este repositorio usa labels para volumes locais descartaveis:

```yaml
labels:
  auto-prune: "true"
  retention: "7d"
  environment: "local"
  owner: "docker-compose"
```

O script de retencao e dry-run por padrao:

```bash
./scripts/docker/prune-volumes.sh
./scripts/docker/prune-volumes.sh --apply
```

No PowerShell:

```powershell
./scripts/docker/prune-volumes.ps1
./scripts/docker/prune-volumes.ps1 -Apply
```

O script lista volumes com `auto-prune=true`, confere `retention=7d`, verifica idade por `CreatedAt`, ignora volumes associados a containers existentes e so remove quando `--apply` ou `-Apply` e informado.

## Classificacao dos volumes do projeto

| Volume | Categoria | Politica |
| --- | --- | --- |
| `postgres-data` | persistente obrigatorio local | Nao remover automaticamente; guarda `appdb` com schemas `ledger`, `balance` e `transfer`. |
| `sonar-postgres-data` | persistente util em desenvolvimento | Nao remover automaticamente; guarda banco local do SonarQube. |
| `sonarqube-data` | persistente util em desenvolvimento | Nao remover automaticamente; guarda estado local do SonarQube. |
| `sonarqube-extensions` | persistente util em desenvolvimento | Nao remover automaticamente; guarda plugins/extensoes locais do SonarQube. |
| `auth-api-data` | persistente util para fluxo legado | Nao remover automaticamente; guarda chave RSA local do `Auth.Api` legado. |
| `nginx-certs` | cache descartavel | Seguro para apagar apos 7 dias quando nao estiver em uso; e recriado a partir de `infra/nginx/certs`. |

Bind mounts de codigo/configuracao:

- `./infra/postgres/init`;
- `./infra/keycloak/realm-poc.json`;
- `./observability/**`;
- `./infra/nginx/**`;
- `./loadtests/k6`, `./artifacts/k6` e `./.env.k6.auto`;
- credencial local do Cloud SQL Auth Proxy configurada por `GOOGLE_APPLICATION_CREDENTIALS`.

Dados temporarios em `tmpfs`:

- Prometheus em `/prometheus`;
- Loki em `/loki`;
- Alertmanager em `/alertmanager`;
- Alloy em `/var/lib/alloy/data`;
- logs locais do SonarQube em `/opt/sonarqube/logs`.

Kafka, Pub/Sub emulator, init jobs e k6 nao usam volumes nomeados persistentes neste Compose. Dados internos desses containers sao descartados ao remover os containers.

## Comandos destrutivos

Estes comandos podem apagar dados locais persistidos:

```bash
docker compose down -v
docker volume prune
docker volume prune -a
docker volume rm <volume>
docker system prune --volumes
```

Use apenas quando os dados forem descartaveis ou houver backup manual. `docker volume prune` pode afetar volumes de outros projetos; revise `docker volume ls` e `docker volume inspect <volume>` antes.

## Servicos que mais consomem disco

- `postgres-db`: dados relacionais locais persistentes.
- `sonar-db` e `sonarqube`: banco, estado e extensoes do SonarQube local.
- `kafka`: logs de topicos ficam no writable layer do container enquanto ele existir.
- `ledger-service`, `balance-service`, `transfer-service` e workers: imagens geradas por build local e cache NuGet/BuildKit.
- Observabilidade opcional: usa `tmpfs` para dados volateis, mas ainda consome memoria e gera logs Docker.

Para reduzir crescimento, prefira Dev Lite sem observabilidade, SonarQube, k6, Nginx e Auth legado; limpe cache de build com retencao de 5GB; e remova volumes persistentes apenas conscientemente.
