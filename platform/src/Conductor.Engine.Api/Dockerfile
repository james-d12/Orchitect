# =====================
# Build Stage
# =====================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

# Copy project files1
COPY ./src ./
COPY Directory.Build.props ./

WORKDIR Conductor.Engine.Api

# Restore and publish
RUN dotnet restore Conductor.Engine.Api.csproj
RUN dotnet publish Conductor.Engine.Api.csproj -c Release --no-restore -o /app/out/Conductor.Engine.Api

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
# Install dependencies
# =====================
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    wget \
    gpg \
    git \
    golang \
    unzip \
 && rm -rf /var/lib/apt/lists/*

# =====================
# Install Terraform
# =====================
RUN wget -O- https://apt.releases.hashicorp.com/gpg | gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg \
 && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(. /etc/os-release && echo $VERSION_CODENAME) main" \
    > /etc/apt/sources.list.d/hashicorp.list \
 && apt-get update && apt-get install -y terraform \
 && rm -rf /var/lib/apt/lists/*

# =====================
# Install Azure CLI
# =====================
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash

# =====================
# Install terraform-config-inspect
# =====================
RUN go install github.com/hashicorp/terraform-config-inspect@latest
ENV PATH="/usr/local/go/bin:/root/go/bin:${PATH}"

# =====================
# Install Helm
# =====================
RUN curl -fsSL -o get_helm.sh https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 \
 && chmod 700 get_helm.sh \
 && ./get_helm.sh \
 && rm get_helm.sh

# =====================
# Configure Terraform/ARM environment
# =====================
ARG ARM_USE_MSI=true
ARG ARM_SUBSCRIPTION_ID=""
ARG ARM_TENANT_ID=""
ARG ARM_CLIENT_ID=""
ARG ARM_CLIENT_SECRET=""
ARG ARM_MSI_API_VERSION="2019-08-01"

ENV ARM_USE_MSI=${ARM_USE_MSI}
ENV ARM_SUBSCRIPTION_ID=${ARM_SUBSCRIPTION_ID}
ENV ARM_TENANT_ID=${ARM_TENANT_ID}
ENV ARM_CLIENT_ID=${ARM_CLIENT_ID}
ENV ARM_CLIENT_SECRET=${ARM_CLIENT_SECRET}
ENV ARM_MSI_API_VERSION=${ARM_MSI_API_VERSION}

# =====================
# Copy application
# =====================
COPY --from=build /app/out/Conductor.Engine.Api ./

# =====================
# Entrypoint
# =====================
ENTRYPOINT ["./Conductor.Engine.Api"]
