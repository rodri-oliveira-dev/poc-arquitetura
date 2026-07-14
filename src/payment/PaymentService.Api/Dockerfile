# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj src/Shared/ContainerHealthProbe/
COPY src/payment/PaymentService.Api/PaymentService.Api.csproj src/payment/PaymentService.Api/
COPY src/payment/PaymentService.Application/PaymentService.Application.csproj src/payment/PaymentService.Application/
COPY src/payment/PaymentService.Domain/PaymentService.Domain.csproj src/payment/PaymentService.Domain/
COPY src/payment/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj src/payment/PaymentService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/payment/PaymentService.Api/PaymentService.Api.csproj --disable-parallel
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj --disable-parallel

COPY src/payment/PaymentService.Api/ ./src/payment/PaymentService.Api/
COPY src/payment/PaymentService.Application/ ./src/payment/PaymentService.Application/
COPY src/payment/PaymentService.Domain/ ./src/payment/PaymentService.Domain/
COPY src/payment/PaymentService.Infrastructure/ ./src/payment/PaymentService.Infrastructure/
COPY src/Shared/ContainerHealthProbe/ ./src/Shared/ContainerHealthProbe/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/payment/PaymentService.Api/PaymentService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj \
    -c Release \
    -o /healthprobe \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build --chown=$APP_UID:0 /app/publish ./
COPY --from=build --chown=$APP_UID:0 /healthprobe /healthprobe

USER $APP_UID

ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]
