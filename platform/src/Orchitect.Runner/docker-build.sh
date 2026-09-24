#!/usr/bin/env bash

set -euo pipefail

IAC_PROVIDER="${1:-terraform}"
TAG="${2:-orchitect-runner:$IAC_PROVIDER}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLATFORM_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo "IaC provider: $IAC_PROVIDER"
echo "Image tag:    $TAG"

docker build \
    -f "$SCRIPT_DIR/Dockerfile" \
    --build-arg ORCHITECT_IAC_PROVIDER="$IAC_PROVIDER" \
    -t "$TAG" \
    "${@:3}" \
    "$PLATFORM_DIR"

docker images "$TAG"
