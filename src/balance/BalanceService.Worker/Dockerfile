# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/balance/BalanceService.Worker/BalanceService.Worker.csproj src/balance/BalanceService.Worker/
COPY src/balance/BalanceService.Application/BalanceService.Application.csproj src/balance/BalanceService.Application/
COPY src/balance/BalanceService.Domain/BalanceService.Domain.csproj src/balance/BalanceService.Domain/
COPY src/balance/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj src/balance/BalanceService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/balance/BalanceService.Worker/BalanceService.Worker.csproj --disable-parallel \
    || dotnet restore src/balance/BalanceService.Worker/BalanceService.Worker.csproj --disable-parallel \
    || dotnet restore src/balance/BalanceService.Worker/BalanceService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/balance/BalanceService.Worker/ ./src/balance/BalanceService.Worker/
COPY src/balance/BalanceService.Application/ ./src/balance/BalanceService.Application/
COPY src/balance/BalanceService.Domain/ ./src/balance/BalanceService.Domain/
COPY src/balance/BalanceService.Infrastructure/ ./src/balance/BalanceService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/balance/BalanceService.Worker/BalanceService.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "BalanceService.Worker.dll"]
