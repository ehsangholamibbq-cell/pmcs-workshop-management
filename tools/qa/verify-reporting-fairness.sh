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
command -v node >/dev/null
command -v psql >/dev/null

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${repository_root}"

source_run_id="71000000-0000-4000-8000-000000000001"
project_a_id="73000000-0000-4000-8000-000000000101"
project_b_id="73000000-0000-4000-8000-000000000102"
project_a_head_run_id="73000000-0000-4000-8000-000000000001"
project_a_second_run_id="73000000-0000-4000-8000-000000000002"
project_a_third_run_id="73000000-0000-4000-8000-000000000003"
project_b_head_run_id="73000000-0000-4000-8000-000000000004"
base_port="${PMCS_QA_PORT:-5090}"

if ! [[ "${base_port}" =~ ^[0-9]+$ ]] || (( base_port < 1024 || base_port > 65534 )); then
  echo "PMCS_QA_PORT must leave two valid ports between 1024 and 65535." >&2
  exit 2
fi
second_port=$((base_port + 1))

temporary_directory="$(mktemp -d)"
process_ids=()
started_pid=""
fixtures_inserted=false

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

start_worker() {
  local worker_instance_id="$1"
  local port="$2"
  local target_run_id="$3"
  local log_file="$4"

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
  ReportingCenter__WorkerEnabled=true \
  ReportingCenter__WorkerInstanceId="${worker_instance_id}" \
  ReportingCenter__QualificationPausePoint=AfterSnapshotRowLock \
  ReportingCenter__QualificationTargetRunId="${target_run_id}" \
  ReportingCenter__MaximumAttempts=3 \
  ReportingCenter__QueueAgeWarningSeconds=5 \
  ReportingCenter__PollSeconds=300 \
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
      echo "Reporting fairness API '${worker_instance_id}' exited before readiness." >&2
      exit 1
    fi
    if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
      ready=true
      break
    fi
    sleep 0.25
  done
  if [[ "${ready}" != true ]]; then
    echo "Reporting fairness API '${worker_instance_id}' did not become ready." >&2
    exit 1
  fi
}

crash_process() {
  local pid="$1"
  if ! kill -0 "${pid}" 2>/dev/null; then
    echo "The fairness qualification worker exited before the requested crash." >&2
    exit 1
  fi
  kill -KILL "${pid}"
  wait "${pid}" 2>/dev/null || true
}

