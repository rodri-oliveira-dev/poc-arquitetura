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

RUN dotnet restore src/BalanceService.Api/BalanceService.Api.csproj --disable-parallel \
    || dotnet restore src/BalanceService.Api/BalanceService.Api.csproj --disable-parallel \
    || dotnet restore src/BalanceService.Api/BalanceService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/ ./src/

RUN dotnet publish src/BalanceService.Api/BalanceService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "BalanceService.Api.dll"]
