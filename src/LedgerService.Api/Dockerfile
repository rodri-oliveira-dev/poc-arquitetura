# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY LedgerService.slnx ./
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/LedgerService.Api/LedgerService.Api.csproj src/LedgerService.Api/
COPY src/LedgerService.Application/LedgerService.Application.csproj src/LedgerService.Application/
COPY src/LedgerService.Domain/LedgerService.Domain.csproj src/LedgerService.Domain/
COPY src/LedgerService.Infrastructure/LedgerService.Infrastructure.csproj src/LedgerService.Infrastructure/

RUN dotnet restore src/LedgerService.Api/LedgerService.Api.csproj --disable-parallel \
    || dotnet restore src/LedgerService.Api/LedgerService.Api.csproj --disable-parallel \
    || dotnet restore src/LedgerService.Api/LedgerService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/ ./src/

RUN dotnet publish src/LedgerService.Api/LedgerService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "LedgerService.Api.dll"]
