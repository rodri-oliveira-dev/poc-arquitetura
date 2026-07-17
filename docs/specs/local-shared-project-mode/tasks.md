# Modo integrado para projetos Shared e pacotes NuGet - tarefas

## Diagnostico

- [x] Inspecionar `Directory.Packages.props` e confirmar versoes centrais dos
  pacotes Shared publicados.
- [x] Inspecionar `Directory.Build.props` e ausencia de `.targets` raiz para
  substituicao centralizada.
- [x] Mapear consumidores de `PocArquitetura.ApiDefaults`,
  `PocArquitetura.ApplicationDefaults` e
  `PocArquitetura.HttpResilienceDefaults`.
- [x] Inspecionar `PocArquitetura.slnx`, `PocArquitetura.Shared.slnx` e
  workflows `dotnet.yml` e `publish-shared-nuget.yml`.

## Especificacao e design

- [x] Criar `requirements.md`.
- [x] Criar `design.md`.
- [x] Criar `tasks.md`.

## Implementacao

- [x] Adicionar `Directory.Build.targets` com modo
  `UseLocalSharedProjects=true`.
- [x] Adicionar guardrails para referencia duplicada de pacote e projeto.
- [x] Atualizar `main-dotnet-ci` para executar contexto integrado quando Shared
  for alterado ou quando o impacto global exigir ambos os contextos.
- [x] Atualizar `publish-shared-nuget` para validar consumidores em modo
  integrado antes do pack.
- [x] Atualizar documentacao de PR/release e comandos locais.

## Validacao

- [x] `dotnet tool restore`.
- [x] `dotnet restore ./PocArquitetura.slnx`.
- [x] `dotnet build ./PocArquitetura.slnx --configuration Release --no-restore`.
- [!] `dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings` estourou timeout local em `PaymentService.IntegrationTests`.
- [x] `dotnet restore ./PocArquitetura.slnx -p:UseLocalSharedProjects=true`.
- [x] `dotnet build ./PocArquitetura.slnx --configuration Release --no-restore -p:UseLocalSharedProjects=true`.
- [!] `dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings -p:UseLocalSharedProjects=true` nao foi concluido localmente porque a suite agregada travou/falhou antes em testes de integracao.
- [x] Validar uma solution contextual em modo integrado: `LedgerService.slnx` restaurou e compilou; Unit/Worker tests passaram.
- [x] Validar que nao ha referencias duplicadas no modo integrado via guardrail MSBuild e build integrado.
- [x] Validar que o workflow de publicacao preserva empacotamento sem publicar por revisao de fluxo: pack/publish mantidos, com validacao integrada adicionada antes do pack.

## Relatorio

- [x] Registrar diagnostico do drift.
- [x] Explicar desenho MSBuild adotado.
- [x] Listar comandos dos dois modos.
- [x] Listar workflows alterados.
- [x] Listar testes executados e limitacoes restantes.
