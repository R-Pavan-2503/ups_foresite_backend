#!/bin/bash
# Backend Deployment Script for Cloud Run
# Run this from: d:\ups\foresite\backend\backend

set -e

echo "🏗️ Building backend Docker image..."
docker build -t backend-backend .

echo "🏷️ Tagging image for GCP..."
docker tag backend-backend us-central1-docker.pkg.dev/codefamily-backend-482013/backend-repo/backend

echo "📤 Pushing image to GCP Artifact Registry..."
docker push us-central1-docker.pkg.dev/codefamily-backend-482013/backend-repo/backend

echo "🚀 Deploying to Cloud Run..."
gcloud run services update codefamily-backend \
  --region=us-central1 \
  --image=us-central1-docker.pkg.dev/codefamily-backend-482013/backend-repo/backend

echo "✅ Backend deployment complete!"
echo "🌐 Backend URL: https://codefamily-backend-854884449726.us-central1.run.app"
