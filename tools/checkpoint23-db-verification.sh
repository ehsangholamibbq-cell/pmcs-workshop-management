#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_VERIFICATION_DATABASE_URL:?Set PMCS_VERIFICATION_DATABASE_URL to the isolated Checkpoint 23 test database.}"

command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
manager_user_id="22222222-2222-2222-2222-222222222222"
field_user_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
project_id="33333333-3333-3333-3333-333333333333"
report_id="44444444-4444-4444-4444-444444444444"
fact_id="55555555-5555-5555-5555-555555555555"
operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1B"
conflicting_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1C"

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
  "idempotent operation receipt" \
  "Applied|2|1" \
  "select status || '|' || attempt_count::text || '|' || replay_count::text from sync_control.operation_receipts where tenant_id = '${tenant_id}' and user_id = '${manager_user_id}' and operation_id = '${operation_id}';"

expect_equal \
  "single accepted change feed event" \
  "1" \
  "select count(*) from sync_control.change_feed where tenant_id = '${tenant_id}' and project_id = '${project_id}' and operation_id = '${operation_id}';"

expect_equal \
  "single applied fact after replay" \
  "1" \
  "select count(*) from field_operations.daily_report_facts where daily_report_id = '${report_id}' and id = '${fact_id}';"

expect_equal \
  "single applied audit after replay" \
  "1" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'OfflineDailyReportFactApplied' and data->>'operationId' = '${operation_id}';"

expect_equal \
  "two-user conflict receipt" \
  "Conflict|${field_user_id}" \
  "select status || '|' || user_id::text from sync_control.operation_receipts where tenant_id = '${tenant_id}' and project_id = '${project_id}' and operation_id = '${conflicting_operation_id}';"

expect_equal \
  "resolved concurrent conflict preserves both actors" \
  "Resolved|${field_user_id}|${manager_user_id}|KeepServer" \
  "select status || '|' || user_id::text || '|' || resolved_by::text || '|' || resolution_type from sync_control.conflicts where tenant_id = '${tenant_id}' and project_id = '${project_id}' and operation_id = '${conflicting_operation_id}';"

expect_at_least_one \
  "conflict detection audit with correlation" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'ConflictDetected' and data->>'operationId' = '${conflicting_operation_id}' and correlation_id is not null and correlation_id <> '';"

expect_at_least_one \
  "conflict resolution audit with correlation" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'ConflictResolved' and data->>'operationId' = '${conflicting_operation_id}' and correlation_id is not null and correlation_id <> '';"

expect_equal \
  "local/server checkpoint alignment" \
  "0" \
  "select greatest(0, (select coalesce(max(sequence), 0) from sync_control.change_feed where tenant_id = '${tenant_id}' and project_id = '${project_id}') - (select last_sequence from sync_control.device_checkpoints where tenant_id = '${tenant_id}' and user_id = '${manager_user_id}' and project_id = '${project_id}' and device_id = 'integration-device-001' and dataset = 'field-operations-v1'));"

expect_equal \
  "operation diagnostics contain no business payload column" \
  "0" \
  "select count(*) from information_schema.columns where table_schema = 'sync_control' and table_name = 'operation_receipts' and column_name in ('payload', 'local_intent', 'server_projection');"

printf 'Checkpoint 23 sync replay, two-user conflict, recovery, audit and diagnostics verification passed.\n'
