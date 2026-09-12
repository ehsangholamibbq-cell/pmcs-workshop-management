#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_BACKUP_CONNECTION_STRING:?Set PMCS_BACKUP_CONNECTION_STRING to the PostgreSQL connection string.}"
: "${PMCS_BACKUP_DIRECTORY:?Set PMCS_BACKUP_DIRECTORY to an absolute backup directory.}"

if [[ "${PMCS_BACKUP_DIRECTORY}" != /* || "${PMCS_BACKUP_DIRECTORY}" == "/" ]]; then
  echo "PMCS_BACKUP_DIRECTORY must be an absolute, non-root directory." >&2
  exit 2
fi

command -v pg_dump >/dev/null
command -v pg_restore >/dev/null
command -v sha256sum >/dev/null

umask 077
mkdir -p -- "${PMCS_BACKUP_DIRECTORY}"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_name="pmcs-${timestamp}-$$.dump"
backup_path="${PMCS_BACKUP_DIRECTORY}/${backup_name}"
temporary_path="$(mktemp "${PMCS_BACKUP_DIRECTORY}/.pmcs-backup-XXXXXX")"

cleanup() {
  if [[ -n "${temporary_path:-}" && -f "${temporary_path}" ]]; then
    rm -f -- "${temporary_path}"
  fi
}
trap cleanup EXIT

pg_dump \
  --dbname="${PMCS_BACKUP_CONNECTION_STRING}" \
  --format=custom \
  --compress=9 \
  --no-owner \
  --no-privileges \
  --file="${temporary_path}"

pg_restore --list "${temporary_path}" >/dev/null
mv -- "${temporary_path}" "${backup_path}"
temporary_path=""

(
  cd -- "${PMCS_BACKUP_DIRECTORY}"
  sha256sum -- "${backup_name}" > "${backup_name}.sha256"
)
chmod 0600 -- "${backup_path}" "${backup_path}.sha256"

printf '%s\n' "${backup_path}"
