#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_TEST_CONNECTION_STRING:?Set PMCS_TEST_CONNECTION_STRING to an isolated PostgreSQL database.}"
: "${PMCS_TEST_S3_ENDPOINT:?Set PMCS_TEST_S3_ENDPOINT to an isolated S3-compatible service.}"
: "${PMCS_TEST_S3_ACCESS_KEY:?Set PMCS_TEST_S3_ACCESS_KEY.}"
: "${PMCS_TEST_S3_SECRET_KEY:?Set PMCS_TEST_S3_SECRET_KEY.}"
: "${PMCS_TEST_S3_BUCKET:?Set PMCS_TEST_S3_BUCKET.}"
: "${PMCS_VERIFICATION_DATABASE_URL:?Set PMCS_VERIFICATION_DATABASE_URL to the integration PostgreSQL URI.}"

command -v curl >/dev/null
command -v dotnet >/dev/null
command -v sha256sum >/dev/null
command -v psql >/dev/null

port="${PMCS_TEST_PORT:-5088}"
if ! [[ "${port}" =~ ^[0-9]+$ ]] || (( port < 1024 || port > 65535 )); then
  echo "PMCS_TEST_PORT must be an integer between 1024 and 65535." >&2
  exit 2
fi

log_file="$(mktemp)"
temporary_directory="$(mktemp -d)"
api_pid=""
current_step="starting PMCS API"
cleanup() {
  exit_code=$?
  set +e
  if (( exit_code != 0 )); then
    echo "Integration smoke failed during: ${current_step}." >&2
    sed -n '1,240p' "${log_file}" >&2
  fi
  if [[ -n "${api_pid}" ]] && kill -0 "${api_pid}" 2>/dev/null; then
    kill "${api_pid}"
    wait "${api_pid}" 2>/dev/null || true
  fi
  rm -f -- "${log_file}"
  rm -rf -- "${temporary_directory}"
  exit "${exit_code}"
}
trap cleanup EXIT

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://127.0.0.1:${port}" \
ConnectionStrings__Pmcs="${PMCS_TEST_CONNECTION_STRING}" \
PMCS_DEV_IDENTITY_ENABLED=true \
PMCS_SEED_ENABLED=true \
ProjectStateRefresh__Enabled=false \
AdvisoryIntelligence__WorkerEnabled=false \
ObjectStorage__ServiceUrl="${PMCS_TEST_S3_ENDPOINT}" \
ObjectStorage__AccessKey="${PMCS_TEST_S3_ACCESS_KEY}" \
ObjectStorage__SecretKey="${PMCS_TEST_S3_SECRET_KEY}" \
ObjectStorage__BucketName="${PMCS_TEST_S3_BUCKET}" \
ObjectStorage__Region="us-east-1" \
ObjectStorage__ForcePathStyle=true \
ObjectStorage__CreateBucketIfMissing=true \
RateLimiting__GeneralPermitLimit=200 \
RateLimiting__GeneralWindowSeconds=60 \
RateLimiting__InsightPermitLimit=5 \
RateLimiting__InsightWindowSeconds=60 \
dotnet run \
  --project src/backend/Pmcs.Api/Pmcs.Api.csproj \
  --configuration Release \
  --no-build \
  --no-launch-profile >"${log_file}" 2>&1 &
api_pid=$!

current_step="waiting for PMCS API readiness"
ready=false
for _ in {1..60}; do
  if ! kill -0 "${api_pid}" 2>/dev/null; then
    echo "PMCS API exited before becoming ready." >&2
    sed -n '1,240p' "${log_file}" >&2
    exit 1
  fi

  if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
    ready=true
    break
  fi
  sleep 1
done

if [[ "${ready}" != true ]]; then
  echo "PMCS API did not become ready within 60 seconds." >&2
  sed -n '1,240p' "${log_file}" >&2
  exit 1
fi

current_step="checking PMCS health and security headers"
curl --silent --fail "http://127.0.0.1:${port}/health/live" | grep -q '"status":"Healthy"'
curl --silent --fail "http://127.0.0.1:${port}/health/ready" | grep -q '"postgres"'
curl --silent --fail --dump-header - --output /dev/null "http://127.0.0.1:${port}/api/v1/foundation" | \
  grep -qi '^X-Content-Type-Options: nosniff'

unauthenticated_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "http://127.0.0.1:${port}/api/v1/projects/")"
if [[ "${unauthenticated_status}" != "401" ]]; then
  echo "Expected unauthenticated project access to return 401; received ${unauthenticated_status}." >&2
  exit 1
fi

current_step="checking the development identity session"
tenant_id="11111111-1111-1111-1111-111111111111"
user_id="22222222-2222-2222-2222-222222222222"
curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/session" | grep -q '"authentication":"development-adapter"'

current_step="checking the fail-closed platform extension catalog"
module_catalog_unauthenticated_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  "http://127.0.0.1:${port}/api/v1/platform/modules/")"
if [[ "${module_catalog_unauthenticated_status}" != "401" ]]; then
  echo "Expected unauthenticated module catalog access to return 401; received ${module_catalog_unauthenticated_status}." >&2
  exit 1
fi

