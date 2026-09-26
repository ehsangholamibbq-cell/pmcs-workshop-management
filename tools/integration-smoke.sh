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

current_step="checking configurable login isolation, versioning and rollback"
login_descriptor_headers="${temporary_directory}/login-descriptor-headers.txt"
login_fallback="$(curl --silent --fail \
  --dump-header "${login_descriptor_headers}" \
  "http://127.0.0.1:${port}/api/v1/public/login-experience?tenantId=${tenant_id}")"
grep -q '"fallbackUsed":true' <<<"${login_fallback}"
grep -qi '^Cache-Control: no-store' "${login_descriptor_headers}"
if grep -Eq 'clientSecret|issuer|authorizationUrl|redirectUri' <<<"${login_fallback}"; then
  echo "The public login presentation leaked an authentication configuration field." >&2
  exit 1
fi

login_member_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
login_denied_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-login-denied' \
  --header 'Content-Type: application/json' \
  --data '{"clientGeneratedId":"82000000-0000-4000-8000-000000000099","compositionVariant":"BlueprintSplit","surfaceTone":"WarmStone","accentPalette":"CorporateNavyGreen","motionPolicy":"Balanced","eyebrow":"PMCS","headline":"Denied","supportingText":"Denied","logoDocumentId":null,"heroDocumentId":null}' \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences")"
if [[ "${login_denied_status}" != "403" ]]; then
  echo "Expected a member login-presentation mutation to return 403; received ${login_denied_status}." >&2
  exit 1
fi

login_v1_id="82000000-0000-4000-8000-000000000001"
login_logo_id="82000000-0000-4000-8000-000000000011"
login_logo_file="${temporary_directory}/login-logo.png"
login_logo_download="${temporary_directory}/login-logo-download.png"
login_logo_headers="${temporary_directory}/login-logo-headers.txt"
printf '\211PNG\r\n\032\n' > "${login_logo_file}"
login_logo_size="$(wc -c < "${login_logo_file}" | tr -d '[:space:]')"
login_logo_sha="$(sha256sum "${login_logo_file}" | cut -d ' ' -f 1)"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-logo-session' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${login_logo_id}\",\"projectId\":null,\"ownerType\":\"LoginExperience\",\"ownerId\":\"${login_v1_id}\",\"originalFileName\":\"login-logo.png\",\"contentType\":\"image/png\",\"sizeBytes\":${login_logo_size},\"sha256\":\"${login_logo_sha}\",\"classification\":\"Internal\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions" | grep -q '"status":"PendingUpload"'
login_logo_upload="$(curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-logo-content' \
  --header 'Content-Type: image/png' \
  --data-binary "@${login_logo_file}" \
  "http://127.0.0.1:${port}/api/v1/documents/${login_logo_id}/content")"
grep -q '"status":"Quarantined"' <<<"${login_logo_upload}"
login_logo_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${login_logo_upload}")"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-logo-release' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${login_logo_revision}}" \
  "http://127.0.0.1:${port}/api/v1/documents/${login_logo_id}/release" | \
  grep -q '"status":"Released"'

login_v1_payload="{\"clientGeneratedId\":\"${login_v1_id}\",\"compositionVariant\":\"BlueprintSplit\",\"surfaceTone\":\"WarmStone\",\"accentPalette\":\"CorporateNavyGreen\",\"motionPolicy\":\"Balanced\",\"eyebrow\":\"PMCS integration\",\"headline\":\"Traceable project decisions\",\"supportingText\":\"Allowlisted presentation without authentication configuration\",\"logoDocumentId\":\"${login_logo_id}\",\"heroDocumentId\":null,\"clientSecret\":\"must-be-ignored\"}"
login_v1="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-v1-create' \
  --header 'Content-Type: application/json' \
  --data "${login_v1_payload}" \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences")"
grep -q '"versionNumber":1' <<<"${login_v1}"
grep -q '"status":"Draft"' <<<"${login_v1}"
if grep -q 'clientSecret' <<<"${login_v1}"; then
  echo "The login presentation response retained an unmapped authentication field." >&2
  exit 1
fi

curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-v1-publish' \
  --header 'Content-Type: application/json' \
  --data '{"baseRevision":1}' \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences/1/publish" | \
  grep -q '"status":"Published"'
login_published="$(curl --silent --fail \
  "http://127.0.0.1:${port}/api/v1/public/login-experience?tenantId=${tenant_id}")"
