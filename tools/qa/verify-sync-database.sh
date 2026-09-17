#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set PMCS_QA_CONNECTION_STRING to the isolated QA database.}"
: "${PMCS_QA_DATABASE_URL:?Set PMCS_QA_DATABASE_URL to the same isolated QA database as a PostgreSQL URI.}"

command -v dotnet >/dev/null
command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
other_project_id="33333333-3333-4333-8333-333333333398"
manager_id="22222222-2222-2222-2222-222222222222"
site_supervisor_id="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
technical_office_id="50000000-0000-4000-8000-000000000002"
site_supervisor_key_id="aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa"
technical_office_key_id="50000000000040008000000000000002"
primary_device_id="pmcs-qa-harness"
negative_device_id="pmcs-qa-harness"
primary_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1D"
conflict_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1E"
invalid_envelope_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1F"
cross_project_operation_id="01K4ZQ9G5V7Q0M8M2V4R6D8F1G"
primary_report_id="70000000-0000-4000-8000-000000000008"
primary_fact_id="70000000-0000-4000-8000-000000000009"
second_user_report_id="70000000-0000-4000-8000-000000000010"
second_user_fact_id="70000000-0000-4000-8000-000000000011"
conflict_report_id="70000000-0000-4000-8000-000000000012"
primary_correlation_id="qa-sync-primary-correlation"
second_user_correlation_id="qa-sync-second-user-correlation"
conflict_correlation_id="qa-sync-conflict-correlation"

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

database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj --configuration Release --no-build --no-launch-profile -- guard)"
connected_database="$(scalar 'select current_database();')"
if [[ "${connected_database}" != "${database_name}" ]]; then
  echo "The ADO and PostgreSQL QA connections target different databases." >&2
  exit 2
fi

expect_equal \
  "accepted receipt survives exact replay and changed-payload reuse" \
  "Applied|3|1|2|${primary_correlation_id}" \
  "select status || '|' || attempt_count::text || '|' || replay_count::text || '|' || server_revision::text || '|' || last_correlation_id from sync_control.operation_receipts where tenant_id = '${tenant_id}' and project_id = '${project_id}' and user_id = '${site_supervisor_id}' and device_id = '${primary_device_id}' and operation_id = '${primary_operation_id}';"

expect_equal \
  "same device and operation identity is isolated by user" \
  "2|2|2" \
  "select count(*)::text || '|' || count(distinct user_id)::text || '|' || count(*) filter (where status = 'Applied')::text from sync_control.operation_receipts where tenant_id = '${tenant_id}' and project_id = '${project_id}' and device_id = '${primary_device_id}' and operation_id = '${primary_operation_id}';"

expect_equal \
  "user-scoped platform idempotency receipts" \
  "2|true" \
  "select count(*)::text || '|' || bool_and(length(key) <= 160)::text from foundation.idempotency_records where tenant_id = '${tenant_id}' and operation = 'sync.daily-report.capture-fact' and (key like 'sync:${site_supervisor_key_id}:%:${primary_operation_id}' or key like 'sync:${technical_office_key_id}:%:${primary_operation_id}');"

expect_equal \
  "legacy actor-ambiguous idempotency identity is absent" \
  "0" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key = 'sync:${primary_device_id}:${primary_operation_id}';"

expect_equal \
  "two users produced independent reports and facts" \
  "${site_supervisor_id}|2099-12-30|2|1|${technical_office_id}|2099-12-31|2|1" \
  "select first.created_by::text || '|' || first.report_date::text || '|' || first.revision::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = first.id)::text || '|' || second.created_by::text || '|' || second.report_date::text || '|' || second.revision::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = second.id)::text from field_operations.daily_reports first cross join field_operations.daily_reports second where first.id = '${primary_report_id}' and second.id = '${second_user_report_id}';"

expect_equal \
  "expected offline facts exist once" \
  "2" \
  "select count(*) from field_operations.daily_report_facts where (daily_report_id = '${primary_report_id}' and id = '${primary_fact_id}') or (daily_report_id = '${second_user_report_id}' and id = '${second_user_fact_id}');"

expect_equal \
  "conflicting report intent did not overwrite server state" \
  "0" \
  "select count(*) from field_operations.daily_reports where id = '${conflict_report_id}';"

expect_equal \
  "applied audit actor and immutable correlation lineage" \
  "1|1" \
  "select count(*) filter (where actor_user_id = '${site_supervisor_id}' and correlation_id = '${primary_correlation_id}')::text || '|' || count(*) filter (where actor_user_id = '${technical_office_id}' and correlation_id = '${second_user_correlation_id}')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'OfflineDailyReportFactApplied' and data->>'operationId' = '${primary_operation_id}';"

expect_equal \
  "applied outbox correlation lineage" \
  "1|1" \
  "select count(*) filter (where correlation_id = '${primary_correlation_id}')::text || '|' || count(*) filter (where correlation_id = '${second_user_correlation_id}')::text from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'FieldOperations.OfflineDailyReportFactApplied' and payload->>'operationId' = '${primary_operation_id}';"

