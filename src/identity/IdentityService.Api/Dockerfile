# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/identity/IdentityService.Api/IdentityService.Api.csproj src/identity/IdentityService.Api/
COPY src/identity/IdentityService.Application/IdentityService.Application.csproj src/identity/IdentityService.Application/
COPY src/identity/IdentityService.Domain/IdentityService.Domain.csproj src/identity/IdentityService.Domain/
COPY src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj src/identity/IdentityService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/identity/IdentityService.Api/IdentityService.Api.csproj --disable-parallel \
    || dotnet restore src/identity/IdentityService.Api/IdentityService.Api.csproj --disable-parallel \
    || dotnet restore src/identity/IdentityService.Api/IdentityService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/identity/IdentityService.Api/ ./src/identity/IdentityService.Api/
COPY src/identity/IdentityService.Application/ ./src/identity/IdentityService.Application/
COPY src/identity/IdentityService.Domain/ ./src/identity/IdentityService.Domain/
COPY src/identity/IdentityService.Infrastructure/ ./src/identity/IdentityService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/identity/IdentityService.Api/IdentityService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
