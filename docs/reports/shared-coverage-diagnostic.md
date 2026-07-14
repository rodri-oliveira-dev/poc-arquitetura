# Diagnostico de cobertura do Shared

Data da reproducao: 2026-07-14.

Branch: `test/improve-shared-coverage`.
Base analisada: `20387efce72244f36dc49e00434ef6b780c409d6` (`main` buscada via HTTPS).
Solution: `PocArquitetura.Shared.slnx`.

## Referencia do workflow

O job `87183515810` do run `29361812098` executou o contexto .NET impactado e publicou artefatos de testes/cobertura. O log confirma checkout em `20387efce72244f36dc49e00434ef6b780c409d6`. A falha final ocorreu no pos-processamento do SonarQube Cloud por autorizacao/projeto nao encontrado ao consultar o Quality Gate, depois de gerar e publicar os artefatos.

## Baseline local inicial

Comandos executados:

```powershell
dotnet restore PocArquitetura.Shared.slnx
dotnet build PocArquitetura.Shared.slnx --configuration Release --no-restore
dotnet test PocArquitetura.Shared.slnx --configuration Release --no-build --logger "trx;LogFilePrefix=test-results" --results-directory ./artifacts/shared-test-results --collect:"XPlat Code Coverage" --settings ./coverlet.runsettings
dotnet tool restore
dotnet tool run reportgenerator -reports:"./artifacts/shared-test-results/**/coverage.cobertura.xml" -targetdir:"./artifacts/shared-coverage-report" -reporttypes:"HtmlInline;JsonSummary;TextSummary"
```

Resultado inicial:

| Metrica | Valor |
| --- | ---: |
| Testes | 103 passed |
| Assemblies no Summary | 4 |
| Line coverage | 41.3% |
| Branch coverage | 40.2% |
| Method coverage | 49.3% |
| Covered / coverable lines | 339 / 819 |
| Uncovered lines | 480 |

Cobertura inicial por assembly:

| Assembly | Linhas | Branches | Metodos |
| --- | ---: | ---: | ---: |
| ApiDefaults | 14.5% | 22.2% | 24.5% |
| ApplicationDefaults | 100% | n/a | 100% |
| HttpResilienceDefaults | 97.5% | 78.5% | 100% |
| KafkaWorkerDefaults | 93.2% | 93.3% | 77.7% |

`ContainerHealthProbe` estava na solution e tinha testes, mas nao entrava no Summary inicial porque todo o codigo produtivo estava em `Program.cs`, arquivo excluido por `coverlet.runsettings`.

## Resultado apos infraestrutura e Prompt 2

Comandos executados:

```powershell
dotnet test tests/Shared/ApiDefaults.Tests/ApiDefaults.Tests.csproj --configuration Release --no-restore
dotnet test PocArquitetura.Shared.slnx --configuration Release --collect:"XPlat Code Coverage" --settings ./coverlet.runsettings --results-directory ./artifacts/shared-test-results-final
dotnet tool run reportgenerator -reports:"./artifacts/shared-test-results-final/**/coverage.cobertura.xml" -targetdir:"./artifacts/shared-coverage-report-final" -reporttypes:"HtmlInline;JsonSummary;TextSummary"
```

Resultado final desta etapa:

| Metrica | Valor |
| --- | ---: |
| Testes | 144 passed |
| Assemblies no Summary | 5 |
| Line coverage | 48.8% |
| Branch coverage | 55.6% |
| Method coverage | 60.4% |
| Covered / coverable lines | 410 / 840 |
| Uncovered lines | 430 |

Cobertura final por assembly:

| Assembly | Linhas | Branches | Metodos |
| --- | ---: | ---: | ---: |
| ApiDefaults | 23.5% | 36.4% | 39.6% |
| ApplicationDefaults | 100% | n/a | 100% |
| ContainerHealthProbe | 100% | 100% | 100% |
| HttpResilienceDefaults | 97.5% | 78.5% | 100% |
| KafkaWorkerDefaults | 93.2% | 93.3% | 77.7% |

## Matriz por classe

Prioridades:

- P0: seguranca, autenticacao ou tratamento global de erros.
- P1: middleware e comportamento HTTP.
- P2: DI, health checks, OpenAPI e observabilidade.
- P3: helpers simples, extensoes ou codigo de baixo risco.
- P4: codigo gerado ou tecnicamente nao testavel.

