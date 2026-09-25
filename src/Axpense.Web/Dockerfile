# Build context is this folder (src/Axpense.Web).
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
# VITE_API_BASE_URL is intentionally left unset: api.ts defaults to "" (relative URLs), and
# Caddy (this same container, see Caddyfile) proxies /api/* to the api service on the same
# origin/domain, so the built SPA never needs an absolute backend URL baked in.
RUN npm run build

FROM caddy:2-alpine AS final
COPY --from=build /app/dist /usr/share/caddy
COPY Caddyfile /etc/caddy/Caddyfile
