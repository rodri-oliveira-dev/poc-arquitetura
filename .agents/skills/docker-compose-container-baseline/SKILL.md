---
name: docker-compose-container-baseline
description: Use esta skill para criar, revisar ou evoluir Dockerfiles, Docker Compose, validadores estruturais e jobs de CI de build de imagens desta POC .NET. Nao use para deploy produtivo, publicacao em registry ou mudancas funcionais nos servicos.
---

# Objetivo

Manter um baseline local e automatizado para Dockerfiles, Docker Compose e build real de imagens da POC, reduzindo regressao em cache, seguranca, port binding, health checks e referencias de projetos .NET.

Esta skill orienta manutencao de containers locais. Ela nao transforma o `compose.yaml` em manifest produtivo.

# Quando usar

- Criar ou alterar Dockerfile de `*.Api` ou `*.Worker`.
- Adicionar executavel novo ao `compose.yaml`.
- Alterar `.csproj`, `ProjectReference`, `Directory.Build.props`, `Directory.Packages.props` ou `global.json` com impacto em container build.
- Alterar `compose*.yaml`, `.dockerignore` ou scripts de validacao de containers.
- Ajustar workflow que executa `docker compose config`, validadores estruturais ou build real das imagens.

# Quando nao usar

- Deploy em Cloud Run, Kubernetes, VM ou outro ambiente produtivo.
- Publicar imagem, taguear release ou fazer push para registry.
- Corrigir regra de negocio, endpoint HTTP, evento, persistencia ou mensageria sem impacto nos containers.
- Adicionar hardening generico sem requisito, validacao ou risco concreto.

# Principios obrigatorios

- Um Dockerfile por executavel publicado, ao lado do `.csproj` de `*.Api` ou `*.Worker`.
- Dockerfiles devem usar multi-stage build.
- APIs usam imagem final `mcr.microsoft.com/dotnet/aspnet:<tag>`.
- Workers usam imagem final `mcr.microsoft.com/dotnet/runtime:<tag>`.
- SDK fica apenas no estagio de build.
- `global.json` deve ser copiado antes do restore.
- `Directory.Packages.props` e `Directory.Build.props` devem ser copiados antes do restore.
- Todos os `ProjectReference` diretos e transitivos devem estar disponiveis antes do restore.
- A copia de projetos deve ser seletiva; nao use `COPY . .` antes do restore.
- Use cache BuildKit para NuGet em `dotnet restore` e `dotnet publish`.
- `dotnet publish` deve usar `--no-restore`.
- `dotnet publish` deve usar `/p:UseAppHost=false`.
- A imagem final deve executar sem root.
- O usuario final aprovado para as imagens .NET deste repositorio e `USER $APP_UID`.
- Copias do estagio final devem usar `COPY --chown=$APP_UID:0`.
- Nenhuma secret deve entrar por `ARG`, `ENV`, imagem ou contexto de build.
- Nenhuma imagem pode ficar sem tag ou digest explicito.
- A tag `latest` e proibida.
- Portas publicadas localmente devem fazer bind em `127.0.0.1`.
- `container_name` e proibido.
- Servicos HTTP da aplicacao precisam de health check.
- `depends_on` pode ordenar startup local, mas nao substitui timeout, retry, readiness, idempotencia ou resiliencia da aplicacao.
- Compose local nao e diretamente promovivel para producao.
- CI deve validar as imagens com build real; teste estrutural sozinho nao prova que restore, publish e runtime fecham.

# Checklist para adicionar um novo executavel

1. Crie o projeto em `src/<contexto>/<Nome>.Api` ou `src/<contexto>/<Nome>.Worker`.
2. Adicione um Dockerfile ao lado do `.csproj`.
3. Use `aspnet` para API e `runtime` para Worker no estagio final.
4. Copie `Directory.Packages.props`, `Directory.Build.props` e `global.json` antes do restore.
5. Copie o `.csproj` do executavel e todos os `ProjectReference` diretos e transitivos antes do restore.
6. Copie apenas os diretorios de codigo necessarios antes do publish.
7. Publique com `--no-restore` e `/p:UseAppHost=false`.
8. Rode com `USER $APP_UID` e use `COPY --chown=$APP_UID:0`.
9. Adicione o servico ao Compose com build, limites locais, redes, variaveis e health check quando for HTTP.
10. Rode o validador estrutural e o build real de Compose.

# Checklist para alterar um `.csproj`

