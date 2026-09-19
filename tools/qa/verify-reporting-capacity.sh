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

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${repository_root}"

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
poison_run_id="72000000-0000-4000-8000-000000000001"
port="${PMCS_QA_PORT:-5090}"

if ! [[ "${port}" =~ ^[0-9]+$ ]] || (( port < 1024 || port > 65535 )); then
  echo "PMCS_QA_PORT must be an integer between 1024 and 65535." >&2
  exit 2
fi

temporary_directory="$(mktemp -d)"
process_ids=()
started_pid=""

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

start_api() {
  local worker_instance_id="$1"
  local worker_enabled="$2"
  local failure_point="$3"
  local target_run_id="$4"
  local log_file="$5"

  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_CONTENTROOT="${repository_root}/src/backend/Pmcs.Api" \
  ASPNETCORE_URLS="http://127.0.0.1:${port}" \
  ConnectionStrings__Pmcs="${PMCS_QA_CONNECTION_STRING};Application Name=${worker_instance_id}" \
  PMCS_DEV_IDENTITY_ENABLED=false \
  PMCS_SEED_ENABLED=true \
  PMCS_QA_GATEWAY_ENABLED=true \
  PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
  ProjectStateRefresh__Enabled=false \
  AdvisoryIntelligence__WorkerEnabled=false \
  ReportingCenter__Phase1Enabled=true \
  ReportingCenter__OutputAccessEnabled=true \
  ReportingCenter__WorkerEnabled="${worker_enabled}" \
  ReportingCenter__WorkerInstanceId="${worker_instance_id}" \
  ReportingCenter__QualificationFailurePoint="${failure_point}" \
  ReportingCenter__QualificationTargetRunId="${target_run_id}" \
  ReportingCenter__MaximumAttempts=3 \
  ReportingCenter__RetryBaseDelaySeconds=1 \
  ReportingCenter__ProcessingTimeoutSeconds=120 \
  ReportingCenter__QueueAgeWarningSeconds=120 \
  ReportingCenter__PollSeconds=1 \
  ReportingCenter__PdfLicense=Unconfigured \
  ObjectStorage__ServiceUrl="${PMCS_QA_S3_ENDPOINT}" \
  ObjectStorage__AccessKey="${PMCS_QA_S3_ACCESS_KEY}" \
  ObjectStorage__SecretKey="${PMCS_QA_S3_SECRET_KEY}" \
  ObjectStorage__BucketName="${PMCS_QA_S3_BUCKET}" \
  ObjectStorage__Region="us-east-1" \
  ObjectStorage__ForcePathStyle=true \
  ObjectStorage__CreateBucketIfMissing=true \
  dotnet src/backend/Pmcs.Api/bin/Release/net10.0/Pmcs.Api.dll >"${log_file}" 2>&1 &
  started_pid=$!
  process_ids+=("${started_pid}")

  local ready=false
  for _ in {1..120}; do
    if ! kill -0 "${started_pid}" 2>/dev/null; then
      echo "Reporting capacity API '${worker_instance_id}' exited before readiness." >&2
      exit 1
    fi
    if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
      ready=true
      break
    fi
    sleep 0.25
  done
  if [[ "${ready}" != true ]]; then
    echo "Reporting capacity API '${worker_instance_id}' did not become ready." >&2
    exit 1
  fi
}

stop_process() {
  local pid="$1"
  if kill -0 "${pid}" 2>/dev/null; then
    kill "${pid}"
    wait "${pid}" 2>/dev/null || true
  fi
}