module_catalog="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/platform/modules/")"
grep -q '"schemaVersion":"pmcs.module/v1"' <<<"${module_catalog}"
grep -q '"schemaVersion":"pmcs.module/legacy-v1"' <<<"${module_catalog}"
grep -q '"moduleId":"platform.foundation"' <<<"${module_catalog}"
grep -q '"moduleId":"documents.shared"' <<<"${module_catalog}"
grep -q '"key":"documents.quarantine.release"' <<<"${module_catalog}"
grep -q '"name":"documents.asset.released","version":1' <<<"${module_catalog}"
grep -q '"id":"platform.modules.describe"' <<<"${module_catalog}"
grep -q '"name":"platform.module-catalog.snapshot","version":1' <<<"${module_catalog}"

curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/platform/modules/platform.foundation" | \
  grep -q '"moduleId":"platform.foundation"'

unknown_module_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/platform/modules/platform.unknown")"
if [[ "${unknown_module_status}" != "404" ]]; then
  echo "Expected an unknown module contract to return 404; received ${unknown_module_status}." >&2
  exit 1
fi

current_step="checking project creation, idempotent replay and activation boundary"
setup_key="integration-project-setup"
setup_payload='{"code":"CI-AUDIT-01","name":"Integration lifecycle project","contractModel":"GeneralContracting","planningMode":"SimpleWorkList","budgetMode":"SetupRequired","qualityMode":"SetupRequired","hseMode":"NotEnabled","timeZone":"Asia/Tehran","financeMode":"SetupRequired","baseCurrencyCode":"IRR","procurementMode":"SetupRequired","projectType":"Building","executionPhase":"PreConstruction","countryCode":"IR","region":"Tehran","startDate":"2026-09-01","plannedFinishDate":"2027-09-01","shortDescription":"Controlled integration lifecycle","unitSystem":"Metric","dailyCutoffLocalTime":"18:00:00","reportingFrequency":"WorkingDays","dailyReportWorkflow":"OneStepApproval","offlinePolicyAccepted":true,"calendarMode":"WorkingWeek","workingDays":["Saturday","Sunday","Monday","Tuesday","Wednesday","Thursday"]}'
setup_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: ${setup_key}" \
  --header 'Content-Type: application/json' \
  --data "${setup_payload}" \
  "http://127.0.0.1:${port}/api/v1/projects")"
setup_project_id="$(sed -n 's/.*"id":"\([^"]*\)".*/\1/p' <<<"${setup_response}")"
setup_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${setup_response}")"
grep -q '"status":"Draft"' <<<"${setup_response}"
if [[ -z "${setup_project_id}" || -z "${setup_revision}" ]]; then
  echo "Project setup did not return a draft identity and revision." >&2
  exit 1
fi

setup_replay="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: ${setup_key}" \
  --header 'Content-Type: application/json' \
  --data "${setup_payload}" \
  "http://127.0.0.1:${port}/api/v1/projects")"
# PostgreSQL jsonb may normalize whitespace in the persisted replay body.
grep -Eq "\"id\"[[:space:]]*:[[:space:]]*\"${setup_project_id}\"" <<<"${setup_replay}"

setup_locations="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${setup_project_id}/locations")"
grep -q '"code":"ROOT"' <<<"${setup_locations}"

current_step="assigning project leadership and checking effective permissions"
curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-project-manager-membership' \
  --header 'Content-Type: application/json' \
  --data '{"roleCode":"ProjectManager"}' \
  "http://127.0.0.1:${port}/api/v1/identity/users/${user_id}/memberships/${setup_project_id}" | grep -q '"roleCode":"ProjectManager"'

permission_preview="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/identity/permissions/preview?userId=${user_id}&projectId=${setup_project_id}&operation=projects.read")"
grep -q '"allowed":true' <<<"${permission_preview}"
grep -q '"policyVersion":"pmcs-rbac-v1"' <<<"${permission_preview}"

work_review_permission="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/identity/permissions/preview?userId=${user_id}&projectId=${setup_project_id}&operation=field.daily-reports.review")"
grep -q '"allowed":true' <<<"${work_review_permission}"

readiness_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${setup_project_id}/readiness")"
grep -q '"isReady":true' <<<"${readiness_response}"
grep -q '"completionPercent":100' <<<"${readiness_response}"

draft_mutation_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-draft-mutation' \
  --header 'Content-Type: application/json' \
  --data '{"clientGeneratedId":null,"reportDate":"2099-02-01","locationName":null,"narrative":null}' \
  "http://127.0.0.1:${port}/api/v1/projects/${setup_project_id}/daily-reports")"
if [[ "${draft_mutation_status}" != "409" ]]; then
  echo "Expected a draft project operation to return 409; received ${draft_mutation_status}." >&2
  exit 1
fi

curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-project-activate' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${setup_revision}}" \
  "http://127.0.0.1:${port}/api/v1/projects/${setup_project_id}/activate" | grep -q '"status":"Active"'

current_step="checking the required project Location boundary"
location_guard_report="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-location-guard-report' \
  --header 'Content-Type: application/json' \
  --data '{"clientGeneratedId":null,"reportDate":"2099-02-02","locationName":null,"narrative":"Location invariant probe"}' \
  "http://127.0.0.1:${port}/api/v1/projects/${setup_project_id}/daily-reports")"
