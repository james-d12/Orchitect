# =====================
# Build Stage
# =====================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

# Copy project files1
COPY ./src ./
COPY Directory.Build.props ./

WORKDIR Conductor.Inventory.Api

# Restore and publish
RUN dotnet restore Conductor.Inventory.Api.csproj
RUN dotnet publish Conductor.Inventory.Api.csproj -c Release --no-restore -o /app/out/Conductor.Inventory.Api

# =====================
# Runtime Stage
# =====================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# =====================
# Create non-root user
# =====================
RUN useradd -m appuser
USER appuser
WORKDIR /app


# =====================
# Copy application
# =====================
COPY --from=build /app/out/Conductor.Inventory.Api ./

# =====================
# Entrypoint
# =====================
ENTRYPOINT ["./Conductor.Inventory.Api"]
