#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL to the isolated QA database.}"
: "${PMCS_QA_BASE_URL:?Set PMCS_QA_BASE_URL to the running isolated QA API.}"
: "${PMCS_QA_AUTH_KEY:?Set PMCS_QA_AUTH_KEY.}"
: "${PMCS_QA_S3_ENDPOINT:?Set PMCS_QA_S3_ENDPOINT.}"
: "${PMCS_QA_S3_ACCESS_KEY:?Set PMCS_QA_S3_ACCESS_KEY.}"
: "${PMCS_QA_S3_SECRET_KEY:?Set PMCS_QA_S3_SECRET_KEY.}"
: "${PMCS_QA_S3_BUCKET:?Set PMCS_QA_S3_BUCKET.}"

command -v dotnet >/dev/null
command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
reporting_succeeded_run_id="71000000-0000-4000-8000-000000000001"

scalar() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

output_id="$(scalar "select id from reporting.report_outputs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_succeeded_run_id}' and format = 'Xlsx';")"
object_key="$(scalar "select asset.object_key from reporting.report_outputs output join documents.assets asset on asset.id = output.generated_document_id and asset.tenant_id = output.tenant_id where output.id = '${output_id}' and asset.owner_type = 'ReportOutput' and asset.owner_id = output.id;")"
expected_sha256="$(scalar "select sha256 from reporting.report_outputs where id = '${output_id}';")"
expected_content_type="$(scalar "select content_type from reporting.report_outputs where id = '${output_id}';")"
audit_count_before="$(scalar "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'CertifiedReportOutputIntegrityFailed' and resource_type = 'ReportOutput' and resource_id = '${output_id}';")"

if ! [[ "${output_id}" =~ ^[0-9a-f-]{36}$ &&
  "${object_key}" =~ ^tenants/[0-9a-f]{32}/projects/[0-9a-f]{32}/documents/[0-9a-f]{32}/v1\.xlsx$ &&
  "${expected_sha256}" =~ ^[0-9a-f]{64}$ &&
  "${audit_count_before}" =~ ^[0-9]+$ ]]; then
  echo "Reporting object-security fixture is missing or invalid." >&2
  exit 1
fi

PMCS_QA_REPORTING_OUTPUT_ID="${output_id}" \
PMCS_QA_REPORTING_OBJECT_KEY="${object_key}" \
PMCS_QA_REPORTING_EXPECTED_SHA256="${expected_sha256}" \
PMCS_QA_REPORTING_EXPECTED_CONTENT_TYPE="${expected_content_type}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- verify-reporting-object-security

audit_count_after="$(scalar "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'CertifiedReportOutputIntegrityFailed' and resource_type = 'ReportOutput' and resource_id = '${output_id}';")"
expected_audit_count=$((audit_count_before + 4))
if [[ "${audit_count_after}" != "${expected_audit_count}" ]]; then
  echo "Object tamper verification did not create exactly four integrity-failure audits." >&2
  exit 1
fi

orphan_count="$(scalar "select count(*) from documents.assets asset left join reporting.report_outputs output on output.id = asset.owner_id and output.tenant_id = asset.tenant_id and output.project_id = asset.project_id where asset.tenant_id = '${tenant_id}' and asset.project_id = '${project_id}' and asset.owner_type = 'ReportOutput' and output.id is null;")"
if [[ "${orphan_count}" != "0" ]]; then
  echo "Reporting object-security verification left an orphan generated document." >&2
  exit 1
fi

printf '{"status":"passed","stage":"reporting-object-security-regression","assertions":8}\n'
