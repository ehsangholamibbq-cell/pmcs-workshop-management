#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set isolated QA database.}"
: "${PMCS_QA_DATABASE_URL:?Set isolated QA PostgreSQL URL.}"

if [[ -n "${OPENAI_API_KEY:-}" || -n "${GEMINI_API_KEY:-}" || -n "${ANTHROPIC_API_KEY:-}" ]]; then
  echo 'INT1 dormant verification requires an environment without provider credentials.' >&2
  exit 1
fi

database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- guard)"
connected_name="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select current_database();')"
if [[ "${database_name}" != "${connected_name}" ]]; then
  echo 'The INT1 dormant probe targets mismatched QA databases.' >&2
  exit 2
fi

db_state="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select (select count(*) from intelligence.administration_grants)::text || '|' || (select count(*) from intelligence.profile_selections)::text || '|' || (select count(*) from intelligence.reference_runs)::text || '|' || (select count(*) from intelligence.provider_registrations where enabled)::text || '|' || (select count(*) from intelligence.model_catalog where enabled)::text;")"
if [[ "${db_state}" != '0|0|0|0|0' ]]; then
  echo "INT1 was not dormant in the fresh QA database: ${db_state}." >&2
  exit 1
fi
probe="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- probe-int1-providers)"
PMCS_INT1_DB_STATE="${db_state}" PMCS_INT1_PROBE_JSON="${probe}" \
  node tools/qa/v1.1-int1-dormant-evidence.mjs
