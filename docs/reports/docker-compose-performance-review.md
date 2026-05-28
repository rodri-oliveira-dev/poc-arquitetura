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
| Performance local | Observabilidade completa inclui varios servicos com CPU/memoria limitados, mas ainda pesada para uso diario. | Ja mitigado por profile `observability`; sem mudanca. |
| Performance local | Loki grava dados no filesystem do container enquanto esta rodando; isso pode crescer em sessoes longas. | Recomendacao futura, sem alteracao automatica. |
| Organização do Compose | Stack principal, overlays de Nginx, k6, SonarQube e Auth legado estao separados, mas nao ha relatorio operacional consolidado de volumes. | Documentado neste relatorio. |

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
- `sonar-postgres-data`, `sonarqube-data`, `sonarqube-extensions`, `sonarqube-logs`: usados pelo SonarQube local opcional.
- `alloy-data`: usado pelo Alloy local no profile `observability`.

Nao foram removidos nem convertidos para `tmpfs`, porque isso poderia apagar estado local, quebrar diagnostico ou surpreender quem espera reuso entre execucoes.

## Recomendacoes futuras

- Avaliar retencao/limite explicito para Loki em sessoes longas de observabilidade local. Trade-off: menor consumo de disco contra menos historico de logs.
- Avaliar mover logs do SonarQube para armazenamento temporario somente se a equipe aceitar perder logs entre reinicios. Trade-off: menos volume persistente contra pior diagnostico pos-falha.
- Considerar imagens por digest apenas se a stack for promovida para ambiente compartilhado. Trade-off: mais reprodutibilidade contra manutencao maior e cuidado multi-arquitetura.
- Medir tempo de `docker compose build` antes/depois em maquinas reais para decidir se vale criar Dockerfiles ou contexts ainda mais especificos por servico.
- Se o fluxo diario nao exigir workers sempre ativos, avaliar profile opcional para workers. Trade-off: menor consumo local contra risco de quebrar cenarios que dependem de Outbox/Kafka por padrao.

## Validacao sugerida

Validacoes executadas nesta revisao:

- `docker compose config`
- `docker compose -f compose.yaml -f compose.nginx.yaml config`
- `docker compose -f compose.yaml -f compose.auth-legacy.yaml --profile legacy-auth config`
- `docker compose -f compose.yaml -f compose.k6.yaml --profile k6 config`
- `docker compose -f compose.sonar.yaml --profile quality config`
- `dotnet test ./tests/LedgerService.UnitTests/LedgerService.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Architecture --no-restore`
- `docker compose -f compose.yaml -f compose.auth-legacy.yaml -f compose.nginx.yaml build ledger-service ledger-worker balance-service balance-worker auth-api nginx-edge`

Observacoes da validacao:

- Os builds das imagens alteradas concluiram com sucesso.
- O contexto de build informado pelo Docker ficou pequeno nas imagens .NET e em `54B` para o Nginx, confirmando que `infra/nginx/.dockerignore` deixou de enviar certificados e conteudo montado em runtime.
- Permanecem warnings C# ja existentes em filtros Swagger/testes e uma mensagem `fatal: not a git repository` durante o publish do `BalanceService.Api`, causada pelo target local de hooks ignorar a ausencia de `.git` dentro do contexto Docker. A mensagem nao falhou o build.

Comandos seguros de validacao:

```bash
docker compose config
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
```

Comandos de limpeza geralmente seguros, mas ainda revisaveis:

```bash
docker builder prune
docker image prune
docker compose down --remove-orphans
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

Mudancas aplicadas:

- `.dockerignore` reforcado.
- `infra/nginx/.dockerignore` criado.
- Dockerfiles .NET ajustados para cache NuGet BuildKit com lock, copia seletiva de fontes e publish sem apphost.

Pontos que precisam de validacao manual:

- Build completo das imagens em uma maquina com Docker-compatible API e acesso as imagens base.
- Subida real da stack com `docker compose up -d --build`.
- Tamanho e tempo de build antes/depois, se a equipe quiser quantificar ganho.

Riscos residuais:

- Volumes persistentes continuam podendo crescer com uso prolongado.
- Observabilidade e SonarQube continuam sendo os maiores consumidores opcionais de recursos.
- Limpezas com `-v`, `volume prune` ou `system prune --volumes` continuam destrutivas e devem ser manuais.
