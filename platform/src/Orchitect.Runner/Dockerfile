# Build context: platform/
# docker build -f src/Orchitect.Runner/Dockerfile \
#   --build-arg ORCHITECT_CLOUD_PROVIDER=azure \
#   --build-arg ORCHITECT_IAC_PROVIDER=terraform \
#   -t orchitect-runner:azure-terraform .

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base

ARG ORCHITECT_CLOUD_PROVIDER=azure
ARG ORCHITECT_IAC_PROVIDER=terraform

USER root
WORKDIR /app

COPY src/Orchitect.Runner/docker-configure.sh /tmp/docker-configure.sh

RUN chmod +x /tmp/docker-configure.sh \
    && /tmp/docker-configure.sh "$ORCHITECT_CLOUD_PROVIDER" "$ORCHITECT_IAC_PROVIDER" \
    && rm /tmp/docker-configure.sh

# BUILD LAYER

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY . .

WORKDIR "/src/src/Orchitect.Runner"

RUN dotnet publish "./Orchitect.Runner.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# RELEASE LAYER

FROM base AS final

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Orchitect.Runner.dll"]
