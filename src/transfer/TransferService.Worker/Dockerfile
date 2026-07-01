# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/Shared/ApplicationDefaults/ApplicationDefaults.csproj src/Shared/ApplicationDefaults/
COPY src/Shared/HttpResilienceDefaults/HttpResilienceDefaults.csproj src/Shared/HttpResilienceDefaults/
COPY src/transfer/TransferService.Worker/TransferService.Worker.csproj src/transfer/TransferService.Worker/
COPY src/transfer/TransferService.Application/TransferService.Application.csproj src/transfer/TransferService.Application/
COPY src/transfer/TransferService.Domain/TransferService.Domain.csproj src/transfer/TransferService.Domain/
COPY src/transfer/TransferService.Infrastructure/TransferService.Infrastructure.csproj src/transfer/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/transfer/TransferService.Worker/TransferService.Worker.csproj --disable-parallel \
    || dotnet restore src/transfer/TransferService.Worker/TransferService.Worker.csproj --disable-parallel \
    || dotnet restore src/transfer/TransferService.Worker/TransferService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/Shared/ApplicationDefaults/ ./src/Shared/ApplicationDefaults/
COPY src/Shared/HttpResilienceDefaults/ ./src/Shared/HttpResilienceDefaults/
COPY src/transfer/TransferService.Worker/ ./src/transfer/TransferService.Worker/
COPY src/transfer/TransferService.Application/ ./src/transfer/TransferService.Application/
COPY src/transfer/TransferService.Domain/ ./src/transfer/TransferService.Domain/
COPY src/transfer/TransferService.Infrastructure/ ./src/transfer/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/transfer/TransferService.Worker/TransferService.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "TransferService.Worker.dll"]