location_guard_report_id="$(sed -n 's/.*"id":"\([^"]*\)".*/\1/p' <<<"${location_guard_report}")"
location_guard_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${location_guard_report}")"
if [[ -z "${location_guard_report_id}" || -z "${location_guard_revision}" ]]; then
  echo "Location guard setup did not return a daily report identity and revision." >&2
  exit 1
fi
missing_location_status="$(curl --silent --output "${temporary_directory}/missing-location.json" --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-location-required' \
  --header 'Content-Type: application/json' \
  --data "{\"kind\":\"Note\",\"description\":\"Missing Location must be rejected\",\"baseRevision\":${location_guard_revision}}" \
  "http://127.0.0.1:${port}/api/v1/projects/${setup_project_id}/daily-reports/${location_guard_report_id}/facts")"
if [[ "${missing_location_status}" != "422" ]] || \
    ! grep -q '"code":"project.location.required"' "${temporary_directory}/missing-location.json"; then
  echo "Expected a fact without Location to return project.location.required (422); received ${missing_location_status}." >&2
  exit 1
fi

current_step="creating an offline sync session"
project_id="33333333-3333-3333-3333-333333333333"
location_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/locations")"
location_id="$(sed -n 's/.*"id":"\([^"]*\)".*"code":"ROOT".*/\1/p' <<<"${location_response}")"
if [[ -z "${location_id}" ]]; then
  echo "Project setup did not expose its root Location." >&2
  exit 1
fi
device_id="integration-device-001"
device_time="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
handshake_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${device_id}\",\"projectId\":\"${project_id}\",\"deviceName\":\"Integration browser\",\"platform\":\"CI\",\"appVersion\":\"0.2.0\",\"protocolVersion\":3,\"localSchemaVersion\":6,\"lastCheckpoint\":null,\"deviceTime\":\"${device_time}\",\"queue\":{\"pendingOperations\":2,\"pendingAttachments\":0,\"pendingAttachmentBytes\":0,\"oldestOperationAt\":\"${device_time}\"}}" \
  "http://127.0.0.1:${port}/api/v1/sync/handshake")"
session_id="$(sed -n 's/.*"sessionId":"\([^"]*\)".*/\1/p' <<<"${handshake_response}")"
lease_id="$(sed -n 's/.*"leaseId":"\([^"]*\)".*/\1/p' <<<"${handshake_response}")"
authorization_version="$(sed -n 's/.*"authorizationVersion":\([0-9][0-9]*\).*/\1/p' <<<"${handshake_response}")"
if [[ -z "${session_id}" || -z "${lease_id}" || -z "${authorization_version}" ]]; then
  echo "Sync handshake did not return a session and offline lease." >&2
  exit 1
fi

current_step="pushing an offline operation"
report_id="44444444-4444-4444-4444-444444444444"
fact_id="55555555-5555-5555-5555-555555555555"
operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1B"
push_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${device_id}\",\"operations\":[{\"operationId\":\"${operation_id}\",\"projectId\":\"${project_id}\",\"entityType\":\"DailyReport\",\"entityId\":\"${report_id}\",\"commandType\":\"CaptureDailyReportFact\",\"baseRevision\":null,\"payloadSchemaVersion\":1,\"createdAtDevice\":\"${device_time}\",\"payload\":{\"factId\":\"${fact_id}\",\"reportDate\":\"2099-01-01\",\"locationName\":null,\"kind\":\"Note\",\"description\":\"Integration observed fact\",\"locationId\":\"${location_id}\"},\"offlineLeaseId\":\"${lease_id}\",\"authorizationVersion\":${authorization_version},\"localSequence\":1,\"dependencies\":[],\"correlationId\":\"integration-sync-1\",\"deviceTimezoneOffsetMinutes\":0}]}" \
  "http://127.0.0.1:${port}/api/v1/sync/operations")"
grep -q '"status":"Applied"' <<<"${push_response}"

current_step="replaying the same immutable offline operation"
replay_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${device_id}\",\"operations\":[{\"operationId\":\"${operation_id}\",\"projectId\":\"${project_id}\",\"entityType\":\"DailyReport\",\"entityId\":\"${report_id}\",\"commandType\":\"CaptureDailyReportFact\",\"baseRevision\":null,\"payloadSchemaVersion\":1,\"createdAtDevice\":\"${device_time}\",\"payload\":{\"factId\":\"${fact_id}\",\"reportDate\":\"2099-01-01\",\"locationName\":null,\"kind\":\"Note\",\"description\":\"Integration observed fact\",\"locationId\":\"${location_id}\"},\"offlineLeaseId\":\"${lease_id}\",\"authorizationVersion\":${authorization_version},\"localSequence\":1,\"dependencies\":[],\"correlationId\":\"integration-sync-1\",\"deviceTimezoneOffsetMinutes\":0}]}" \
  "http://127.0.0.1:${port}/api/v1/sync/operations")"
grep -q '"status":"Applied"' <<<"${replay_response}"
grep -q '"wasReplay":true' <<<"${replay_response}"

current_step="checking Daily Report correction lineage, My Work and notification receipts"
report_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${report_id}")"
report_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${report_response}")"
submitted_report="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-report-submit' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${report_revision}}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${report_id}/submit")"
submitted_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${submitted_report}")"
grep -q '"status":"Submitted"' <<<"${submitted_report}"

notifications_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/notifications?unreadOnly=true")"
notification_id="$(sed -n 's/.*"id":"\([^"]*\)".*/\1/p' <<<"${notifications_response}")"
notification_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${notifications_response}")"
grep -q '"category":"DailyReportReview"' <<<"${notifications_response}"
if [[ -z "${notification_id}" || -z "${notification_revision}" ]]; then
  echo "Submitted report did not create a recipient-scoped notification." >&2
  exit 1
fi
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-notification-ack' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${notification_revision}}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/notifications/${notification_id}/acknowledge" | \
  grep -q '"acknowledgedAt":'

approved_report="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-report-approve' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${submitted_revision},\"comment\":\"Integration approval\"}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${report_id}/approve")"
approved_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${approved_report}")"
grep -q '"status":"Approved"' <<<"${approved_report}"

correction_id="99999999-9999-4999-8999-999999999999"
correction_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-report-correction' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${correction_id}\",\"baseRevision\":${approved_revision},\"reason\":\"Integration correction lineage\"}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${report_id}/corrections")"
correction_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${correction_response}")"
grep -q '"versionNumber":2' <<<"${correction_response}"
grep -q "\"supersedesReportId\":\"${report_id}\"" <<<"${correction_response}"
grep -q "\"copiedFromFactId\":\"${fact_id}\"" <<<"${correction_response}"

correction_submitted="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-correction-submit' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${correction_revision}}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${correction_id}/submit")"
correction_submitted_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${correction_submitted}")"

my_work_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/my-work")"
grep -q '"kind":"DailyReportReview"' <<<"${my_work_response}"
grep -q "\"targetId\":\"${correction_id}\"" <<<"${my_work_response}"

curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-correction-approve' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${correction_submitted_revision},\"comment\":\"Replacement approved\"}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${correction_id}/approve" | \
  grep -q '"status":"Approved"'

superseded_report="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/daily-reports/${report_id}")"
grep -q '"status":"Superseded"' <<<"${superseded_report}"
grep -q "\"supersededByReportId\":\"${correction_id}\"" <<<"${superseded_report}"

current_step="creating an evidence upload session"
evidence_id="88888888-8888-8888-8888-888888888888"
evidence_file="${temporary_directory}/integration-evidence.pdf"
downloaded_file="${temporary_directory}/downloaded-evidence.pdf"
printf '%%PDF-1.7\n%% PMCS integration object storage evidence\n%%%%EOF\n' > "${evidence_file}"
evidence_size="$(wc -c < "${evidence_file}" | tr -d '[:space:]')"
evidence_sha="$(sha256sum "${evidence_file}" | cut -d ' ' -f 1)"
upload_session_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: integration-evidence-session" \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${evidence_id}\",\"dailyReportId\":\"${report_id}\",\"dailyFactId\":\"${fact_id}\",\"originalFileName\":\"integration-evidence.pdf\",\"contentType\":\"application/pdf\",\"sizeBytes\":${evidence_size},\"sha256\":\"${evidence_sha}\",\"capturedAtDevice\":\"${device_time}\"}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/evidence/upload-sessions")"
grep -q '"status":"PendingUpload"' <<<"${upload_session_response}"

current_step="uploading evidence content"
curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: integration-evidence-content" \
  --header 'Content-Type: application/pdf' \
  --data-binary "@${evidence_file}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/evidence/${evidence_id}/content" | \
  grep -q '"status":"Uploaded"'

current_step="downloading and verifying evidence content"
curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --output "${downloaded_file}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/evidence/${evidence_id}/content"
if [[ "$(sha256sum "${downloaded_file}" | cut -d ' ' -f 1)" != "${evidence_sha}" ]]; then
  echo "Evidence roundtrip changed the object content." >&2
  exit 1
fi

current_step="verifying shared document upload, quarantine, permission and release"
document_uploader_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
document_unregistered_id="50000000-0000-4000-8000-000000000001"
document_reader_id="${document_uploader_id}"
document_id="81000000-0000-4000-8000-000000000001"
document_denied_id="81000000-0000-4000-8000-000000000002"
document_infected_id="81000000-0000-4000-8000-000000000003"
document_version_id="81000000-0000-4000-8000-000000000004"
document_file="${temporary_directory}/shared-document.pdf"
document_download="${temporary_directory}/shared-document-download.pdf"
printf '%%PDF-1.7\n%% PMCS shared document foundation\n%%%%EOF\n' > "${document_file}"
document_size="$(wc -c < "${document_file}" | tr -d '[:space:]')"
document_sha="$(sha256sum "${document_file}" | cut -d ' ' -f 1)"
document_payload="{\"clientGeneratedId\":\"${document_id}\",\"projectId\":\"${project_id}\",\"ownerType\":\"ProjectGeneral\",\"ownerId\":\"${project_id}\",\"originalFileName\":\"shared-document.pdf\",\"contentType\":\"application/pdf\",\"sizeBytes\":${document_size},\"sha256\":\"${document_sha}\",\"classification\":\"Internal\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}"
document_session="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-session' \
  --header 'Content-Type: application/json' \
  --data "${document_payload}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions")"
grep -q '"status":"PendingUpload"' <<<"${document_session}"
grep -q '"versionNumber":1' <<<"${document_session}"

document_denied_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_unregistered_id}" \
  --header 'Idempotency-Key: integration-document-unregistered-denied' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${document_denied_id}\",\"projectId\":\"${project_id}\",\"ownerType\":\"ProjectGeneral\",\"ownerId\":\"${project_id}\",\"originalFileName\":\"denied.pdf\",\"contentType\":\"application/pdf\",\"sizeBytes\":${document_size},\"sha256\":\"${document_sha}\",\"classification\":\"Internal\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions")"
