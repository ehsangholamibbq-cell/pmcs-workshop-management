#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL to the isolated QA database.}"
: "${PMCS_QA_BASE_URL:?Set PMCS_QA_BASE_URL to the running isolated QA API.}"
: "${PMCS_QA_AUTH_KEY:?Set PMCS_QA_AUTH_KEY to the independent QA gateway key.}"

command -v curl >/dev/null
command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
foreign_tenant_id="11111111-1111-4111-8111-111111111199"
project_id="33333333-3333-3333-3333-333333333333"
technical_office_id="50000000-0000-4000-8000-000000000002"
technical_office_membership_id="60000000-0000-4000-8000-000000000002"
reporting_succeeded_run_id="71000000-0000-4000-8000-000000000001"

if [[ ! "${PMCS_QA_BASE_URL}" =~ ^https?://[^[:space:]]+$ ]]; then
  echo "PMCS_QA_BASE_URL must be an absolute HTTP or HTTPS URL." >&2
  exit 2
fi

scalar() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

execute() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --quiet --command "$1" >/dev/null
}

expect_status() {
  local label="$1"
  local actual="$2"
  local allowed="$3"
  if ! [[ "${actual}" =~ ^(${allowed})$ ]]; then
    echo "${label}: expected HTTP ${allowed//|/ or }, received ${actual}." >&2
    exit 1
  fi
}

output_id="$(scalar "select id from reporting.report_outputs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_succeeded_run_id}' and format = 'Xlsx';")"
document_id="$(scalar "select generated_document_id from reporting.report_outputs where id = '${output_id}';")"
document_sha="$(scalar "select sha256 from documents.assets where id = '${document_id}' and owner_type = 'ReportOutput' and owner_id = '${output_id}';")"
membership_status="$(scalar "select status from identity_access.project_memberships where id = '${technical_office_membership_id}' and tenant_id = '${tenant_id}' and project_id = '${project_id}' and user_id = '${technical_office_id}';")"
membership_revision="$(scalar "select revision from identity_access.project_memberships where id = '${technical_office_membership_id}';")"

if ! [[ "${output_id}" =~ ^[0-9a-f-]{36}$ && "${document_id}" =~ ^[0-9a-f-]{36}$ && "${document_sha}" =~ ^[0-9a-f]{64}$ ]]; then
  echo "Reporting security fixture identities are missing or invalid." >&2
  exit 1
fi
if [[ "${membership_status}" != "Active" || ! "${membership_revision}" =~ ^[0-9]+$ ]]; then
  echo "Technical Office membership is not an active deterministic QA fixture." >&2
  exit 1
fi

response_file="$(mktemp)"
membership_changed=false
document_changed=false
cleanup() {
  exit_code=$?
  set +e
  if [[ "${document_changed}" == true ]]; then
    execute "update documents.assets set sha256 = '${document_sha}' where id = '${document_id}';"
  fi
  if [[ "${membership_changed}" == true ]]; then
    execute "update identity_access.project_memberships set status = '${membership_status}', revision = ${membership_revision} where id = '${technical_office_membership_id}';"
  fi
  rm -f -- "${response_file}"
  exit "${exit_code}"
}
trap cleanup EXIT

verify_path="${PMCS_QA_BASE_URL}/api/v1/projects/${project_id}/reports/outputs/${output_id}/verify"
content_path="${PMCS_QA_BASE_URL}/api/v1/projects/${project_id}/reports/outputs/${output_id}/content"

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' "${verify_path}")"
expect_status "anonymous report verification is denied" "${status}" "401|403"

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${foreign_tenant_id}" \
  --header "X-User-Id: ${technical_office_id}" \
  "${verify_path}")"
expect_status "cross-tenant report verification is denied" "${status}" "401|403|404"

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${technical_office_id}" \
  "${PMCS_QA_BASE_URL}/api/v1/documents/${document_id}/content")"
expect_status "generic Documents download hides ReportOutput" "${status}" "404"

execute "update identity_access.project_memberships set status = 'Suspended', revision = revision + 1 where id = '${technical_office_membership_id}';"
membership_changed=true
status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${technical_office_id}" \
  "${content_path}")"
expect_status "suspended membership cannot download a completed report" "${status}" "401|403"
execute "update identity_access.project_memberships set status = '${membership_status}', revision = ${membership_revision} where id = '${technical_office_membership_id}';"
membership_changed=false

execute "update documents.assets set sha256 = repeat('0', 64) where id = '${document_id}';"
document_changed=true
status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${technical_office_id}" \
  "${verify_path}")"
expect_status "tampered report metadata fails closed" "${status}" "502"
if ! grep -q 'reporting.output.integrity_failed' "${response_file}"; then
  echo "Tampered report verification did not return the safe integrity diagnostic." >&2
  exit 1
fi
execute "update documents.assets set sha256 = '${document_sha}' where id = '${document_id}';"
document_changed=false

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${technical_office_id}" \
  "${verify_path}")"
expect_status "restored report metadata verifies again" "${status}" "200"
if ! grep -Eq '"status"[[:space:]]*:[[:space:]]*"Valid"' "${response_file}"; then
  echo "Restored report verification did not return Valid." >&2
  exit 1
fi

integrity_audits="$(scalar "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'CertifiedReportOutputIntegrityFailed' and resource_type = 'ReportOutput' and resource_id = '${output_id}';")"
if ! [[ "${integrity_audits}" =~ ^[0-9]+$ ]] || (( integrity_audits < 1 )); then
  echo "Tamper verification did not write an integrity-failure audit event." >&2
  exit 1
fi

printf '{"status":"passed","stage":"reporting-security-regression","assertions":6}\n'
