#!/usr/bin/env bash
set -euo pipefail

echo "==> Deploying Secure Media Vault..."

# Ensure env file exists
if [ ! -f .env ]; then
  echo "Error: .env file not found. Copy .env.example to .env and configure secrets."
  exit 1
fi

# Pull latest images and start containers
docker compose pull
docker compose up -d --remove-orphans

echo "==> Deployment completed successfully!"
docker compose ps
