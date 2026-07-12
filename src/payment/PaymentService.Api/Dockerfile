# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia csproj primeiro para aproveitar cache
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/payment/PaymentService.Api/PaymentService.Api.csproj src/payment/PaymentService.Api/
COPY src/payment/PaymentService.Application/PaymentService.Application.csproj src/payment/PaymentService.Application/
COPY src/payment/PaymentService.Domain/PaymentService.Domain.csproj src/payment/PaymentService.Domain/
COPY src/payment/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj src/payment/PaymentService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/payment/PaymentService.Api/PaymentService.Api.csproj --disable-parallel \
    || dotnet restore src/payment/PaymentService.Api/PaymentService.Api.csproj --disable-parallel \
    || dotnet restore src/payment/PaymentService.Api/PaymentService.Api.csproj --disable-parallel

# Agora copia o restante
COPY src/payment/PaymentService.Api/ ./src/payment/PaymentService.Api/
COPY src/payment/PaymentService.Application/ ./src/payment/PaymentService.Application/
COPY src/payment/PaymentService.Domain/ ./src/payment/PaymentService.Domain/
COPY src/payment/PaymentService.Infrastructure/ ./src/payment/PaymentService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/payment/PaymentService.Api/PaymentService.Api.csproj \
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

ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]
