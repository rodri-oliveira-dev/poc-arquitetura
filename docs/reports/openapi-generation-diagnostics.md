# Diagnostico de geracao OpenAPI

## Resumo

Este diagnostico analisou a configuracao atual de Swagger/OpenAPI das APIs principais `LedgerService.Api` e `BalanceService.Api`, o projeto compartilhado `ApiDefaults`, o manifesto de ferramentas .NET locais e a presenca de ferramentas Node.

As duas APIs usam Swashbuckle com versionamento por `Asp.Versioning` e geram hoje um documento identificado como `v1`. O endpoint HTTP documentado para runtime, quando Swagger esta habilitado, e `/swagger/v1/swagger.json`.

Nao foi implementada geracao automatizada nesta etapa.

## APIs analisadas

- `src/LedgerService.Api`
- `src/BalanceService.Api`
- `src/Shared/ApiDefaults`

`Auth.Api` nao foi alterada nem removida.

## Configuracao atual

### ApiDefaults

Arquivos principais:

- `src/Shared/ApiDefaults/Extensions/ApiDefaultsServiceCollectionExtensions.cs`
- `src/Shared/ApiDefaults/Extensions/ApiDefaultsApplicationExtensions.cs`

Configuracoes relevantes:

- `AddApiDefaults<TExceptionHandler>` registra exception handler, problem details, forwarded headers, limites, CORS, rate limiting, versionamento e API explorer.
- O versionamento usa `DefaultApiVersion = 1.0`, `AssumeDefaultVersionWhenUnspecified = true`, `ReportApiVersions = true` e `UrlSegmentApiVersionReader`.
- O API explorer usa `GroupNameFormat = "'v'VVV"` e `SubstituteApiVersionInUrl = true`.
- `AddApiSwaggerDefaults<TConfigureSwaggerOptions>` registra `IConfigureOptions<SwaggerGenOptions>`, `AddSwaggerGen`, annotations, `DocInclusionPredicate` por `GroupName`, XML comments quando o arquivo existe e security scheme para `X-Correlation-Id`.
- `UseApiSwaggerDefaults` habilita `UseSwagger` e `UseSwaggerUI` somente quando `Development` ou `Swagger:Enabled=true`.
- A Swagger UI usa `RoutePrefix = string.Empty`, mantendo UI na raiz quando habilitada.

### LedgerService.Api

Arquivos principais:

- `src/LedgerService.Api/Program.cs`
- `src/LedgerService.Api/Extensions/ApiCompositionExtensions.cs`
- `src/LedgerService.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/LedgerService.Api/Extensions/WebApplicationExtensions.cs`
- `src/LedgerService.Api/Swagger/ConfigureSwaggerOptions.cs`

Configuracoes relevantes:

- `Program.cs` chama `builder.WebHost.ConfigureApiDefaults()`, `AddLedgerApiComposition`, `UseApiSwagger`, `UseApiDefaults`, autenticacao, autorizacao e controllers.
- `AddLedgerApiComposition` registra defaults compartilhados, Swagger, OpenTelemetry opcional, JWT/JWKS, Application, EF Core/PostgreSQL e repositorios.
- `AddApiSwagger` adiciona operation filter de exemplos, security scheme `Idempotency-Key`, security scheme `Bearer` e `AuthorizeOperationFilter`.
- `ConfigureSwaggerOptions` cria documentos via `options.SwaggerDoc(description.GroupName, ...)`.
- Endpoints minimalistas `/health` e `/ready` usam `.WithGroupName("v1")`.
- Controllers usam rotas versionadas como `api/v{version:apiVersion}/...`.

### BalanceService.Api

Arquivos principais:

- `src/BalanceService.Api/Program.cs`
- `src/BalanceService.Api/Extensions/ApiCompositionExtensions.cs`
- `src/BalanceService.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/BalanceService.Api/Extensions/WebApplicationExtensions.cs`
- `src/BalanceService.Api/Swagger/ConfigureSwaggerOptions.cs`

Configuracoes relevantes:

