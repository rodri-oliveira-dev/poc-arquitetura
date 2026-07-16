# Modo integrado para projetos Shared e pacotes NuGet - requisitos

## Diagnostico

Os servicos do repositorio consomem os pacotes `PocArquitetura.ApiDefaults`,
`PocArquitetura.ApplicationDefaults` e
`PocArquitetura.HttpResilienceDefaults` por `PackageReference`, com versoes
centralizadas em `Directory.Packages.props`. Esse e o modo consumidor normal e
representa o comportamento de um consumidor externo dos pacotes publicados.

O risco de drift aparece quando uma mudanca em `src/Shared` compila e testa a
solution `PocArquitetura.Shared.slnx`, mas ainda nao foi publicada no NuGet. Os
servicos continuam restaurando a ultima versao publicada e podem nao revelar
incompatibilidades entre a API publica atual de Shared e os consumidores reais
do proprio repositorio antes da publicacao.

`KafkaWorkerDefaults` e `ContainerHealthProbe` ja sao usados internamente como
projetos Shared e nao participam do fluxo de publicacao NuGet atual. O modo
integrado desta spec cobre os pacotes Shared que hoje sao consumidos como
NuGet pelos servicos.

## Objetivos

- Preservar o modo consumidor como padrao do repositorio.
- Permitir um modo integrado explicito por propriedade MSBuild.
- Testar consumidores contra o codigo atual de `src/Shared` sem publicar
  pacotes temporarios.
- Impedir referencia simultanea ao pacote Shared e ao projeto Shared no mesmo
  consumidor.
- Validar o modo integrado no CI antes de uma publicacao Shared.
- Manter Central Package Management, restore, build, testes e cobertura
  funcionando nos dois modos.
- Documentar execucao local, diagnostico de conflitos e relacao com os
  workflows.

## Nao objetivos

- Abandonar pacotes NuGet como mecanismo normal de consumo.
- Converter permanentemente consumidores para `ProjectReference`.
- Publicar pacotes durante validacao local ou de PR.
- Alterar namespaces, APIs publicas ou estrategia de versionamento.
- Incluir regras ou servicos de negocio nos pacotes Shared.
- Criar uma solution paralela apenas para o modo integrado.

## Requisitos funcionais

### RF-001 - Modo consumidor padrao

Sem propriedades adicionais, `dotnet restore`, `dotnet build` e `dotnet test`
devem usar os `PackageReference` existentes para os pacotes
`PocArquitetura.*` publicados.

### RF-002 - Modo integrado explicito

Quando `UseLocalSharedProjects=true` for passado ao MSBuild, cada consumidor de
um pacote Shared publicado deve passar a referenciar o projeto local
correspondente em `src/Shared`.

### RF-003 - Sem referencias duplicadas

No modo integrado, o build deve falhar se um projeto ainda mantiver
simultaneamente `PackageReference` e `ProjectReference` para o mesmo pacote
Shared.

### RF-004 - Cobertura de consumidores

O modo integrado deve funcionar com `PocArquitetura.slnx` e com as solutions
contextuais, permitindo compilar e testar APIs, Applications, Infrastructures,
Workers e testes que consomem os pacotes Shared.

### RF-005 - Publicacao preservada

O workflow de publicacao deve continuar empacotando os projetos Shared pela
solution dedicada `PocArquitetura.Shared.slnx`, calculando versao por
GitVersion e publicando com `--skip-duplicate`.

## Requisitos nao funcionais

- A solucao deve ser centralizada e legivel, evitando editar manualmente todos
  os `.csproj` consumidores.
- A logica MSBuild deve ser pequena, declarativa e localizada.
- Dependencias circulares entre Shared e servicos nao devem ser introduzidas.
- O CI deve deixar claro nos logs e no summary qual modo esta em execucao.
- A documentacao deve listar comandos reproduziveis para os dois modos.

## Criterios de aceite

- Mudancas em `src/Shared` disparam validacao agregada integrada no CI de PR.
- O modo padrao continua usando NuGet e as versoes centralizadas.
- O modo integrado substitui os pacotes Shared por `ProjectReference`.
- Nao ha referencias duplicadas entre pacote e projeto.
- A publicacao Shared continua idempotente e baseada no SHA aprovado pelo CI.
- Os comandos locais documentados restauram, compilam e testam nos dois modos.
