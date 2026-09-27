#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_DATABASE_URL:?Set the isolated QA database URL.}"
: "${PMCS_QA_BASE_URL:?Set the isolated QA API URL.}"
: "${PMCS_QA_AUTH_KEY:?Set the independent QA gateway key.}"
command -v psql >/dev/null
command -v curl >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
foreign_tenant_id="11111111-1111-4111-8111-111111111199"
administrator_id="22222222-2222-2222-2222-222222222222"
run_id="7f000000-0000-4000-8000-000000000001"

scalar() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}
execute() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --quiet --command "$1" >/dev/null
}
assert_status() {
  if ! [[ "$2" =~ ^($3)$ ]]; then
    echo "$1: expected HTTP ${3//|/ or }, got $2." >&2
    exit 1
  fi
}

output_id="$(scalar "select id from reporting.report_outputs where run_id = '${run_id}' and tenant_id = '${tenant_id}' and project_id is null and scope = 'Portfolio' and format = 'Xlsx';")"
document_id="$(scalar "select generated_document_id from reporting.report_outputs where id = '${output_id}';")"
document_sha="$(scalar "select sha256 from documents.assets where id = '${document_id}' and owner_type = 'TenantReportOutput' and owner_id = '${output_id}' and project_id is null;")"
if ! [[ "${output_id}" =~ ^[0-9a-f-]{36}$ && "${document_id}" =~ ^[0-9a-f-]{36}$ && "${document_sha}" =~ ^[0-9a-f]{64}$ ]]; then
  echo "Portfolio output and TenantReportOutput owner fixture are invalid." >&2
  exit 1
fi

response_file="$(mktemp)"
changed=false
cleanup() {
  exit_code=$?
  set +e
  if [[ "${changed}" == true ]]; then
    execute "update documents.assets set sha256 = '${document_sha}' where id = '${document_id}';"
  fi
  rm -f -- "${response_file}"
  exit "${exit_code}"
}
trap cleanup EXIT

verify_path="${PMCS_QA_BASE_URL}/api/v1/portfolio/reports/outputs/${output_id}/verify"
content_path="${PMCS_QA_BASE_URL}/api/v1/portfolio/reports/outputs/${output_id}/content"
status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' "${verify_path}")"
assert_status "anonymous tenant output" "${status}" "401|403"

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${foreign_tenant_id}" \
  --header "X-User-Id: ${administrator_id}" "${verify_path}")"
assert_status "foreign tenant output" "${status}" "401|403|404"

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${administrator_id}" \
  "${PMCS_QA_BASE_URL}/api/v1/documents/${document_id}/content")"
assert_status "generic document route hides tenant output" "${status}" "404"

execute "update documents.assets set sha256 = repeat('0', 64) where id = '${document_id}';"
changed=true
for path in "${verify_path}" "${content_path}"; do
  status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
    --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
    --header "X-Tenant-Id: ${tenant_id}" \
    --header "X-User-Id: ${administrator_id}" "${path}")"
  assert_status "tampered tenant report output" "${status}" "502"
  if ! rg -q 'reporting.output.integrity_failed' "${response_file}"; then
    echo "Tampered tenant output did not return the safe diagnostic." >&2
    exit 1
  fi
done
execute "update documents.assets set sha256 = '${document_sha}' where id = '${document_id}';"
changed=false

status="$(curl --silent --show-error --max-time 30 --output "${response_file}" --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header "X-Tenant-Id: ${tenant_id}" \
  --header "X-User-Id: ${administrator_id}" "${verify_path}")"
assert_status "restored tenant output" "${status}" "200"
if ! rg -q '"status"[[:space:]]*:[[:space:]]*"Valid"' "${response_file}"; then
  echo "Restored tenant output did not verify." >&2
  exit 1
fi

printf '{"status":"passed","stage":"reporting-portfolio-security-regression","assertions":6}\n'
