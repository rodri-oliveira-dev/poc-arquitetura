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

RUN dotnet restore src/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel \
    || dotnet restore src/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel \
    || dotnet restore src/LedgerService.Worker/LedgerService.Worker.csproj --disable-parallel

# Agora copia o restante
COPY src/ ./src/

RUN dotnet publish src/LedgerService.Worker/LedgerService.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "LedgerService.Worker.dll"]