if [[ "${document_denied_status}" != "403" ]]; then
  echo "Expected an unregistered actor document upload to return 403; received ${document_denied_status}." >&2
  exit 1
fi

document_upload="$(curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-content' \
  --header 'Content-Type: application/pdf' \
  --data-binary "@${document_file}" \
  "http://127.0.0.1:${port}/api/v1/documents/${document_id}/content")"
grep -q '"status":"Quarantined"' <<<"${document_upload}"
grep -q '"scanVerdict":"Clean"' <<<"${document_upload}"
document_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${document_upload}")"
if [[ "${document_revision}" != "2" ]]; then
  echo "Expected the quarantined document revision to be 2; received ${document_revision}." >&2
  exit 1
fi

document_quarantine_download_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_reader_id}" \
  "http://127.0.0.1:${port}/api/v1/documents/${document_id}/content")"
if [[ "${document_quarantine_download_status}" != "409" ]]; then
  echo "Expected quarantined content download to return 409; received ${document_quarantine_download_status}." >&2
  exit 1
fi

document_uploader_release_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-uploader-release-denied' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${document_revision}}" \
  "http://127.0.0.1:${port}/api/v1/documents/${document_id}/release")"
if [[ "${document_uploader_release_status}" != "403" ]]; then
  echo "Expected a Site Supervisor quarantine release to return 403; received ${document_uploader_release_status}." >&2
  exit 1
