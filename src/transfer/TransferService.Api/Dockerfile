# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/transfer/TransferService.Api/TransferService.Api.csproj src/transfer/TransferService.Api/
COPY src/transfer/TransferService.Application/TransferService.Application.csproj src/transfer/TransferService.Application/
COPY src/transfer/TransferService.Domain/TransferService.Domain.csproj src/transfer/TransferService.Domain/
COPY src/transfer/TransferService.Infrastructure/TransferService.Infrastructure.csproj src/transfer/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/transfer/TransferService.Api/TransferService.Api.csproj --disable-parallel \
    || dotnet restore src/transfer/TransferService.Api/TransferService.Api.csproj --disable-parallel \
    || dotnet restore src/transfer/TransferService.Api/TransferService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/transfer/TransferService.Api/ ./src/transfer/TransferService.Api/
COPY src/transfer/TransferService.Application/ ./src/transfer/TransferService.Application/
COPY src/transfer/TransferService.Domain/ ./src/transfer/TransferService.Domain/
COPY src/transfer/TransferService.Infrastructure/ ./src/transfer/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/transfer/TransferService.Api/TransferService.Api.csproj \
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

ENTRYPOINT ["dotnet", "TransferService.Api.dll"]
