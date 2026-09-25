# Build context: platform/
# docker build -f src/Orchitect.Runner/Dockerfile \
#   --build-arg ORCHITECT_IAC_PROVIDER=terraform \
#   -t orchitect-runner:terraform .
#
# Requires BuildKit (the default builder). The legacy builder builds every stage,
# including the unsupported iac-opentofu and iac-pulumi stages, which fail on purpose.

ARG ORCHITECT_IAC_PROVIDER=terraform

# TOOL LAYERS

FROM golang:1.27.1-alpine3.24 AS config-inspect

ARG TF_CONFIG_INSPECT_REF=75d64de68c31445dbe4ee8308350e0b6dda1571d

RUN CGO_ENABLED=0 GOBIN=/out go install -trimpath -ldflags="-s -w" \
    github.com/hashicorp/terraform-config-inspect@${TF_CONFIG_INSPECT_REF}

FROM alpine:3.24 AS helm

ARG HELM_VERSION=3.22.0
ARG TARGETARCH

WORKDIR /tmp/helm

RUN apk add --no-cache wget

RUN ARCH="${TARGETARCH:-amd64}" \
    && FILE="helm-v${HELM_VERSION}-linux-${ARCH}.tar.gz" \
    && wget --max-redirect=0  -q "https://get.helm.sh/${FILE}" "https://get.helm.sh/${FILE}.sha256sum" \
    && sha256sum -c "${FILE}.sha256sum" \
    && mkdir /out \
    && tar -xzf "${FILE}" -C /out --strip-components=1 "linux-${ARCH}/helm"

FROM alpine:3.24 AS iac-terraform

ARG TERRAFORM_VERSION=1.16.4
ARG HASHICORP_PGP_FINGERPRINT=C874011F0AB405110D02105534365D9472D7468F
ARG TARGETARCH

WORKDIR /tmp/terraform

RUN apk add --no-cache gnupg wget

RUN ARCH="${TARGETARCH:-amd64}" \
    && FILE="terraform_${TERRAFORM_VERSION}_linux_${ARCH}.zip" \
    && SUMS="terraform_${TERRAFORM_VERSION}_SHA256SUMS" \
    && BASE="https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}" \
    && wget --max-redirect=0 -q "${BASE}/${FILE}" "${BASE}/${SUMS}" "${BASE}/${SUMS}.sig" \
    && wget --max-redirect=0 -q -O hashicorp.asc "https://www.hashicorp.com/.well-known/pgp-key.txt" \
    && export GNUPGHOME="$(mktemp -d)" \
    && gpg --batch --quiet --import hashicorp.asc \
    && gpg --batch --with-colons --fingerprint | grep -q "^fpr:::::::::${HASHICORP_PGP_FINGERPRINT}:" \
    && gpg --batch --verify "${SUMS}.sig" "${SUMS}" \
    && grep " ${FILE}\$" "${SUMS}" | sha256sum -c \
    && mkdir /out \
    && unzip -q "${FILE}" terraform -d /out

COPY --from=config-inspect /out/terraform-config-inspect /out/terraform-config-inspect

FROM alpine:3.24 AS iac-opentofu

RUN echo "OpenTofu runner images are not supported yet." && exit 1

FROM alpine:3.24 AS iac-pulumi

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
