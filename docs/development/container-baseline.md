# Baseline de Dockerfiles e Docker Compose

Este guia descreve as validacoes automáticas para evitar regressao em Dockerfiles, Compose e build de imagens da stack local.

O Compose deste repositorio e um laboratorio local. Ele nao deve ser promovido diretamente para producao.

## Como rodar localmente

Execute a validacao estrutural:

```powershell
dotnet run --project tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .
```

Execute o teste negativo controlado do proprio validador:

```powershell
dotnet run --project tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root . --self-test-invalid
```

Execute os testes automatizados dedicados do validador:

```powershell
dotnet test tests/tooling/ContainerBaselineValidator.Tests/ContainerBaselineValidator.Tests.csproj --configuration Release
```

Valide a sintaxe efetiva de todas as combinacoes Compose suportadas:

```powershell
./scripts/quality/containers/validate-compose-configs.ps1
```

Em Linux/macOS ou no GitHub Actions:

```bash
./scripts/quality/containers/validate-compose-configs.sh
```

Construa todas as imagens definidas no Compose base:

```powershell
docker compose --env-file .env.local.example -f compose.yaml build
```

## Validacao local no pre-push

O hook `.githooks/pre-push` executa uma pre-validacao leve quando o diff contem arquivos de container:

- Dockerfiles (`Dockerfile`, `**/Dockerfile`, `Dockerfile.*`, `**/Dockerfile.*`) executam uma unica vez o `ContainerBaselineValidator`;
- arquivos Compose (`compose.yaml`, `compose.yml`, `compose.*.yaml`, `compose.*.yml` em qualquer diretorio) executam uma unica vez o script oficial `scripts/quality/containers/validate-compose-configs.sh`;
- mudancas em `tools/ContainerBaselineValidator/**`, `.dockerignore`, `global.json`, `Directory.Build.props` ou `Directory.Packages.props` tambem executam o validador estrutural;
- mudancas em `scripts/quality/containers/validate-compose-configs.*`, `scripts/lib/common.*` ou `.env.local.example` executam a validacao oficial de Compose.

O hook nao mantem uma segunda matriz de Compose. A lista oficial de combinacoes permanece nos scripts `validate-compose-configs.*`.

O `pre-push` bloqueia o envio quando o `ContainerBaselineValidator` encontra uma violacao real ou quando `docker compose config --quiet` falha para alguma combinacao suportada. Ele nao faz build completo de imagens, nao executa `docker compose up`, nao sobe containers, nao faz push de imagens e nao executa Trivy completo.

Se `docker`/`docker compose` nao estiver disponivel localmente, o hook informa que a validacao Compose nao foi executada e lembra que o gate bloqueante continua no Pull Request/GitHub Actions. Se o SDK .NET nao estiver disponivel, o mesmo comportamento vale para o `ContainerBaselineValidator`. Nesses casos, a etapa nao e apresentada como aprovada.

## Como adicionar um novo executavel

1. Crie o projeto em `src/<contexto>/<Nome>.Api` ou `src/<contexto>/<Nome>.Worker`.
2. Crie um Dockerfile ao lado do `.csproj`.
3. Use multi-stage build com SDK apenas no estagio de build.
4. Use `aspnet` como imagem final de API e `runtime` como imagem final de Worker.
5. Copie `Directory.Packages.props`, `Directory.Build.props` e `global.json` antes do restore.
6. Copie todos os `.csproj` necessarios antes do restore.
7. Copie os diretorios de codigo necessarios antes do publish.
8. Publique com `dotnet publish --no-restore /p:UseAppHost=false`.
9. Rode a imagem final com `USER $APP_UID` e `COPY --chown=$APP_UID:0`.
10. Adicione o servico ao Compose com limites locais de CPU, memoria e PIDs.
11. Se for HTTP, publique porta em `127.0.0.1`, declare a variavel em `.env.local.example` e adicione health check.

## Como atualizar Dockerfile apos `ProjectReference`

Quando um `.csproj` passa a referenciar outro projeto, todos os Dockerfiles que publicam executaveis dependentes precisam enxergar essa referencia antes do restore.

Adicione uma copia do `.csproj` antes do `dotnet restore`:

```dockerfile
COPY src/Shared/NovoProjeto/NovoProjeto.csproj src/Shared/NovoProjeto/
```

Adicione a copia do diretorio antes do `dotnet publish`:

```dockerfile
COPY src/Shared/NovoProjeto/ ./src/Shared/NovoProjeto/
```

Nao resolva esse tipo de falha com `COPY . .` amplo, restore repetido, fallback com `||` ou remoção de testes.

## Por que build real no CI e obrigatorio

O validador estrutural encontra drift previsivel: projeto referenciado ausente antes do restore, imagem final errada, `latest`, `container_name`, porta aberta no host, health check ausente e Dockerfile inexistente.

Ele nao substitui build real. Apenas `docker compose build` prova que o contexto de build, restore NuGet, publish, cache BuildKit, `COPY`, imagens base e entrypoints ainda fecham juntos no runner.

## Skill, teste estrutural e build real

