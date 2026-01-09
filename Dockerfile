# ===================================
# RDL-Core Docker Multi-Stage Build
# ===================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY Directory.Build.props Directory.Packages.props ./
COPY src/RdlCore.Abstractions/RdlCore.Abstractions.csproj src/RdlCore.Abstractions/
COPY src/RdlCore.Parsing/RdlCore.Parsing.csproj src/RdlCore.Parsing/
COPY src/RdlCore.Logic/RdlCore.Logic.csproj src/RdlCore.Logic/
COPY src/RdlCore.Generation/RdlCore.Generation.csproj src/RdlCore.Generation/
COPY src/RdlCore.Rendering/RdlCore.Rendering.csproj src/RdlCore.Rendering/
COPY src/RdlCore.Agent/RdlCore.Agent.csproj src/RdlCore.Agent/
COPY src/RdlCore.WebApi/RdlCore.WebApi.csproj src/RdlCore.WebApi/

# Restore dependencies
RUN dotnet restore src/RdlCore.WebApi/RdlCore.WebApi.csproj

# Copy all source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/RdlCore.WebApi
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install common fonts and curl for health check
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    fonts-liberation \
    fonts-dejavu-core \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r rdlcore && useradd -r -g rdlcore rdlcore

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R rdlcore:rdlcore /app

# Switch to non-root user
USER rdlcore

# Environment configuration
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "RdlCore.WebApi.dll"]
