# SmallMind Server - Multi-stage Docker build
# Produces a minimal container with the OpenAI-compatible API server

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files
COPY SmallMind.slnx .
COPY Directory.Build.props .
COPY src/SmallMind.Abstractions/SmallMind.Abstractions.csproj src/SmallMind.Abstractions/
COPY src/SmallMind.Engine/SmallMind.Engine.csproj src/SmallMind.Engine/
COPY src/SmallMind.Public/SmallMind.Public.csproj src/SmallMind.Public/
COPY src/SmallMind.ModelRegistry/SmallMind.ModelRegistry.csproj src/SmallMind.ModelRegistry/
COPY tools/SmallMind.Server/SmallMind.Server.csproj tools/SmallMind.Server/

# Restore dependencies
RUN dotnet restore tools/SmallMind.Server/SmallMind.Server.csproj

# Copy source code
COPY src/ src/
COPY tools/SmallMind.Server/ tools/SmallMind.Server/

# Build and publish
WORKDIR /source/tools/SmallMind.Server
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create directories for models
RUN mkdir -p /app/models

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/healthz || exit 1

# Set environment defaults
ENV ASPNETCORE_URLS=http://+:8080
ENV ServerOptions__Host=0.0.0.0
ENV ServerOptions__Port=8080

# Run the server
ENTRYPOINT ["dotnet", "SmallMind.Server.dll"]
