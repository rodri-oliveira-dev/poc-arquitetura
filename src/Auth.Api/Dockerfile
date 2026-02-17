# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/Auth.Api/Auth.Api.csproj src/Auth.Api/

RUN dotnet restore src/Auth.Api/Auth.Api.csproj --disable-parallel \
    || dotnet restore src/Auth.Api/Auth.Api.csproj --disable-parallel \
    || dotnet restore src/Auth.Api/Auth.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/ ./src/

RUN dotnet publish src/Auth.Api/Auth.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Porta interna do container
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Auth.Api.dll"]
