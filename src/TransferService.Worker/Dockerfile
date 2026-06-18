# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/TransferService.Worker/TransferService.Worker.csproj src/TransferService.Worker/
COPY src/TransferService.Application/TransferService.Application.csproj src/TransferService.Application/
COPY src/TransferService.Domain/TransferService.Domain.csproj src/TransferService.Domain/
COPY src/TransferService.Infrastructure/TransferService.Infrastructure.csproj src/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/TransferService.Worker/TransferService.Worker.csproj --disable-parallel \
    || dotnet restore src/TransferService.Worker/TransferService.Worker.csproj --disable-parallel \
    || dotnet restore src/TransferService.Worker/TransferService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/TransferService.Worker/ ./src/TransferService.Worker/
COPY src/TransferService.Application/ ./src/TransferService.Application/
COPY src/TransferService.Domain/ ./src/TransferService.Domain/
COPY src/TransferService.Infrastructure/ ./src/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/TransferService.Worker/TransferService.Worker.csproj \
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
