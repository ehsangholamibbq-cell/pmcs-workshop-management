#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING to the isolated QA database.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL to the same isolated QA database as a PostgreSQL URI.}"

command -v dotnet >/dev/null
command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
site_supervisor_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
observer_id="50000000-0000-4000-8000-000000000001"
workflow_report_id="70000000-0000-4000-8000-000000000001"
workflow_fact_id="70000000-0000-4000-8000-000000000002"
verified_evidence_id="70000000-0000-4000-8000-000000000003"
signature_mismatch_evidence_id="70000000-0000-4000-8000-000000000004"
verified_sha256="f0a951f7037b25843b68d52c36bc65385686e2c527ca22f77ad0c9fad2ec61bc"
signature_mismatch_sha256="5a8c13bafe422334fb8723feb0fee37aaf1c20cf5affe467d16a02d7f257a2d0"
verified_object_key="tenants/11111111111111111111111111111111/projects/33333333333333333333333333333333/evidence/70000000000040008000000000000003.pdf"

scalar() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

expect_equal() {
  local label="$1"
  local expected="$2"
  local query="$3"
  local actual
  actual="$(scalar "${query}")"
  if [[ "${actual}" != "${expected}" ]]; then
    echo "${label}: expected '${expected}', received '${actual}'." >&2
    exit 1
  fi
}

database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
connected_database="$(scalar 'select current_database();')"
if [[ "${connected_database}" != "${database_name}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

expect_equal \
  "verified evidence metadata and lineage" \
  "Uploaded|2|application/pdf|68|${verified_sha256}|${site_supervisor_id}|${workflow_report_id}|${workflow_fact_id}|${verified_object_key}|true" \
  "select status || '|' || revision::text || '|' || content_type || '|' || size_bytes::text || '|' || sha256 || '|' || created_by::text || '|' || daily_report_id::text || '|' || daily_fact_id::text || '|' || object_key || '|' || (storage_etag is not null and btrim(storage_etag) <> '')::text from evidence.files where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${verified_evidence_id}';"

expect_equal \
  "disguised content remains pending without storage receipt" \
  "PendingUpload|1|application/pdf|31|${signature_mismatch_sha256}|true" \
  "select status || '|' || revision::text || '|' || content_type || '|' || size_bytes::text || '|' || sha256 || '|' || (storage_etag is null and uploaded_at is null)::text from evidence.files where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${signature_mismatch_evidence_id}';"

expect_equal \
  "denied and invalid sessions created no metadata" \
  "0" \
  "select count(*) from evidence.files where id in ('70000000-0000-4000-8000-000000000005', '70000000-0000-4000-8000-000000000006', '70000000-0000-4000-8000-000000000007');"

expect_equal \
  "verified evidence audit coverage" \
  "3" \
  "select count(distinct event_type) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'EvidenceFile' and resource_id = '${verified_evidence_id}' and event_type in ('EvidenceUploadSessionCreated', 'EvidenceUploaded', 'EvidenceDownloaded');"

expect_equal \
  "verified evidence audit actor lineage" \
  "2|1" \
  "select count(*) filter (where actor_user_id = '${site_supervisor_id}' and event_type in ('EvidenceUploadSessionCreated', 'EvidenceUploaded'))::text || '|' || count(*) filter (where actor_user_id = '${observer_id}' and event_type = 'EvidenceDownloaded')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'EvidenceFile' and resource_id = '${verified_evidence_id}';"

expect_equal \
  "evidence audit correlation and hash completeness" \
  "0" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'EvidenceFile' and resource_id in ('${verified_evidence_id}', '${signature_mismatch_evidence_id}') and (correlation_id is null or btrim(correlation_id) = '' or data->>'sha256' is null);"

expect_equal \
  "disguised content produced no uploaded audit" \
  "1|0" \
  "select count(*) filter (where event_type = 'EvidenceUploadSessionCreated')::text || '|' || count(*) filter (where event_type = 'EvidenceUploaded')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'EvidenceFile' and resource_id = '${signature_mismatch_evidence_id}';"

expect_equal \
  "verified evidence transactional outbox coverage" \
  "2" \
  "select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type in ('Evidence.EvidenceUploadSessionCreated', 'Evidence.EvidenceUploaded') and coalesce(payload->'evidence'->>'id', payload->>'id') = '${verified_evidence_id}' and correlation_id is not null and btrim(correlation_id) <> '';"

expect_equal \
  "evidence idempotency receipts are unique" \
  "3" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('qa-v3-evidence-session', 'qa-v3-evidence-content', 'qa-v3-evidence-signature-session');"

expect_equal \
  "rejected evidence operations left no success receipt" \
  "0" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('qa-v3-evidence-observer-denied', 'qa-v3-evidence-missing-target', 'qa-v3-evidence-unsupported-type', 'qa-v3-evidence-signature-rejected', 'qa-v3-evidence-client-id-reused', 'qa-v3-evidence-wrong-content-type', 'qa-v3-evidence-wrong-hash');"

printf 'QA file, attachment, object-storage and evidence audit verification passed for %s.\n' "${database_name}"
