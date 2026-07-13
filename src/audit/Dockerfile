# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY src/audit/AuditService.Api/AuditService.Api.csproj src/audit/AuditService.Api/
COPY src/audit/AuditService.Worker/AuditService.Worker.csproj src/audit/AuditService.Worker/
COPY src/audit/AuditService.Application/AuditService.Application.csproj src/audit/AuditService.Application/
COPY src/audit/AuditService.Domain/AuditService.Domain.csproj src/audit/AuditService.Domain/
COPY src/audit/AuditService.Infrastructure/AuditService.Infrastructure.csproj src/audit/AuditService.Infrastructure/
COPY src/Shared/KafkaWorkerDefaults/KafkaWorkerDefaults.csproj src/Shared/KafkaWorkerDefaults/
COPY src/Shared/ApiDefaults/ApiDefaults.csproj src/Shared/ApiDefaults/
COPY src/Shared/HttpResilienceDefaults/HttpResilienceDefaults.csproj src/Shared/HttpResilienceDefaults/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/audit/AuditService.Api/AuditService.Api.csproj --disable-parallel \
    && dotnet restore src/audit/AuditService.Worker/AuditService.Worker.csproj --disable-parallel

COPY src/audit/AuditService.Api/ ./src/audit/AuditService.Api/
COPY src/audit/AuditService.Worker/ ./src/audit/AuditService.Worker/
COPY src/audit/AuditService.Application/ ./src/audit/AuditService.Application/
COPY src/audit/AuditService.Domain/ ./src/audit/AuditService.Domain/
COPY src/audit/AuditService.Infrastructure/ ./src/audit/AuditService.Infrastructure/
COPY src/Shared/KafkaWorkerDefaults/ ./src/Shared/KafkaWorkerDefaults/
COPY src/Shared/ApiDefaults/ ./src/Shared/ApiDefaults/
COPY src/Shared/HttpResilienceDefaults/ ./src/Shared/HttpResilienceDefaults/

FROM build AS api-publish
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/audit/AuditService.Api/AuditService.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM build AS worker-publish
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/audit/AuditService.Worker/AuditService.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api-final
WORKDIR /app
EXPOSE 8080

RUN chown -R $APP_UID:0 /app

COPY --from=api-publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "AuditService.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker-final
WORKDIR /app

RUN chown -R $APP_UID:0 /app

COPY --from=worker-publish --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "AuditService.Worker.dll"]
