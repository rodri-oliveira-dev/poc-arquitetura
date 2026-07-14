# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY global.json ./
COPY src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj src/Shared/ContainerHealthProbe/
COPY src/audit/AuditService.Api/AuditService.Api.csproj src/audit/AuditService.Api/
COPY src/audit/AuditService.Application/AuditService.Application.csproj src/audit/AuditService.Application/
COPY src/audit/AuditService.Domain/AuditService.Domain.csproj src/audit/AuditService.Domain/
COPY src/audit/AuditService.Infrastructure/AuditService.Infrastructure.csproj src/audit/AuditService.Infrastructure/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/audit/AuditService.Api/AuditService.Api.csproj --disable-parallel
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Shared/ContainerHealthProbe/ContainerHealthProbe.csproj --disable-parallel

COPY src/audit/AuditService.Api/ ./src/audit/AuditService.Api/
COPY src/audit/AuditService.Application/ ./src/audit/AuditService.Application/
COPY src/audit/AuditService.Domain/ ./src/audit/AuditService.Domain/
COPY src/audit/AuditService.Infrastructure/ ./src/audit/AuditService.Infrastructure/
COPY src/Shared/ContainerHealthProbe/ ./src/Shared/ContainerHealthProbe/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/audit/AuditService.Api/AuditService.Api.csproj \
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

ENTRYPOINT ["dotnet", "AuditService.Api.dll"]
