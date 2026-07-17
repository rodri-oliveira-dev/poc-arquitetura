# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj src/Shared/ContainerHealthProbe/
COPY src/Shared/HttpResilienceDefaults/HttpResilienceDefaults.csproj src/Shared/HttpResilienceDefaults/
COPY src/Shared/ApiDefaults/ApiDefaults.csproj src/Shared/ApiDefaults/
COPY src/identity/IdentityService.Api/IdentityService.Api.csproj src/identity/IdentityService.Api/
COPY src/identity/IdentityService.Application/IdentityService.Application.csproj src/identity/IdentityService.Application/
COPY src/identity/IdentityService.Domain/IdentityService.Domain.csproj src/identity/IdentityService.Domain/
COPY src/identity/IdentityService.Infrastructure/IdentityService.Infrastructure.csproj src/identity/IdentityService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/identity/IdentityService.Api/IdentityService.Api.csproj --disable-parallel
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj --disable-parallel

# Agora copia o restante
COPY src/identity/IdentityService.Api/ ./src/identity/IdentityService.Api/
COPY src/identity/IdentityService.Application/ ./src/identity/IdentityService.Application/
COPY src/identity/IdentityService.Domain/ ./src/identity/IdentityService.Domain/
COPY src/identity/IdentityService.Infrastructure/ ./src/identity/IdentityService.Infrastructure/
COPY src/Shared/ContainerHealthProbe/ ./src/Shared/ContainerHealthProbe/
COPY src/Shared/HttpResilienceDefaults/ ./src/Shared/HttpResilienceDefaults/
COPY src/Shared/ApiDefaults/ ./src/Shared/ApiDefaults/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/identity/IdentityService.Api/IdentityService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj \
    -c Release \
    -o /healthprobe \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build --chown=$APP_UID:0 /app/publish ./
COPY --from=build --chown=$APP_UID:0 /healthprobe /healthprobe

USER $APP_UID

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
