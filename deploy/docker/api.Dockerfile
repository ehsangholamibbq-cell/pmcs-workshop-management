FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
ARG PMCS_RELEASE_COMMIT=development
ARG PMCS_RELEASE_VERSION=0.0.0-dev
ARG PMCS_RELEASE_BUILT_AT=unknown
WORKDIR /source
COPY . .
RUN dotnet restore PMCS.slnx
RUN dotnet publish src/backend/Pmcs.Api/Pmcs.Api.csproj -c Release --no-restore -o /app \
    /p:PMCSReleaseCommit="${PMCS_RELEASE_COMMIT}" \
    /p:PMCSReleaseVersion="${PMCS_RELEASE_VERSION}" \
    /p:PMCSReleaseBuiltAt="${PMCS_RELEASE_BUILT_AT}"

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS runtime
ARG PMCS_RELEASE_COMMIT=development
ARG PMCS_RELEASE_VERSION=0.0.0-dev
LABEL org.opencontainers.image.revision="${PMCS_RELEASE_COMMIT}" \
      org.opencontainers.image.version="${PMCS_RELEASE_VERSION}"
USER root
WORKDIR /app
COPY --from=build /app .
COPY assets/reporting/fonts/DejaVuSans.ttf /app/fonts/DejaVuSans.ttf
COPY assets/reporting/fonts/DejaVuSans-Bold.ttf /app/fonts/DejaVuSans-Bold.ttf
COPY assets/reporting/fonts/LICENSE.txt /app/licenses/dejavu/LICENSE.txt
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pmcs.Api.dll"]
