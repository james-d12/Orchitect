# =====================
# Build Stage
# =====================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

# Copy project files
COPY ./src ./
COPY Directory.Build.props ./

WORKDIR Orchitect.Api

# Restore and publish
RUN dotnet restore Orchitect.Api.csproj
RUN dotnet publish Orchitect.Api.csproj -c Release --no-restore -o /app/out/Orchitect.Api

# =====================
# Runtime Stage
# =====================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN useradd -m appuser

WORKDIR /app

COPY --from=build /app/out/Orchitect.Api ./

USER appuser

ENTRYPOINT ["./Orchitect.Api"]
