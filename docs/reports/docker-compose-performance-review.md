# Revisao Docker, Compose, volumes e desempenho local

## Escopo

Revisao focada em Docker, Docker Compose, desempenho local das maquinas de desenvolvimento, tamanho de contexto/imagens e consumo de espaco por volumes.

Arquivos analisados:

- `.dockerignore`
- `compose.yaml`
- `compose.nginx.yaml`
- `compose.auth-legacy.yaml`
- `compose.k6.yaml`
- `compose.sonar.yaml`
- `src/LedgerService.Api/Dockerfile`
- `src/LedgerService.Worker/Dockerfile`
- `src/BalanceService.Api/Dockerfile`
- `src/BalanceService.Worker/Dockerfile`
- `src/Auth.Api/Dockerfile`
- `infra/nginx/Dockerfile`
- `infra/nginx/.dockerignore`
- `infra/nginx/nginx.conf`
- `infra/nginx/security-headers.conf`
- `infra/nginx/README.md`
- `scripts/start-local-stack.ps1`
- `scripts/start-local-stack.sh`
- `scripts/start-full-stack.ps1`
- `scripts/start-full-stack.sh`
- `scripts/stop-full-stack.ps1`
- `scripts/stop-full-stack.sh`
- `scripts/run-loadtests.ps1`
- `scripts/run-loadtests.sh`
- `scripts/compose-env.ps1`
- `scripts/compose-env.sh`
- `docs/development/local-development.md`
- `docs/quality/sonarqube.md`
- ADRs relacionadas a Compose, Docker-compatible API, containers, observabilidade local, Nginx e Keycloak.

## Alteracoes realizadas

### Build e cache

- Os Dockerfiles .NET passaram a usar cache BuildKit com `sharing=locked` para pacotes NuGet em `dotnet restore` e `dotnet publish`.
- O `COPY src/ ./src/` foi substituido por copias dos diretorios realmente usados por cada imagem.
- O `dotnet publish` passou a usar `/p:UseAppHost=false`, mantendo a execucao por `dotnet *.dll` e reduzindo artefatos publicados.

Justificativa: diminui invalidez de cache por mudancas em servicos nao relacionados, reduz bytes copiados para camadas intermediarias e evita publicar executaveis apphost desnecessarios para containers framework-dependent.

Risco: baixo. Depende de BuildKit, ja implicito pelo uso de `# syntax=docker/dockerfile:1`. O cache usa lock para evitar corrida quando varios servicos fazem restore em paralelo. A execucao continua via `dotnet <assembly>.dll`.

### Contexto de build

- `.dockerignore` foi expandido para excluir `.git`, metadados de IDE/agentes, caches locais, `bin`, `obj`, `TestResults`, cobertura, `artifacts`, `dist`, `data`, `zap-reports`, `node_modules`, `.env*`, docs, testes, loadtests e observabilidade.
- Foi criado `infra/nginx/.dockerignore` permitindo apenas o `Dockerfile` no contexto de build da imagem Nginx.

Justificativa: os Dockerfiles .NET copiam apenas arquivos de projeto e `src`; docs, testes, relatorios, caches locais e ambientes nao precisam ser enviados ao daemon. O contexto do Nginx nao precisa receber certificados locais, HTML ou configuracoes, pois esses arquivos sao bind mounts em runtime.

Risco: baixo. Se um Dockerfile futuro precisar de arquivos hoje ignorados, ele deve ajustar explicitamente o `.dockerignore`.

## Problemas encontrados

### Alto impacto

| Categoria | Problema | Situacao |
| --- | --- | --- |
| Build e cache | `COPY src/ ./src/` em todos os Dockerfiles .NET invalidava camadas por mudancas em servicos nao relacionados. | Corrigido. |
| Build e cache | O contexto raiz permitia envio de muitos artefatos locais, incluindo relatorios, caches e diretorios de testes. | Corrigido. |
| Segurança e manutenção | O contexto de build do Nginx podia incluir certificados locais em `infra/nginx/certs` se eles existissem no workspace. | Corrigido com `infra/nginx/.dockerignore`. |

### Medio impacto