1. Verifique se o projeto e executavel ou dependencia de executavel containerizado.
2. Se adicionar/remover `ProjectReference`, atualize os Dockerfiles que publicam projetos dependentes.
3. Confirme que o `.csproj` referenciado e copiado antes do restore.
4. Confirme que o diretorio de codigo referenciado e copiado antes do publish.
5. Rode `dotnet run --project tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .`.
6. Rode pelo menos os testes arquiteturais afetados.

# Checklist para adicionar um novo `ProjectReference`

1. Identifique todos os executaveis que dependem direta ou transitivamente do projeto alterado.
2. Para cada Dockerfile afetado, adicione `COPY <referencia>.csproj <diretorio>/` antes do restore.
3. Adicione `COPY <diretorio>/ ./<diretorio>/` antes do publish.
4. Nao resolva falha de restore com `||`, restore duplicado ou `COPY . .` amplo.
5. Rode o validador estrutural; a mensagem deve apontar o projeto ausente e a origem da dependencia se algo faltar.

# Checklist para alterar o Compose

1. Rode `docker compose -f compose.yaml config --quiet`.
2. Garanta imagens com tag explicita e sem `latest`.
3. Nao use `container_name`.
4. Publique portas somente em `127.0.0.1`.
5. Declare variaveis de portas em `.env.local.example`.
6. Se o servico HTTP da aplicacao for definido por build, mantenha health check para `/ready`.
7. Preserve limites locais de `cpus`, `memory` e `pids` nos servicos cobertos pela politica.
8. Se usar `build.dockerfile` ou `build.target`, confirme que o caminho e o stage existem.
9. Rode o validador estrutural.
10. Rode build real das imagens do Compose.

# Procedimento de validacao

```powershell
dotnet run --project tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .
dotnet run --project tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root . --self-test-invalid
docker compose -f compose.yaml config --quiet
docker compose -f compose.yaml build
dotnet test ./LedgerService.slnx --configuration Release --filter FullyQualifiedName~ContainerSecurityPolicyTests
```

No CI, a validacao de container deve:

- executar `docker compose config --quiet`;
- executar o validador estrutural;
- construir todas as imagens definidas pelo Compose;
- usar cache de build quando apropriado;
- nao usar secrets reais;
- nao publicar imagens e nao fazer push para registry.

# Antipadroes e exemplos concretos

## Restore mascarado

```dockerfile
RUN dotnet restore src/ledger/LedgerService.Api/LedgerService.Api.csproj || dotnet restore src/ledger/LedgerService.Api/LedgerService.Api.csproj
```

Corrija as copias de `.csproj` e `ProjectReference` antes do restore.

## Projeto referenciado ausente antes do restore

```dockerfile
COPY src/ledger/LedgerService.Api/LedgerService.Api.csproj src/ledger/LedgerService.Api/
RUN dotnet restore src/ledger/LedgerService.Api/LedgerService.Api.csproj
```

Se a API referencia Application, Domain, Infrastructure ou Shared, copie todos os `.csproj` antes do restore.

## Imagem final errada

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS final
```

Use `aspnet` para API e `runtime` para Worker no estagio final.

## Porta aberta no host

```yaml
ports:
  - "${LEDGER_SERVICE_HOST_PORT:-5226}:8080"
```

Use:

```yaml
ports:
  - "127.0.0.1:${LEDGER_SERVICE_HOST_PORT:-5226}:8080"
```

## `depends_on` tratado como resiliencia

```yaml
depends_on:
  postgres-db:
    condition: service_healthy
```

Isso ajuda a ordem local, mas a aplicacao ainda precisa lidar com indisponibilidade, timeout, retry e readiness.

# Referencias oficiais consultadas

- Docker Build best practices: https://docs.docker.com/build/building/best-practices/
- Docker multi-stage builds: https://docs.docker.com/build/building/multi-stage/
- Docker build cache optimization: https://docs.docker.com/build/cache/optimize/
- Docker build secrets: https://docs.docker.com/build/building/secrets/
- Dockerfile `HEALTHCHECK`: https://docs.docker.com/reference/dockerfile/#healthcheck
- Docker Compose Specification: https://compose-spec.github.io/compose-spec/spec.html
- Docker Compose services reference: https://docs.docker.com/reference/compose-file/services/
- Docker Compose secrets: https://docs.docker.com/compose/how-tos/use-secrets/
- Microsoft .NET container images: https://learn.microsoft.com/en-us/dotnet/core/docker/container-images
- Microsoft .NET container app tutorial: https://learn.microsoft.com/en-us/dotnet/core/docker/build-container
- Microsoft .NET non-root `app` user and `APP_UID`: https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/app-user
- GitHub Actions publishing Docker images: https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images
- Docker GitHub Actions build cache: https://docs.docker.com/build/ci/github-actions/cache/
