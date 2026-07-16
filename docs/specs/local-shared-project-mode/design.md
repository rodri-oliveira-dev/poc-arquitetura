# Modo integrado para projetos Shared e pacotes NuGet - design

## Desenho adotado

A solucao usa um `Directory.Build.targets` na raiz do repositorio. Esse arquivo
e importado depois dos `.csproj`, portanto consegue observar os
`PackageReference` declarados pelos consumidores e aplicar uma substituicao
centralizada quando `UseLocalSharedProjects=true`.

No modo consumidor, nenhum item e alterado: os projetos mantem
`PackageReference` para os pacotes `PocArquitetura.*` e Central Package
Management continua resolvendo as versoes em `Directory.Packages.props`.

No modo integrado, o target central:

1. identifica os `PackageReference` para pacotes Shared publicados;
2. remove esses `PackageReference`;
3. adiciona o `ProjectReference` local equivalente;
4. valida que pacote e projeto equivalente nao ficaram presentes ao mesmo
   tempo.

## Mapeamento de pacotes

| Pacote | Projeto local |
| --- | --- |
| `PocArquitetura.ApiDefaults` | `src/Shared/ApiDefaults/ApiDefaults.csproj` |
| `PocArquitetura.ApplicationDefaults` | `src/Shared/ApplicationDefaults/ApplicationDefaults.csproj` |
| `PocArquitetura.HttpResilienceDefaults` | `src/Shared/HttpResilienceDefaults/HttpResilienceDefaults.csproj` |

`PocArquitetura.ApiDefaults` depende de
`PocArquitetura.HttpResilienceDefaults`. No modo integrado, essa dependencia
continua sendo resolvida pelo `ProjectReference` ja existente dentro de
`ApiDefaults.csproj`.

## Guardrails MSBuild

O modo integrado deve falhar cedo se detectar a combinacao de pacote e projeto
equivalente no mesmo grafo avaliado. Isso evita um build aparentemente verde
com dois assemblies conceitualmente equivalentes.

A validacao e propositalmente limitada aos tres pacotes publicados que os
consumidores usam via NuGet. Projetos Shared internos que ja sao referenciados
por projeto permanecem inalterados.

## CI de pull request

O workflow `main-dotnet-ci` ja classifica mudancas em `src/Shared/**` como
impacto agregado e Shared. Quando isso acontecer, alem do modo consumidor
normal, o job deve executar um contexto adicional:

```text
integrated-shared -> ./PocArquitetura.slnx com UseLocalSharedProjects=true
```

Esse contexto valida restore, auditoria NuGet, build, testes, cobertura e o
guardrail de ausencia de duplicidade usando o codigo Shared ainda nao
publicado.

## Workflow de publicacao

O workflow `publish-shared-nuget` continua empacotando apenas
`PocArquitetura.Shared.slnx`. Antes de empacotar, ele executa uma validacao
integrada da solution agregadora com `UseLocalSharedProjects=true`, garantindo
que o SHA aprovado pelo CI ainda consegue compilar e testar consumidores contra
o codigo local de Shared antes de gerar artifacts de pacote.

O fluxo de versao, `dotnet pack`, Trusted Publishing e `--skip-duplicate`
permanece inalterado.

## Execucao local

Modo consumidor:

```powershell
dotnet restore ./PocArquitetura.slnx
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings
```

Modo integrado:

```powershell
dotnet restore ./PocArquitetura.slnx -p:UseLocalSharedProjects=true
dotnet build ./PocArquitetura.slnx --configuration Release --no-restore -p:UseLocalSharedProjects=true
dotnet test ./PocArquitetura.slnx --configuration Release --no-build --settings ./coverlet.runsettings -p:UseLocalSharedProjects=true
```

As mesmas propriedades podem ser usadas com uma solution contextual, por
exemplo `LedgerService.slnx`, quando a validacao local estiver restrita ao
contexto.

## Diagnostico de conflitos

Para investigar duplicidade, execute:

```powershell
dotnet msbuild ./PocArquitetura.slnx -t:Restore -p:UseLocalSharedProjects=true -v:minimal
```

Se houver erro de pacote e projeto simultaneos, procure no `.csproj` afetado
por `PackageReference Include="PocArquitetura.*"` ou por um `ProjectReference`
manual para `src/Shared`. O modo integrado deve ser a unica camada que faz essa
substituicao para consumidores.