grep -q '"version":1' <<<"${login_published}"
grep -q '"fallbackUsed":false' <<<"${login_published}"
grep -q '"logoUrl":"/api/v1/public/login-experience/assets/logo?' <<<"${login_published}"
curl --silent --fail \
  --dump-header "${login_logo_headers}" \
  --output "${login_logo_download}" \
  "http://127.0.0.1:${port}/api/v1/public/login-experience/assets/logo?tenantId=${tenant_id}&version=1"
grep -Eqi '^Cache-Control:[[:space:]]*public,[[:space:]]*max-age=31536000,[[:space:]]*immutable' "${login_logo_headers}"
if [[ "$(sha256sum "${login_logo_download}" | cut -d ' ' -f 1)" != "${login_logo_sha}" ]]; then
  echo "Published login asset roundtrip changed the verified object content." >&2
  exit 1
fi

login_v2_id="82000000-0000-4000-8000-000000000002"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-v2-create' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${login_v2_id}\",\"compositionVariant\":\"WarmMinimal\",\"surfaceTone\":\"WarmIvory\",\"accentPalette\":\"GreenStone\",\"motionPolicy\":\"Calm\",\"eyebrow\":\"PMCS integration\",\"headline\":\"Warm controlled experience\",\"supportingText\":\"Second version for rollback verification\",\"logoDocumentId\":null,\"heroDocumentId\":null}" \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences" | grep -q '"versionNumber":2'
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-v2-publish' \
  --header 'Content-Type: application/json' \
  --data '{"baseRevision":1}' \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences/2/publish" | \
  grep -q '"status":"Published"'

login_v1_revision="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select revision from identity_access.login_experiences where id = '${login_v1_id}' and status = 'Superseded';")"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-v1-rollback' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${login_v1_revision}}" \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences/1/rollback" | \
  grep -q '"status":"Published"'
curl --silent --fail \
  "http://127.0.0.1:${port}/api/v1/public/login-experience?tenantId=${tenant_id}" | \
  grep -q '"version":1'

login_governance_evidence="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select
      (select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and event_type = 'identity.login-experience.published.v1' and payload->>'experienceId' in ('${login_v1_id}', '${login_v2_id}'))::text || '|' ||
      (select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and resource_type = 'LoginExperience' and resource_id in ('${login_v1_id}', '${login_v2_id}'))::text || '|' ||
      (select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('integration-login-v1-create', 'integration-login-v1-publish', 'integration-login-v2-create', 'integration-login-v2-publish', 'integration-login-v1-rollback'))::text || '|' ||
      (select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and payload->>'experienceId' in ('${login_v1_id}', '${login_v2_id}') and (payload ? 'clientSecret' or payload ? 'issuer' or payload ? 'authorizationUrl' or payload ? 'redirectUri'))::text;")"
if [[ "${login_governance_evidence}" != "3|5|5|0" ]]; then
  echo "Login experience audit/outbox/idempotency evidence is incomplete or unsafe: ${login_governance_evidence}." >&2
  exit 1
fi

current_step="verifying concurrent login draft version allocation"
login_concurrent_a="${temporary_directory}/login-concurrent-a.json"
login_concurrent_b="${temporary_directory}/login-concurrent-b.json"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-concurrent-a' \
  --header 'Content-Type: application/json' \
  --data '{"clientGeneratedId":"82000000-0000-4000-8000-000000000003","compositionVariant":"BlueprintSplit","surfaceTone":"WarmStone","accentPalette":"CorporateNavyGreen","motionPolicy":"Balanced","eyebrow":"PMCS concurrency","headline":"Concurrent presentation A","supportingText":"Serialized tenant version allocation A","logoDocumentId":null,"heroDocumentId":null}' \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences" > "${login_concurrent_a}" &
login_concurrent_a_pid=$!
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-login-concurrent-b' \
  --header 'Content-Type: application/json' \
  --data '{"clientGeneratedId":"82000000-0000-4000-8000-000000000004","compositionVariant":"WarmMinimal","surfaceTone":"WarmIvory","accentPalette":"GreenStone","motionPolicy":"Calm","eyebrow":"PMCS concurrency","headline":"Concurrent presentation B","supportingText":"Serialized tenant version allocation B","logoDocumentId":null,"heroDocumentId":null}' \
  "http://127.0.0.1:${port}/api/v1/identity/login-experiences" > "${login_concurrent_b}" &
