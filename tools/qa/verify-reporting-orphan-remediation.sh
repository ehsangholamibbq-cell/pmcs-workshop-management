#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL.}"
: "${PMCS_QA_AUTH_KEY:?Set PMCS_QA_AUTH_KEY.}"
: "${PMCS_QA_S3_ENDPOINT:?Set PMCS_QA_S3_ENDPOINT.}"
: "${PMCS_QA_S3_ACCESS_KEY:?Set PMCS_QA_S3_ACCESS_KEY.}"
: "${PMCS_QA_S3_SECRET_KEY:?Set PMCS_QA_S3_SECRET_KEY.}"
: "${PMCS_QA_S3_BUCKET:?Set PMCS_QA_S3_BUCKET.}"

command -v curl >/dev/null
command -v dotnet >/dev/null
command -v psql >/dev/null
command -v sha256sum >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
source_run_id="71000000-0000-4000-8000-000000000001"
eligible_run_id="73000000-0000-4000-8000-000000000001"
retention_run_id="73000000-0000-4000-8000-000000000002"
legal_hold_run_id="73000000-0000-4000-8000-000000000003"
owned_run_id="73000000-0000-4000-8000-000000000004"
eligible_output_id="73000000-0000-4000-8000-000000000101"
retention_output_id="73000000-0000-4000-8000-000000000102"
legal_hold_output_id="73000000-0000-4000-8000-000000000103"
owned_output_id="73000000-0000-4000-8000-000000000104"
eligible_document_id="73000000-0000-4000-8000-000000000201"
retention_document_id="73000000-0000-4000-8000-000000000202"
legal_hold_document_id="73000000-0000-4000-8000-000000000203"
owned_document_id="73000000-0000-4000-8000-000000000204"
system_actor_id="00000000-0000-0000-0000-000000000001"
eligible_payload="qa-orphan-eligible,1"
retention_payload="qa-orphan-retention,1"
legal_hold_payload="qa-orphan-legal-hold,1"
owned_payload="qa-orphan-owned,1"
eligible_sha="$(printf '%s' "${eligible_payload}" | sha256sum | cut -d ' ' -f 1)"
retention_sha="$(printf '%s' "${retention_payload}" | sha256sum | cut -d ' ' -f 1)"
legal_hold_sha="$(printf '%s' "${legal_hold_payload}" | sha256sum | cut -d ' ' -f 1)"
owned_sha="$(printf '%s' "${owned_payload}" | sha256sum | cut -d ' ' -f 1)"
base_port="${PMCS_QA_ORPHAN_PORT:-5096}"

if ! [[ "${base_port}" =~ ^[0-9]+$ ]] || (( base_port < 1024 || base_port > 65535 )); then
  echo "PMCS_QA_ORPHAN_PORT must be an integer between 1024 and 65535." >&2
  exit 2
fi

scalar() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

execute() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --quiet --command "$1"
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

log_file="$(mktemp)"
api_pid=""

stop_api() {
  if [[ -n "${api_pid}" ]] && kill -0 "${api_pid}" 2>/dev/null; then
    kill "${api_pid}"
    wait "${api_pid}" 2>/dev/null || true
  fi
  api_pid=""
}

cleanup_database_fixtures() {
  execute "
    delete from reporting.report_outputs where id = '${owned_output_id}';
    delete from documents.assets where id in (
      '${eligible_document_id}', '${retention_document_id}',
      '${legal_hold_document_id}', '${owned_document_id}');
    delete from foundation.audit_events
    where event_type = 'GeneratedReportDocumentReleased'
      and resource_id in (
        '${eligible_document_id}', '${retention_document_id}',
        '${legal_hold_document_id}', '${owned_document_id}');
    delete from reporting.report_runs where id in (
      '${eligible_run_id}', '${retention_run_id}', '${legal_hold_run_id}', '${owned_run_id}');
  "
}

cleanup() {
  local exit_code=$?
  trap - EXIT
  set +e
  stop_api
  if (( exit_code != 0 )); then
    sed -n '1,300p' "${log_file}" >&2
  fi
  cleanup_database_fixtures
  dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
    --configuration Release --no-build --no-launch-profile -- cleanup-reporting-orphan-objects >/dev/null
  rm -f -- "${log_file}"
  exit "${exit_code}"
}
trap cleanup EXIT

