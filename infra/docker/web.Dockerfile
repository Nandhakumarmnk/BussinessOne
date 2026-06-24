# Build the React SPA (apps/web) and bake it into a Caddy image that serves the
# SPA and reverse-proxies API routes to the api container. Build context = repo root.

FROM node:20-alpine AS build
WORKDIR /repo
RUN corepack enable
COPY . .
# @erp/web is self-contained (react + vite); scope the install to it so we don't
# pull the mobile app's React Native dependencies.
RUN pnpm install --frozen-lockfile --filter "@erp/web..." \
 || pnpm install --filter "@erp/web..."
RUN pnpm --filter @erp/web build

FROM caddy:2-alpine
COPY infra/docker/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /repo/apps/web/dist /srv