login_concurrent_b_pid=$!
wait "${login_concurrent_a_pid}"
wait "${login_concurrent_b_pid}"
login_concurrent_versions="$(sed -n 's/.*"versionNumber":\([0-9][0-9]*\).*/\1/p' "${login_concurrent_a}" "${login_concurrent_b}" | sort -n | paste -sd '|' -)"
if [[ "${login_concurrent_versions}" != "3|4" ]]; then
  echo "Concurrent login drafts did not receive unique sequential versions: ${login_concurrent_versions}." >&2
  exit 1
fi

current_step="checking minimal member profile self-service and privacy scope"
profile_response="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  "http://127.0.0.1:${port}/api/v1/member-profile")"
profile_user_revision="$(sed -n 's/.*"userRevision":\([0-9][0-9]*\).*/\1/p' <<<"${profile_response}")"
profile_revision="$(sed -n 's/.*"profileRevision":\([0-9][0-9]*\).*/\1/p' <<<"${profile_response}")"
if [[ -z "${profile_user_revision}" || -z "${profile_revision}" ]]; then
  echo "Member profile did not return both concurrency revisions." >&2
  exit 1
fi
profile_updated="$(curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-profile-self-update' \
  --header 'Content-Type: application/json' \
  --data "{\"baseUserRevision\":${profile_user_revision},\"baseProfileRevision\":${profile_revision},\"displayName\":\"سرپرست کارگاه آزمون\",\"jobTitle\":\"سرپرست کارگاه\",\"workPhone\":\"+98 21 1000\",\"avatarDocumentId\":null,\"avatarCrop\":null}" \
  "http://127.0.0.1:${port}/api/v1/member-profile")"
grep -q '"jobTitle":"سرپرست کارگاه"' <<<"${profile_updated}"
grep -q '"organizationUnit":null' <<<"${profile_updated}"
curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  "http://127.0.0.1:${port}/api/v1/member-profiles/${user_id}" | \
  grep -q "\"userId\":\"${user_id}\""

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

current_step="checking controlled project bootstrap preview, replay, execute and independent activation"
bootstrap_key="integration-project-bootstrap-create"
bootstrap_payload="{\"sourceProjectId\":\"${setup_project_id}\",\"target\":{\"code\":\"CI-BOOT-01\",\"name\":\"Integration bootstrap target\",\"projectType\":\"Building\",\"executionPhase\":\"PreConstruction\",\"countryCode\":\"IR\",\"region\":\"Qazvin\",\"startDate\":\"2026-10-01\",\"plannedFinishDate\":\"2027-10-01\",\"shortDescription\":\"Controlled bootstrap destination\",\"timeZone\":\"Asia/Tehran\",\"baseCurrencyCode\":\"IRR\",\"unitSystem\":\"Metric\",\"offlinePolicyAccepted\":true},\"categories\":[\"BaseSettings\",\"Calendar\",\"Locations\",\"RoleTemplates\",\"WorkflowTemplates\",\"FormTemplates\",\"ReportTemplates\",\"Lookups\",\"Members\",\"NotificationDefaults\",\"GroupDefaults\"],\"members\":[{\"userId\":\"${user_id}\",\"roleCode\":\"ProjectManager\",\"accessScope\":\"Project\"}],\"conflictPolicy\":\"FailOnConflict\"}"
bootstrap_preview="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: ${bootstrap_key}" \
  --header 'Content-Type: application/json' \
  --data "${bootstrap_payload}" \
  "http://127.0.0.1:${port}/api/v1/project-bootstraps")"
bootstrap_plan_id="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).planId)' "${bootstrap_preview}")"
bootstrap_target_id="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).targetProjectId)' "${bootstrap_preview}")"
bootstrap_plan_revision="$(node -e 'process.stdout.write(String(JSON.parse(process.argv[1]).planRevision))' "${bootstrap_preview}")"
bootstrap_digest="$(node -e 'process.stdout.write(JSON.parse(process.argv[1]).previewDigest)' "${bootstrap_preview}")"
grep -q '"status":"PreviewReady"' <<<"${bootstrap_preview}"
grep -q '"status":"Draft"' <<<"${bootstrap_preview}"
grep -q '"blocked":0' <<<"${bootstrap_preview}"
if [[ -z "${bootstrap_plan_id}" || -z "${bootstrap_target_id}" || -z "${bootstrap_digest}" ]]; then
  echo "Project bootstrap preview did not return stable plan, target and digest identities." >&2
  exit 1
