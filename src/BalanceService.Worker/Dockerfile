# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/BalanceService.Worker/BalanceService.Worker.csproj src/BalanceService.Worker/
COPY src/BalanceService.Application/BalanceService.Application.csproj src/BalanceService.Application/
COPY src/BalanceService.Domain/BalanceService.Domain.csproj src/BalanceService.Domain/
COPY src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj src/BalanceService.Infrastructure/

RUN dotnet restore src/BalanceService.Worker/BalanceService.Worker.csproj --disable-parallel \
    || dotnet restore src/BalanceService.Worker/BalanceService.Worker.csproj --disable-parallel \
    || dotnet restore src/BalanceService.Worker/BalanceService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/ ./src/

RUN dotnet publish src/BalanceService.Worker/BalanceService.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "BalanceService.Worker.dll"]
