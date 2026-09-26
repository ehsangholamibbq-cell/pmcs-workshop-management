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
technical_office_id="50000000-0000-4000-8000-000000000002"
technical_office_membership_id="60000000-0000-4000-8000-000000000002"
revocation_run_id="71000000-0000-4000-8000-000000000009"
port="${PMCS_QA_PORT:-5090}"

if ! [[ "${port}" =~ ^[0-9]+$ ]] || (( port < 1024 || port > 65535 )); then
  echo "PMCS_QA_PORT must be an integer between 1024 and 65535." >&2
  exit 2
fi

temporary_directory="$(mktemp -d)"
process_ids=()
started_pid=""
membership_changed=false
membership_status=""
membership_revision=""

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
  local worker_enabled="$2"
  local pause_point="$3"
  local target_run_id="$4"
  local pause_seconds="$5"
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
  ReportingCenter__QualificationPauseSeconds="${pause_seconds}" \
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
      echo "Reporting revocation API '${worker_instance_id}' exited before readiness." >&2
      exit 1
    fi
    if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
      ready=true
      break
    fi
    sleep 0.25
  done
  if [[ "${ready}" != true ]]; then
    echo "Reporting revocation API '${worker_instance_id}' did not become ready." >&2
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
  if [[ "${membership_changed}" == true ]]; then
    execute "update identity_access.project_memberships set status = '${membership_status}', revision = ${membership_revision} where id = '${technical_office_membership_id}';"
  fi
  for pid in "${process_ids[@]}"; do
    if kill -0 "${pid}" 2>/dev/null; then
      kill "${pid}" 2>/dev/null
      wait "${pid}" 2>/dev/null || true
    fi
  done
  if (( exit_code != 0 )); then
    for log_file in "${temporary_directory}"/*.log; do
      if [[ -f "${log_file}" ]]; then
        echo "Reporting revocation log: $(basename "${log_file}")" >&2
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

membership_status="$(scalar "select status from identity_access.project_memberships where id = '${technical_office_membership_id}' and tenant_id = '${tenant_id}' and project_id = '${project_id}' and user_id = '${technical_office_id}';")"
membership_revision="$(scalar "select revision from identity_access.project_memberships where id = '${technical_office_membership_id}';")"
if [[ "${membership_status}" != "Active" || ! "${membership_revision}" =~ ^[0-9]+$ ]]; then
  echo "Technical Office membership is not an active deterministic QA fixture." >&2
  exit 1
fi

prepare_log="${temporary_directory}/prepare.log"
start_api "qa-rpt1-revocation-prepare" false None "" "" "${prepare_log}"
prepare_pid="${started_pid}"
PMCS_QA_BASE_URL="http://127.0.0.1:${port}" \
PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- prepare-reporting-worker-revocation
stop_process "${prepare_pid}"

worker_log="${temporary_directory}/worker.log"
start_api \
  "qa-rpt1-worker-revocation" \
  true \
  BeforeStoragePermissionRecheck \
  "${revocation_run_id}" \
  10 \
  "${worker_log}"
worker_pid="${started_pid}"
wait_for_log \
  "worker-time permission recheck pause" \
  "${worker_log}" \
  "paused at BeforeStoragePermissionRecheck for run ${revocation_run_id}" \
  "${worker_pid}"
expect_equal \
  "revocation fixture reached rendering without publishing bytes" \
  "Processing|Rendering|1|1|0" \
  "select run.status || '|' || run.pipeline_stage || '|' || run.attempt_count::text || '|' || (select count(*) from reporting.report_snapshots where run_id = run.id)::text || '|' || (select count(*) from reporting.report_outputs where run_id = run.id)::text from reporting.report_runs run where run.id = '${revocation_run_id}';"

execute "update identity_access.project_memberships set status = 'Suspended', revision = revision + 1 where id = '${technical_office_membership_id}';"
membership_changed=true
wait_equal \
  "revocation during rendering fails before storage" \
  "Failed|Failed|1|0|reporting.permission.revoked" \
  "select status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || coalesce(diagnostic_code, '<none>') from reporting.report_runs where id = '${revocation_run_id}';"

execute "update identity_access.project_memberships set status = '${membership_status}', revision = ${membership_revision} where id = '${technical_office_membership_id}';"
membership_changed=false

PMCS_QA_BASE_URL="http://127.0.0.1:${port}" \
PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- verify-reporting-worker-revocation

expect_equal \
  "worker-time revocation published no document or output" \
  "0|0|0" \
  "select (select count(*) from reporting.report_outputs where run_id = '${revocation_run_id}')::text || '|' || (select count(*) from foundation.audit_events event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${revocation_run_id}' and event.event_type = 'GeneratedReportDocumentReleased')::text || '|' || (select count(*) from foundation.outbox_messages event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${revocation_run_id}' and event.event_type = 'documents.asset.released.v1')::text;"
expect_equal \
  "worker-time revocation records both denied permissions" \
  "2" \
  "select count(*) from reporting.report_runs run cross join lateral jsonb_array_elements(run.processing_permission_snapshot->'decisions') decision where run.id = '${revocation_run_id}' and decision->>'operation' in ('reporting.run.create', 'field.daily-reports.read') and not (decision->>'allowed')::boolean;"
expect_equal \
  "worker-time revocation audit has explicit worker lineage" \
  "1|qa-rpt1-worker-revocation|reporting.permission.revoked" \
  "select count(*)::text || '|' || min(data->>'workerInstanceId') || '|' || min(data->>'diagnosticCode') from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'ReportRun' and resource_id = '${revocation_run_id}' and event_type = 'CertifiedReportRunFailed';"
expect_equal \
  "worker-time revocation create receipt is singular" \
  "1" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key = 'qa-rpt1-worker-revocation';"

stop_process "${worker_pid}"
printf '{"status":"passed","stage":"reporting-worker-revocation-regression","assertions":8}\n'
