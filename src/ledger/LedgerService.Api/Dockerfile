# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj src/Shared/ContainerHealthProbe/
COPY src/ledger/LedgerService.Api/LedgerService.Api.csproj src/ledger/LedgerService.Api/
COPY src/ledger/LedgerService.Application/LedgerService.Application.csproj src/ledger/LedgerService.Application/
COPY src/ledger/LedgerService.Domain/LedgerService.Domain.csproj src/ledger/LedgerService.Domain/
COPY src/ledger/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj src/ledger/LedgerService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/ledger/LedgerService.Api/LedgerService.Api.csproj --disable-parallel
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj --disable-parallel

# Agora copia o restante
COPY src/ledger/LedgerService.Api/ ./src/ledger/LedgerService.Api/
COPY src/ledger/LedgerService.Application/ ./src/ledger/LedgerService.Application/
COPY src/ledger/LedgerService.Domain/ ./src/ledger/LedgerService.Domain/
COPY src/ledger/LedgerService.Infrastructure/ ./src/ledger/LedgerService.Infrastructure/
COPY src/Shared/ContainerHealthProbe/ ./src/Shared/ContainerHealthProbe/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/ledger/LedgerService.Api/LedgerService.Api.csproj \
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

ENTRYPOINT ["dotnet", "LedgerService.Api.dll"]
