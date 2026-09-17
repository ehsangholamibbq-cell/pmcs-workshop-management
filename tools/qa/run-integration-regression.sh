#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_TEST_CONNECTION_STRING:?Set PMCS_TEST_CONNECTION_STRING.}"
: "${PMCS_VERIFICATION_DATABASE_URL:?Set PMCS_VERIFICATION_DATABASE_URL.}"
: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL.}"
: "${PMCS_QA_DATABASE_NAME:?Set PMCS_QA_DATABASE_NAME.}"
: "${PMCS_QA_RESET_CONFIRM:?Set PMCS_QA_RESET_CONFIRM.}"
: "${PMCS_QA_AUTH_KEY:?Set PMCS_QA_AUTH_KEY.}"
: "${PMCS_BACKUP_CONNECTION_STRING:?Set PMCS_BACKUP_CONNECTION_STRING.}"
: "${PMCS_RESTORE_ADMIN_CONNECTION_STRING:?Set PMCS_RESTORE_ADMIN_CONNECTION_STRING.}"
: "${PMCS_RESTORE_TARGET_CONNECTION_STRING:?Set PMCS_RESTORE_TARGET_CONNECTION_STRING.}"
: "${PMCS_RESTORE_TARGET_DATABASE:?Set PMCS_RESTORE_TARGET_DATABASE.}"

if [[ ! "${PMCS_QA_DATABASE_NAME}" =~ ^pmcs_qa_[a-z0-9_]+$ ]]; then
  echo "PMCS_QA_DATABASE_NAME must be an isolated pmcs_qa_* database." >&2
  exit 2
fi

command -v curl >/dev/null
command -v createdb >/dev/null
command -v docker >/dev/null
command -v dotnet >/dev/null
command -v psql >/dev/null

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${repository_root}"

curl --fail --silent "${PMCS_TEST_S3_ENDPOINT:?Set PMCS_TEST_S3_ENDPOINT.}/minio/health/live" >/dev/null

dotnet restore PMCS.slnx
dotnet build PMCS.slnx --configuration Release --no-restore
./tools/integration-smoke.sh

for verifier in \
  tools/checkpoint22-db-verification.sh \
  tools/checkpoint23-db-verification.sh \
  tools/checkpoint24-db-verification.sh; do
  docker run --rm --network host \
    --volume "${repository_root}:/workspace" \
    --workdir /workspace \
    --env PMCS_VERIFICATION_DATABASE_URL \
    postgres:17-alpine sh -c \
    "apk add --no-cache bash >/dev/null && bash ${verifier}"
done

if ! psql --no-psqlrc --set ON_ERROR_STOP=1 --dbname postgres --tuples-only --no-align \
  --command "select 1 from pg_database where datname = '${PMCS_QA_DATABASE_NAME}';" | grep -qx 1; then
  createdb "${PMCS_QA_DATABASE_NAME}"
fi
./tools/qa/reset-database.sh
./tools/qa/seed-diagnostics.sh

temporary_backup_root="${RUNNER_TEMP:-}"
remove_temporary_backup_root=false
if [[ -z "${temporary_backup_root}" ]]; then
  temporary_backup_root="$(mktemp -d)"
  remove_temporary_backup_root=true
fi
backup_directory="${temporary_backup_root}/pmcs-backup"
mkdir -p "${backup_directory}"
cleanup() {
  if [[ "${remove_temporary_backup_root}" == true ]]; then
    rm -rf -- "${temporary_backup_root}"
  fi
}
trap cleanup EXIT

docker run --rm --network host \
  --volume "${repository_root}:/workspace" \
  --volume "${backup_directory}:/backup" \
  --workdir /workspace \
  --env PMCS_BACKUP_CONNECTION_STRING \
  --env PMCS_BACKUP_DIRECTORY=/backup \
  postgres:17-alpine sh -c \
  'apk add --no-cache bash coreutils >/dev/null && bash ops/backup/postgres-backup.sh'

backup_file="$(find "${backup_directory}" -maxdepth 1 -name '*.dump' -type f -print -quit)"
if [[ -z "${backup_file}" ]]; then
  echo "The integration regression did not produce a PostgreSQL backup." >&2
  exit 1
fi

docker run --rm --network host \
  --volume "${repository_root}:/workspace" \
  --volume "${backup_directory}:/backup" \
  --workdir /workspace \
  --env PMCS_RESTORE_ADMIN_CONNECTION_STRING \
  --env PMCS_RESTORE_TARGET_CONNECTION_STRING \
  --env PMCS_RESTORE_TARGET_DATABASE \
  --env "PMCS_RESTORE_BACKUP_FILE=/backup/$(basename "${backup_file}")" \
  postgres:17-alpine sh -c \
  'apk add --no-cache bash coreutils >/dev/null && bash ops/backup/postgres-restore-drill.sh'

echo '{"status":"passed","stage":"connected-integration-regression"}'
