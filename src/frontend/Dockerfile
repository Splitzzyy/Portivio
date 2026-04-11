# syntax=docker/dockerfile:1.7
# Portivio frontend — Angular 18 built with Bun, served by nginx

# ---------- Build stage ----------
FROM oven/bun:1-alpine AS build
WORKDIR /app

# Install deps first for layer caching
COPY package.json bun.lock ./
RUN bun install --frozen-lockfile

# Copy source and build for production
COPY . .
ARG API_URL=http://backend:5274
ENV NG_APP_API_URL=$API_URL
RUN bun run prod

# ---------- Runtime stage ----------
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/portivio /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# Banner log on startup so the mapped port is visible in `docker compose up`
CMD ["sh", "-c", "echo '==> [portivio-frontend] nginx serving on http://0.0.0.0:80'; exec nginx -g 'daemon off;'"]
