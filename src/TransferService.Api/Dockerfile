# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/Shared/ApiDefaults/ApiDefaults.csproj src/Shared/ApiDefaults/
COPY src/TransferService.Api/TransferService.Api.csproj src/TransferService.Api/
COPY src/TransferService.Application/TransferService.Application.csproj src/TransferService.Application/
COPY src/TransferService.Domain/TransferService.Domain.csproj src/TransferService.Domain/
COPY src/TransferService.Infrastructure/TransferService.Infrastructure.csproj src/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/TransferService.Api/TransferService.Api.csproj --disable-parallel \
    || dotnet restore src/TransferService.Api/TransferService.Api.csproj --disable-parallel \
    || dotnet restore src/TransferService.Api/TransferService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/Shared/ApiDefaults/ ./src/Shared/ApiDefaults/
COPY src/TransferService.Api/ ./src/TransferService.Api/
COPY src/TransferService.Application/ ./src/TransferService.Application/
COPY src/TransferService.Domain/ ./src/TransferService.Domain/
COPY src/TransferService.Infrastructure/ ./src/TransferService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/TransferService.Api/TransferService.Api.csproj \
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
