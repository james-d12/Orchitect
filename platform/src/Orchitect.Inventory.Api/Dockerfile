# =====================
# Build Stage
# =====================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

# Copy project files1
COPY ./src ./
COPY Directory.Build.props ./

WORKDIR Orchitect.Inventory.Api

# Restore and publish
RUN dotnet restore Orchitect.Inventory.Api.csproj
RUN dotnet publish Orchitect.Inventory.Api.csproj -c Release --no-restore -o /app/out/Orchitect.Inventory.Api

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
COPY --from=build /app/out/Orchitect.Inventory.Api ./

# =====================
# Entrypoint
# =====================
ENTRYPOINT ["./Orchitect.Inventory.Api"]