fi

document_release="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-document-release' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${document_revision}}" \
  "http://127.0.0.1:${port}/api/v1/documents/${document_id}/release")"
grep -q '"status":"Released"' <<<"${document_release}"
grep -q '"revision":3' <<<"${document_release}"

curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_reader_id}" \
  --output "${document_download}" \
  "http://127.0.0.1:${port}/api/v1/documents/${document_id}/content"
if [[ "$(sha256sum "${document_download}" | cut -d ' ' -f 1)" != "${document_sha}" ]]; then
  echo "Shared document roundtrip changed the object content." >&2
  exit 1
fi

document_duplicate="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-duplicate' \
  --header 'Content-Type: application/json' \
  --data "${document_payload}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions")"
grep -q '"status":"Released"' <<<"${document_duplicate}"
grep -q '"versionNumber":1' <<<"${document_duplicate}"

document_version_session="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-version-2' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${document_version_id}\",\"projectId\":\"${project_id}\",\"ownerType\":\"ProjectGeneral\",\"ownerId\":\"${project_id}\",\"originalFileName\":\"shared-document.pdf\",\"contentType\":\"application/pdf\",\"sizeBytes\":${document_size},\"sha256\":\"${document_sha}\",\"classification\":\"Internal\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions")"
grep -q '"versionNumber":2' <<<"${document_version_session}"

infected_file="${temporary_directory}/scanner-test.txt"
printf 'EICAR-STANDARD-ANTIVIRUS-TEST-FILE' > "${infected_file}"
infected_size="$(wc -c < "${infected_file}" | tr -d '[:space:]')"
infected_sha="$(sha256sum "${infected_file}" | cut -d ' ' -f 1)"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-infected-session' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${document_infected_id}\",\"projectId\":\"${project_id}\",\"ownerType\":\"ProjectChat\",\"ownerId\":\"${report_id}\",\"originalFileName\":\"scanner-test.txt\",\"contentType\":\"text/plain\",\"sizeBytes\":${infected_size},\"sha256\":\"${infected_sha}\",\"classification\":\"Internal\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions" >/dev/null
infected_response_file="${temporary_directory}/infected-response.json"
infected_status="$(curl --silent --output "${infected_response_file}" --write-out '%{http_code}' \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${document_uploader_id}" \
  --header 'Idempotency-Key: integration-document-infected-content' \
  --header 'Content-Type: text/plain' \
  --data-binary "@${infected_file}" \
  "http://127.0.0.1:${port}/api/v1/documents/${document_infected_id}/content")"
if [[ "${infected_status}" != "422" ]]; then
  echo "Expected the scanner test file to return 422; received ${infected_status}." >&2
  exit 1
fi
grep -q '"code":"documents.scan.infected"' "${infected_response_file}"
grep -q '"status":"Rejected"' "${infected_response_file}"

infected_release_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-document-infected-release' \
  --header 'Content-Type: application/json' \
  --data '{"baseRevision":2}' \
  "http://127.0.0.1:${port}/api/v1/documents/${document_infected_id}/release")"
if [[ "${infected_release_status}" != "400" ]]; then
  echo "Expected an infected document release to fail closed with 400; received ${infected_release_status}." >&2
  exit 1
fi

current_step="verifying shared document database, audit and outbox isolation"
document_database_state="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select status || '|' || scan_verdict || '|' || revision::text || '|' || version_number::text || '|' || (storage_etag is not null and released_by = '${user_id}')::text from documents.assets where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${document_id}';")"
if [[ "${document_database_state}" != "Released|Clean|3|1|true" ]]; then
  echo "Unexpected released document database state: ${document_database_state}." >&2
  exit 1
fi
infected_database_state="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select status || '|' || scan_verdict || '|' || revision::text || '|' || (storage_etag is null)::text from documents.assets where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${document_infected_id}';")"
if [[ "${infected_database_state}" != "Rejected|Infected|2|true" ]]; then
  echo "Unexpected rejected document database state: ${infected_database_state}." >&2
  exit 1
fi
denied_document_count="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select count(*) from documents.assets where id = '${document_denied_id}';")"
if [[ "${denied_document_count}" != "0" ]]; then
  echo "A denied document upload created metadata." >&2
  exit 1
