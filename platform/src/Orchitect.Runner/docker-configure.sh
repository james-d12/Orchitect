#!/usr/bin/env bash

set -euo pipefail

CLOUD_PROVIDER="$1"
IAC_PROVIDER="$2"

echo "Cloud provider: $CLOUD_PROVIDER"
echo "IaC provider:   $IAC_PROVIDER"

# =====================
# Base dependencies
# =====================

echo "Installing base dependencies..."

apt-get update

apt-get install -y --no-install-recommends \
    curl \
    wget \
    gpg \
    git \
    golang \
    unzip

rm -rf /var/lib/apt/lists/*


# =====================
# Cloud provider
# =====================

case "$CLOUD_PROVIDER" in

    azure)

        echo "Installing Azure CLI..."

        curl -sL https://aka.ms/InstallAzureCLIDeb | bash

        ;;

    aws)

        echo "Installing AWS CLI..."

        curl -fsSL \
            https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip \
            -o /tmp/awscliv2.zip

        unzip -q /tmp/awscliv2.zip -d /tmp

        /tmp/aws/install

        rm -rf \
            /tmp/aws \
            /tmp/awscliv2.zip

        ;;

    gcp)

        echo "Installing Google Cloud CLI..."

        # GCP installation here

        ;;

    *)

        echo "Unsupported cloud provider: $CLOUD_PROVIDER"
        exit 1

        ;;

esac


# =====================
# IaC provider
# =====================

case "$IAC_PROVIDER" in

    terraform)

        echo "Installing Terraform..."

        wget -O- https://apt.releases.hashicorp.com/gpg \
            | gpg --dearmor \
            -o /usr/share/keyrings/hashicorp-archive-keyring.gpg

        echo \
            "deb [arch=$(dpkg --print-architecture) \
            signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] \
            https://apt.releases.hashicorp.com \
            $(. /etc/os-release && echo "$VERSION_CODENAME") main" \
            > /etc/apt/sources.list.d/hashicorp.list

        apt-get update

        apt-get install -y terraform

        rm -rf /var/lib/apt/lists/*

        echo "Installing terraform-config-inspect..."

        GOBIN=/usr/local/bin go install \
            github.com/hashicorp/terraform-config-inspect@latest

        ;;

    opentofu)

        echo "Installing OpenTofu..."

        # OpenTofu installation here

        ;;

    pulumi)

        echo "Installing Pulumi..."

        # Pulumi installation here

        ;;

    *)

        echo "Unsupported IaC provider: $IAC_PROVIDER"
        exit 1

        ;;

esac


# =====================
# Common tools
# =====================

echo "Installing Helm..."

curl -fsSL \
    -o /tmp/get_helm.sh \
    https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3

chmod 700 /tmp/get_helm.sh

/tmp/get_helm.sh

rm /tmp/get_helm.sh


echo "Tool installation complete."