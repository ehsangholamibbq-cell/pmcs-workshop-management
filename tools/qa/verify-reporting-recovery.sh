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
locked_run_id="71000000-0000-4000-8000-000000000004"
skipped_run_id="71000000-0000-4000-8000-000000000005"
before_storage_run_id="71000000-0000-4000-8000-000000000006"
after_storage_run_id="71000000-0000-4000-8000-000000000007"
stale_lease_run_id="71000000-0000-4000-8000-000000000008"

base_port="${PMCS_QA_PORT:-5090}"
if ! [[ "${base_port}" =~ ^[0-9]+$ ]] || (( base_port < 1024 || base_port > 65534 )); then
  echo "PMCS_QA_PORT must leave two valid ports between 1024 and 65535." >&2
  exit 2
fi
second_port=$((base_port + 1))

temporary_directory="$(mktemp -d)"
process_ids=()
started_pid=""

scalar() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

execute() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --quiet --command "$1" >/dev/null
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

wait_equal() {
  local label="$1"
  local expected="$2"
  local query="$3"
  local actual=""
  for _ in {1..160}; do
    actual="$(scalar "${query}")"
    if [[ "${actual}" == "${expected}" ]]; then
      return
    fi
    sleep 0.25
  done
  echo "${label}: expected '${expected}', last received '${actual}'." >&2
  exit 1
}

wait_for_log() {
  local label="$1"
  local log_file="$2"
  local pattern="$3"
  local pid="$4"
  for _ in {1..160}; do
    if grep -q "${pattern}" "${log_file}"; then
      return
    fi
    if ! kill -0 "${pid}" 2>/dev/null; then
      echo "${label}: worker exited before the qualification pause." >&2
      exit 1
    fi
    sleep 0.25
  done
  echo "${label}: qualification pause marker was not observed." >&2
  exit 1
}