| Prioridade | Assembly | Classe | Linhas | Branches | Cobertas | Nao cobertas | Metodos |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| P0 | ApiDefaults | ApiDefaults.Extensions.JwtAuthenticationServiceCollectionExtensions | 2.4% | 0% | 3 | 120 | 12 |
| P2 | ApiDefaults | ApiDefaults.Extensions.ApiDefaultsServiceCollectionExtensions | 0% | 0% | 0 | 99 | 5 |
| P2 | ApiDefaults | ApiDefaults.Extensions.OpenTelemetryServiceCollectionExtensions | 0% | 0% | 0 | 54 | 4 |
| P2 | ApiDefaults | ApiDefaults.Extensions.HealthEndpointRouteBuilderExtensions | 0% | 0% | 0 | 53 | 2 |
| P2 | ApiDefaults | ApiDefaults.Extensions.ApiDefaultsApplicationExtensions | 6.5% | 25% | 3 | 43 | 4 |
| P2 | ApiDefaults | ApiDefaults.Swagger.OpenApiContractQualityDocumentFilter | 0% | 0% | 0 | 25 | 3 |
| P0 | ApiDefaults | ApiDefaults.Security.MerchantClaims | 0% | 0% | 0 | 14 | 2 |
| P0 | ApiDefaults | ApiDefaults.Authentication.ApiJwtAuthenticationOptions | 0% | n/a | 0 | 9 | 1 |
| P0 | ApiDefaults | ApiDefaults.Security.ScopeClaimAuthorizationExtensions | 66.6% | 100% | 8 | 4 | 2 |
| P3 | KafkaWorkerDefaults | PocArquitetura.KafkaWorkerDefaults.KafkaConsumerConfigFactory | 86.6% | n/a | 13 | 2 | 3 |
| P3 | KafkaWorkerDefaults | PocArquitetura.KafkaWorkerDefaults.KafkaOffsetResetParser | 71.4% | 50% | 5 | 2 | 1 |
| P3 | HttpResilienceDefaults | HttpResilienceDefaults.HttpClientResilienceOptions | 91.3% | 85.7% | 21 | 2 | 2 |
| P3 | HttpResilienceDefaults | HttpResilienceDefaults.HttpResilienceMetrics | 96.6% | 75% | 58 | 2 | 13 |
| P2 | HttpResilienceDefaults | HttpResilienceDefaults.HttpClientResilienceBuilderExtensions | 99% | 50% | 105 | 1 | 3 |
| P1 | ApiDefaults | ApiDefaults.Middlewares.CorrelationIdMiddleware | 100% | 100% | 12 | 0 | 3 |
| P0 | ApiDefaults | ApiDefaults.Middlewares.ExceptionHandlerResponseWriter | 100% | 87.5% | 18 | 0 | 3 |
| P0 | ApiDefaults | ApiDefaults.Middlewares.GlobalExceptionHandlerBase`2 | 100% | 100% | 20 | 0 | 3 |
| P1 | ApiDefaults | ApiDefaults.Middlewares.RequestBodySizeLimitMiddleware | 100% | n/a | 4 | 0 | 1 |
| P1 | ApiDefaults | ApiDefaults.Middlewares.SecurityHeadersMiddleware | 100% | n/a | 3 | 0 | 1 |
| P0 | ApiDefaults | ApiDefaults.Validation.ValidationErrorResponseFactory | 100% | 82.5% | 59 | 0 | 7 |
| P3 | ApplicationDefaults | ApplicationDefaults.Behaviors.ValidationBehavior`2 | 100% | n/a | 4 | 0 | 1 |
| P3 | ContainerHealthProbe | ContainerHealthProbe.ProbeTarget | 100% | 100% | 21 | 0 | 3 |
| P3 | HttpResilienceDefaults | HttpResilienceDefaults.HttpResilienceMetricsHandler | 100% | n/a | 16 | 0 | 2 |
| P3 | KafkaWorkerDefaults | PocArquitetura.KafkaWorkerDefaults.KafkaClientSecurity | 100% | 100% | 31 | 0 | 4 |
| P3 | KafkaWorkerDefaults | PocArquitetura.KafkaWorkerDefaults.KafkaConsumerLifecycle | 100% | n/a | 6 | 0 | 1 |

Dez classes com maior impacto negativo, por linhas nao cobertas:

1. `JwtAuthenticationServiceCollectionExtensions`: 120 linhas.
2. `ApiDefaultsServiceCollectionExtensions`: 99 linhas.
3. `OpenTelemetryServiceCollectionExtensions`: 54 linhas.
4. `HealthEndpointRouteBuilderExtensions`: 53 linhas.
5. `ApiDefaultsApplicationExtensions`: 43 linhas.
6. `OpenApiContractQualityDocumentFilter`: 25 linhas.
7. `MerchantClaims`: 14 linhas.
8. `ApiJwtAuthenticationOptions`: 9 linhas.
9. `ScopeClaimAuthorizationExtensions`: 4 linhas.
10. `KafkaOffsetResetParser`, `KafkaConsumerConfigFactory`, `HttpClientResilienceOptions` e `HttpResilienceMetrics`: 2 linhas cada.

