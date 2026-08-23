# Build
FROM node:22-alpine AS build
WORKDIR /src

# Install from the lockfile as its own layer, so a source-only change does not
# re-resolve the dependency tree.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Baked in at build time, because Vite inlines env vars into the bundle - there is
# no runtime configuration in a static build. Override with
# `--build-arg VITE_API_URL=...` to point the UI at a different API.
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

# Serve
FROM nginx:1.27-alpine AS runtime
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
