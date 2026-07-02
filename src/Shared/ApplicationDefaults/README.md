# PocArquitetura.ApplicationDefaults

Defaults compartilhados para a camada Application em servicos .NET da POC `poc-arquitetura`.

Use este pacote quando uma camada Application baseada em MediatR e FluentValidation precisar reutilizar o comportamento padrao de validacao do pipeline.

## Instalacao

```bash
dotnet add package PocArquitetura.ApplicationDefaults
```

## Uso basico

```csharp
using ApplicationDefaults.Behaviors;

services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
```

## Recursos

- Integracao com MediatR por meio de `IPipelineBehavior<TRequest, TResponse>`.
- Execucao de validadores FluentValidation registrados para o request.
- Lancamento de `FluentValidation.ValidationException` quando houver falhas.
- Pipeline behavior reutilizavel para comandos e queries.

Esta e uma biblioteca de estudo/POC. Licenca MIT.