## Matriz por metodo com gap

| Prioridade | Classe | Metodo | Linhas | Nao cobertas | Complexidade |
| --- | --- | --- | ---: | ---: | ---: |
| P0 | JwtAuthenticationServiceCollectionExtensions | `AddApiJwtBearerAuthentication(...)` | 0% | 30 | 1 |
| P0 | JwtAuthenticationServiceCollectionExtensions | `BuildJwksResilienceConfiguration(...)` | 0% | 17 | 2 |
| P0 | JwtAuthenticationServiceCollectionExtensions | `BuildJwtBearerEvents()` | 0% | 13 | 1 |
| P0 | JwtAuthenticationServiceCollectionExtensions | `BuildTokenValidationParameters(...)` | 0% | 12 | 1 |
| P0 | JwtAuthenticationServiceCollectionExtensions | `ReadOptions(...)` | 0% | 9 | 6 |
| P0 | JwtAuthenticationServiceCollectionExtensions | `ContainsAudience(...)` | 0% | 9 | 8 |
| P0 | JwtAuthenticationServiceCollectionExtensions | `ValidateTransport(...)` | 0% | 8 | 12 |
| P2 | ApiDefaultsServiceCollectionExtensions | `AddApiDefaults(...)` | 0% | 34 | 8 |
| P2 | ApiDefaultsServiceCollectionExtensions | `AddApiSwaggerDefaults(...)` | 0% | 20 | 1 |
| P2 | ApiDefaultsServiceCollectionExtensions | `AddCors(...)` | 0% | 16 | 1 |
| P2 | ApiDefaultsServiceCollectionExtensions | `AddVersioningAndExplorer(...)` | 0% | 15 | 1 |
| P2 | ApiDefaultsServiceCollectionExtensions | `AddRateLimiting(...)` | 0% | 14 | 2 |
| P2 | HealthEndpointRouteBuilderExtensions | `MapApiHealthEndpoints(...TState...)` | 0% | 43 | 2 |
| P2 | OpenTelemetryServiceCollectionExtensions | `AddApiOpenTelemetryDefaults(...)` | 0% | 25 | 1 |
| P2 | OpenTelemetryServiceCollectionExtensions | `AddConfiguredApiOpenTelemetryDefaults(...)` | 0% | 19 | 4 |
| P2 | OpenApiContractQualityDocumentFilter | `Apply(...)` | 0% | 9 | 8 |
| P2 | OpenApiContractQualityDocumentFilter | `.cctor()` | 0% | 9 | 1 |
| P2 | OpenApiContractQualityDocumentFilter | `MarkPublicOperation(...)` | 0% | 7 | 8 |
| P0 | MerchantClaims | `AllowsMerchant(...)` | 0% | 7 | 6 |
| P0 | MerchantClaims | `AuthorizedMerchantIds(...)` | 0% | 7 | 4 |

## Testes existentes

Projetos na solution:

| Projeto | Framework | xUnit | Mock | Fixtures/builders relevantes | Observacoes |
| --- | --- | --- | --- | --- | --- |
| `ApiDefaults.Tests` | `Microsoft.NET.Test.Sdk`, xUnit v3 | 3.2.2 | Moq | `HttpContextBuilder`, `MiddlewareTestDelegate`, `ServiceProviderTestFactory`, `TestLogger<T>` | Antes concentrava JWKs, correlation, swagger policy e validation; agora cobre middlewares e erro global. |
| `ApplicationDefaults.Tests` | `Microsoft.NET.Test.Sdk`, xUnit v3 | 3.2.2 | nenhum | sem fixture global | Testes pequenos e diretos para behavior de validacao. |
| `ContainerHealthProbe.Tests` | `Microsoft.NET.Test.Sdk`, xUnit v3 | 3.2.2 | nenhum | handlers HTTP fake locais | Bons asserts de SSRF/loopback/exit code; nao usa porta real. |
| `HttpResilienceDefaults.Tests` | `Microsoft.NET.Test.Sdk`, xUnit v3 | 3.2.2 | nenhum | `FakeHttpMessageHandler` | Ha `Task.Delay` controlado para circuito/resiliencia; risco moderado de flakiness, mas ligado ao comportamento testado. |
| `KafkaWorkerDefaults.Tests` | `Microsoft.NET.Test.Sdk`, xUnit v3 | 3.2.2 | nenhum | fakes internos pequenos | Testes sem dependencia de Kafka real. |

Achados de qualidade:

