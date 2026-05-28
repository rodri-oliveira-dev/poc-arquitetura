# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/BalanceService.Api/BalanceService.Api.csproj src/BalanceService.Api/
COPY src/BalanceService.Application/BalanceService.Application.csproj src/BalanceService.Application/
COPY src/BalanceService.Domain/BalanceService.Domain.csproj src/BalanceService.Domain/
COPY src/BalanceService.Infrastructure/BalanceService.Infrastructure.csproj src/BalanceService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/BalanceService.Api/BalanceService.Api.csproj --disable-parallel \
    || dotnet restore src/BalanceService.Api/BalanceService.Api.csproj --disable-parallel \
    || dotnet restore src/BalanceService.Api/BalanceService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/BalanceService.Api/ ./src/BalanceService.Api/
COPY src/BalanceService.Application/ ./src/BalanceService.Application/
COPY src/BalanceService.Domain/ ./src/BalanceService.Domain/
COPY src/BalanceService.Infrastructure/ ./src/BalanceService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/BalanceService.Api/BalanceService.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "BalanceService.Api.dll"]
