# Multi-stage Dockerfile for Node.js Sidecar

# ============================================
# Stage 1: Build TypeScript
# ============================================
FROM node:20 AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install ALL dependencies (including dev deps for build)
RUN npm ci

# Copy source code
COPY . .

# Build TypeScript
RUN npm run build

# ============================================
# Stage 2: Production Runtime
# ============================================
FROM node:20-slim AS runtime
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install ONLY production dependencies
RUN npm ci --omit=dev

# Copy built application from build stage
COPY --from=build /app/dist ./dist

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user if it doesn't exist
RUN if ! id -u appuser > /dev/null 2>&1; then useradd -m -u 1001 appuser; fi && \
    chown -R $(id -u appuser):$(id -g appuser) /app
USER appuser

# Environment variables
ENV NODE_ENV=production \
    PORT=3002

# Expose port
EXPOSE 3002

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:3002/health || exit 1

# Start application
CMD ["node", "dist/index.js"]
