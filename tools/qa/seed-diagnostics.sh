#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL.}"
: "${PMCS_QA_AUTH_KEY:?Set PMCS_QA_AUTH_KEY to an independent secret of at least 32 bytes.}"
: "${PMCS_QA_S3_ENDPOINT:?Set PMCS_QA_S3_ENDPOINT.}"
: "${PMCS_QA_S3_ACCESS_KEY:?Set PMCS_QA_S3_ACCESS_KEY.}"
: "${PMCS_QA_S3_SECRET_KEY:?Set PMCS_QA_S3_SECRET_KEY.}"
: "${PMCS_QA_S3_BUCKET:?Set PMCS_QA_S3_BUCKET.}"

command -v curl >/dev/null
command -v docker >/dev/null
command -v dotnet >/dev/null
command -v psql >/dev/null

dotnet build PMCS.slnx --configuration Release --nologo --verbosity minimal >&2
database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
connected_database="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align --command 'select current_database();')"
if [[ "${connected_database}" != "${database_name}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

port="${PMCS_QA_PORT:-5090}"
if ! [[ "${port}" =~ ^[0-9]+$ ]] || (( port < 1024 || port > 65535 )); then
  echo "PMCS_QA_PORT must be an integer between 1024 and 65535." >&2
  exit 2
fi

log_file="$(mktemp)"
api_pid=""
stop_api() {
  if [[ -n "${api_pid}" ]] && kill -0 "${api_pid}" 2>/dev/null; then
    kill "${api_pid}"
    wait "${api_pid}" 2>/dev/null || true
  fi
  api_pid=""
}

cleanup() {
  exit_code=$?
  set +e
  if (( exit_code != 0 )); then
    sed -n '1,240p' "${log_file}" >&2
  fi
  stop_api
  rm -f -- "${log_file}"
  exit "${exit_code}"
}
trap cleanup EXIT

start_api() {
  local worker_enabled="$1"
  local pdf_license="${2:-Unconfigured}"
  : >"${log_file}"
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://127.0.0.1:${port}" \
  ConnectionStrings__Pmcs="${PMCS_QA_CONNECTION_STRING}" \
  PMCS_DEV_IDENTITY_ENABLED=false \
  PMCS_SEED_ENABLED=true \
  PMCS_QA_GATEWAY_ENABLED=true \
  PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
  ProjectStateRefresh__Enabled=false \
  AdvisoryIntelligence__WorkerEnabled=false \
  ReportingCenter__Phase1Enabled=true \
  ReportingCenter__OutputAccessEnabled=true \
  ReportingCenter__WorkerEnabled="${worker_enabled}" \
  ReportingCenter__PollSeconds=1 \
  ReportingCenter__PdfLicense="${pdf_license}" \
  ReportingCenter__PdfRegularFontPath="${PWD}/assets/reporting/fonts/DejaVuSans.ttf" \
  ReportingCenter__PdfBoldFontPath="${PWD}/assets/reporting/fonts/DejaVuSans-Bold.ttf" \
  ReportingCenter__PdfRegularFontSha256=ae7b7855e115a5966d8b1b3f80f254ccc117ec86f9965e202ee2940453837280 \
  ReportingCenter__PdfBoldFontSha256=5c1247acef7f2b8522a31742c76d6adcb5569bacc0be7ceaa4dc39dd252ce895 \
  ReportingCenter__PdfRendererImageDigest=sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c \
  ObjectStorage__ServiceUrl="${PMCS_QA_S3_ENDPOINT}" \
  ObjectStorage__AccessKey="${PMCS_QA_S3_ACCESS_KEY}" \
  ObjectStorage__SecretKey="${PMCS_QA_S3_SECRET_KEY}" \
  ObjectStorage__BucketName="${PMCS_QA_S3_BUCKET}" \
  ObjectStorage__Region="us-east-1" \
  ObjectStorage__ForcePathStyle=true \
  ObjectStorage__CreateBucketIfMissing=true \
  dotnet run --project src/backend/Pmcs.Api/Pmcs.Api.csproj --configuration Release --no-build --no-launch-profile >"${log_file}" 2>&1 &
  api_pid=$!

  local ready=false
  for _ in {1..60}; do
    if ! kill -0 "${api_pid}" 2>/dev/null; then
      echo "PMCS API exited before QA readiness." >&2
      exit 1
    fi
    if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
      ready=true
      break
    fi
    sleep 1
  done

  if [[ "${ready}" != true ]]; then
    echo "PMCS QA API did not become ready within 60 seconds." >&2
    exit 1
  fi
}

start_api true Unconfigured

qa_base_url="http://127.0.0.1:${port}"
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- probe
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-golden
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-files
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-sync
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-exploratory
PMCS_QA_BASE_URL="${qa_base_url}" ./tools/qa/verify-reporting-security.sh
PMCS_QA_BASE_URL="${qa_base_url}" ./tools/qa/verify-reporting-object-security.sh

stop_api
start_api true Community
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-pdf-golden
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-periodic
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-executive-state

stop_api
start_api false Unconfigured
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-cancellation

stop_api
./tools/qa/verify-reporting-recovery.sh
./tools/qa/verify-reporting-worker-revocation.sh
./tools/qa/verify-reporting-capacity.sh
./tools/qa/verify-reporting-observability.sh
./tools/qa/verify-reporting-orphan-remediation.sh

./tools/qa/verify-database.sh
./tools/qa/verify-files-database.sh
./tools/qa/verify-sync-database.sh
