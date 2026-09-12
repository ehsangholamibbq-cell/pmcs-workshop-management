FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG PMCS_RELEASE_COMMIT=development
ARG PMCS_RELEASE_VERSION=0.0.0-dev
LABEL org.opencontainers.image.revision="${PMCS_RELEASE_COMMIT}" \
      org.opencontainers.image.version="${PMCS_RELEASE_VERSION}"
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pmcs.Api.dll"]