- Skill: documenta o padrao para humanos e agentes Codex em `.agents/skills/docker-compose-container-baseline/SKILL.md`.
- Teste estrutural: executa uma regra deterministica em `tools/ContainerBaselineValidator` e cobre fixtures isoladas em `tests/tooling/ContainerBaselineValidator.Tests`.
- Build real: executa Docker/BuildKit de verdade no CI e falha quando a imagem deixa de construir.

Esses mecanismos se complementam. A skill orienta, o teste estrutural bloqueia regressões baratas de detectar, e o build real confirma o comportamento que o Compose executa.

## Validacao efetiva dos overlays Compose

O script `scripts/quality/containers/validate-compose-configs.sh` executa `docker compose config --quiet` para cada combinacao oficialmente suportada. Ele usa `.env.local.example` por padrao, aceita outro arquivo com `--env-file <arquivo>` ou `COMPOSE_ENV_FILE=<arquivo>`, injeta apenas placeholders nao secretos necessarios para a interpolacao do overlay Cloud SQL e nao inicializa containers.

Matriz validada:

| Nome | Arquivos | Profiles | Papel |
| --- | --- | --- | --- |
| `stack-base` | `compose.yaml` | nenhum | Core funcional local com Kafka padrao. |
| `stack-base-kafka-alias` | `compose.yaml`, `compose.kafka.yaml` | nenhum | Alias compativel; Kafka ja esta no Compose base. |
| `stack-observability` | `compose.yaml`, `compose.observability.yaml` | `observability` | Core funcional com Jaeger, Collector, Prometheus, Loki, Alloy, Alertmanager e Grafana. |
| `stack-nginx` | `compose.yaml`, `compose.nginx.yaml` | nenhum | Borda local Nginx com duas instancias do Ledger. |
| `stack-full-nginx-observability` | `compose.yaml`, `compose.observability.yaml`, `compose.nginx.yaml` | `observability` | Stack completa usada por `scripts/local/start-full-stack.*`. |
| `stack-k6` | `compose.yaml`, `compose.k6.yaml` | `k6` | Overlay k6 padrao para cenarios de carga Kafka. |
| `stack-kafka-k6` | `compose.yaml`, `compose.kafka.yaml`, `compose.k6.yaml` | `k6` | Caminho k6 full-stack que aplica o alias Kafka explicito. |
| `stack-cloudsql` | `compose.yaml`, `compose.cloudsql.yaml` | nenhum | Smoke manual/local com Cloud SQL Auth Proxy. |
| `stack-sonar` | `compose.yaml`, `compose.sonar.yaml` | `quality` | SonarQube local junto da rede Compose do projeto. |
| `stack-pubsub-legacy` | `compose.yaml`, `compose.pubsub.yaml` | `legacy-pubsub` | Provider alternativo/legado Pub/Sub. |

Overlays alternativos e incompatibilidades deliberadas:

- `compose.pubsub.yaml` e alternativo ao Kafka padrao para Ledger/Balance e nao e combinado com `compose.kafka.yaml`.
- `compose.pubsub.yaml` nao e combinado com `compose.k6.yaml`; os runners k6 versionados usam Kafka.
- `compose.cloudsql.yaml` e um smoke manual/local de banco e nao e combinado com Pub/Sub, k6 ou Sonar.
- `compose.sonar.yaml` valida o ambiente de qualidade local; ele nao participa da stack funcional, k6 ou Cloud SQL.
- O build real das imagens continua restrito a `docker compose --env-file .env.local.example -f compose.yaml build`.

## CI

O workflow `.github/workflows/container-baseline.yml` roda em pull requests e pushes para `main` quando arquivos relevantes mudam:

- `compose*.yaml`;
- `.dockerignore`;
- `global.json`;
- `Directory.Build.props`;
- `Directory.Packages.props`;
- Dockerfiles;
- `.csproj`;
- validador;
- skill;
- workflow relacionado.

Mudancas isoladas em `src/**/Dockerfile` pertencem a este fluxo de container e nao ao workflow OpenAPI. Um Dockerfile de API, sozinho, nao altera contrato HTTP versionado; por isso ele aciona `container-baseline` e `infrastructure-security`, mas nao executa restore/build/geracao/lint/diff de OpenAPI.

O job executa:

```text
dotnet run --no-restore --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .
dotnet run --no-restore --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root . --self-test-invalid
./scripts/quality/containers/validate-compose-configs.sh
docker compose --env-file .env.local.example -f compose.yaml build
```

O workflow nao faz login em registry, nao usa secrets reais e nao executa push de imagens.

## Limitacoes conhecidas

- O validador entende os Compose como estrutura YAML e normaliza `!reset` para leitura local; a fonte final de sintaxe continua sendo `docker compose config`.
- Health check e limites sao cobrados em serviços definidos no arquivo por `build` ou `image`; overlays parciais que apenas sobrescrevem variaveis continuam dependendo da validacao do Compose efetivo.
- A regra de imagem `aspnet` para APIs e `runtime` para workers assume nomes de projeto `*.Api` e `*.Worker`.
- `docker compose config --quiet` valida o merge e a interpolacao dos arquivos, mas nao prova startup, conectividade externa, existencia de credenciais Cloud SQL, readiness dos servicos nem compatibilidade operacional de volumes locais.