expect_equal \
  "project change feed preserves both actors and correlations" \
  "2|2|2" \
  "select count(*)::text || '|' || count(distinct actor_user_id)::text || '|' || count(distinct correlation_id)::text from sync_control.change_feed where tenant_id = '${tenant_id}' and project_id = '${project_id}' and operation_id = '${primary_operation_id}' and correlation_id in ('${primary_correlation_id}', '${second_user_correlation_id}');"

expect_equal \
  "changed-payload reuse is audited without downgrading its receipt" \
  "1" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and actor_user_id = '${site_supervisor_id}' and event_type = 'OfflineOperationRejected' and data->>'operationId' = '${primary_operation_id}' and data->>'code' = 'sync.operation.reused' and correlation_id = '${primary_correlation_id}';"

expect_equal \
  "concurrent second-user conflict receipt" \
  "Conflict|${technical_office_id}|${conflict_correlation_id}" \
  "select status || '|' || user_id::text || '|' || last_correlation_id from sync_control.operation_receipts where tenant_id = '${tenant_id}' and project_id = '${project_id}' and device_id = '${primary_device_id}' and operation_id = '${conflict_operation_id}';"

expect_equal \
  "resolved conflict preserves detector and manager lineage" \
  "Resolved|${technical_office_id}|${manager_id}|KeepServer" \
  "select status || '|' || user_id::text || '|' || resolved_by::text || '|' || resolution_type from sync_control.conflicts where tenant_id = '${tenant_id}' and project_id = '${project_id}' and operation_id = '${conflict_operation_id}';"

expect_equal \
  "conflict detection uses operation correlation" \
  "1" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and actor_user_id = '${technical_office_id}' and event_type = 'ConflictDetected' and data->>'operationId' = '${conflict_operation_id}' and correlation_id = '${conflict_correlation_id}';"

expect_equal \
  "conflict resolution audit is emitted once after replay" \
  "1" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and actor_user_id = '${manager_id}' and event_type = 'ConflictResolved' and data->>'operationId' = '${conflict_operation_id}' and correlation_id is not null and btrim(correlation_id) <> '';"

expect_equal \
  "primary device checkpoint matches project watermark" \
  "0" \
  "select greatest(0, (select coalesce(max(sequence), 0) from sync_control.change_feed where tenant_id = '${tenant_id}' and project_id = '${project_id}') - (select last_sequence from sync_control.device_checkpoints where tenant_id = '${tenant_id}' and user_id = '${site_supervisor_id}' and project_id = '${project_id}' and device_id = '${primary_device_id}' and dataset = 'field-operations-v1'));"

expect_equal \
  "checkpoint replay emits one audit only" \
  "1" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and actor_user_id = '${site_supervisor_id}' and event_type = 'CheckpointAdvanced' and data->>'deviceId' = '${primary_device_id}';"

expect_equal \
  "invalid envelopes are durable bounded rejections in session scope" \
  "2|2|2" \
  "select count(*)::text || '|' || count(*) filter (where status = 'Rejected' and code = 'sync.operation.envelope.invalid')::text || '|' || count(*) filter (where project_id = '${project_id}')::text from sync_control.operation_receipts where tenant_id = '${tenant_id}' and user_id = '${manager_id}' and device_id = '${negative_device_id}' and operation_id in ('${invalid_envelope_operation_id}', '${cross_project_operation_id}');"

expect_equal \
  "invalid diagnostic text is safely bounded" \
  "Invalid|DailyReport" \
  "select string_agg(entity_type, '|' order by operation_id) from sync_control.operation_receipts where tenant_id = '${tenant_id}' and user_id = '${manager_id}' and device_id = '${negative_device_id}' and operation_id in ('${invalid_envelope_operation_id}', '${cross_project_operation_id}');"

expect_equal \
  "cross-project invalid envelope cannot poison another project" \
  "0|0" \
  "select (select count(*) from sync_control.operation_receipts where tenant_id = '${tenant_id}' and project_id = '${other_project_id}' and operation_id = '${cross_project_operation_id}')::text || '|' || (select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${other_project_id}' and data->>'operationId' = '${cross_project_operation_id}')::text;"

expect_equal \
  "revoked device closes leases and sessions" \
  "Revoked|true|0|0" \
  "select device.status || '|' || (device.revoked_at is not null)::text || '|' || (select count(*) from sync_control.offline_leases where tenant_id = '${tenant_id}' and user_id = '${manager_id}' and device_id = '${negative_device_id}' and status <> 'Revoked')::text || '|' || (select count(*) from sync_control.sessions where tenant_id = '${tenant_id}' and user_id = '${manager_id}' and device_id = '${negative_device_id}' and closed_at is null)::text from sync_control.devices device where device.tenant_id = '${tenant_id}' and device.user_id = '${manager_id}' and device.device_id = '${negative_device_id}';"

expect_equal \
  "operation diagnostics remain payload-free" \
  "0" \
  "select count(*) from information_schema.columns where table_schema = 'sync_control' and table_name = 'operation_receipts' and column_name in ('payload', 'local_intent', 'server_projection');"

printf 'QA offline, reconnect, replay, multi-user conflict, recovery and sync database verification passed for %s.\n' "${database_name}"
