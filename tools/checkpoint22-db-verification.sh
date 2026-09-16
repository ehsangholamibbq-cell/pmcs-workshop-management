#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_VERIFICATION_DATABASE_URL:?Set PMCS_VERIFICATION_DATABASE_URL to the isolated Checkpoint 22 test database.}"

command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
original_report_id="44444444-4444-4444-4444-444444444444"
original_fact_id="55555555-5555-5555-5555-555555555555"
correction_id="99999999-9999-4999-8999-999999999999"

scalar() {
  psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
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

expect_at_least_one() {
  local label="$1"
  local query="$2"
  local count
  count="$(scalar "${query}")"
  if ! [[ "${count}" =~ ^[0-9]+$ ]] || (( count < 1 )); then
    echo "${label}: expected at least one row, received '${count}'." >&2
    exit 1
  fi
}

expect_equal \
  "original report supersession state" \
  "Superseded|${correction_id}" \
  "select status || '|' || superseded_by_report_id::text from field_operations.daily_reports where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${original_report_id}';"

expect_equal \
  "approved correction lineage" \
  "Approved|2|${original_report_id}" \
  "select status || '|' || version_number::text || '|' || supersedes_report_id::text from field_operations.daily_reports where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${correction_id}';"

expect_at_least_one \
  "copied fact lineage" \
  "select count(*) from field_operations.daily_report_facts where daily_report_id = '${correction_id}' and copied_from_fact_id = '${original_fact_id}';"

expect_at_least_one \
  "acknowledged recipient notification" \
  "select count(*) from work_management.notifications where tenant_id = '${tenant_id}' and project_id = '${project_id}' and category = 'DailyReportReview' and target_id = '${original_report_id}' and read_at is not null and acknowledged_at is not null;"

expect_at_least_one \
  "correction audit lineage and correlation" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'DailyReportCorrectionStarted' and resource_id = '${correction_id}' and data->>'supersedesReportId' = '${original_report_id}' and (data->>'versionNumber')::integer = 2 and correlation_id is not null and correlation_id <> '';"

expect_at_least_one \
  "notification acknowledgement audit" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'NotificationAcknowledged' and data->>'recipientUserId' is not null and data->>'acknowledgedAt' is not null and correlation_id is not null and correlation_id <> '';"

expect_at_least_one \
  "correction approval outbox" \
  "select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'FieldOperations.DailyReportApproved' and payload->>'id' = '${correction_id}' and correlation_id is not null and correlation_id <> '';"

expect_at_least_one \
  "correction idempotency receipt" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and operation = 'daily-reports.start-correction' and key = 'integration-report-correction';"

printf 'Checkpoint 22 database, workflow, audit and notification verification passed.\n'
