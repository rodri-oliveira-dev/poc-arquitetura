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

Valide a sintaxe efetiva do Compose base:

```powershell
docker compose --env-file .env.local.example -f compose.yaml config --quiet
```

Construa todas as imagens definidas no Compose base:

```powershell
docker compose --env-file .env.local.example -f compose.yaml build
```

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
- Teste estrutural: executa uma regra deterministica em `tools/ContainerBaselineValidator` e em `ContainerSecurityPolicyTests`.
- Build real: executa Docker/BuildKit de verdade no CI e falha quando a imagem deixa de construir.

Esses mecanismos se complementam. A skill orienta, o teste estrutural bloqueia regressões baratas de detectar, e o build real confirma o comportamento que o Compose executa.

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

O job executa:

```text
docker compose --env-file .env.local.example -f compose.yaml config --quiet
dotnet run --no-restore --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root .
dotnet run --no-restore --project ./tools/ContainerBaselineValidator/ContainerBaselineValidator.csproj -- --root . --self-test-invalid
docker compose --env-file .env.local.example -f compose.yaml build
```

O workflow nao faz login em registry, nao usa secrets reais e nao executa push de imagens.

## Limitacoes conhecidas

- O validador entende os Compose como estrutura YAML e normaliza `!reset` para leitura local; a fonte final de sintaxe continua sendo `docker compose config`.
- Health check e limites sao cobrados em serviços definidos no arquivo por `build` ou `image`; overlays parciais que apenas sobrescrevem variaveis continuam dependendo da validacao do Compose efetivo.
- A regra de imagem `aspnet` para APIs e `runtime` para workers assume nomes de projeto `*.Api` e `*.Worker`.
