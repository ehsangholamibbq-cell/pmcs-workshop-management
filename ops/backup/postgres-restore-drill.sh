#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_RESTORE_ADMIN_CONNECTION_STRING:?Set the maintenance database connection string.}"
: "${PMCS_RESTORE_TARGET_CONNECTION_STRING:?Set a connection string that targets the new drill database.}"
: "${PMCS_RESTORE_TARGET_DATABASE:?Set a new database name beginning with pmcs_restore_drill_.}"
: "${PMCS_RESTORE_BACKUP_FILE:?Set the absolute path of a PMCS custom-format backup.}"

if [[ "${PMCS_RESTORE_BACKUP_FILE}" != /* || ! -f "${PMCS_RESTORE_BACKUP_FILE}" ]]; then
  echo "PMCS_RESTORE_BACKUP_FILE must be an existing absolute file path." >&2
  exit 2
fi

if ! printf '%s\n' "${PMCS_RESTORE_TARGET_DATABASE}" | grep -Eq '^pmcs_restore_drill_[a-z0-9_]+$'; then
  echo "The target database must match ^pmcs_restore_drill_[a-z0-9_]+$." >&2
  exit 2
fi

command -v psql >/dev/null
command -v createdb >/dev/null
command -v pg_restore >/dev/null
command -v sha256sum >/dev/null

checksum_file="${PMCS_RESTORE_BACKUP_FILE}.sha256"
if [[ ! -f "${checksum_file}" ]]; then
  echo "The adjacent checksum file is missing: ${checksum_file}" >&2
  exit 2
fi

backup_directory="$(dirname -- "${PMCS_RESTORE_BACKUP_FILE}")"
checksum_name="$(basename -- "${checksum_file}")"
(
  cd -- "${backup_directory}"
  sha256sum --check "${checksum_name}"
)
pg_restore --list "${PMCS_RESTORE_BACKUP_FILE}" >/dev/null

existing="$(psql "${PMCS_RESTORE_ADMIN_CONNECTION_STRING}" -X -v ON_ERROR_STOP=1 -tAc \
  "select 1 from pg_database where datname = '${PMCS_RESTORE_TARGET_DATABASE}';")"
if [[ -n "${existing//[[:space:]]/}" ]]; then
  echo "Target database already exists; no changes were made." >&2
  exit 3
fi

createdb --maintenance-db="${PMCS_RESTORE_ADMIN_CONNECTION_STRING}" "${PMCS_RESTORE_TARGET_DATABASE}"

actual_database="$(psql "${PMCS_RESTORE_TARGET_CONNECTION_STRING}" -X -v ON_ERROR_STOP=1 -tAc \
  'select current_database();')"
actual_database="${actual_database//[[:space:]]/}"
if [[ "${actual_database}" != "${PMCS_RESTORE_TARGET_DATABASE}" ]]; then
  echo "Target connection string does not point to the newly created drill database." >&2
  exit 4
fi

pg_restore \
  --dbname="${PMCS_RESTORE_TARGET_CONNECTION_STRING}" \
  --no-owner \
  --no-privileges \
  --exit-on-error \
  "${PMCS_RESTORE_BACKUP_FILE}"

migration_count="$(psql "${PMCS_RESTORE_TARGET_CONNECTION_STRING}" -X -v ON_ERROR_STOP=1 -tAc \
  'select count(*) from foundation.schema_migrations;')"
migration_count="${migration_count//[[:space:]]/}"
if [[ ! "${migration_count}" =~ ^[1-9][0-9]*$ ]]; then
  echo "Restore validation failed: no applied PMCS migrations were found." >&2
  exit 5
fi

required_relations="$(psql "${PMCS_RESTORE_TARGET_CONNECTION_STRING}" -X -v ON_ERROR_STOP=1 -tAc \
  "select (to_regclass('foundation.audit_events') is not null and to_regclass('projects.projects') is not null)::int;")"
required_relations="${required_relations//[[:space:]]/}"
if [[ "${required_relations}" != "1" ]]; then
  echo "Restore validation failed: required PMCS relations are missing." >&2
  exit 6
fi

printf 'Restore drill passed for %s with %s migrations. The drill database was retained for inspection.\n' \
  "${PMCS_RESTORE_TARGET_DATABASE}" "${migration_count}"
