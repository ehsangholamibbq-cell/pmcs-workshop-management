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
. tools/qa/typography-env.sh

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
    grep -E -A 12 'Portfolio report worker loop failed|Portfolio report run .* failed unexpectedly' "${log_file}" | tail -100 >&2 || true
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
  Collaboration__Enabled=true \
  ReportingCenter__Phase1Enabled=true \
  ReportingCenter__OutputAccessEnabled=true \
  ReportingCenter__WorkerEnabled="${worker_enabled}" \
  ReportingCenter__PollSeconds=1 \
  ReportingCenter__PdfLicense="${pdf_license}" \
  ReportingCenter__PdfRegularFontPath="${PMCS_PDF_REGULAR_FONT_PATH}" \
  ReportingCenter__PdfBoldFontPath="${PMCS_PDF_BOLD_FONT_PATH}" \
  ReportingCenter__PdfRegularFontSha256="${PMCS_PDF_REGULAR_FONT_SHA256}" \
  ReportingCenter__PdfBoldFontSha256="${PMCS_PDF_BOLD_FONT_SHA256}" \
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
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-core
psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --command "update identity_access.project_memberships set status = 'Suspended' where id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb' and tenant_id = '11111111-1111-1111-1111-111111111111' and project_id = '33333333-3333-3333-3333-333333333333';" >/dev/null
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-revoked
psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --command "update identity_access.project_memberships set status = 'Active' where id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb' and tenant_id = '11111111-1111-1111-1111-111111111111' and project_id = '33333333-3333-3333-3333-333333333333';" >/dev/null
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-interactions
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-live
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-attachment
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-governance
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- prepare-reporting-portfolio-retry
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
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-project-progress
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-project-financial-position
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-project-commercial-procurement-supply
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-project-technical-office
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-project-quality-hse
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-project-governance-action
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-portfolio-summary
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-portfolio-retry
PMCS_QA_BASE_URL="${qa_base_url}" ./tools/qa/verify-reporting-portfolio-security.sh

stop_api
start_api false Unconfigured
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-cancellation
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-reporting-portfolio-cancellation

stop_api
./tools/qa/verify-reporting-recovery.sh
./tools/qa/verify-reporting-worker-revocation.sh
./tools/qa/verify-reporting-capacity.sh
./tools/qa/verify-reporting-observability.sh
./tools/qa/verify-reporting-orphan-remediation.sh

./tools/qa/verify-database.sh
./tools/qa/verify-files-database.sh
./tools/qa/verify-sync-database.sh

# Run INT1 on the isolated QA database only after the historical baseline assertions.
./tools/qa/verify-int1-reference.sh

# Conversion fixtures are created after the reporting Golden and baseline
# database assertions, so new official Action/Issue rows cannot change them.
start_api false Unconfigured
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-action-conversions
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-technical-conversions
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-field-evidence-conversions
stop_api
conversion_state="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align --command "select (select count(*) from collaboration.message_conversions where project_id = '33333333-3333-3333-3333-333333333333')::text || '|' || (select count(*) from action_control.actions where id = 'ca110000-0000-4000-8000-000000000201' and source_fact_id is null and source_message_id is not null)::text || '|' || (select count(*) from action_control.issues where id = 'ca110000-0000-4000-8000-000000000202' and source_module = 'collaboration' and source_revision = 1)::text || '|' || (select count(*) from technical_office.rfis where id = 'ca110000-0000-4000-8000-000000000302')::text || '|' || (select count(*) from technical_office.documents where id = 'ca110000-0000-4000-8000-000000000303')::text || '|' || (select count(*) from technical_office.document_revisions where document_id = 'ca110000-0000-4000-8000-000000000303' and sha256 is not null)::text || '|' || (select count(*) from foundation.audit_events where event_type in ('ProjectChatConvertedToAction','ProjectChatConvertedToIssue','ProjectChatConvertedToRfi','ProjectChatConvertedToTechnicalDocument') and project_id = '33333333-3333-3333-3333-333333333333')::text;")"
if [[ "${conversion_state}" != "6|1|1|1|1|1|4" ]]; then
  echo "Collaboration conversion lineage and owner state diverged: ${conversion_state}." >&2
  exit 1
fi
field_conversion_state="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align --command "select (select count(*) from field_operations.daily_report_facts where id = 'ca110000-0000-4000-8000-000000000403' and reference_code like 'chat:%')::text || '|' || (select count(*) from evidence.files where id = 'ca110000-0000-4000-8000-000000000405' and source_message_id is not null and source_document_id = 'ca110000-0000-4000-8000-000000000404' and source_document_version = 1 and status = 'Uploaded')::text || '|' || (select count(*) from foundation.audit_events where event_type in ('ProjectChatConvertedToDailyFact','ProjectChatConvertedToEvidence') and project_id = '33333333-3333-3333-3333-333333333333')::text;")"
if [[ "${field_conversion_state}" != "1|1|2" ]]; then
  echo "Field and evidence conversion owner state diverged: ${field_conversion_state}." >&2
  exit 1
fi

# Final COL1 gate: a shared destination is serialized across two authorized
# actors, then official file access is revoked with the project membership.
start_api false Unconfigured
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-qualification
race_state="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align --command "select (select count(*) from collaboration.message_conversions where destination_type = 'RFI' and destination_id = 'cb120001-0000-4000-8000-000000000502')::text || '|' || (select count(*) from technical_office.rfis where id = 'cb120001-0000-4000-8000-000000000502')::text || '|' || (select count(*) from foundation.audit_events where event_type = 'ProjectChatConvertedToRfi' and resource_id = 'cb120001-0000-4000-8000-000000000502')::text;")"
if [[ "${race_state}" != "1|1|1" ]]; then
  echo "Concurrent owner conversion diverged: ${race_state}." >&2
  exit 1
fi
psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --command "update identity_access.project_memberships set status = 'Suspended' where id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb' and tenant_id = '11111111-1111-1111-1111-111111111111' and project_id = '33333333-3333-3333-3333-333333333333';" >/dev/null
PMCS_QA_BASE_URL="${qa_base_url}" PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- verify-collaboration-official-revoked
psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --command "update identity_access.project_memberships set status = 'Active' where id = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb' and tenant_id = '11111111-1111-1111-1111-111111111111' and project_id = '33333333-3333-3333-3333-333333333333';" >/dev/null
stop_api
