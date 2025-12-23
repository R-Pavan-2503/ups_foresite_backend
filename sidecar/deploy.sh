#!/bin/bash
# Sidecar Deployment Script for Cloud Run
# Run this from: d:\ups\foresite\backend\sidecar

set -e

echo "🏗️ Building sidecar Docker image..."
docker build -t sidecar-app .

echo "🏷️ Tagging image for GCP..."
docker tag sidecar-app us-central1-docker.pkg.dev/codefamily-backend-482013/backend-repo/sidecar

echo "📤 Pushing image to GCP Artifact Registry..."
docker push us-central1-docker.pkg.dev/codefamily-backend-482013/backend-repo/sidecar

echo "🚀 Deploying to Cloud Run..."
gcloud run services update codefamily-sidecar \
  --region=us-central1 \
  --image=us-central1-docker.pkg.dev/codefamily-backend-482013/backend-repo/sidecar

echo "✅ Sidecar deployment complete!"
echo "🌐 Sidecar URL: https://codefamily-sidecar-854884449726.us-central1.run.app"