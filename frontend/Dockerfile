# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM node:20-alpine AS build
WORKDIR /src

# Copy only the manifest files first so `npm ci` is cached separately from
# source changes — this step only re-runs when package.json/lock actually change.
COPY package.json package-lock.json* ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# ---- Runtime stage ----
# The @angular-devkit/build-angular:application builder (used here) nests
# output under dist/<project>/browser/ — not directly under dist/<project>/.
FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/daftech-crm/browser/ /usr/share/nginx/html/

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/ || exit 1
