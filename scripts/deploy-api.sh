#!/usr/bin/env bash
# Deploy Handmade API on EC2 via Docker Compose (API service only).
# Postgres and MinIO are not recreated. Requires production .env on the host.
#
# Required environment variables:
#   API_IMAGE  — full ECR image URI including tag (e.g. 123456789012.dkr.ecr.eu-west-1.amazonaws.com/handmade-api:abc1234)
#
# Optional:
#   AWS_REGION   — used for ECR login (inferred from API_IMAGE when omitted)
#   HEALTH_URL   — default http://127.0.0.1:8080/health
#   STATE_DIR    — default <repo>/.deploy (stores previous image for rollback)
#   COMPOSE_DIR  — default repository root (parent of scripts/)

set -euo pipefail

COMPOSE_DIR="${COMPOSE_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
cd "$COMPOSE_DIR"

API_IMAGE="${API_IMAGE:?API_IMAGE is required (full ECR URI including tag)}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:8080/health}"
STATE_DIR="${STATE_DIR:-$COMPOSE_DIR/.deploy}"
PREVIOUS_IMAGE_FILE="$STATE_DIR/previous-api-image"
HEALTH_RETRIES="${HEALTH_RETRIES:-30}"
HEALTH_INTERVAL_SECONDS="${HEALTH_INTERVAL_SECONDS:-2}"

COMPOSE=(docker-compose -f docker-compose.yml -f docker-compose.prod.yml --profile api)

mkdir -p "$STATE_DIR"

rollback() {
  if [[ ! -f "$PREVIOUS_IMAGE_FILE" ]]; then
    echo "No previous image recorded; rollback skipped."
    return
  fi

  local previous_image
  previous_image="$(cat "$PREVIOUS_IMAGE_FILE")"
  if [[ -z "$previous_image" ]]; then
    echo "Previous image file is empty; rollback skipped."
    return
  fi

  echo "Rolling back to: $previous_image"
  export API_IMAGE="$previous_image"
  "${COMPOSE[@]}" up -d --no-deps api
}

on_failure() {
  local exit_code=$?
  if [[ "$exit_code" -ne 0 ]]; then
    echo "Deploy failed (exit $exit_code). Attempting rollback..."
    rollback || true
  fi
  exit "$exit_code"
}

trap on_failure EXIT

if docker inspect handmade-api >/dev/null 2>&1; then
  current_image="$(docker inspect --format='{{.Config.Image}}' handmade-api)"
  if [[ -n "$current_image" && "$current_image" != "$API_IMAGE" ]]; then
    echo "$current_image" >"$PREVIOUS_IMAGE_FILE"
    echo "Saved rollback image: $current_image"
  fi
fi

if [[ "$API_IMAGE" == *.amazonaws.com/* ]]; then
  registry="${API_IMAGE%%/*}"
  region="${AWS_REGION:-}"
  if [[ -z "$region" ]]; then
    region="$(sed -n 's/.*\.dkr\.ecr\.\([^.]*\)\.amazonaws\.com.*/\1/p' <<<"$registry")"
  fi
  if [[ -z "$region" ]]; then
    echo "Could not determine AWS region for ECR login. Set AWS_REGION."
    exit 1
  fi
  echo "Logging in to ECR ($registry) in $region..."
  aws ecr get-login-password --region "$region" | docker login --username AWS --password-stdin "$registry"
fi

export API_IMAGE

echo "Pulling API image: $API_IMAGE"
"${COMPOSE[@]}" pull api

echo "Recreating API container (postgres and minio unchanged)..."
"${COMPOSE[@]}" up -d --no-deps api

echo "Waiting for health check: $HEALTH_URL"
for _ in $(seq 1 "$HEALTH_RETRIES"); do
  if curl -fsS "$HEALTH_URL" >/dev/null; then
    echo "Health check passed."
    trap - EXIT
    exit 0
  fi
  sleep "$HEALTH_INTERVAL_SECONDS"
done

echo "Health check failed after $((HEALTH_RETRIES * HEALTH_INTERVAL_SECONDS)) seconds."
exit 1