fi

bootstrap_replay="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header "Idempotency-Key: ${bootstrap_key}" \
  --header 'Content-Type: application/json' \
  --data "${bootstrap_payload}" \
  "http://127.0.0.1:${port}/api/v1/project-bootstraps")"
grep -Eq "\"planId\"[[:space:]]*:[[:space:]]*\"${bootstrap_plan_id}\"" <<<"${bootstrap_replay}"

bootstrap_execute_payload="{\"baseRevision\":${bootstrap_plan_revision},\"previewDigest\":\"${bootstrap_digest}\"}"
bootstrap_result="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-project-bootstrap-execute' \
  --header 'Content-Type: application/json' \
  --data "${bootstrap_execute_payload}" \
  "http://127.0.0.1:${port}/api/v1/project-bootstraps/${bootstrap_plan_id}/execute")"
grep -q '"status":"Completed"' <<<"${bootstrap_result}"
grep -q '"status":"Draft"' <<<"${bootstrap_result}"
grep -q '"code":"operational-data-excluded","passed":true' <<<"${bootstrap_result}"
bootstrap_completed_revision="$(node -e 'process.stdout.write(String(JSON.parse(process.argv[1]).planRevision))' "${bootstrap_result}")"
bootstrap_target_revision="$(node -e 'process.stdout.write(String(JSON.parse(process.argv[1]).targetProject.revision))' "${bootstrap_result}")"

bootstrap_execute_replay="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-project-bootstrap-execute' \
  --header 'Content-Type: application/json' \
  --data "${bootstrap_execute_payload}" \
  "http://127.0.0.1:${port}/api/v1/project-bootstraps/${bootstrap_plan_id}/execute")"
grep -Eq "\"planId\"[[:space:]]*:[[:space:]]*\"${bootstrap_plan_id}\"" <<<"${bootstrap_execute_replay}"

curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  "http://127.0.0.1:${port}/api/v1/project-bootstraps/${bootstrap_plan_id}/result" | \
  grep -q '"status":"Completed"'

bootstrap_activation="$(curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --header 'Idempotency-Key: integration-project-bootstrap-activate' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${bootstrap_completed_revision},\"targetBaseRevision\":${bootstrap_target_revision}}" \
  "http://127.0.0.1:${port}/api/v1/project-bootstraps/${bootstrap_plan_id}/activate")"
grep -q '"status":"Activated"' <<<"${bootstrap_activation}"
grep -q '"status":"Active"' <<<"${bootstrap_activation}"
bootstrap_database_state="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --tuples-only --no-align --set ON_ERROR_STOP=1 --command \
  "select plan.status || '|' ||
      (select count(*) from field_operations.daily_reports report where report.tenant_id = plan.tenant_id and report.project_id = plan.target_project_id)::text || '|' ||
      (select count(*) from identity_access.project_memberships membership where membership.tenant_id = plan.tenant_id and membership.project_id = plan.target_project_id and membership.user_id = '${user_id}' and membership.status = 'Active')::text || '|' ||
      (select count(*) from foundation.audit_events audit where audit.tenant_id = plan.tenant_id and audit.project_id = plan.target_project_id and audit.event_type = 'ProjectBootstrapCompleted')::text || '|' ||
      (select count(*) from foundation.outbox_messages message where message.tenant_id = plan.tenant_id and message.project_id = plan.target_project_id and message.event_type = 'projects.bootstrap.completed.v1')::text
    from projects.project_bootstrap_plans plan
    where plan.tenant_id = '${tenant_id}' and plan.id = '${bootstrap_plan_id}';")"
if [[ "${bootstrap_database_state}" != "Activated|0|1|1|1" ]]; then
  echo "Project bootstrap persistence boundary is invalid: ${bootstrap_database_state}." >&2
  exit 1
fi

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