cleanup_fixtures() {
  execute "delete from foundation.outbox_messages where correlation_id like 'qa-rpt1-fairness-%';"
  execute "delete from foundation.audit_events where correlation_id like 'qa-rpt1-fairness-%' or resource_id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"
  execute "delete from reporting.report_outputs where run_id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"
  execute "delete from documents.assets where project_id in ('${project_a_id}', '${project_b_id}') and owner_type = 'ReportOutput';"
  execute "update reporting.report_runs set snapshot_id = null where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"
  execute "delete from reporting.report_snapshots where run_id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"
  execute "delete from reporting.report_runs where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"
  fixtures_inserted=false
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
  if [[ "${fixtures_inserted}" == true ]]; then
    cleanup_fixtures
  fi
  if (( exit_code != 0 )); then
    for log_file in "${temporary_directory}"/*.log; do
      if [[ -f "${log_file}" ]]; then
        echo "Reporting fairness log: $(basename "${log_file}")" >&2
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

expect_equal \
  "fairness source run is a completed deterministic QA fixture" \
  "Succeeded|Complete|1" \
  "select status || '|' || pipeline_stage || '|' || output_count::text from reporting.report_runs where id = '${source_run_id}';"
source_forbidden_identities="$(scalar "select tenant_id::text || '|' || requested_by::text from reporting.report_runs where id = '${source_run_id}';")"
expect_equal \
  "fairness fixture identities are unused" \
  "0" \
  "select count(*) from reporting.report_runs where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"

execute "
  insert into reporting.report_runs(
      id, tenant_id, project_id, definition_id, definition_code,
      template_version_id, template_version, parameters_json, parameters_hash,
      requested_formats, as_of_utc, project_time_zone, requested_by,
      request_permission_snapshot, processing_permission_snapshot,
      correlation_id, idempotency_key_hash, status, pipeline_stage,
      attempt_count, created_at, claimed_at, started_at, completed_at,
      next_attempt_at, diagnostic_code, diagnostic_detail, snapshot_id,
      output_count, revision)
  select fixture.run_id,
         source.tenant_id,
         fixture.project_id,
         source.definition_id,
         source.definition_code,
         source.template_version_id,
         source.template_version,
         source.parameters_json,
         source.parameters_hash,
         source.requested_formats,
         source.as_of_utc,
         source.project_time_zone,
         source.requested_by,
         source.request_permission_snapshot,
         null,
         fixture.correlation_id,
         md5(fixture.correlation_id) || md5(fixture.correlation_id),
         'Queued',
         'Queued',
         0,
         clock_timestamp() - fixture.queue_age_seconds * interval '1 second',
         null,
         null,
         null,
         null,
         null,
         null,
         null,
         0,
         1
  from reporting.report_runs source
  cross join (values
      ('${project_a_head_run_id}'::uuid, '${project_a_id}'::uuid, 'qa-rpt1-fairness-a1', 40),
      ('${project_a_second_run_id}'::uuid, '${project_a_id}'::uuid, 'qa-rpt1-fairness-a2', 30),
      ('${project_a_third_run_id}'::uuid, '${project_a_id}'::uuid, 'qa-rpt1-fairness-a3', 20),
      ('${project_b_head_run_id}'::uuid, '${project_b_id}'::uuid, 'qa-rpt1-fairness-b1', 10)
  ) fixture(run_id, project_id, correlation_id, queue_age_seconds)
  where source.id = '${source_run_id}';"
fixtures_inserted=true

expect_equal \
  "fairness fixture has three project-A runs and one project-B run" \
  "4|3|1|4" \
  "select count(*)::text || '|' || count(*) filter (where project_id = '${project_a_id}')::text || '|' || count(*) filter (where project_id = '${project_b_id}')::text || '|' || count(*) filter (where status = 'Queued' and pipeline_stage = 'Queued' and attempt_count = 0)::text from reporting.report_runs where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"

worker_a_log="${temporary_directory}/worker-a.log"
start_worker "qa-rpt1-fairness-worker-a" "${base_port}" "${project_a_head_run_id}" "${worker_a_log}"
worker_a_pid="${started_pid}"
wait_for_log \
  "project-A head row-lock pause" \
  "${worker_a_log}" \
  "paused at AfterSnapshotRowLock for run ${project_a_head_run_id}" \
  "${worker_a_pid}"
expect_equal \
  "project-A head claim remains invisible before commit" \
  "Queued|0" \
  "select status || '|' || attempt_count::text from reporting.report_runs where id = '${project_a_head_run_id}';"
expect_equal \
  "first fairness worker holds one open claim transaction" \
  "1" \
  "select count(*) from pg_stat_activity where datname = current_database() and application_name = 'qa-rpt1-fairness-worker-a' and state = 'idle in transaction';"

worker_b_log="${temporary_directory}/worker-b.log"
start_worker "qa-rpt1-fairness-worker-b" "${second_port}" "${project_b_head_run_id}" "${worker_b_log}"
worker_b_pid="${started_pid}"
wait_for_log \
  "project-B fair row-lock pause" \
  "${worker_b_log}" \
  "paused at AfterSnapshotRowLock for run ${project_b_head_run_id}" \
  "${worker_b_pid}"
expect_equal \
  "second worker serves project B before project A's second and third runs" \
  "73000000-0000-4000-8000-000000000001:Queued:0|73000000-0000-4000-8000-000000000002:Queued:0|73000000-0000-4000-8000-000000000003:Queued:0|73000000-0000-4000-8000-000000000004:Queued:0" \
  "select string_agg(id::text || ':' || status || ':' || attempt_count::text, '|' order by id) from reporting.report_runs where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"
expect_equal \
  "both fairness workers hold independent PostgreSQL row-lock transactions" \
  "2" \
  "select count(*) from pg_stat_activity where datname = current_database() and application_name in ('qa-rpt1-fairness-worker-a', 'qa-rpt1-fairness-worker-b') and state = 'idle in transaction';"

health_payload="$(curl --silent --fail "http://127.0.0.1:${second_port}/health/ready")"
PMCS_REPORTING_HEALTH_PAYLOAD="${health_payload}" \
PMCS_REPORTING_HEALTH_FORBIDDEN="${source_forbidden_identities}|${project_a_id}|${project_b_id}|${project_a_head_run_id}|${project_a_second_run_id}|${project_a_third_run_id}|${project_b_head_run_id}" \
node <<'NODE'
const payload = JSON.parse(process.env.PMCS_REPORTING_HEALTH_PAYLOAD);
const reporting = payload.checks?.["reporting-worker"];
const data = reporting?.data;
const forbidden = (process.env.PMCS_REPORTING_HEALTH_FORBIDDEN ?? "").split("|");
if (payload.status !== "Degraded" || reporting?.status !== "Degraded") {
  throw new Error("Reporting health must be Degraded while the aged fair queue is locked.");
}
if (!String(reporting.description).includes("queue age")) {
  throw new Error("Reporting health must identify the queue-age budget without payload detail.");
}
if (data?.queuedRuns !== 4 || data?.activeRuns !== 0 ||
    !(data?.oldestQueueAgeSeconds >= 40) || !(data?.heartbeatAgeSeconds >= 0)) {
  throw new Error(`Unexpected bounded reporting health data: ${JSON.stringify(data)}`);
}
const serialized = JSON.stringify(payload);
if (forbidden.some(value => value && serialized.includes(value))) {
  throw new Error("Reporting health exposed a tenant, project, user or run identifier.");
}
for (const key of Object.keys(data ?? {})) {
  if (!["activeRuns", "heartbeatAgeSeconds", "oldestQueueAgeSeconds", "queuedRuns"].includes(key)) {
    throw new Error(`Reporting health exposed unexpected data key '${key}'.`);
  }
}
NODE

crash_process "${worker_a_pid}"
crash_process "${worker_b_pid}"
expect_equal \
  "worker crashes roll both uncommitted fairness claims back" \
  "4|4|0" \
  "select count(*)::text || '|' || count(*) filter (where status = 'Queued' and pipeline_stage = 'Queued' and attempt_count = 0)::text || '|' || count(*) filter (where claimed_at is not null or started_at is not null or snapshot_id is not null or output_count <> 0)::text from reporting.report_runs where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}');"

cleanup_fixtures
expect_equal \
  "ephemeral fairness fixtures are removed after verification" \
  "0|0|0" \
  "select (select count(*) from reporting.report_runs where id in ('${project_a_head_run_id}', '${project_a_second_run_id}', '${project_a_third_run_id}', '${project_b_head_run_id}'))::text || '|' || (select count(*) from foundation.audit_events where correlation_id like 'qa-rpt1-fairness-%')::text || '|' || (select count(*) from foundation.outbox_messages where correlation_id like 'qa-rpt1-fairness-%')::text;"

printf '{"status":"passed","stage":"reporting-worker-project-fairness-regression","assertions":10,"projects":2,"workers":2,"health":"degraded-queue-age"}\n'