- `Program.cs` tem composicao equivalente a `LedgerService.Api`.
- `AddBalanceApiComposition` registra defaults compartilhados, Swagger, OpenTelemetry opcional, JWT/JWKS, opcoes de limite da API, Application, EF Core/PostgreSQL e repositorios.
- `AddApiSwagger` adiciona operation filter de exemplos, security scheme `Bearer` e `AuthorizeOperationFilter`.
- `ConfigureSwaggerOptions` cria documentos via `options.SwaggerDoc(description.GroupName, ...)`.
- Endpoints minimalistas `/health` e `/ready` usam `.WithGroupName("v1")`.
- Controllers usam rotas versionadas como `api/v{version:apiVersion}/...`.

## Documentos Swagger identificados

Documento identificado nas duas APIs:

- `v1`

URLs em runtime quando Swagger esta habilitado:

- Ledger: `/swagger/v1/swagger.json`
- Balance: `/swagger/v1/swagger.json`

Arquivos alvo desejados para a proxima etapa:

- `docs/openapi/ledger.v1.json`
- `docs/openapi/balance.v1.json`

## Ferramentas locais e Node

Existe manifesto .NET local em:

- `dotnet-tools.json`

Ferramentas atuais no manifesto:

- `dotnet-ef`
- `dotnet-reportgenerator-globaltool`
- `dotnet-stryker`
- `gitversion.tool`
- `dotnet-sonarscanner`

`Swashbuckle.AspNetCore.Cli` nao esta instalado no manifesto local.

Comando executado para verificar disponibilidade atual:

```powershell
dotnet swagger --help
```

Resultado:

- falhou porque `dotnet-swagger` nao existe no manifesto local nem no PATH atual.

Nao foram encontrados `package.json`, `package-lock.json` ou outros locks Node comuns no repositorio. Nao ha sinal atual de dependencia Node para esta automacao.

## Swashbuckle CLI e geracao por assembly

O caminho esperado para geracao offline e usar o CLI do Swashbuckle contra os assemblies em Release, informando o documento `v1`.

Formato esperado dos comandos na proxima etapa, apos instalar a ferramenta local:

```powershell
dotnet swagger tofile --output docs/openapi/ledger.v1.json src/LedgerService.Api/bin/Release/net10.0/LedgerService.Api.dll v1
dotnet swagger tofile --output docs/openapi/balance.v1.json src/BalanceService.Api/bin/Release/net10.0/BalanceService.Api.dll v1
```

O repositorio ja referencia `Swashbuckle.AspNetCore` e `Swashbuckle.AspNetCore.Annotations` via Central Package Management. A ferramenta CLI ainda precisa ser adicionada em etapa futura, preferencialmente alinhada a versao dos pacotes Swashbuckle usados pelas APIs.

## Riscos encontrados

### Ambiente e validacao JWT/JWKS

As APIs validam configuracao JWT durante a composicao de DI:

- `Jwt:Issuer` obrigatorio
- `Jwt:Audience` obrigatorio
- `Jwt:JwksUrl` obrigatorio
- fora de `Development`, `Local` e `Test`, `RequireHttpsMetadata=false` e `JwksUrl` HTTP sao rejeitados

Os `appsettings.json` base usam `JwksUrl` HTTP para Keycloak local e `RequireHttpsMetadata=true`. Em um ambiente padrao de CI que suba como `Production`, a composicao pode falhar porque `JwksUrl` usa HTTP fora de ambiente local.

### Banco e EF Core

As duas APIs registram `DbContext` com Npgsql e exigem `ConnectionStrings:DefaultConnection`.

Durante a geracao por assembly, a expectativa e que o banco nao seja acessado se apenas o provider Swagger for construido. Mesmo assim, o host registra EF Core e repositorios. Qualquer mudanca futura que execute hosted services, migrations, readiness ou consultas durante startup pode quebrar a geracao em CI se nao houver banco.

### OpenTelemetry

OpenTelemetry esta desabilitado por padrao em `appsettings.json`.

Se `Observability:OpenTelemetry:Enabled=true` vazar para a geracao, exporters console ou OTLP podem ser registrados. Isso nao deve ser necessario para gerar contrato e pode criar ruido ou dependencia operacional.

### Swagger habilitado por ambiente

