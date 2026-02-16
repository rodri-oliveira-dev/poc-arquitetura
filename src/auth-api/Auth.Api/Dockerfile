# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY src/auth-api/Auth.Api/Auth.Api.csproj src/auth-api/Auth.Api/

RUN dotnet restore src/auth-api/Auth.Api/Auth.Api.csproj

# Agora copia o restante
COPY src/ ./src/

RUN dotnet publish src/auth-api/Auth.Api/Auth.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Porta interna do container
EXPOSE 8080

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "Auth.Api.dll"]
