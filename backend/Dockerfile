# Multi-stage Dockerfile for .NET 9 Backend

# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY CodeFamily.sln .
COPY src/Api/CodeFamily.Api.csproj src/Api/
COPY src/Core/ src/Core/
COPY src/Workers/ src/Workers/

# Restore dependencies
WORKDIR /src/src/Api
RUN dotnet restore

# Copy all source code
WORKDIR /src
COPY src/ ./src/

# Build and publish
WORKDIR /src/src/Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install LibGit2Sharp native dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgit2-dev \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Copy settings.json (configuration template)
COPY settings.json /app/settings.json

# Create directories for runtime data
RUN mkdir -p /app/repos /app/testdata/repos

# Environment variables with defaults
ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=5000 \
    ASPNETCORE_URLS=http://+:5000

# Expose port
EXPOSE 5000

# Run as non-root user
RUN useradd -m -u 1001 appuser && \
    chown -R appuser:appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "CodeFamily.Api.dll"]