start_api() {
  local worker_instance_id="$1"
  local port="$2"
  local worker_enabled="$3"
  local pause_point="$4"
  local target_run_id="$5"
  local log_file="$6"

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
  ReportingCenter__QualificationPausePoint="${pause_point}" \
  ReportingCenter__QualificationTargetRunId="${target_run_id}" \
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
  for _ in {1..80}; do
    if ! kill -0 "${started_pid}" 2>/dev/null; then
      echo "Reporting recovery API '${worker_instance_id}' exited before readiness." >&2
      exit 1
    fi
    if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
      ready=true
      break
    fi
    sleep 0.25
  done
  if [[ "${ready}" != true ]]; then
    echo "Reporting recovery API '${worker_instance_id}' did not become ready." >&2
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

crash_process() {
  local pid="$1"
  if ! kill -0 "${pid}" 2>/dev/null; then
    echo "The qualification worker exited before the requested crash." >&2
    exit 1
  fi
  kill -KILL "${pid}"
  wait "${pid}" 2>/dev/null || true
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
        echo "Reporting recovery log: $(basename "${log_file}")" >&2
        sed -n '1,260p' "${log_file}" >&2
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

prepare_log="${temporary_directory}/prepare.log"
start_api "qa-rpt1-prepare" "${base_port}" false None "" "${prepare_log}"
prepare_pid="${started_pid}"
PMCS_QA_BASE_URL="http://127.0.0.1:${base_port}" \
PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- prepare-reporting-recovery
execute "update reporting.report_runs set next_attempt_at = now() + interval '1 day' where id in ('${before_storage_run_id}', '${after_storage_run_id}', '${stale_lease_run_id}');"
stop_process "${prepare_pid}"

locked_log="${temporary_directory}/locked-worker.log"
start_api "qa-rpt1-worker-a" "${base_port}" true AfterSnapshotRowLock "${locked_run_id}" "${locked_log}"
locked_pid="${started_pid}"
wait_for_log \
  "first worker row-lock pause" \
  "${locked_log}" \
  "paused at AfterSnapshotRowLock for run ${locked_run_id}" \
  "${locked_pid}"
expect_equal \
  "locked run remains invisible before claim commit" \
  "Queued|0" \
  "select status || '|' || attempt_count::text from reporting.report_runs where id = '${locked_run_id}';"
expect_equal \
  "first real worker holds an open PostgreSQL claim transaction" \
  "1" \
  "select count(*) from pg_stat_activity where datname = current_database() and application_name = 'qa-rpt1-worker-a' and state = 'idle in transaction';"

skipped_log="${temporary_directory}/skipped-worker.log"
start_api "qa-rpt1-worker-b" "${second_port}" true None "" "${skipped_log}"
skipped_pid="${started_pid}"
wait_equal \
  "second worker skips the locked head run" \
  "Succeeded|1|1" \
  "select status || '|' || attempt_count::text || '|' || output_count::text from reporting.report_runs where id = '${skipped_run_id}';"
expect_equal \
  "head run stays queued while the second worker completes later work" \
  "Queued|0" \
  "select status || '|' || attempt_count::text from reporting.report_runs where id = '${locked_run_id}';"
expect_equal \
  "second worker identity is persisted in audit evidence" \
  "qa-rpt1-worker-b" \
  "select data->>'workerInstanceId' from foundation.audit_events where event_type = 'CertifiedReportSnapshotBuilt' and resource_id = '${skipped_run_id}' order by occurred_at desc limit 1;"

crash_process "${locked_pid}"
wait_equal \
  "rolled-back locked claim is recovered by the surviving worker" \
  "Succeeded|1|1" \
  "select status || '|' || attempt_count::text || '|' || output_count::text from reporting.report_runs where id = '${locked_run_id}';"
stop_process "${skipped_pid}"

execute "update reporting.report_runs set next_attempt_at = null where id = '${before_storage_run_id}';"
before_log="${temporary_directory}/before-storage-crash.log"
start_api "qa-rpt1-before-crash" "${base_port}" true BeforeStorage "${before_storage_run_id}" "${before_log}"
before_pid="${started_pid}"
wait_for_log \
  "before-storage pause" \
  "${before_log}" \
  "paused at BeforeStorage for run ${before_storage_run_id}" \
  "${before_pid}"
expect_equal \
  "before-storage crash window has snapshot but no document or output" \
  "Processing|Rendering|1|1|0|0" \
  "select run.status || '|' || run.pipeline_stage || '|' || run.attempt_count::text || '|' || (select count(*) from reporting.report_snapshots where run_id = run.id)::text || '|' || (select count(*) from reporting.report_outputs where run_id = run.id)::text || '|' || (select count(*) from foundation.audit_events where event_type = 'GeneratedReportDocumentReleased' and correlation_id = run.correlation_id)::text from reporting.report_runs run where run.id = '${before_storage_run_id}';"
crash_process "${before_pid}"
execute "update reporting.report_runs set claimed_at = now() - interval '11 minutes' where id = '${before_storage_run_id}' and status = 'Processing' and pipeline_stage = 'Rendering';"

before_recovery_log="${temporary_directory}/before-storage-recovery.log"
start_api "qa-rpt1-before-recovery" "${base_port}" true None "" "${before_recovery_log}"
before_recovery_pid="${started_pid}"
wait_equal \
  "before-storage stale rendering lease is recovered" \
  "Succeeded|2|1" \
  "select status || '|' || attempt_count::text || '|' || output_count::text from reporting.report_runs where id = '${before_storage_run_id}';"
stop_process "${before_recovery_pid}"

execute "update reporting.report_runs set next_attempt_at = null where id = '${after_storage_run_id}';"
after_log="${temporary_directory}/after-storage-crash.log"
start_api "qa-rpt1-after-crash" "${base_port}" true AfterStorage "${after_storage_run_id}" "${after_log}"
after_pid="${started_pid}"
wait_for_log \
  "after-storage pause" \
  "${after_log}" \
  "paused at AfterStorage for run ${after_storage_run_id}" \
  "${after_pid}"
expect_equal \
  "after-storage crash window preserves one document without a reporting output" \
  "Processing|Rendering|1|1|0|1" \
  "select run.status || '|' || run.pipeline_stage || '|' || run.attempt_count::text || '|' || (select count(*) from reporting.report_snapshots where run_id = run.id)::text || '|' || (select count(*) from reporting.report_outputs where run_id = run.id)::text || '|' || (select count(*) from foundation.audit_events where event_type = 'GeneratedReportDocumentReleased' and correlation_id = run.correlation_id)::text from reporting.report_runs run where run.id = '${after_storage_run_id}';"
expect_equal \
  "orphan inventory identifies the crash-after-storage document" \
  "1" \
  "select count(*) from documents.assets asset join foundation.audit_events event on event.event_type = 'GeneratedReportDocumentReleased' and event.resource_type = 'DocumentAsset' and event.resource_id = asset.id::text join reporting.report_runs run on run.correlation_id = event.correlation_id and run.id = '${after_storage_run_id}' left join reporting.report_outputs output on output.id = asset.owner_id and output.tenant_id = asset.tenant_id and output.project_id = asset.project_id where asset.owner_type = 'ReportOutput' and output.id is null;"
crash_process "${after_pid}"
execute "update reporting.report_runs set claimed_at = now() - interval '11 minutes' where id = '${after_storage_run_id}' and status = 'Processing' and pipeline_stage = 'Rendering';"

after_recovery_log="${temporary_directory}/after-storage-recovery.log"
start_api "qa-rpt1-after-recovery" "${base_port}" true None "" "${after_recovery_log}"
after_recovery_pid="${started_pid}"
wait_equal \
  "after-storage stale rendering lease reuses the stable document" \
  "Succeeded|2|1" \
  "select status || '|' || attempt_count::text || '|' || output_count::text from reporting.report_runs where id = '${after_storage_run_id}';"
expect_equal \
  "orphan inventory is empty after stable recovery" \
  "0" \
  "select count(*) from documents.assets asset join foundation.audit_events event on event.event_type = 'GeneratedReportDocumentReleased' and event.resource_type = 'DocumentAsset' and event.resource_id = asset.id::text join reporting.report_runs run on run.correlation_id = event.correlation_id and run.id = '${after_storage_run_id}' left join reporting.report_outputs output on output.id = asset.owner_id and output.tenant_id = asset.tenant_id and output.project_id = asset.project_id where asset.owner_type = 'ReportOutput' and output.id is null;"
stop_process "${after_recovery_pid}"

execute "update reporting.report_runs set status = 'Processing', pipeline_stage = 'BuildingSnapshot', attempt_count = 1, claimed_at = now() - interval '11 minutes', started_at = now() - interval '11 minutes', next_attempt_at = null, diagnostic_code = null, diagnostic_detail = null, revision = revision + 1 where id = '${stale_lease_run_id}' and status = 'Queued';"
stale_log="${temporary_directory}/stale-lease-recovery.log"
start_api "qa-rpt1-stale-recovery" "${base_port}" true None "" "${stale_log}"
stale_pid="${started_pid}"
wait_equal \
  "stale snapshot-building lease is reclaimed" \
  "Succeeded|2|1" \
  "select status || '|' || attempt_count::text || '|' || output_count::text from reporting.report_runs where id = '${stale_lease_run_id}';"

PMCS_QA_BASE_URL="http://127.0.0.1:${base_port}" \
PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- verify-reporting-recovery

expect_equal \
  "all recovery runs have exactly one output and one governed document" \
  "5|5|5" \
  "select count(distinct run.id)::text || '|' || count(distinct output.id)::text || '|' || count(distinct asset.id)::text from reporting.report_runs run join reporting.report_outputs output on output.run_id = run.id join documents.assets asset on asset.id = output.generated_document_id and asset.owner_type = 'ReportOutput' and asset.owner_id = output.id where run.tenant_id = '${tenant_id}' and run.project_id = '${project_id}' and run.id in ('${locked_run_id}', '${skipped_run_id}', '${before_storage_run_id}', '${after_storage_run_id}', '${stale_lease_run_id}') and run.status = 'Succeeded';"
expect_equal \
  "after-storage recovery did not duplicate document publication side effects" \
  "1|1|1" \
  "select (select count(*) from foundation.audit_events event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${after_storage_run_id}' and event.event_type = 'GeneratedReportDocumentReleased')::text || '|' || (select count(*) from foundation.outbox_messages event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${after_storage_run_id}' and event.event_type = 'documents.asset.released.v1')::text || '|' || (select count(*) from foundation.audit_events where event_type = 'CertifiedReportRunCompleted' and resource_id = '${after_storage_run_id}')::text;"
expect_equal \
  "crash recovery worker lineage is explicit" \
  "qa-rpt1-before-crash|qa-rpt1-before-recovery|qa-rpt1-after-crash|qa-rpt1-after-recovery|qa-rpt1-stale-recovery" \
  "select (select data->>'workerInstanceId' from foundation.audit_events where event_type = 'CertifiedReportRenderingStarted' and resource_id = '${before_storage_run_id}' order by occurred_at limit 1) || '|' || (select data->>'workerInstanceId' from foundation.audit_events where event_type = 'CertifiedReportRenderingResumed' and resource_id = '${before_storage_run_id}' order by occurred_at limit 1) || '|' || (select data->>'workerInstanceId' from foundation.audit_events where event_type = 'CertifiedReportRenderingStarted' and resource_id = '${after_storage_run_id}' order by occurred_at limit 1) || '|' || (select data->>'workerInstanceId' from foundation.audit_events where event_type = 'CertifiedReportRenderingResumed' and resource_id = '${after_storage_run_id}' order by occurred_at limit 1) || '|' || (select data->>'workerInstanceId' from foundation.audit_events where event_type = 'CertifiedReportSnapshotBuilt' and resource_id = '${stale_lease_run_id}' order by occurred_at limit 1);"

stop_process "${stale_pid}"
printf '{"status":"passed","stage":"reporting-worker-recovery-regression","assertions":17}\n'
