FROM node:24-alpine AS dependencies
WORKDIR /app
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci

FROM node:24-alpine AS build
ARG PMCS_RELEASE_COMMIT=development
ARG PMCS_RELEASE_VERSION=0.0.0-dev
ARG PMCS_RELEASE_BUILT_AT=unknown
ARG PMCS_RELEASE_REQUIRED=false
ENV PMCS_RELEASE_COMMIT=${PMCS_RELEASE_COMMIT} \
    PMCS_RELEASE_VERSION=${PMCS_RELEASE_VERSION} \
    PMCS_RELEASE_BUILT_AT=${PMCS_RELEASE_BUILT_AT} \
    PMCS_RELEASE_REQUIRED=${PMCS_RELEASE_REQUIRED}
WORKDIR /app
COPY --from=dependencies /app/node_modules ./node_modules
COPY src/web .
RUN npm run build

FROM node:24-alpine AS runtime
ARG PMCS_RELEASE_COMMIT=development
ARG PMCS_RELEASE_VERSION=0.0.0-dev
LABEL org.opencontainers.image.revision="${PMCS_RELEASE_COMMIT}" \
      org.opencontainers.image.version="${PMCS_RELEASE_VERSION}"
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public
EXPOSE 3000
CMD ["node", "server.js"]