- Nao foram encontrados testes dependentes de ordem.
- Nao ha dependencia de portas TCP reais nos testes Shared.
- `JwksDocumentRetrieverTests` usa `TestServer` local e `Task.Delay` em cenarios de circuito; manter sob observacao por custo/flakiness.
- `HttpResilienceDefaults.Tests` tambem usa atrasos curtos em cenarios de circuito; justificavel para resiliencia, mas deve permanecer isolado.
- Nao foram removidos testes existentes; os testes novos adicionam asserts semanticos e nao apenas smoke de "nao lancou".

## Helpers adicionados

- `HttpContextBuilder`: cria `DefaultHttpContext` com metodo, path, HTTPS, body, content type, content length, headers, claims, services, `Response.Body` e cancellation token.
- `MiddlewareTestDelegate`: captura se o proximo middleware foi chamado e simula sucesso, excecao, escrita de resposta, status code e cancelamento.
- `ServiceProviderTestFactory`: cria `ServiceCollection`, `ServiceProvider`, `IOptions<T>`, options de autenticacao/autorizacao, health checks e `SwaggerGenOptions` para OpenAPI/Swagger.
- `TestLogger<T>`: captura nivel, `EventId`, mensagem, excecao e scopes de log.

## Classes tratadas no Prompt 2

- `SecurityHeadersMiddleware`: chamada ao proximo middleware, headers exatos, preservacao de headers existentes, HTTP/HTTPS, respostas 2xx/4xx/5xx e ausencia de `Cache-Control` por nao existir no middleware.
- `RequestBodySizeLimitMiddleware`: menor/exato/maior que limite, `Content-Length` ausente/invalido, streaming sem tamanho conhecido, metodo GET com `Content-Length` excedente, payload 413, content type e cancelamento.
- `CorrelationIdMiddleware`: ID ausente, valido, vazio/invalido/excessivo, multiplos valores, scope de logging e excecao do proximo middleware.
- `ExceptionHandlerResponseWriter`: validacao FluentValidation, JSON invalido, retorno falso, `ProblemDetails`, status, type, title, detail, traceId, serializacao falhando, resposta iniciada e cancelamento.
- `GlobalExceptionHandlerBase`: validacao, dominio, not found, conflito, nao autorizado, proibido, cancelamento, timeout e excecao desconhecida com resposta generica segura.
- `ValidationErrorResponseFactory`: multiplos erros, mesmo campo, chave vazia/global, mensagem vazia, ModelState e caminho JSON.

## Alteracoes produtivas

Foi feita uma unica alteracao produtiva para testabilidade/cobertura correta: `ContainerHealthProbe` teve `ProbeRunner` e `ProbeTarget` movidos de `Program.cs` para `ProbeRunner.cs`. O entrypoint continua chamando `ProbeRunner.RunAsync(args)`. Isso evita esconder codigo produtivo pelo `ExcludeByFile=**/Program.cs` do `coverlet.runsettings`, sem alterar contrato, portas, rede ou comportamento.

## Riscos e bugs encontrados

- `RequestBodySizeLimitMiddleware` protege somente requisicoes com `ContentLength` conhecido; streaming sem tamanho conhecido passa para o proximo middleware. Isso ficou documentado em teste, sem alterar comportamento.
- `RequestBodySizeLimitMiddleware` nao diferencia metodo HTTP: qualquer `Content-Length` acima do limite recebe 413, inclusive GET. Isso tambem ficou documentado.
- A propagacao do correlation ID para response header usa `Response.OnStarting`; `DefaultHttpContext` puro nao e suficiente para validar a emissao real do header como um servidor faria. Para aumentar confianca neste ponto, usar `TestServer` em um teste de integracao leve.
- `ApiDefaults` ainda domina o deficit por DI/auth/health/OpenAPI/observabilidade, nao pelos middlewares.

## Ordem recomendada para os proximos prompts

1. Prompt 3: P0 de autenticacao/autorizacao, com foco em `JwtAuthenticationServiceCollectionExtensions`, `MerchantClaims`, `ScopeClaimAuthorizationExtensions` e `ApiJwtAuthenticationOptions`.
2. Prompt 4: P2 de composicao HTTP, cobrindo `ApiDefaultsServiceCollectionExtensions`, `HealthEndpointRouteBuilderExtensions`, `ApiDefaultsApplicationExtensions`, `OpenApiContractQualityDocumentFilter` e OpenTelemetry.
3. Depois: lacunas P3 pequenas em `KafkaOffsetResetParser`, `KafkaConsumerConfigFactory` e branches restantes de `HttpResilienceDefaults`.

O caminho para 80% passa por `ApiDefaults`: ele tem 421 das 430 linhas nao cobertas finais do Shared.
