# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/audit/AuditService.Worker/AuditService.Worker.csproj src/audit/AuditService.Worker/
COPY src/audit/AuditService.Application/AuditService.Application.csproj src/audit/AuditService.Application/
COPY src/audit/AuditService.Domain/AuditService.Domain.csproj src/audit/AuditService.Domain/
COPY src/audit/AuditService.Infrastructure/AuditService.Infrastructure.csproj src/audit/AuditService.Infrastructure/
COPY src/Shared/KafkaWorkerDefaults/KafkaWorkerDefaults.csproj src/Shared/KafkaWorkerDefaults/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/audit/AuditService.Worker/AuditService.Worker.csproj --disable-parallel

COPY src/audit/AuditService.Worker/ ./src/audit/AuditService.Worker/
COPY src/audit/AuditService.Application/ ./src/audit/AuditService.Application/
COPY src/audit/AuditService.Domain/ ./src/audit/AuditService.Domain/
COPY src/audit/AuditService.Infrastructure/ ./src/audit/AuditService.Infrastructure/
COPY src/Shared/KafkaWorkerDefaults/ ./src/Shared/KafkaWorkerDefaults/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/audit/AuditService.Worker/AuditService.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

COPY --from=build --chown=$APP_UID:0 /app/publish ./

USER $APP_UID

ENTRYPOINT ["dotnet", "AuditService.Worker.dll"]