fi
document_audit_count="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select count(distinct event_type) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'DocumentAsset' and resource_id = '${document_id}' and event_type in ('DocumentUploadSessionCreated','DocumentQuarantined','DocumentReleased','DocumentDownloaded');")"
if [[ "${document_audit_count}" != "4" ]]; then
  echo "Shared document audit coverage is incomplete." >&2
  exit 1
fi
document_release_event_count="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'documents.asset.released.v1' and payload->>'assetId' = '${document_id}' and not (payload ? 'sha256') and not (payload ? 'originalFileName');")"
if [[ "${document_release_event_count}" != "1" ]]; then
  echo "The released document event is missing or exposes unsafe payload fields." >&2
  exit 1
fi
document_receipt_count="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('integration-document-session','integration-document-content','integration-document-release','integration-document-duplicate','integration-document-version-2','integration-document-infected-session','integration-document-infected-content');")"
if [[ "${document_receipt_count}" != "7" ]]; then
  echo "Shared document idempotency receipts are incomplete: ${document_receipt_count}." >&2
  exit 1
fi
document_denied_receipt_count="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('integration-document-unregistered-denied','integration-document-uploader-release-denied','integration-document-infected-release');")"
if [[ "${document_denied_receipt_count}" != "0" ]]; then
  echo "Rejected document operations left success idempotency receipts: ${document_denied_receipt_count}." >&2
  exit 1
fi

