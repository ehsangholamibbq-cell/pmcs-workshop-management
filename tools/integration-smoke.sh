#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_TEST_CONNECTION_STRING:?Set PMCS_TEST_CONNECTION_STRING to an isolated PostgreSQL database.}"
: "${PMCS_TEST_S3_ENDPOINT:?Set PMCS_TEST_S3_ENDPOINT to an isolated S3-compatible service.}"
: "${PMCS_TEST_S3_ACCESS_KEY:?Set PMCS_TEST_S3_ACCESS_KEY.}"
: "${PMCS_TEST_S3_SECRET_KEY:?Set PMCS_TEST_S3_SECRET_KEY.}"
: "${PMCS_TEST_S3_BUCKET:?Set PMCS_TEST_S3_BUCKET.}"

command -v curl >/dev/null
command -v dotnet >/dev/null
command -v sha256sum >/dev/null

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
RateLimiting__GeneralPermitLimit=20 \
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

current_step="creating an offline sync session"
project_id="33333333-3333-3333-3333-333333333333"
device_id="integration-device-001"
device_time="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
handshake_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${device_id}\",\"projectId\":\"${project_id}\",\"deviceName\":\"Integration browser\",\"platform\":\"CI\",\"appVersion\":\"0.1.0\",\"protocolVersion\":2,\"localSchemaVersion\":5,\"lastCheckpoint\":null,\"deviceTime\":\"${device_time}\",\"queue\":{\"pendingOperations\":2,\"pendingAttachments\":0,\"pendingAttachmentBytes\":0,\"oldestOperationAt\":\"${device_time}\"}}" \
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
  --data "{\"deviceId\":\"${device_id}\",\"operations\":[{\"operationId\":\"${operation_id}\",\"projectId\":\"${project_id}\",\"entityType\":\"DailyReport\",\"entityId\":\"${report_id}\",\"commandType\":\"CaptureDailyReportFact\",\"baseRevision\":null,\"payloadSchemaVersion\":1,\"createdAtDevice\":\"${device_time}\",\"payload\":{\"factId\":\"${fact_id}\",\"reportDate\":\"2099-01-01\",\"locationName\":\"Integration site\",\"kind\":\"Note\",\"description\":\"Integration observed fact\"},\"offlineLeaseId\":\"${lease_id}\",\"authorizationVersion\":${authorization_version},\"localSequence\":1,\"dependencies\":[],\"correlationId\":\"integration-sync-1\",\"deviceTimezoneOffsetMinutes\":0}]}" \
  "http://127.0.0.1:${port}/api/v1/sync/operations")"
grep -q '"status":"Applied"' <<<"${push_response}"

current_step="creating an evidence upload session"
evidence_id="88888888-8888-8888-8888-888888888888"
evidence_file="${temporary_directory}/integration-evidence.jpg"
downloaded_file="${temporary_directory}/downloaded-evidence.jpg"
printf 'PMCS integration object storage evidence\n' > "${evidence_file}"
evidence_size="$(wc -c < "${evidence_file}" | tr -d '[:space:]')"
evidence_sha="$(sha256sum "${evidence_file}" | cut -d ' ' -f 1)"
upload_session_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: integration-evidence-session" \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${evidence_id}\",\"dailyReportId\":\"${report_id}\",\"dailyFactId\":\"${fact_id}\",\"originalFileName\":\"integration-evidence.jpg\",\"contentType\":\"image/jpeg\",\"sizeBytes\":${evidence_size},\"sha256\":\"${evidence_sha}\",\"capturedAtDevice\":\"${device_time}\"}" \
  "http://127.0.0.1:${port}/api/v1/projects/${project_id}/evidence/upload-sessions")"
grep -q '"status":"PendingUpload"' <<<"${upload_session_response}"

current_step="uploading evidence content"
curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: integration-evidence-content" \
  --header 'Content-Type: image/jpeg' \
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

current_step="creating and resolving a sync conflict"
conflicting_report_id="66666666-6666-6666-6666-666666666666"
conflicting_fact_id="77777777-7777-7777-7777-777777777777"
conflicting_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1C"
conflict_response="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  --header 'Content-Type: application/json' \
  --data "{\"deviceId\":\"${device_id}\",\"operations\":[{\"operationId\":\"${conflicting_operation_id}\",\"projectId\":\"${project_id}\",\"entityType\":\"DailyReport\",\"entityId\":\"${conflicting_report_id}\",\"commandType\":\"CaptureDailyReportFact\",\"baseRevision\":null,\"payloadSchemaVersion\":1,\"createdAtDevice\":\"${device_time}\",\"payload\":{\"factId\":\"${conflicting_fact_id}\",\"reportDate\":\"2099-01-01\",\"locationName\":\"Integration site\",\"kind\":\"Note\",\"description\":\"Conflicting observed fact\"},\"offlineLeaseId\":\"${lease_id}\",\"authorizationVersion\":${authorization_version},\"localSequence\":2,\"dependencies\":[],\"correlationId\":\"integration-sync-2\",\"deviceTimezoneOffsetMinutes\":0}]}" \
  "http://127.0.0.1:${port}/api/v1/sync/operations")"
conflict_id="$(sed -n 's/.*"conflictId":"\([^"]*\)".*/\1/p' <<<"${conflict_response}")"
grep -q '"status":"Conflict"' <<<"${conflict_response}"
if [[ -z "${conflict_id}" ]]; then
  echo "Conflicting operation did not create a conflict case." >&2
  exit 1
fi
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "X-Pmcs-Sync-Session: ${session_id}" \
  --header 'Content-Type: application/json' \
  --data '{"baseRevision":0,"resolution":"KeepServer","replacementOperationId":null,"comment":"Integration resolution"}' \
  "http://127.0.0.1:${port}/api/v1/sync/conflicts/${conflict_id}/resolve" | grep -q '"status":"Resolved"'

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
for _ in {1..25}; do
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