| Categoria | Problema | Situacao |
| --- | --- | --- |
| Tamanho de imagem | `dotnet publish` podia gerar apphost desnecessario, embora o container execute DLL via `dotnet`. | Corrigido com `/p:UseAppHost=false`. |
| Volumes e consumo de disco | Volumes persistentes de PostgreSQL e SonarQube podem crescer com uso local prolongado. | Mantido por seguranca; limpeza deve ser manual e consciente. |
| Performance local | Observabilidade completa inclui varios servicos com CPU/memoria limitados, mas ainda pesada para uso diario. | Movida para `compose.observability.yaml`; o core funcional nao sobe observabilidade por padrao. |
| Performance local | Loki, Prometheus, Alertmanager e Alloy podiam consumir disco local em sessoes longas. | Corrigido com `tmpfs` configuravel para dados descartaveis de observabilidade. |
| Organização do Compose | Stack principal, observabilidade, Nginx, k6, SonarQube e Auth legado precisavam de fronteiras mais claras. | Corrigido: `compose.yaml` contem core funcional com workers; observabilidade fica em overlay proprio. |

### Baixo impacto

| Categoria | Problema | Situacao |
| --- | --- | --- |
| Manutenção | Comentarios antigos em Dockerfiles eram corretos, mas a copia de fontes era ampla demais para o objetivo de cache. | Corrigido indiretamente. |
| Segurança e manutenção | Imagens seguem tags versionadas sem digest. | Mantido conforme ADR-0032; digest pinning fica para ambiente compartilhado/produtivo. |
| Startup | APIs nao tem healthcheck HTTP no compose porque imagens runtime nao trazem `curl`/`wget`. | Mantido conforme documentacao local. |

## Avaliacao de volumes

Volumes persistentes mantidos:

- `ledger-postgres-data`: necessario para banco local do Ledger.
- `balance-postgres-data`: necessario para banco local do Balance.
- `auth-api-data`: necessario para chave RSA persistida do Auth.Api legado.
- `sonar-postgres-data`, `sonarqube-data`, `sonarqube-extensions`: usados pelo SonarQube local opcional.

Dados descartaveis convertidos para `tmpfs`:

- Loki em `/loki`, default `LOKI_TMPFS_SIZE=256m`, com retencao local curta configurada em `6h`.
- Prometheus em `/prometheus`, default `PROMETHEUS_TMPFS_SIZE=512m`, com `PROMETHEUS_RETENTION_TIME=6h` e `PROMETHEUS_RETENTION_SIZE=512MB`.
- Alertmanager em `/alertmanager`, default `ALERTMANAGER_TMPFS_SIZE=64m`.
- Alloy em `/var/lib/alloy/data`, default `ALLOY_TMPFS_SIZE=64m`; o volume persistente `alloy-data` foi removido.
- Logs do SonarQube em `/opt/sonarqube/logs`, default `SONAR_LOGS_TMPFS_SIZE=256m`; dados principais e extensoes continuam persistentes.

PostgreSQL Ledger e Balance nao foram convertidos para `tmpfs` e nao receberam quota rigida em volume Docker nomeado. A decisao preserva diagnostico do fluxo ponta a ponta e evita configuracao nao portatil.

## Recomendacoes futuras

- Considerar imagens por digest apenas se a stack for promovida para ambiente compartilhado. Trade-off: mais reprodutibilidade contra manutencao maior e cuidado multi-arquitetura.
- Medir tempo de `docker compose build` antes/depois em maquinas reais para decidir se vale criar Dockerfiles ou contexts ainda mais especificos por servico.
- Se a observabilidade local evoluir para retencao historica real, trocar `tmpfs` por volumes dedicados e documentar a politica de retencao. O desenho atual e intencionalmente descartavel.

## Validacao sugerida

Validacoes executadas nesta atualizacao incremental:

- `docker compose -f compose.yaml config`
- `docker compose -f compose.yaml -f compose.observability.yaml --profile observability config`
- `docker compose -f compose.yaml -f compose.nginx.yaml config`
- `docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config`
- `docker compose -f compose.sonar.yaml --profile quality config`
- `docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth config`
- `docker run --rm -v <loki-config>:/etc/loki/loki-config.yaml:ro docker.io/grafana/loki:3.7.0 "-config.file=/etc/loki/loki-config.yaml" "-verify-config"`
- `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/docker-disk-report.ps1`
- `dotnet restore`
- `dotnet build ./LedgerService.slnx --configuration Release --no-restore`
- `dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings`
- `docker compose -f compose.yaml up -d --build`
- `GET http://localhost:5226/health`
- `GET http://localhost:5228/health`
- `docker compose -f compose.yaml down --remove-orphans`