start_api() {
  local mode="$1"
  : >"${log_file}"
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://127.0.0.1:${base_port}" \
  ConnectionStrings__Pmcs="${PMCS_QA_CONNECTION_STRING}" \
  PMCS_DEV_IDENTITY_ENABLED=false \
  PMCS_SEED_ENABLED=false \
  PMCS_QA_GATEWAY_ENABLED=true \
  PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
  ProjectStateRefresh__Enabled=false \
  AdvisoryIntelligence__WorkerEnabled=false \
  ReportingCenter__Phase1Enabled=true \
  ReportingCenter__OutputAccessEnabled=false \
  ReportingCenter__WorkerEnabled=false \
  ReportingCenter__OrphanRemediationMode="${mode}" \
  ReportingCenter__OrphanRemediationMinimumAgeHours=24 \
  ReportingCenter__OrphanRemediationPollSeconds=5 \
  ReportingCenter__OrphanRemediationBatchSize=10 \
  ReportingCenter__OrphanRemediationMaximumCandidatesPerSweep=20 \
  ReportingCenter__PdfLicense=Unconfigured \
  ObjectStorage__ServiceUrl="${PMCS_QA_S3_ENDPOINT}" \
  ObjectStorage__AccessKey="${PMCS_QA_S3_ACCESS_KEY}" \
  ObjectStorage__SecretKey="${PMCS_QA_S3_SECRET_KEY}" \
  ObjectStorage__BucketName="${PMCS_QA_S3_BUCKET}" \
  ObjectStorage__Region="us-east-1" \
  ObjectStorage__ForcePathStyle=true \
  ObjectStorage__CreateBucketIfMissing=true \
  dotnet run --project src/backend/Pmcs.Api/Pmcs.Api.csproj \
    --configuration Release --no-build --no-launch-profile >"${log_file}" 2>&1 &
  api_pid=$!

  local ready=false
  for _ in {1..60}; do
    if ! kill -0 "${api_pid}" 2>/dev/null; then
      echo "PMCS orphan-remediation API exited before readiness." >&2
      exit 1
    fi
    if curl --silent --fail "http://127.0.0.1:${base_port}/health/ready" >/dev/null; then
      ready=true
      break
    fi
    sleep 1
  done
  if [[ "${ready}" != true ]]; then
    echo "PMCS orphan-remediation API did not become ready within 60 seconds." >&2
    exit 1
  fi
}

wait_for_log() {
  local label="$1"
  local expected="$2"
  for _ in {1..30}; do
    if grep -Fq -- "${expected}" "${log_file}"; then
      return
    fi
    if ! kill -0 "${api_pid}" 2>/dev/null; then
      echo "${label}: API exited before the expected inventory evidence." >&2
      exit 1
    fi
    sleep 1
  done
  echo "${label}: expected log evidence was not observed." >&2
  exit 1
}

