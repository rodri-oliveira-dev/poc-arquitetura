---
name: docker-compose-container-baseline
description: Use esta skill para criar ou revisar Dockerfiles e Docker Compose de aplicações .NET locais, incluindo segurança básica, cache de build, health checks e validação de imagens. Não use para deploy produtivo ou para adicionar containers sem necessidade.
---

# Objetivo

Manter um baseline local simples e seguro para containers, sem tratar Docker Compose como arquitetura produtiva.

## Quando usar

- Criar ou alterar Dockerfile de API, Worker ou aplicação web.
- Adicionar serviço ao `compose.yaml`.
- Alterar `.csproj`, `ProjectReference`, `Directory.Build.props`, `Directory.Packages.props` ou `global.json` com impacto no build da imagem.
- Alterar `.dockerignore` ou variáveis locais de containers.
- Criar validação de build de imagens no CI.

## Dockerfiles .NET

- Use multi-stage build.
- Use imagem `sdk` apenas no estágio de build.
- Use `aspnet` para API e `runtime` para Worker no estágio final.
- Copie `global.json`, `Directory.Build.props` e `Directory.Packages.props` antes do restore.
- Copie os `.csproj` necessários antes do restore.
- Não use `COPY . .` antes do restore.
- Publique com `--no-restore` e `/p:UseAppHost=false`.
- Execute como usuário não root.
- Não coloque segredos em `ARG`, `ENV` ou na imagem.
- Não use tag `latest`.

## Frontend

Quando o frontend for containerizado:

- mantenha build e runtime em estágios separados;
- não copie `node_modules` local;
- use lockfile no restore;
- não exponha variáveis secretas no bundle;
- trate URLs públicas como configuração, não segredo.

## Docker Compose

- Use Compose apenas para desenvolvimento e testes locais.
- Não use `container_name`.
- Faça bind de portas locais em `127.0.0.1` quando não houver necessidade de exposição na rede.
- Use health checks em serviços HTTP da aplicação.
- Use imagens com tags explícitas.
- Documente variáveis em `.env.local.example` quando esse arquivo existir.
- `depends_on` ordena startup, mas não substitui timeout, retry e readiness.
- Evite adicionar broker, cache, gateway ou stack de observabilidade antes de existir consumidor real.

## Processo

1. Identifique o executável e todas as referências de projeto.
2. Crie ou ajuste o Dockerfile seletivamente.
3. Atualize o Compose somente com os serviços necessários.
4. Valide a configuração:

```bash
docker compose config --quiet
```

5. Faça build real:

```bash
docker compose build
```

6. Suba os serviços e valide health/readiness quando existirem.
7. Revise tamanho, usuário, portas, secrets e contexto de build.
8. Atualize a documentação local.

## Restrições

- Não declarar que Compose representa produção.
- Não publicar imagens ou fazer push em registry sem pedido explícito.
- Não mascarar erro de restore com comandos duplicados ou `|| true`.
- Não usar imagem sem tag.
- Não adicionar hardening genérico sem risco ou validação concreta.
- Não introduzir infraestrutura apenas para demonstrar tecnologia.
