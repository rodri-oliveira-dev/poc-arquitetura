# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/ledger/LedgerService.Worker/LedgerService.Worker.csproj src/ledger/LedgerService.Worker/
COPY src/ledger/LedgerService.Application/LedgerService.Application.csproj src/ledger/LedgerService.Application/
COPY src/ledger/LedgerService.Domain/LedgerService.Domain.csproj src/ledger/LedgerService.Domain/
COPY src/ledger/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj src/ledger/LedgerService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/ledger/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel \
    || dotnet restore src/ledger/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel \
    || dotnet restore src/ledger/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/ledger/LedgerService.Worker/ ./src/ledger/LedgerService.Worker/
COPY src/ledger/LedgerService.Application/ ./src/ledger/LedgerService.Application/
COPY src/ledger/LedgerService.Domain/ ./src/ledger/LedgerService.Domain/
COPY src/ledger/LedgerService.Infrastructure/ ./src/ledger/LedgerService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/ledger/LedgerService.Worker/LedgerService.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "LedgerService.Worker.dll"]
