# syntax=docker/dockerfile:1

# ---- 1. Build Tailwind CSS ----------------------------------------------------
FROM node:20-alpine AS css
WORKDIR /src/web
COPY src/TaskManagement.Web/package.json src/TaskManagement.Web/package-lock.json* ./
RUN npm ci
COPY src/TaskManagement.Web/Styles ./Styles
COPY src/TaskManagement.Web/Components ./Components
RUN npm run css:build

# ---- 2. Publish the .NET app -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/TaskManagement.Domain/*.csproj src/TaskManagement.Domain/
COPY src/TaskManagement.Application/*.csproj src/TaskManagement.Application/
COPY src/TaskManagement.Infrastructure/*.csproj src/TaskManagement.Infrastructure/
COPY src/TaskManagement.Web/*.csproj src/TaskManagement.Web/
RUN dotnet restore src/TaskManagement.Web/TaskManagement.Web.csproj

COPY src/ src/
# The npm-built stylesheet comes from the css stage; skip the in-build npm run.
COPY --from=css /src/web/wwwroot/css/app.generated.css src/TaskManagement.Web/wwwroot/css/app.generated.css
RUN dotnet publish src/TaskManagement.Web/TaskManagement.Web.csproj \
    -c Release -o /app /p:SkipTailwind=true /p:UseAppHost=false

# ---- 3. Runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DataProtection__KeyPath=/keys \
    FileStorage__RootPath=/app/uploads
RUN apt-get update && apt-get install -y --no-install-recommends curl adduser && rm -rf /var/lib/apt/lists/* && \
    mkdir -p /keys /app/uploads && \
    adduser --disabled-password --gecos "" appuser && \
    chown -R appuser /keys /app/uploads
USER appuser
COPY --from=build --chown=appuser /app ./
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "TaskManagement.Web.dll"]