Observacoes da validacao:

- Todos os `docker compose config` relevantes passaram.
- A configuracao do Loki foi validada pela propria imagem `grafana/loki:3.7.0`.
- `dotnet restore`, build Release e a suite completa de testes passaram. O build ainda exibe warnings C# ja existentes no projeto.
- A subida real do core funcional passou com APIs saudaveis em `/health`; o teardown usou `down --remove-orphans` sem `-v`, preservando volumes. O Docker encontrou containers orfaos de overlays locais anteriores e os removeu nesse teardown, tambem sem remover volumes.
- A execucao direta de `.ps1` pode ser bloqueada por Execution Policy da maquina; a validacao do script PowerShell foi feita com `powershell -NoProfile -ExecutionPolicy Bypass -File`, padrao seguro para execucao local pontual.

Comandos seguros de validacao:

```bash
docker compose -f compose.yaml config
docker compose -f compose.yaml -f compose.observability.yaml --profile observability config
docker compose -f compose.yaml -f compose.nginx.yaml config
docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth config
docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config
docker compose -f compose.sonar.yaml --profile quality config
docker compose build ledger-service ledger-worker balance-service balance-worker
docker compose -f compose.yaml -f compose.nginx.yaml build nginx-edge
docker compose up -d --build
```

Comandos de diagnostico de espaco seguros:

```bash
docker system df
docker volume ls
docker volume inspect <volume>
docker image ls
docker builder du
./scripts/docker-disk-report.sh
```

Comandos de limpeza geralmente seguros, mas ainda revisaveis:

```bash
docker builder prune
docker image prune
docker compose down --remove-orphans
./scripts/docker-clean-safe.sh
```

`docker builder prune` remove cache de build. `docker image prune` remove imagens sem tag/nao usadas. `docker compose down --remove-orphans` remove containers e rede do projeto, mas preserva volumes quando usado sem `-v`.

Comandos destrutivos para dados persistentes locais:

```bash
docker compose down -v
docker volume prune
docker volume rm <volume>
docker system prune --volumes
```

Use esses comandos somente quando os dados locais forem descartaveis. Eles podem remover bancos PostgreSQL, historico do SonarQube, chave local do Auth.Api legado e outros volumes Docker nao relacionados ao projeto, dependendo do comando.

## Resumo executivo

Principais ganhos esperados:

- Contexto de build menor e mais previsivel.
- Menos rebuild desnecessario quando a mudanca afeta apenas um servico.
- Imagens .NET um pouco menores por nao publicar apphost.
- Menor risco de certificados locais do Nginx serem enviados ao daemon Docker.
- Menor consumo local padrao ao manter observabilidade fora do `compose.yaml`.
- Menor crescimento de disco por `tmpfs` em dados descartaveis e rotacao de logs Docker.

Mudancas aplicadas:

- `.dockerignore` reforcado.
- `infra/nginx/.dockerignore` criado.
- Dockerfiles .NET ajustados para cache NuGet BuildKit com lock, copia seletiva de fontes e publish sem apphost.
- Observabilidade movida para `compose.observability.yaml`.
- Rotacao de logs Docker adicionada aos Compose principais.
- Scripts `docker-disk-report.*` e `docker-clean-safe.*` adicionados.

Pontos que precisam de validacao manual:

- Build completo das imagens em uma maquina com Docker-compatible API e acesso as imagens base.
- Subida real da stack com `docker compose up -d --build`.
- Tamanho e tempo de build antes/depois, se a equipe quiser quantificar ganho.

Riscos residuais:

- Volumes persistentes continuam podendo crescer com uso prolongado.
- Observabilidade e SonarQube continuam sendo os maiores consumidores opcionais de recursos.
- Limpezas com `-v`, `volume prune` ou `system prune --volumes` continuam destrutivas e devem ser manuais.
