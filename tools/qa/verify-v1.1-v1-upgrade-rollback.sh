#!/usr/bin/env bash
set -euo pipefail

: "${RUNNER_TEMP:?This rehearsal requires an isolated CI runner.}"
: "${PMCS_V1_UPGRADE_CONNECTION_STRING:?Set isolated V1 upgrade ADO connection.}"
: "${PMCS_V1_UPGRADE_DATABASE_URL:?Set isolated V1 upgrade PostgreSQL URL.}"
: "${PMCS_QA_AUTH_KEY:?Set isolated QA gateway key.}"
: "${PMCS_RESTORE_ADMIN_CONNECTION_STRING:?Set restore maintenance connection.}"
: "${PMCS_TEST_S3_ENDPOINT:?Set isolated object storage.}"

baseline_commit="26bf222d44634562ca7f3fc0931f3f8b79ca04a1"
database_name="pmcs_qa_v1_upgrade"
restore_name="pmcs_restore_drill_v1_upgrade"
root="$(pwd)"
tmp="$(mktemp -d "${RUNNER_TEMP}/pmcs-v11-upgrade.XXXXXX")"
baseline_root="${tmp}/v1-baseline"
api_pid=""
api_log="${tmp}/api.log"

stop_api() {
  if [[ -n "${api_pid}" ]] && kill -0 "${api_pid}" 2>/dev/null; then
    kill "${api_pid}"
    wait "${api_pid}" 2>/dev/null || true
  fi
  api_pid=""
}
cleanup() {
  code=$?
  set +e
  if (( code != 0 )); then
    tail -100 "${api_log}" >&2 || true
  fi
  stop_api
  if [[ -d "${baseline_root}" ]]; then git worktree remove --force "${baseline_root}"; fi
  rm -rf -- "${tmp}"
  exit "${code}"
}
trap cleanup EXIT

if [[ "${PMCS_V1_UPGRADE_CONNECTION_STRING}" != *"Database=${database_name};"* &&
      "${PMCS_V1_UPGRADE_CONNECTION_STRING}" != *"Database=${database_name}" ]]; then
  echo 'V1 upgrade connection must target the dedicated pmcs_qa_v1_upgrade database.' >&2
  exit 2