connected_database="$(scalar 'select current_database();')"
guarded_database="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
if [[ "${connected_database}" != "${guarded_database}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

cleanup_database_fixtures
execute "delete from foundation.audit_events where event_type = 'GeneratedReportOrphanRemediated' and resource_id = '${eligible_document_id}';"

dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- prepare-reporting-orphan-objects

execute "
  with fixtures(run_id, correlation_id, idempotency_hash, status, pipeline_stage, output_count) as (
    values
      ('${eligible_run_id}'::uuid, 'qa-rpt1-orphan-eligible', repeat('a', 64), 'Failed', 'Failed', 0),
      ('${retention_run_id}'::uuid, 'qa-rpt1-orphan-retention', repeat('b', 64), 'Failed', 'Failed', 0),
      ('${legal_hold_run_id}'::uuid, 'qa-rpt1-orphan-legal-hold', repeat('c', 64), 'Failed', 'Failed', 0),
      ('${owned_run_id}'::uuid, 'qa-rpt1-orphan-owned', repeat('d', 64), 'Succeeded', 'Complete', 1)
  ), source as (
    select * from reporting.report_runs where id = '${source_run_id}'
  )
  insert into reporting.report_runs(
    id, tenant_id, project_id, definition_id, definition_code, template_version_id,
    template_version, parameters_json, parameters_hash, requested_formats, as_of_utc,
    project_time_zone, requested_by, request_permission_snapshot,
    processing_permission_snapshot, correlation_id, idempotency_key_hash, status,
    pipeline_stage, attempt_count, created_at, claimed_at, started_at, completed_at,
    next_attempt_at, diagnostic_code, diagnostic_detail, snapshot_id, output_count, revision)
  select fixture.run_id, source.tenant_id, source.project_id, source.definition_id,
    source.definition_code, source.template_version_id, source.template_version,
    source.parameters_json, source.parameters_hash, source.requested_formats, source.as_of_utc,
    source.project_time_zone, source.requested_by, source.request_permission_snapshot,
    source.processing_permission_snapshot, fixture.correlation_id, fixture.idempotency_hash,
    fixture.status, fixture.pipeline_stage, 1, now() - interval '11 years', null,
    now() - interval '11 years', now() - interval '10 years', null,
    case when fixture.status = 'Failed' then 'reporting.renderer.transient' else null end,
    null, source.snapshot_id, fixture.output_count, 3
  from fixtures fixture cross join source;

  insert into documents.assets(
    id, tenant_id, project_id, owner_type, owner_id, version_number, original_file_name,
    content_type, size_bytes, sha256, object_key, classification, retention_policy,
    retain_until, legal_hold, status, scan_verdict, scan_provider, scan_details,
    created_by, created_at, upload_expires_at, uploaded_at, storage_etag, released_by,
    released_at, classified_by, classified_at, deleted_at, revision)
  values
    ('${eligible_document_id}', '${tenant_id}', '${project_id}', 'ReportOutput',
      '${eligible_output_id}', 1, 'qa-orphan-eligible.csv', 'text/csv', ${#eligible_payload},
      '${eligible_sha}', 'tenants/11111111111111111111111111111111/projects/33333333333333333333333333333333/documents/73000000000040008000000000000201/v1.csv',
      'Internal', 'LongTerm', now() - interval '1 day', false, 'Released', 'Clean',
      'pmcs-generated-content', 'QA orphan-remediation fixture.', '${system_actor_id}',
      now() - interval '11 years', now() - interval '11 years' + interval '1 day',
      now() - interval '11 years', 'qa-fixture', '${system_actor_id}',
      now() - interval '10 years', null, null, null, 3),
    ('${retention_document_id}', '${tenant_id}', '${project_id}', 'ReportOutput',
      '${retention_output_id}', 1, 'qa-orphan-retention.csv', 'text/csv', ${#retention_payload},
      '${retention_sha}', 'tenants/11111111111111111111111111111111/projects/33333333333333333333333333333333/documents/73000000000040008000000000000202/v1.csv',
      'Internal', 'LongTerm', now() + interval '1 year', false, 'Released', 'Clean',
      'pmcs-generated-content', 'QA orphan-remediation fixture.', '${system_actor_id}',
      now() - interval '11 years', now() - interval '11 years' + interval '1 day',
      now() - interval '11 years', 'qa-fixture', '${system_actor_id}',
      now() - interval '10 years', null, null, null, 3),
    ('${legal_hold_document_id}', '${tenant_id}', '${project_id}', 'ReportOutput',
      '${legal_hold_output_id}', 1, 'qa-orphan-legal-hold.csv', 'text/csv', ${#legal_hold_payload},
      '${legal_hold_sha}', 'tenants/11111111111111111111111111111111/projects/33333333333333333333333333333333/documents/73000000000040008000000000000203/v1.csv',
      'Internal', 'LongTerm', now() - interval '1 day', true, 'Released', 'Clean',
      'pmcs-generated-content', 'QA orphan-remediation fixture.', '${system_actor_id}',
      now() - interval '11 years', now() - interval '11 years' + interval '1 day',
      now() - interval '11 years', 'qa-fixture', '${system_actor_id}',
      now() - interval '10 years', null, null, null, 3),
    ('${owned_document_id}', '${tenant_id}', '${project_id}', 'ReportOutput',
      '${owned_output_id}', 1, 'qa-orphan-owned.csv', 'text/csv', ${#owned_payload},
      '${owned_sha}', 'tenants/11111111111111111111111111111111/projects/33333333333333333333333333333333/documents/73000000000040008000000000000204/v1.csv',
      'Internal', 'LongTerm', now() - interval '1 day', false, 'Released', 'Clean',
      'pmcs-generated-content', 'QA orphan-remediation fixture.', '${system_actor_id}',
      now() - interval '11 years', now() - interval '11 years' + interval '1 day',
      now() - interval '11 years', 'qa-fixture', '${system_actor_id}',
      now() - interval '10 years', null, null, null, 3);

  insert into foundation.audit_events(
    event_id, tenant_id, project_id, actor_user_id, event_type, resource_type,
    resource_id, occurred_at, data, correlation_id)
  values
    ('73000000-0000-4000-8000-000000000301', '${tenant_id}', '${project_id}',
      '${system_actor_id}', 'GeneratedReportDocumentReleased', 'DocumentAsset',
      '${eligible_document_id}', now() - interval '10 years',
      jsonb_build_object('executor', 'SystemWorker', 'ownerType', 'ReportOutput',
        'ownerId', '${eligible_output_id}'), 'qa-rpt1-orphan-eligible'),
    ('73000000-0000-4000-8000-000000000302', '${tenant_id}', '${project_id}',
      '${system_actor_id}', 'GeneratedReportDocumentReleased', 'DocumentAsset',
      '${retention_document_id}', now() - interval '10 years',
      jsonb_build_object('executor', 'SystemWorker', 'ownerType', 'ReportOutput',
        'ownerId', '${retention_output_id}'), 'qa-rpt1-orphan-retention'),
    ('73000000-0000-4000-8000-000000000303', '${tenant_id}', '${project_id}',
      '${system_actor_id}', 'GeneratedReportDocumentReleased', 'DocumentAsset',
      '${legal_hold_document_id}', now() - interval '10 years',
      jsonb_build_object('executor', 'SystemWorker', 'ownerType', 'ReportOutput',
        'ownerId', '${legal_hold_output_id}'), 'qa-rpt1-orphan-legal-hold');

  insert into reporting.report_outputs(
    id, run_id, snapshot_id, template_version_id, tenant_id, project_id, format,
    content_type, file_name, generated_document_id, size_bytes, sha256,
    verification_code, manifest_sha256, classification, retention_policy,
    archive_state, created_at, archived_at)
  select '${owned_output_id}', '${owned_run_id}', source.snapshot_id,
    source.template_version_id, source.tenant_id, source.project_id, 'Csv', 'text/csv',
    'qa-orphan-owned.csv', '${owned_document_id}', ${#owned_payload}, '${owned_sha}',
    'RPT-7300-0000-0000-0104', repeat('e', 64), 'Internal', 'LongTerm', 'Active',
    now() - interval '10 years', null
  from reporting.report_runs source where source.id = '${source_run_id}';
"

start_api InventoryOnly
wait_for_log \
  "dry-run orphan inventory" \
  "mode=InventoryOnly, scanned=4, orphans=3, eligible=1, remediated=0, retentionProtected=1, legalHoldProtected=1, owned=1, recoverable=0, ambiguous=0, failed=0"
expect_equal \
  "inventory-only mode preserves every candidate" \
  "4|0" \
  "select (select count(*) from documents.assets where id in ('${eligible_document_id}', '${retention_document_id}', '${legal_hold_document_id}', '${owned_document_id}'))::text || '|' || (select count(*) from foundation.audit_events where event_type = 'GeneratedReportOrphanRemediated' and resource_id = '${eligible_document_id}')::text;"
stop_api

start_api ApplyEligible
wait_for_log \
  "apply orphan remediation" \
  "mode=ApplyEligible, scanned=4, orphans=3, eligible=1, remediated=1, retentionProtected=1, legalHoldProtected=1, owned=1, recoverable=0, ambiguous=0, failed=0"
expect_equal \
  "only the expired unheld orphan is remediated" \
  "0|1|1|1" \
  "select (select count(*) from documents.assets where id = '${eligible_document_id}')::text || '|' || (select count(*) from documents.assets where id = '${retention_document_id}')::text || '|' || (select count(*) from documents.assets where id = '${legal_hold_document_id}')::text || '|' || (select count(*) from documents.assets where id = '${owned_document_id}')::text;"
expect_equal \
  "orphan remediation audit is singular complete and object-key free" \
  "1|${eligible_output_id}|${eligible_run_id}|LongTerm|true" \
  "select count(*)::text || '|' || min(data->>'ownerId') || '|' || min(data->>'runId') || '|' || min(data->>'retentionPolicy') || '|' || bool_and(not (data ? 'objectKey'))::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'GeneratedReportOrphanRemediated' and resource_type = 'DocumentAsset' and resource_id = '${eligible_document_id}' and actor_user_id = '${system_actor_id}';"

dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- verify-reporting-orphan-objects

sleep 6
expect_equal \
  "orphan remediation is idempotent across another sweep" \
  "1|1|1|1" \
  "select (select count(*) from foundation.audit_events where event_type = 'GeneratedReportOrphanRemediated' and resource_id = '${eligible_document_id}')::text || '|' || (select count(*) from documents.assets where id = '${retention_document_id}')::text || '|' || (select count(*) from documents.assets where id = '${legal_hold_document_id}')::text || '|' || (select count(*) from documents.assets where id = '${owned_document_id}')::text;"

stop_api
printf '{"status":"passed","stage":"reporting-orphan-remediation-regression","assertions":7}\n'