cleanup() {
  local exit_code=$?
  set +e
  for pid in "${process_ids[@]}"; do
    if kill -0 "${pid}" 2>/dev/null; then
      kill "${pid}" 2>/dev/null
      wait "${pid}" 2>/dev/null || true
    fi
  done
  if (( exit_code != 0 )); then
    for log_file in "${temporary_directory}"/*.log; do
      if [[ -f "${log_file}" ]]; then
        echo "Reporting capacity log: $(basename "${log_file}")" >&2
        sed -n '1,320p' "${log_file}" >&2
      fi
    done
  fi
  rm -f -- "${temporary_directory}"/*.log
  rmdir "${temporary_directory}" 2>/dev/null || true
  exit "${exit_code}"
}
trap cleanup EXIT

connected_database="$(scalar 'select current_database();')"
guarded_database="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
if [[ "${connected_database}" != "${guarded_database}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

expect_equal \
  "capacity fixture identities are unused before preparation" \
  "0" \
  "select count(*) from reporting.report_runs where id = '${poison_run_id}' or id in (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid from generate_series(101, 120) value);"

prepare_log="${temporary_directory}/prepare.log"
start_api "qa-rpt1-capacity-prepare" false None "" "${prepare_log}"
prepare_pid="${started_pid}"
PMCS_QA_BASE_URL="http://127.0.0.1:${port}" \
PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- prepare-reporting-capacity
expect_equal \
  "capacity preparation queues twenty healthy runs and one poison run" \
  "21|21|0" \
  "select count(*)::text || '|' || count(*) filter (where status = 'Queued' and pipeline_stage = 'Queued' and attempt_count = 0)::text || '|' || count(*) filter (where snapshot_id is not null or output_count <> 0)::text from reporting.report_runs where id = '${poison_run_id}' or id in (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid from generate_series(101, 120) value);"
stop_process "${prepare_pid}"

worker_log="${temporary_directory}/worker.log"
start_api \
  "qa-rpt1-capacity-worker" \
  true \
  BeforeStorageTransientFailure \
  "${poison_run_id}" \
  "${worker_log}"
worker_pid="${started_pid}"

PMCS_QA_BASE_URL="http://127.0.0.1:${port}" \
PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- verify-reporting-capacity

expect_equal \
  "healthy capacity runs complete once while poison exhausts exactly three attempts" \
  "20|20|20|Failed|Failed|3|0|reporting.qa.transient_injected" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value) select (select count(*) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id)::text || '|' || (select count(*) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id where run.status = 'Succeeded' and run.pipeline_stage = 'Complete' and run.attempt_count = 1 and run.output_count = 1)::text || '|' || (select count(*) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id where run.completed_at is not null)::text || '|' || status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || coalesce(diagnostic_code, '<none>') from reporting.report_runs where id = '${poison_run_id}';"
expect_equal \
  "healthy outputs retain one-to-one governed document ownership" \
  "20|20|20|0" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value) select count(distinct run.id)::text || '|' || count(distinct output.id)::text || '|' || count(distinct asset.id)::text || '|' || count(*) filter (where asset.status <> 'Released' or asset.owner_type <> 'ReportOutput' or asset.owner_id <> output.id or asset.sha256 <> output.sha256)::text from healthy_ids join reporting.report_runs run on run.id = healthy_ids.id join reporting.report_outputs output on output.run_id = run.id join documents.assets asset on asset.id = output.generated_document_id;"
expect_equal \
  "poison audit lineage has two requeues and one terminal failure" \
  "2|1|1|2" \
  "select count(*) filter (where event_type = 'CertifiedReportRunRequeued')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunFailed')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRenderingStarted')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRenderingResumed')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'ReportRun' and resource_id = '${poison_run_id}' and data->>'workerInstanceId' = 'qa-rpt1-capacity-worker';"
expect_equal \
  "poison isolation publishes no output document or completion event" \
  "0|0|0" \
  "select (select count(*) from reporting.report_outputs where run_id = '${poison_run_id}')::text || '|' || (select count(*) from foundation.audit_events event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${poison_run_id}' and event.event_type = 'GeneratedReportDocumentReleased')::text || '|' || (select count(*) from foundation.outbox_messages where event_type = 'reporting.report.completed.v1' and payload->>'runId' = '${poison_run_id}')::text;"
expect_equal \
  "healthy capacity audit lineage is singular and worker-attributed" \
  "20|20" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value) select count(*) filter (where event_type = 'CertifiedReportSnapshotBuilt')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunCompleted')::text from foundation.audit_events event join healthy_ids on healthy_ids.id::text = event.resource_id where event.resource_type = 'ReportRun' and event.data->>'workerInstanceId' = 'qa-rpt1-capacity-worker';"
expect_equal \
  "healthy completion and document outbox messages are one per run" \
  "20|20|20|20" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value), healthy_outputs as (select output.id from reporting.report_outputs output join healthy_ids on healthy_ids.id = output.run_id) select (select count(*) from foundation.outbox_messages event join healthy_ids on healthy_ids.id::text = event.payload->>'runId' where event.event_type = 'reporting.report.completed.v1')::text || '|' || (select count(distinct event.payload->>'runId') from foundation.outbox_messages event join healthy_ids on healthy_ids.id::text = event.payload->>'runId' where event.event_type = 'reporting.report.completed.v1')::text || '|' || (select count(*) from foundation.outbox_messages event join healthy_outputs on healthy_outputs.id::text = event.payload->>'ownerId' where event.event_type = 'documents.asset.released.v1')::text || '|' || (select count(distinct event.payload->>'ownerId') from foundation.outbox_messages event join healthy_outputs on healthy_outputs.id::text = event.payload->>'ownerId' where event.event_type = 'documents.asset.released.v1')::text;"
expect_equal \
  "capacity API idempotency receipts are singular" \
  "21|21" \
  "select count(*)::text || '|' || count(distinct key)::text from foundation.idempotency_records where tenant_id = '${tenant_id}' and key like 'qa-rpt1-capacity-%';"
expect_equal \
  "healthy capacity p95 is below thirty seconds" \
  "t" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value) select count(*) = 20 and percentile_disc(0.95) within group (order by extract(epoch from run.completed_at - run.created_at)) < 30 from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id where run.completed_at is not null;"

health_payload="$(curl --silent --fail "http://127.0.0.1:${port}/health/ready")"
if [[ "${health_payload}" != *'"reporting-worker":{"status":"Healthy"'* ]]; then
  echo "Reporting worker health did not become Healthy after the capacity queue drained." >&2
  echo "${health_payload}" >&2
  exit 1
fi

stop_process "${worker_pid}"
printf '{"status":"passed","stage":"reporting-worker-capacity-regression","assertions":11,"healthyRuns":20,"poisonAttempts":3,"p95BudgetSeconds":30}\n'
