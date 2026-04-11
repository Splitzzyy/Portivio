# Frontend Dockerfile (build Angular and serve with nginx)
# Build stage
FROM oven/bun:1 AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package.json bun.lock ./
RUN bun install

# Copy source and build
COPY . .
ARG API_URL=http://backend:5274
ENV NG_APP_API_URL=$API_URL

# Use the production build with bun
RUN bun run build -- --configuration production || bun run build

# Runtime stage using nginx
FROM nginx:alpine
COPY --from=build /app/dist/portivio /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