O gating de `UseSwagger` depende de `Development` ou `Swagger:Enabled=true`. Para o CLI por assembly, o ponto critico e a existencia dos servicos Swagger registrados, nao necessariamente a exposicao HTTP do middleware. Ainda assim, usar um ambiente dedicado evita confusao e reduz risco de pegar configuracoes de runtime.

### ValidateOnStart

`ApiDefaultsOptions` e `BalanceService.Api.Options.ApiLimitsOptions` usam `ValidateOnStart`.

Os valores atuais em `appsettings.json` atendem as validacoes. Se um ambiente de geracao sobrescrever essas secoes de forma incompleta, a geracao pode falhar antes de emitir o contrato.

### Ferramenta ausente

O comando `dotnet swagger` ainda nao esta disponivel. A automacao futura precisa adicionar `Swashbuckle.AspNetCore.Cli` ao manifesto local antes de tentar gerar os arquivos.

### Versao do SDK

O repositorio usa `global.json` com SDK `10.0.100`. A geracao por assembly deve ocorrer depois de `dotnet restore` e `dotnet build` com esse SDK, usando os assemblies `net10.0`.

## Ajustes necessarios antes da geracao automatizada

1. Adicionar `Swashbuckle.AspNetCore.Cli` ao manifesto local `.NET`, sem introduzir Node.
2. Definir ambiente de geracao deterministico, por exemplo `ASPNETCORE_ENVIRONMENT=OpenApi` e uma flag como `OPENAPI_GENERATION=true`.
3. Ajustar as APIs para tratar `OpenApi` ou `OPENAPI_GENERATION=true` como contexto seguro de composicao sem inicializacoes desnecessarias.
4. Garantir que a geracao nao dependa de Keycloak, JWKS remoto, PostgreSQL, Pub/Sub, Kafka, OpenTelemetry Collector ou portas HTTP.
5. Manter o documento como `v1` e escrever os arquivos em `docs/openapi/ledger.v1.json` e `docs/openapi/balance.v1.json`.
6. Criar validacao simples para detectar drift, comparando contratos gerados com os arquivos versionados.

## Recomendacao objetiva

O caminho mais simples e seguro e gerar os contratos com Swashbuckle CLI a partir dos assemblies Release das APIs, sem subir Docker, sem chamar endpoints HTTP e sem criar dependencia Node.

Na proxima etapa, a implementacao deve:

- instalar `Swashbuckle.AspNetCore.Cli` como ferramenta local no `dotnet-tools.json`;
- criar um ambiente de geracao isolado, preferencialmente `OpenApi`, ou usar `OPENAPI_GENERATION=true`;
- fazer pequenos guards na composicao para evitar validacoes de transporte JWT inadequadas e futuras inicializacoes externas durante a geracao;
- executar `dotnet restore`, `dotnet build --configuration Release --no-restore` e `dotnet swagger tofile` para `v1`;
- versionar os arquivos gerados em `docs/openapi/`.

Nao ha necessidade atual de package.json, package-lock.json, Docker, compose, SDK novo ou workflow novo para provar o caminho de geracao.

## Arquivos provavelmente alterados nas proximas etapas

- `dotnet-tools.json`
- `src/LedgerService.Api/Extensions/JwtAuthServiceCollectionExtensions.cs`
- `src/BalanceService.Api/Extensions/JwtAuthServiceCollectionExtensions.cs`
- `src/LedgerService.Api/Extensions/ApiCompositionExtensions.cs`
- `src/BalanceService.Api/Extensions/ApiCompositionExtensions.cs`
- `docs/openapi/ledger.v1.json`
- `docs/openapi/balance.v1.json`
- `docs/README.md`
- `.github/workflows/*`, somente quando a automacao em CI for solicitada

## Validacoes executadas

```powershell
dotnet restore ./LedgerService.slnx
```

Resultado:

- sucesso.

```powershell
dotnet build ./LedgerService.slnx --configuration Release --no-restore
```

Resultado:

- sucesso com warnings existentes;
- 0 erros.

```powershell
dotnet swagger --help
```

Resultado:

- falha esperada porque `dotnet-swagger` nao esta instalado.
