#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING to the isolated QA database.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL to the same isolated QA database as a PostgreSQL URI.}"

command -v dotnet >/dev/null
command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
root_location_id="33333333-3333-4333-8333-333333333334"
administrator_id="22222222-2222-2222-2222-222222222222"
site_supervisor_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
technical_office_id="50000000-0000-4000-8000-000000000002"
workflow_report_id="70000000-0000-4000-8000-000000000001"
workflow_fact_id="70000000-0000-4000-8000-000000000002"

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

expect_at_least() {
  local label="$1"
  local minimum="$2"
  local query="$3"
  local actual
  actual="$(scalar "${query}")"
  if ! [[ "${actual}" =~ ^[0-9]+$ ]] || (( actual < minimum )); then
    echo "${label}: expected at least '${minimum}', received '${actual}'." >&2
    exit 1
  fi
}

database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
connected_database="$(scalar 'select current_database();')"
if [[ "${connected_database}" != "${database_name}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

expect_equal \
  "canonical migration ledger size" \
  "41" \
  "select count(*) from foundation.schema_migrations;"

expect_equal \
  "controlled project bootstrap migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'projects' and version = '20260918-007';"

expect_equal \
  "controlled project bootstrap table is available" \
  "1" \
  "select count(*) from information_schema.tables where table_schema = 'projects' and table_name = 'project_bootstrap_plans';"

expect_equal \
  "identity experience migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'identity-access' and version = '20260918-002';"

expect_equal \
  "shared documents migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'documents' and version = '20260918-001';"

expect_equal \
  "canonical field-operations measurement migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'field-operations' and version = '20260911-004';"

expect_equal \
  "legacy field_operations migration identity removed" \
  "0" \
  "select count(*) from foundation.schema_migrations where module = 'field_operations';"

expect_equal \
  "active deterministic QA actors" \
  "12" \
  "select count(*) from identity_access.users where tenant_id = '${tenant_id}' and status = 'Active';"

expect_equal \
  "one tenant-consistent member profile per QA actor" \
  "12|0" \
  "select count(*)::text || '|' || count(*) filter (where profile.tenant_id <> actor.tenant_id)::text from identity_access.users actor join identity_access.member_profiles profile on profile.user_id = actor.id where actor.tenant_id = '${tenant_id}';"

expect_equal \
  "active deterministic project memberships and role coverage" \
  "12|12" \
  "select count(*)::text || '|' || count(distinct role_code)::text from identity_access.project_memberships where tenant_id = '${tenant_id}' and project_id = '${project_id}' and status = 'Active';"

expect_equal \
  "denied observer mutation left no report" \
  "0" \
  "select count(*) from field_operations.daily_reports where id = '70000000-0000-4000-8000-000000000099';"

expect_equal \
  "role-separated daily report workflow" \
  "Approved|4|${site_supervisor_id}|${technical_office_id}" \
  "select status || '|' || revision::text || '|' || created_by::text || '|' || reviewed_by::text from field_operations.daily_reports where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${workflow_report_id}';"

expect_equal \
  "workflow fact and project location lineage" \
  "Note|${root_location_id}|${site_supervisor_id}" \
  "select kind || '|' || location_id::text || '|' || created_by::text from field_operations.daily_report_facts where id = '${workflow_fact_id}' and daily_report_id = '${workflow_report_id}';"

expect_at_least \
  "permission matrix audit coverage" \
  "24" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and actor_user_id = '${administrator_id}' and event_type = 'QaGateway.PermissionPreviewRead' and resource_id = '${project_id}' and correlation_id is not null and btrim(correlation_id) <> '';"

expect_equal \
  "permission matrix covers every seeded actor" \
  "12" \
  "select count(distinct data->>'targetUserId') from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'QaGateway.PermissionPreviewRead';"

expect_equal \
  "permission audit project and correlation completeness" \
  "0" \
  "select count(*) from foundation.audit_events where event_type = 'QaGateway.PermissionPreviewRead' and (project_id is null or project_id <> '${project_id}' or correlation_id is null or btrim(correlation_id) = '');"

expect_equal \
  "workflow audit event coverage" \
  "4" \
  "select count(distinct event_type) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'DailyReport' and resource_id = '${workflow_report_id}' and event_type in ('DailyReportCreated', 'DailyReportFactAdded', 'DailyReportSubmitted', 'DailyReportApproved') and correlation_id is not null and btrim(correlation_id) <> '';"

expect_equal \
  "workflow audit actor lineage" \
  "3|1" \
  "select count(*) filter (where actor_user_id = '${site_supervisor_id}')::text || '|' || count(*) filter (where actor_user_id = '${technical_office_id}')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'DailyReport' and resource_id = '${workflow_report_id}' and event_type in ('DailyReportCreated', 'DailyReportFactAdded', 'DailyReportSubmitted', 'DailyReportApproved');"

expect_equal \
  "workflow transactional outbox coverage" \
  "4" \
  "select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type in ('FieldOperations.DailyReportCreated', 'FieldOperations.DailyReportFactAdded', 'FieldOperations.DailyReportSubmitted', 'FieldOperations.DailyReportApproved') and payload->>'id' = '${workflow_report_id}' and correlation_id is not null and btrim(correlation_id) <> '';"

expect_equal \
  "workflow idempotency receipt coverage" \
  "4" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('qa-v2-daily-report-create', 'qa-v2-daily-report-fact', 'qa-v2-daily-report-submit', 'qa-v2-daily-report-approve') and request_hash is not null and btrim(request_hash) <> '';"

expect_at_least \
  "submitted workflow notification evidence" \
  "1" \
  "select count(*) from work_management.notifications where tenant_id = '${tenant_id}' and project_id = '${project_id}' and category = 'DailyReportReview' and target_id = '${workflow_report_id}';"

printf 'QA permission, workflow, database and audit verification passed for %s.\n' "${database_name}"
