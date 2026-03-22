#!/usr/bin/env bash

set -euo pipefail

IMAGE_NAME="uptext/sqlwebapi"
SOURCE_IMAGE="sqlwebapi:latest"
VERSION_TAG="${1:-v1.0.7}"

docker tag "$SOURCE_IMAGE" "$IMAGE_NAME:$VERSION_TAG"
docker tag "$SOURCE_IMAGE" "$IMAGE_NAME:latest"

docker push "$IMAGE_NAME:$VERSION_TAG"
docker push "$IMAGE_NAME:latest"
