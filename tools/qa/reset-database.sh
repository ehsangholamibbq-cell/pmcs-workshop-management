#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING to the isolated QA database.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL to the same isolated QA database as a PostgreSQL URI.}"
: "${PMCS_QA_RESET_CONFIRM:?Set PMCS_QA_RESET_CONFIRM to RESET:<database-name>.}"

command -v dotnet >/dev/null
command -v psql >/dev/null

dotnet build src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --nologo --verbosity minimal >&2
database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
connected_database="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --no-align --command 'select current_database();')"

if [[ "${connected_database}" != "${database_name}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

if [[ "${PMCS_QA_RESET_CONFIRM}" != "RESET:${database_name}" ]]; then
  echo "Reset confirmation must exactly equal RESET:${database_name}." >&2
  exit 2
fi

schemas=(
  action_control
  commercial
  evidence
  field_operations
  finance
  foundation
  identity_access
  intelligence
  planning
  project_intelligence
  projects
  quality_safety
  sync_control
  technical_office
  work_management
)

for schema in "${schemas[@]}"; do
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 --command "drop schema if exists \"${schema}\" cascade;"
done

printf 'Isolated QA database %s was reset; migration and seed must now be rerun.\n' "${database_name}"
