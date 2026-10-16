# The web image (ADR 0033): the production build of the Angular app — the market's language at /, the source language
# under /en/ (ADR 0032) — served by Caddy, which also forwards /api to the Api: one origin, so no CORS.
# Built from the repository root: docker build -f deploy/web.Dockerfile .

FROM node:22-alpine AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM caddy:2.10-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /web/dist/apptemplate-web/browser /srv