fi
connected_name="$(psql "${PMCS_V1_UPGRADE_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select current_database();' 2>/dev/null || true)"
if [[ -n "${connected_name}" ]]; then
  echo 'V1 upgrade database already exists; refusing to modify it.' >&2
  exit 2
fi
git cat-file -e "${baseline_commit}^{commit}"
git worktree add --detach "${baseline_root}" "${baseline_commit}" >/dev/null
createdb "${database_name}"

(
  cd "${baseline_root}"
  dotnet restore PMCS.slnx
  dotnet build PMCS.slnx --configuration Release --no-restore
)

launch_api() {
  local source="$1" seed="$2" port="$3"
  local connection="${4:-${PMCS_V1_UPGRADE_CONNECTION_STRING}}"
  local qa_gateway="${5:-true}"
  : >"${api_log}"
  (
    cd "${source}"
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="http://127.0.0.1:${port}" \
    ConnectionStrings__Pmcs="${connection}" \
    PMCS_QA_CONNECTION_STRING="${connection}" \
    PMCS_QA_DATABASE_URL="${PMCS_V1_UPGRADE_DATABASE_URL}" \
    PMCS_DEV_IDENTITY_ENABLED=false \
    PMCS_SEED_ENABLED="${seed}" \
    PMCS_QA_GATEWAY_ENABLED="${qa_gateway}" \
    PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
    ProjectStateRefresh__Enabled=false \
    AdvisoryIntelligence__WorkerEnabled=false \
    ReportingCenter__WorkerEnabled=false \
    Intelligence__INT1ReferenceEnabled=false \
    Intelligence__INT1FixtureEnabled=false \
    ObjectStorage__ServiceUrl="${PMCS_TEST_S3_ENDPOINT}" \
    ObjectStorage__AccessKey="${PMCS_TEST_S3_ACCESS_KEY}" \
    ObjectStorage__SecretKey="${PMCS_TEST_S3_SECRET_KEY}" \
    ObjectStorage__BucketName="${PMCS_TEST_S3_BUCKET}" \
    ObjectStorage__ForcePathStyle=true \
    dotnet run --project src/backend/Pmcs.Api/Pmcs.Api.csproj --configuration Release \
      --no-build --no-launch-profile
  ) >"${api_log}" 2>&1 &
  api_pid=$!
}

start_api() {
  local source="$1" seed="$2" port="$3"
  launch_api "${source}" "${seed}" "${port}" "${4:-${PMCS_V1_UPGRADE_CONNECTION_STRING}}" "${5:-true}"
  for _ in {1..90}; do
    if ! kill -0 "${api_pid}" 2>/dev/null; then
      echo 'V1 upgrade API exited before readiness.' >&2
      return 1
    fi
    if curl --fail --silent "http://127.0.0.1:${port}/health/ready" >/dev/null; then
      return 0
    fi
    sleep 1
  done
  echo 'V1 upgrade API did not become ready.' >&2
  return 1
}

scalar() {
  psql "${PMCS_V1_UPGRADE_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

start_api "${baseline_root}" true 5097
v1_count="$(scalar 'select count(*) from foundation.schema_migrations;')"
v1_data="$(scalar "select (select count(*) from projects.projects)::text || '|' || (select count(*) from identity_access.users)::text;")"
if ! [[ "${v1_count}" =~ ^[1-9][0-9]*$ ]] || (( v1_count >= 70 )) ||
   [[ "${v1_data}" == '0|'* || "${v1_data}" == *'|0' ]]; then
  echo "Representative V1 seed/ledger invalid: ${v1_count}, ${v1_data}." >&2
  exit 1
fi
stop_api

mkdir -p "${tmp}/backup"
docker run --rm --network host --volume "${root}:/workspace" \
  --volume "${tmp}/backup:/backup" --workdir /workspace \
  --env "PMCS_BACKUP_CONNECTION_STRING=${PMCS_V1_UPGRADE_DATABASE_URL}" \
  --env PMCS_BACKUP_DIRECTORY=/backup postgres:17-alpine sh -c \
  'apk add --no-cache bash coreutils >/dev/null && bash ops/backup/postgres-backup.sh'
backup_file="$(find "${tmp}/backup" -maxdepth 1 -name '*.dump' -type f -print -quit)"
if [[ -z "${backup_file}" ]]; then echo 'V1 baseline backup missing.' >&2; exit 1; fi
backup_sha256="$(docker run --rm --volume "${tmp}/backup:/backup:ro" \
  postgres:17-alpine sha256sum "/backup/$(basename "${backup_file}")" | cut -d' ' -f1)"

docker run --rm --network host --volume "${root}:/workspace" \
  --volume "${tmp}/backup:/backup" --workdir /workspace \
  --env PMCS_RESTORE_ADMIN_CONNECTION_STRING \
  --env "PMCS_RESTORE_TARGET_CONNECTION_STRING=${PMCS_V1_RESTORE_DATABASE_URL:?Set V1 restore target.}" \
  --env "PMCS_RESTORE_TARGET_DATABASE=${restore_name}" \
  --env "PMCS_RESTORE_BACKUP_FILE=/backup/$(basename "${backup_file}")" \
  postgres:17-alpine sh -c \
  'apk add --no-cache bash coreutils >/dev/null && bash ops/backup/postgres-restore-drill.sh'
restored_count="$(psql "${PMCS_V1_RESTORE_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select count(*) from foundation.schema_migrations;')"
if [[ "${restored_count}" != "${v1_count}" ]]; then
  echo 'V1 backup did not restore its exact migration count.' >&2
  exit 1
fi

start_api "${root}" false 5098
evidence_dir="${root}/artifacts/qa/v1.1-evidence"
mkdir -p "${evidence_dir}"
candidate_count="$(scalar 'select count(*) from foundation.schema_migrations;')"
candidate_data="$(scalar "select (select count(*) from projects.projects)::text || '|' || (select count(*) from identity_access.users)::text;")"
if [[ "${candidate_count}" != 70 || "${candidate_data}" != "${v1_data}" ]]; then
  echo "Candidate upgrade lost V1 data or migrations: ${candidate_count}, ${candidate_data}." >&2
  exit 1
fi
PMCS_QA_CONNECTION_STRING="${PMCS_V1_UPGRADE_CONNECTION_STRING}" \
PMCS_QA_BASE_URL=http://127.0.0.1:5098 \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- probe \
  >"${evidence_dir}/migration-upgrade-probe.artifact.json"
PMCS_QA_CONNECTION_STRING="${PMCS_V1_UPGRADE_CONNECTION_STRING}" \
PMCS_QA_BASE_URL=http://127.0.0.1:5098 \
dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- verify \
  >"${evidence_dir}/migration-upgrade-permissions.artifact.json"
stop_api

start_api "${baseline_root}" false 5099
rollback_count="$(scalar 'select count(*) from foundation.schema_migrations;')"
rollback_data="$(scalar "select (select count(*) from projects.projects)::text || '|' || (select count(*) from identity_access.users)::text;")"
if [[ "${rollback_count}" != 70 || "${rollback_data}" != "${v1_data}" ]]; then
  echo 'V1 runtime rollback changed the expanded Candidate schema or V1 data.' >&2
  exit 1
fi
(
  cd "${baseline_root}"
  PMCS_QA_CONNECTION_STRING="${PMCS_V1_UPGRADE_CONNECTION_STRING}" \
  PMCS_QA_BASE_URL=http://127.0.0.1:5099 \
  dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
    --configuration Release --no-build --no-launch-profile -- probe \
    >"${evidence_dir}/migration-rollback-probe.artifact.json"
)
stop_api

interrupt_name="pmcs_restore_drill_v1_interrupted"
interrupt_url="${PMCS_V1_INTERRUPT_DATABASE_URL:?Set interrupted restore target.}"
interrupt_connection="${PMCS_V1_INTERRUPT_CONNECTION_STRING:?Set interrupted restore ADO connection.}"
docker run --rm --network host --volume "${root}:/workspace" \
  --volume "${tmp}/backup:/backup" --workdir /workspace \
  --env PMCS_RESTORE_ADMIN_CONNECTION_STRING \
  --env "PMCS_RESTORE_TARGET_CONNECTION_STRING=${interrupt_url}" \
  --env "PMCS_RESTORE_TARGET_DATABASE=${interrupt_name}" \
  --env "PMCS_RESTORE_BACKUP_FILE=/backup/$(basename "${backup_file}")" \
  postgres:17-alpine sh -c \
  'apk add --no-cache bash coreutils >/dev/null && bash ops/backup/postgres-restore-drill.sh'
before_interruption="$(psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select count(*) from foundation.schema_migrations;')"
if [[ "${before_interruption}" != "${v1_count}" ]]; then
  echo 'Interrupted restore did not start from the locked V1 ledger.' >&2
  exit 1
fi
psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null <<'SQL'
create function foundation.qa_pause_migration_record() returns trigger language plpgsql as $$
begin
  perform pg_sleep(90);
  return new;
end;
$$;
create trigger qa_pause_migration_record before insert on foundation.schema_migrations
for each row execute function foundation.qa_pause_migration_record();
SQL
launch_api "${root}" false 5100 "${interrupt_connection}" false
observed=false
for _ in {1..60}; do
  sleeping="$(psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "select count(*) from pg_stat_activity where datname = '${interrupt_name}' and wait_event = 'PgSleep' and pid <> pg_backend_pid();")"
  if [[ "${sleeping}" != 0 ]]; then observed=true; break; fi
  if ! kill -0 "${api_pid}" 2>/dev/null; then break; fi
  sleep 1
done
if [[ "${observed}" != true ]]; then
  echo 'Candidate did not reach the injected mid-transaction migration pause.' >&2
  exit 1
fi
terminated="$(psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select bool_and(pg_terminate_backend(pid)) from pg_stat_activity where datname = '${interrupt_name}' and wait_event = 'PgSleep' and pid <> pg_backend_pid();")"
if [[ "${terminated}" != t ]]; then
  echo 'The mid-migration database connection was not terminated.' >&2
  exit 1
fi
stop_api
after_interruption="$(psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select count(*) from foundation.schema_migrations;')"
if [[ "${after_interruption}" != "${v1_count}" ]]; then
  echo 'A partial migration remained committed after connection loss.' >&2
  exit 1
fi
psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null <<'SQL'
drop trigger qa_pause_migration_record on foundation.schema_migrations;
drop function foundation.qa_pause_migration_record();
SQL
start_api "${root}" false 5101 "${interrupt_connection}" false
after_recovery="$(psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select count(*) from foundation.schema_migrations;')"
recovered_data="$(psql "${interrupt_url}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select (select count(*) from projects.projects)::text || '|' || (select count(*) from identity_access.users)::text;")"
if [[ "${after_recovery}" != 70 || "${recovered_data}" != "${v1_data}" ]]; then
  echo 'Candidate did not recover exactly once after the injected interruption.' >&2
  exit 1
fi
stop_api

PMCS_INTERRUPT_BEFORE="${before_interruption}" \
PMCS_INTERRUPT_AFTER_FAILURE="${after_interruption}" \
PMCS_INTERRUPT_RECOVERED="${after_recovery}" \
PMCS_INTERRUPT_DATA_COUNTS="${recovered_data}" \
PMCS_INTERRUPT_V1_DATA_COUNTS="${v1_data}" \
  node tools/qa/v1.1-interruption-evidence.mjs

PMCS_V1_LEDGER_COUNT="${v1_count}" PMCS_V1_RESTORED_COUNT="${restored_count}" \
PMCS_V11_UPGRADE_COUNT="${candidate_count}" PMCS_V11_ROLLBACK_COUNT="${rollback_count}" \
PMCS_V1_DATA_COUNTS="${v1_data}" PMCS_V11_DATA_COUNTS="${candidate_data}" \
PMCS_V11_ROLLBACK_DATA_COUNTS="${rollback_data}" PMCS_V1_BACKUP_SHA256="${backup_sha256}" \
  node tools/qa/v1.1-upgrade-evidence.mjs