current_step="pulling sync changes and accepting a checkpoint"
pull_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  "http://127.0.0.1:${port}/api/v1/sync/pull?projectId=${project_id}&limit=100")"
checkpoint_offer="$(sed -n 's/.*"checkpointOffer":"\([^"]*\)".*/\1/p' <<<"${pull_response}")"
grep -q '"changeType":"ServerAccepted"' <<<"${pull_response}"
if [[ -z "${checkpoint_offer}" ]]; then
  echo "Sync pull did not return a checkpoint offer." >&2
  exit 1
fi
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"projectId\":\"${project_id}\",\"checkpointOffer\":\"${checkpoint_offer}\"}" \
  "http://127.0.0.1:${port}/api/v1/sync/checkpoints" | grep -q '"sequence":'

current_step="opening a second-user sync session for a concurrent edit"
field_user_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
field_device_id="integration-device-002"
field_handshake_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${field_user_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${field_device_id}\",\"projectId\":\"${project_id}\",\"deviceName\":\"Concurrent field browser\",\"platform\":\"CI\",\"appVersion\":\"0.2.0\",\"protocolVersion\":3,\"localSchemaVersion\":6,\"lastCheckpoint\":null,\"deviceTime\":\"${device_time}\",\"queue\":{\"pendingOperations\":1,\"pendingAttachments\":0,\"pendingAttachmentBytes\":0,\"oldestOperationAt\":\"${device_time}\"}}" \
  "http://127.0.0.1:${port}/api/v1/sync/handshake")"
field_session_id="$(sed -n 's/.*"sessionId":"\([^"]*\)".*/\1/p' <<<"${field_handshake_response}")"
field_lease_id="$(sed -n 's/.*"leaseId":"\([^"]*\)".*/\1/p' <<<"${field_handshake_response}")"
field_authorization_version="$(sed -n 's/.*"authorizationVersion":\([0-9][0-9]*\).*/\1/p' <<<"${field_handshake_response}")"
if [[ -z "${field_session_id}" || -z "${field_lease_id}" || -z "${field_authorization_version}" ]]; then
  echo "Second-user sync handshake did not return a session and offline lease." >&2
  exit 1
fi

current_step="creating and resolving a concurrent two-user sync conflict"
conflicting_report_id="66666666-6666-6666-6666-666666666666"
conflicting_fact_id="77777777-7777-7777-7777-777777777777"
conflicting_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1C"
conflict_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${field_user_id}" \
  --header "X-Pmcs-Sync-Session: ${field_session_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${field_device_id}\",\"operations\":[{\"operationId\":\"${conflicting_operation_id}\",\"projectId\":\"${project_id}\",\"entityType\":\"DailyReport\",\"entityId\":\"${conflicting_report_id}\",\"commandType\":\"CaptureDailyReportFact\",\"baseRevision\":null,\"payloadSchemaVersion\":1,\"createdAtDevice\":\"${device_time}\",\"payload\":{\"factId\":\"${conflicting_fact_id}\",\"reportDate\":\"2099-01-01\",\"locationName\":null,\"kind\":\"Note\",\"description\":\"Concurrent field user fact\",\"locationId\":\"${location_id}\"},\"offlineLeaseId\":\"${field_lease_id}\",\"authorizationVersion\":${field_authorization_version},\"localSequence\":1,\"dependencies\":[],\"correlationId\":\"integration-sync-2\",\"deviceTimezoneOffsetMinutes\":0}]}" \
  "http://127.0.0.1:${port}/api/v1/sync/operations")"
conflict_id="$(sed -n 's/.*"conflictId":"\([^"]*\)".*/\1/p' <<<"${conflict_response}")"
grep -q '"status":"Conflict"' <<<"${conflict_response}"
if [[ -z "${conflict_id}" ]]; then
  echo "Conflicting operation did not create a conflict case." >&2
  exit 1
fi
conflicts_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/sync/conflicts?projectId=${project_id}")"
grep -q "\"conflictId\":\"${conflict_id}\"" <<<"${conflicts_response}"
conflict_revision="$(sed -n 's/.*\"revision\":\([0-9][0-9]*\).*/\1/p' <<<"${conflicts_response}")"
if [[ -z "${conflict_revision}" ]]; then
  echo "Conflict listing did not return its current revision." >&2
  exit 1
fi
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${conflict_revision},\"resolution\":\"KeepServer\",\"replacementOperationId\":null,\"comment\":\"Integration resolution\"}" \
  "http://127.0.0.1:${port}/api/v1/sync/conflicts/${conflict_id}/resolve" | grep -q '"status":"Resolved"'

current_step="verifying readable sync recovery diagnostics"
sync_diagnostics="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/sync/diagnostics?projectId=${project_id}&deviceId=${device_id}")"
grep -q '"recoveryState":"Healthy"' <<<"${sync_diagnostics}"
grep -q '"checkpointLag":0' <<<"${sync_diagnostics}"
grep -Eq '"recentReplayCount":[1-9][0-9]*' <<<"${sync_diagnostics}"

current_step="checking Checkpoint 24 finance controls and independent verification"
finance_operator_capture="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/identity/permissions/preview?userId=${field_user_id}&projectId=${project_id}&operation=finance.obligations.capture&proposedRoleCode=FinanceOperator")"
grep -q '"allowed":true' <<<"${finance_operator_capture}"
finance_operator_review="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/identity/permissions/preview?userId=${field_user_id}&projectId=${project_id}&operation=finance.obligations.review&proposedRoleCode=FinanceOperator")"
grep -q '"allowed":false' <<<"${finance_operator_review}"
finance_manager_verification="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/identity/permissions/preview?userId=${field_user_id}&projectId=${project_id}&operation=finance.verification.read&proposedRoleCode=FinanceManager")"
grep -q '"allowed":true' <<<"${finance_manager_verification}"
create_posted_financial_record() {
  local record_id="$1"
  local record_type="$2"
  local record_amount="$3"
  local record_number="$4"
  local create_response
  local submit_response
  local record_revision

  create_response="$(curl --silent --fail \
    --request POST \
    --header "X-Tenant-Id: ${tenant_id}" \
    --header "X-User-Id: ${user_id}" \
    --header "Idempotency-Key: cp24-record-${record_number}" \
    --header 'Content-Type: application/json' \
    --data "{\"clientGeneratedId\":\"${record_id}\",\"type\":\"${record_type}\",\"transactionDate\":\"2026-09-01\",\"amount\":${record_amount},\"currencyCode\":\"IRR\",\"description\":\"Checkpoint 24 verified ${record_type}\",\"counterparty\":\"Integration party\",\"documentNumber\":\"${record_number}\",\"contractReference\":null,\"contractId\":null,\"commitmentId\":null,\"costCenterCode\":\"CI\",\"partyId\":null,\"locationId\":\"${location_id}\",\"wbsReference\":\"CI-WBS\"}" \
    "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/records")"
  record_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${create_response}")"
  submit_response="$(curl --silent --fail \
    --request POST \
    --header "X-Tenant-Id: ${tenant_id}" \
    --header "X-User-Id: ${user_id}" \
    --header "Idempotency-Key: cp24-record-submit-${record_number}" \
    --header 'Content-Type: application/json' \
    --data "{\"baseRevision\":${record_revision}}" \
    "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/records/${record_id}/submit")"
  record_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${submit_response}")"
  curl --silent --fail \
    --request POST \
    --header "X-Tenant-Id: ${tenant_id}" \
    --header "X-User-Id: ${user_id}" \
    --header "Idempotency-Key: cp24-record-post-${record_number}" \
    --header 'Content-Type: application/json' \
    --data "{\"baseRevision\":${record_revision},\"comment\":\"CI verified\"}" \
    "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/records/${record_id}/post" >/dev/null
}

payment_record_id="88888888-8888-4888-8888-888888888881"
funding_record_id="88888888-8888-4888-8888-888888888882"
expense_record_id="88888888-8888-4888-8888-888888888883"
return_record_id="88888888-8888-4888-8888-888888888884"
create_posted_financial_record "${payment_record_id}" "Payment" "1000" "PAY-CI-01"
create_posted_financial_record "${funding_record_id}" "PettyCashFunding" "500" "PCF-CI-01"
create_posted_financial_record "${expense_record_id}" "PettyCashExpense" "400" "PCE-CI-01"
create_posted_financial_record "${return_record_id}" "Receipt" "100" "PCR-CI-01"

obligation_id="99999999-9999-4999-8999-999999999991"
obligation_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: cp24-obligation-create' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${obligation_id}\",\"type\":\"Payable\",\"number\":\"OB-CI-01\",\"description\":\"Checkpoint 24 payable\",\"issueDate\":\"2026-09-01\",\"dueDate\":\"2026-09-10\",\"amount\":1000,\"currencyCode\":\"IRR\",\"partyId\":null,\"counterparty\":\"Integration party\",\"contractId\":null,\"commitmentId\":null,\"costCenterCode\":\"CI\",\"wbsReference\":\"CI-WBS\",\"locationId\":\"${location_id}\"}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/obligations")"
obligation_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${obligation_response}")"
obligation_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-obligation-submit' --header 'Content-Type: application/json' --data "{\"baseRevision\":${obligation_revision}}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/obligations/${obligation_id}/submit")"
obligation_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${obligation_response}")"
obligation_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-obligation-approve' --header 'Content-Type: application/json' --data "{\"baseRevision\":${obligation_revision},\"comment\":\"CI approved\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/obligations/${obligation_id}/approve")"
obligation_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${obligation_response}")"
curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-obligation-settle' --header 'Content-Type: application/json' --data "{\"baseRevision\":${obligation_revision},\"financialRecordId\":\"${payment_record_id}\",\"amount\":1000}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/obligations/${obligation_id}/settlements" | grep -q '"status":"Settled"'

petty_cash_id="99999999-9999-4999-8999-999999999992"
petty_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-petty-create' --header 'Content-Type: application/json' --data "{\"clientGeneratedId\":\"${petty_cash_id}\",\"number\":\"PC-CI-01\",\"purpose\":\"Checkpoint 24 petty cash\",\"custodian\":\"CI cashier\",\"requestDate\":\"2026-09-01\",\"reconciliationDueDate\":\"2026-09-10\",\"requestedAmount\":500,\"currencyCode\":\"IRR\",\"locationId\":\"${location_id}\",\"costCenterCode\":\"CI\",\"wbsReference\":\"CI-WBS\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/petty-cash-requests")"
petty_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${petty_response}")"
petty_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-petty-submit' --header 'Content-Type: application/json' --data "{\"baseRevision\":${petty_revision}}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/petty-cash-requests/${petty_cash_id}/submit")"
petty_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${petty_response}")"
petty_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-petty-approve' --header 'Content-Type: application/json' --data "{\"baseRevision\":${petty_revision},\"approvedAmount\":500,\"comment\":\"CI approved\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/petty-cash-requests/${petty_cash_id}/approve")"
petty_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${petty_response}")"
petty_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-petty-advance' --header 'Content-Type: application/json' --data "{\"baseRevision\":${petty_revision},\"advanceRecordId\":\"${funding_record_id}\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/petty-cash-requests/${petty_cash_id}/advance")"
petty_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${petty_response}")"
petty_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-petty-reconcile' --header 'Content-Type: application/json' --data "{\"baseRevision\":${petty_revision},\"expenseAmount\":400,\"returnedAmount\":100,\"expenseRecordId\":\"${expense_record_id}\",\"returnRecordId\":\"${return_record_id}\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/petty-cash-requests/${petty_cash_id}/reconciliation")"
petty_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${petty_response}")"
curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-petty-reconciliation-approve' --header 'Content-Type: application/json' --data "{\"baseRevision\":${petty_revision},\"comment\":\"CI reconciled\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/petty-cash-requests/${petty_cash_id}/reconciliation/approve" | grep -q '"status":"Reconciled"'

fee_policy_id="99999999-9999-4999-8999-999999999993"
fee_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-fee-create' --header 'Content-Type: application/json' --data "{\"clientGeneratedId\":\"${fee_policy_id}\",\"title\":\"CI management fee\",\"ratePercent\":5,\"calculationBase\":\"RecognizedSpend\",\"effectiveFrom\":\"2026-09-01\",\"notes\":\"Checkpoint 24\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/management-fee-policies")"
fee_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${fee_response}")"
fee_response="$(curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-fee-submit' --header 'Content-Type: application/json' --data "{\"baseRevision\":${fee_revision}}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/management-fee-policies/${fee_policy_id}/submit")"
fee_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${fee_response}")"
curl --silent --fail --request POST --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" --header 'Idempotency-Key: cp24-fee-approve' --header 'Content-Type: application/json' --data "{\"baseRevision\":${fee_revision},\"comment\":\"CI approved\"}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/management-fee-policies/${fee_policy_id}/approve" | grep -q '"status":"Approved"'

finance_control_state="$(curl --silent --fail --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/control-state")"
grep -q '"openPayableAmount":0' <<<"${finance_control_state}"
grep -q '"outstandingAdvanceAmount":0' <<<"${finance_control_state}"
grep -q '"managementFeeAmount":70' <<<"${finance_control_state}"
finance_verification="$(curl --silent --fail --header "X-Tenant-Id: ${tenant_id}" --header "X-User-Id: ${user_id}" "http://127.0.0.1:${port}/api/v1/projects/${project_id}/finance/verification")"
grep -q '"snapshotMatchesIndependentCalculation":true' <<<"${finance_verification}"
grep -q '"invalidLocationLinkCount":0' <<<"${finance_verification}"
grep -q '"invalidCommercialLinkCount":0' <<<"${finance_verification}"
grep -q '"passed":true' <<<"${finance_verification}"

current_step="checking the insight rate limit"
insight_status=""
for _ in {1..6}; do
  insight_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
    --request POST \
    --header "X-Tenant-Id: ${tenant_id}" \
    --header "X-User-Id: ${user_id}" \
    --header 'Content-Type: application/json' \
    --data '{}' \
    "http://127.0.0.1:${port}/api/v1/projects/${project_id}/insight-generation-requests")"
done
if [[ "${insight_status}" != "429" ]]; then
  echo "Expected the sixth insight request to return 429; received ${insight_status}." >&2
  exit 1
fi

current_step="checking the anonymous API rate limit"
rate_limited=false
for _ in {1..220}; do
  status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
    "http://127.0.0.1:${port}/api/v1/foundation")"
  if [[ "${status}" == "429" ]]; then
    rate_limited=true
    break
  fi
done
if [[ "${rate_limited}" != true ]]; then
  echo "Expected the global anonymous rate limit to return 429." >&2
  exit 1
fi

current_step="completed"
printf 'PMCS PostgreSQL/API and object-storage roundtrip integration smoke test passed.\n'
