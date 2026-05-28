# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/LedgerService.Worker/LedgerService.Worker.csproj src/LedgerService.Worker/
COPY src/LedgerService.Application/LedgerService.Application.csproj src/LedgerService.Application/
COPY src/LedgerService.Domain/LedgerService.Domain.csproj src/LedgerService.Domain/
COPY src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj src/LedgerService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel \
    || dotnet restore src/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel \
    || dotnet restore src/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/LedgerService.Worker/ ./src/LedgerService.Worker/
COPY src/LedgerService.Application/ ./src/LedgerService.Application/
COPY src/LedgerService.Domain/ ./src/LedgerService.Domain/
COPY src/LedgerService.Infrastructure/ ./src/LedgerService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/LedgerService.Worker/LedgerService.Worker.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "LedgerService.Worker.dll"]
