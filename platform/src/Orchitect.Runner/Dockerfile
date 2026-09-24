# Build context: platform/
# docker build -f src/Orchitect.Runner/Dockerfile \
#   --build-arg ORCHITECT_IAC_PROVIDER=terraform \
#   -t orchitect-runner:terraform .

ARG ORCHITECT_IAC_PROVIDER=terraform

# TOOL LAYERS

FROM golang:1.27-alpine AS config-inspect

ARG TF_CONFIG_INSPECT_REF=75d64de68c31445dbe4ee8308350e0b6dda1571d

RUN CGO_ENABLED=0 GOBIN=/out go install -trimpath -ldflags="-s -w" \
    github.com/hashicorp/terraform-config-inspect@${TF_CONFIG_INSPECT_REF}

FROM alpine:3 AS helm

ARG HELM_VERSION=3.22.0
ARG TARGETARCH

WORKDIR /tmp/helm

RUN ARCH="${TARGETARCH:-amd64}" \
    && FILE="helm-v${HELM_VERSION}-linux-${ARCH}.tar.gz" \
    && wget -q "https://get.helm.sh/${FILE}" "https://get.helm.sh/${FILE}.sha256sum" \
    && sha256sum -c "${FILE}.sha256sum" \
    && mkdir /out \
    && tar -xzf "${FILE}" -C /out --strip-components=1 "linux-${ARCH}/helm"

FROM alpine:3 AS iac-terraform

ARG TERRAFORM_VERSION=1.16.4
ARG TARGETARCH

WORKDIR /tmp/terraform

RUN ARCH="${TARGETARCH:-amd64}" \
    && FILE="terraform_${TERRAFORM_VERSION}_linux_${ARCH}.zip" \
    && BASE="https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}" \
    && wget -q "${BASE}/${FILE}" "${BASE}/terraform_${TERRAFORM_VERSION}_SHA256SUMS" \
    && grep " ${FILE}\$" "terraform_${TERRAFORM_VERSION}_SHA256SUMS" | sha256sum -c \
    && mkdir /out \
    && unzip -q "${FILE}" terraform -d /out

COPY --from=config-inspect /out/terraform-config-inspect /out/terraform-config-inspect

FROM alpine:3 AS iac-opentofu

RUN echo "OpenTofu runner images are not supported yet." && exit 1

FROM alpine:3 AS iac-pulumi

RUN echo "Pulumi runner images are not supported yet." && exit 1

FROM iac-${ORCHITECT_IAC_PROVIDER} AS iac

# BUILD LAYER

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Orchitect.Common/Orchitect.Common.csproj src/Orchitect.Common/
COPY src/Orchitect.Domain/Orchitect.Domain.csproj src/Orchitect.Domain/
COPY src/Orchitect.Infrastructure.Engine/Orchitect.Infrastructure.Engine.csproj src/Orchitect.Infrastructure.Engine/
COPY src/Orchitect.Persistence/Orchitect.Persistence.csproj src/Orchitect.Persistence/
COPY src/Orchitect.Runner/Orchitect.Runner.csproj src/Orchitect.Runner/

RUN dotnet restore src/Orchitect.Runner/Orchitect.Runner.csproj

COPY . .

WORKDIR "/src/src/Orchitect.Runner"

RUN dotnet publish "./Orchitect.Runner.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# RELEASE LAYER

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final

USER root

RUN apk add --no-cache git ca-certificates krb5-libs

COPY --from=iac /out/ /usr/local/bin/
COPY --from=helm /out/helm /usr/local/bin/helm

WORKDIR /app

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "Orchitect.Runner.dll"]
