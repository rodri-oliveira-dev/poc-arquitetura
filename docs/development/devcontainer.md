# Dev Container opcional

Este repositorio possui uma configuracao inicial de Dev Container para padronizar o ambiente de desenvolvimento no VS Code sem substituir o fluxo local existente. O uso e opcional: quem abre o projeto diretamente no host pelo VS Code, Visual Studio, Rider ou terminal continua usando os mesmos comandos documentados em [desenvolvimento local](local-development.md).

## Como abrir sem Dev Container

Abra o repositorio normalmente no host e use os comandos atuais:

```bash
dotnet tool restore
dotnet restore ./LedgerService.slnx
dotnet build ./LedgerService.slnx --configuration Release --no-restore
dotnet test ./LedgerService.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Os scripts existentes continuam sendo a fonte principal para execucao local:

```powershell
./scripts/local/start-stack.ps1
```

```bash
./scripts/local/start-stack.sh
```

## Como abrir com Dev Container

No VS Code:

1. Instale a extensao Dev Containers.
2. Abra a pasta do repositorio ou o workspace `poc-arquitetura.code-workspace`.
3. Execute `Dev Containers: Reopen in Container`.

O Dev Container usa uma imagem de desenvolvimento .NET 10 compativel com o SDK definido em `global.json`, instala o suporte a Docker outside of Docker e recomenda as mesmas extensoes principais ja listadas em `.vscode/extensions.json`.

## O que ele faz

Durante a criacao do container, o `postCreateCommand` executa somente comandos seguros e nao destrutivos:

```bash
dotnet --info
dotnet tool restore
dotnet restore ./LedgerService.slnx
```

O recurso Docker outside of Docker instala a CLI Docker e encaminha o socket do Docker do host para permitir comandos como:

```bash
docker version
docker compose version
```

O Docker do host precisa estar disponivel. Se `docker version` falhar dentro do Dev Container, valide se o Docker-compatible runtime local esta iniciado e acessivel pelo host antes de recriar ou reabrir o container.

## O que ele nao faz automaticamente

O Dev Container nao sobe a stack local, nao aplica migrations, nao executa testes, nao inicia observabilidade e nao roda load tests automaticamente. Essas acoes continuam manuais e intencionais.

Para subir a stack local, use os scripts existentes:

```powershell
./scripts/local/start-stack.ps1
```

```bash
./scripts/local/start-stack.sh
```

Para observabilidade, migrations e testes de carga, siga os guias especificos em [desenvolvimento local](local-development.md), [observabilidade](../observability.md) e [k6 load tests](../../loadtests/k6/README.md).

## Validacao do ambiente

Dentro do Dev Container, valide o ambiente com:

```bash
dotnet --info
dotnet tool restore
dotnet restore ./LedgerService.slnx
docker version
docker compose version
```

Build e testes continuam usando os mesmos comandos do host:

```bash
dotnet build ./LedgerService.slnx --configuration Release
dotnet test ./LedgerService.slnx --configuration Release --settings ./coverlet.runsettings
```