current_step="verifying profile image policy, self release and private rendition"
profile_image_id="83000000-0000-4000-8000-000000000001"
profile_image_file="${temporary_directory}/profile.png"
profile_image_download="${temporary_directory}/profile-download.png"
printf '\211PNG\r\n\032\n' > "${profile_image_file}"
profile_image_size="$(wc -c < "${profile_image_file}" | tr -d '[:space:]')"
profile_image_sha="$(sha256sum "${profile_image_file}" | cut -d ' ' -f 1)"
profile_pdf_rejection_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-profile-pdf-denied' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"83000000-0000-4000-8000-000000000099\",\"projectId\":null,\"ownerType\":\"MemberProfile\",\"ownerId\":\"${login_member_id}\",\"originalFileName\":\"profile.pdf\",\"contentType\":\"application/pdf\",\"sizeBytes\":${document_size},\"sha256\":\"${document_sha}\",\"classification\":\"Confidential\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions")"
if [[ "${profile_pdf_rejection_status}" != "422" ]]; then
  echo "Expected a non-image profile asset to return 422; received ${profile_pdf_rejection_status}." >&2
  exit 1
fi

curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-profile-image-session' \
  --header 'Content-Type: application/json' \
  --data "{\"clientGeneratedId\":\"${profile_image_id}\",\"projectId\":null,\"ownerType\":\"MemberProfile\",\"ownerId\":\"${login_member_id}\",\"originalFileName\":\"profile.png\",\"contentType\":\"image/png\",\"sizeBytes\":${profile_image_size},\"sha256\":\"${profile_image_sha}\",\"classification\":\"Confidential\",\"retentionPolicy\":\"Standard\",\"retainUntil\":null,\"legalHold\":false}" \
  "http://127.0.0.1:${port}/api/v1/upload-sessions" | grep -q '"status":"PendingUpload"'
profile_image_upload="$(curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-profile-image-content' \
  --header 'Content-Type: image/png' \
  --data-binary "@${profile_image_file}" \
  "http://127.0.0.1:${port}/api/v1/documents/${profile_image_id}/content")"
grep -q '"status":"Quarantined"' <<<"${profile_image_upload}"
profile_image_revision="$(sed -n 's/.*"revision":\([0-9][0-9]*\).*/\1/p' <<<"${profile_image_upload}")"
curl --silent --fail \
  --request POST \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-profile-image-release' \
  --header 'Content-Type: application/json' \
  --data "{\"baseRevision\":${profile_image_revision}}" \
  "http://127.0.0.1:${port}/api/v1/documents/${profile_image_id}/release" | \
  grep -q '"status":"Released"'

profile_current="$(curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  "http://127.0.0.1:${port}/api/v1/member-profile")"
profile_user_revision="$(sed -n 's/.*"userRevision":\([0-9][0-9]*\).*/\1/p' <<<"${profile_current}")"
profile_revision="$(sed -n 's/.*"profileRevision":\([0-9][0-9]*\).*/\1/p' <<<"${profile_current}")"
profile_with_avatar="$(curl --silent --fail \
  --request PUT \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${login_member_id}" \
  --header 'Idempotency-Key: integration-profile-avatar-associate' \
  --header 'Content-Type: application/json' \
  --data "{\"baseUserRevision\":${profile_user_revision},\"baseProfileRevision\":${profile_revision},\"displayName\":\"سرپرست کارگاه آزمون\",\"jobTitle\":\"سرپرست کارگاه\",\"workPhone\":\"+98 21 1000\",\"avatarDocumentId\":\"${profile_image_id}\",\"avatarCrop\":{\"x\":0,\"y\":0,\"width\":1,\"height\":1}}" \
  "http://127.0.0.1:${port}/api/v1/member-profile")"
grep -q "\"avatarDocumentId\":\"${profile_image_id}\"" <<<"${profile_with_avatar}"
curl --silent --fail \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${user_id}" \
  --output "${profile_image_download}" \
  "http://127.0.0.1:${port}/api/v1/member-profiles/${login_member_id}/avatar?v=${profile_image_id}"
if [[ "$(sha256sum "${profile_image_download}" | cut -d ' ' -f 1)" != "${profile_image_sha}" ]]; then
  echo "Profile image rendition changed the verified object content." >&2
  exit 1
fi

profile_event_safety="$(psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command \
  "select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and event_type = 'identity.member-profile.updated.v1' and payload->>'userId' = '${login_member_id}' and not (payload ? 'email') and not (payload ? 'workPhone') and not (payload ? 'displayName');")"
if [[ "${profile_event_safety}" != "2" ]]; then
  echo "Profile update events are missing or expose private profile fields." >&2
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
