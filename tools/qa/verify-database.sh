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
reporting_succeeded_run_id="71000000-0000-4000-8000-000000000001"
reporting_license_failure_run_id="71000000-0000-4000-8000-000000000002"
reporting_cancelled_run_id="71000000-0000-4000-8000-000000000003"
reporting_concurrency_locked_run_id="71000000-0000-4000-8000-000000000004"
reporting_concurrency_skipped_run_id="71000000-0000-4000-8000-000000000005"
reporting_crash_before_storage_run_id="71000000-0000-4000-8000-000000000006"
reporting_crash_after_storage_run_id="71000000-0000-4000-8000-000000000007"
reporting_stale_lease_run_id="71000000-0000-4000-8000-000000000008"
reporting_worker_revocation_run_id="71000000-0000-4000-8000-000000000009"
reporting_capacity_poison_run_id="72000000-0000-4000-8000-000000000001"
reporting_remediated_orphan_document_id="73000000-0000-4000-8000-000000000201"
reporting_golden_measurement_item_id="74000000-0000-4000-8000-000000000001"
reporting_golden_v1_report_id="74000000-0000-4000-8000-000000000010"
reporting_golden_v2_report_id="74000000-0000-4000-8000-000000000020"
reporting_golden_v3_report_id="74000000-0000-4000-8000-000000000030"
reporting_golden_material_fact_id="74000000-0000-4000-8000-000000000104"
reporting_golden_replacement_fact_id="74000000-0000-4000-8000-000000000201"
reporting_golden_draft_fact_id="74000000-0000-4000-8000-000000000301"
reporting_golden_before_run_id="75000000-0000-4000-8000-000000000001"
reporting_golden_before_twin_run_id="75000000-0000-4000-8000-000000000002"
reporting_golden_after_run_id="75000000-0000-4000-8000-000000000003"
reporting_golden_after_twin_run_id="75000000-0000-4000-8000-000000000004"
reporting_periodic_run_id="77000000-0000-4000-8000-000000000001"
reporting_project_progress_run_id="79000000-0000-4000-8000-000000000001"
system_actor_id="00000000-0000-0000-0000-000000000001"

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
  "46" \
  "select count(*) from foundation.schema_migrations;"

expect_equal \
  "certified reporting migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'reporting' and version = '20260918-001';"

expect_equal \
  "certified reporting verification-code migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'reporting' and version = '20260918-002';"

expect_equal \
  "project-periodic reporting catalog migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'reporting' and version = '20260920-003';"

expect_equal \
  "project-progress reporting catalog migration identity" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'reporting' and version = '20260920-005';"

expect_equal \
  "project-periodic certified definition and immutable template are published" \
  "Active|pmcs.reporting.project-periodic.parameters/v1|1.0.0|pmcs.reporting.project-periodic.renderer/v1|pmcs.reporting.project-periodic.layout/v1|eca353e00f07fbdb054613768399496e137ab0deda731c5d319d5cd4ebd3be6b" \
  "select definition.status || '|' || definition.parameter_schema_version || '|' || template.version || '|' || template.renderer_contract_version || '|' || template.layout_contract_version || '|' || template.content_digest from reporting.report_definitions definition join reporting.report_template_versions template on template.id = definition.current_template_version_id and template.definition_id = definition.id where definition.code = 'project-periodic-certified' and template.retired_at is null;"

expect_equal \
  "project-periodic pinned profile storage is constrained JSON" \
  "jsonb|YES|1" \
  "select column_state.data_type || '|' || column_state.is_nullable || '|' || (select count(*) from pg_constraint where conrelid = 'reporting.report_runs'::regclass and conname = 'ck_reporting_run_pinned_project_profile')::text from information_schema.columns column_state where column_state.table_schema = 'reporting' and column_state.table_name = 'report_runs' and column_state.column_name = 'pinned_project_profile';"

expect_equal \
  "project-progress certified definition and immutable template are published" \
  "Active|pmcs.reporting.project-progress.parameters/v1|true|1.0.0|pmcs.reporting.project-progress.renderer/v1|pmcs.reporting.project-progress.layout/v1|3f19d880a7790854fcc0d79d4822c5653cb6bb888294eadf8eaeeee8b5857816|Landscape" \
  "select definition.status || '|' || definition.parameter_schema_version || '|' || (jsonb_array_length(definition.required_permissions) = 3 and definition.required_permissions @> '[\"planning.progress.read\"]'::jsonb and definition.required_permissions @> '[\"planning.baselines.read\"]'::jsonb and definition.required_permissions @> '[\"planning.milestones.read\"]'::jsonb)::text || '|' || template.version || '|' || template.renderer_contract_version || '|' || template.layout_contract_version || '|' || template.content_digest || '|' || template.orientation from reporting.report_definitions definition join reporting.report_template_versions template on template.id = definition.current_template_version_id and template.definition_id = definition.id where definition.code = 'project-progress-certified' and template.retired_at is null;"

expect_equal \
  "legacy unique reporting verification-code constraint removed" \
  "0" \
  "select count(*) from pg_constraint where conrelid = 'reporting.report_outputs'::regclass and conname = 'report_outputs_verification_code_key';"

expect_equal \
  "deterministic reporting verification-code lookup is non-unique" \
  "1" \
  "select count(*) from pg_index index_state join pg_class index_class on index_class.oid = index_state.indexrelid join pg_class table_class on table_class.oid = index_state.indrelid join pg_namespace schema_state on schema_state.oid = table_class.relnamespace where schema_state.nspname = 'reporting' and table_class.relname = 'report_outputs' and index_class.relname = 'ix_reporting_outputs_verification_code' and not index_state.indisunique;"

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

expect_equal \
  "reporting Golden measurement lineage is deterministic" \
  "Active|m3|100.000000" \
  "select status || '|' || unit || '|' || target_quantity::text from planning.measurement_items where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${reporting_golden_measurement_item_id}';"

expect_equal \
  "reporting Golden correction chain retains official and draft lineage" \
  "Superseded|${reporting_golden_v2_report_id}|Approved|${reporting_golden_v1_report_id}|Draft|${reporting_golden_v2_report_id}|1,2,3" \
  "select v1.status || '|' || v1.superseded_by_report_id::text || '|' || v2.status || '|' || v2.supersedes_report_id::text || '|' || v3.status || '|' || v3.supersedes_report_id::text || '|' || (select string_agg(version_number::text, ',' order by version_number) from field_operations.daily_reports where root_report_id = '${reporting_golden_v1_report_id}') from field_operations.daily_reports v1 cross join field_operations.daily_reports v2 cross join field_operations.daily_reports v3 where v1.id = '${reporting_golden_v1_report_id}' and v2.id = '${reporting_golden_v2_report_id}' and v3.id = '${reporting_golden_v3_report_id}';"

expect_equal \
  "reporting Golden facts preserve copy remove replace and draft semantics" \
  "8|8|9|7|0|1" \
  "select (select count(*) from field_operations.daily_report_facts where daily_report_id = '${reporting_golden_v1_report_id}')::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = '${reporting_golden_v2_report_id}')::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = '${reporting_golden_v3_report_id}')::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = '${reporting_golden_v2_report_id}' and copied_from_fact_id is not null)::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = '${reporting_golden_v2_report_id}' and copied_from_fact_id = '${reporting_golden_material_fact_id}')::text || '|' || (select count(*) from field_operations.daily_report_facts where daily_report_id = '${reporting_golden_v2_report_id}' and id = '${reporting_golden_replacement_fact_id}' and copied_from_fact_id is null)::text;"

expect_equal \
  "reporting Golden runs publish two stable cutoff snapshots" \
  "4|4|4|4|2|2" \
  "select count(distinct run.id)::text || '|' || count(distinct run.id) filter (where run.status = 'Succeeded' and run.pipeline_stage = 'Complete')::text || '|' || count(distinct snapshot.id)::text || '|' || count(distinct output.id)::text || '|' || count(distinct snapshot.sha256)::text || '|' || count(distinct snapshot.source_manifest_sha256)::text from reporting.report_runs run left join reporting.report_snapshots snapshot on snapshot.run_id = run.id left join reporting.report_outputs output on output.run_id = run.id where run.id in ('${reporting_golden_before_run_id}', '${reporting_golden_before_twin_run_id}', '${reporting_golden_after_run_id}', '${reporting_golden_after_twin_run_id}');"

expect_equal \
  "reporting Golden twins share hashes while official correction changes hashes" \
  "1|1|true|1|1|true" \
  "with before_snapshots as (select snapshot.sha256, snapshot.source_manifest_sha256 from reporting.report_snapshots snapshot where snapshot.run_id in ('${reporting_golden_before_run_id}', '${reporting_golden_before_twin_run_id}')), after_snapshots as (select snapshot.sha256, snapshot.source_manifest_sha256 from reporting.report_snapshots snapshot where snapshot.run_id in ('${reporting_golden_after_run_id}', '${reporting_golden_after_twin_run_id}')) select (select count(distinct sha256) from before_snapshots)::text || '|' || (select count(distinct sha256) from after_snapshots)::text || '|' || ((select min(sha256) from before_snapshots) <> (select min(sha256) from after_snapshots))::text || '|' || (select count(distinct source_manifest_sha256) from before_snapshots)::text || '|' || (select count(distinct source_manifest_sha256) from after_snapshots)::text || '|' || ((select min(source_manifest_sha256) from before_snapshots) <> (select min(source_manifest_sha256) from after_snapshots))::text;"

expect_equal \
  "reporting Golden historical cutoff hides future supersession state" \
  "2" \
  "select count(*) from reporting.report_snapshots snapshot where snapshot.run_id in ('${reporting_golden_before_run_id}', '${reporting_golden_before_twin_run_id}') and snapshot.payload_json->>'currentOfficialReportId' = '${reporting_golden_v1_report_id}' and jsonb_array_length(snapshot.payload_json->'versions') = 1 and snapshot.payload_json#>>'{versions,0,reportId}' = '${reporting_golden_v1_report_id}' and snapshot.payload_json#>>'{versions,0,state}' = 'Approved' and snapshot.payload_json#>>'{versions,0,revision}' = '11' and snapshot.payload_json#>>'{versions,0,supersededByReportId}' is null and snapshot.payload_json#>>'{versions,0,supersededAt}' is null and snapshot.payload_json#>>'{versions,0,correctionReason}' is null and snapshot.payload_json#>>'{versions,0,approvedAt}' = snapshot.payload_json#>>'{versions,0,lastModifiedAt}' and jsonb_array_length(snapshot.payload_json#>'{versions,0,facts}') = 8 and snapshot.source_manifest_json#>>'{versions,0,revision}' = '11';"

expect_equal \
  "reporting Golden corrected cutoff contains only the two official versions" \
  "2" \
  "select count(*) from reporting.report_snapshots snapshot where snapshot.run_id in ('${reporting_golden_after_run_id}', '${reporting_golden_after_twin_run_id}') and snapshot.payload_json->>'currentOfficialReportId' = '${reporting_golden_v2_report_id}' and jsonb_array_length(snapshot.payload_json->'versions') = 2 and snapshot.payload_json#>>'{versions,0,reportId}' = '${reporting_golden_v1_report_id}' and snapshot.payload_json#>>'{versions,0,state}' = 'Superseded' and snapshot.payload_json#>>'{versions,0,revision}' = '12' and snapshot.payload_json#>>'{versions,0,supersededByReportId}' = '${reporting_golden_v2_report_id}' and snapshot.payload_json#>>'{versions,0,correctionReason}' = 'Replace the material evidence with the corrected official fact.' and jsonb_array_length(snapshot.payload_json#>'{versions,0,facts}') = 8 and snapshot.payload_json#>>'{versions,1,reportId}' = '${reporting_golden_v2_report_id}' and snapshot.payload_json#>>'{versions,1,state}' = 'Approved' and snapshot.payload_json#>>'{versions,1,revision}' = '5' and snapshot.payload_json#>>'{versions,1,supersedesReportId}' = '${reporting_golden_v1_report_id}' and jsonb_array_length(snapshot.payload_json#>'{versions,1,facts}') = 8 and snapshot.source_manifest_json#>>'{versions,0,revision}' = '12' and snapshot.source_manifest_json#>>'{versions,1,revision}' = '5';"

expect_equal \
  "reporting Golden snapshots exclude the draft correction and marker" \
  "0" \
  "select count(*) from reporting.report_snapshots snapshot where snapshot.run_id in ('${reporting_golden_before_run_id}', '${reporting_golden_before_twin_run_id}', '${reporting_golden_after_run_id}', '${reporting_golden_after_twin_run_id}') and (snapshot.payload_json::text like '%${reporting_golden_v3_report_id}%' or snapshot.payload_json::text like '%${reporting_golden_draft_fact_id}%' or snapshot.payload_json::text like '%GOLDEN-DRAFT-V3-MUST-NOT-APPEAR%');"

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

expect_equal \
  "certified XLSX run completed exactly once" \
  "Succeeded|Complete|1|1|<none>" \
  "select status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || coalesce(diagnostic_code, '<none>') from reporting.report_runs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${reporting_succeeded_run_id}';"

expect_equal \
  "unconfigured PDF renderer failed closed after explicit retry" \
  "Failed|Failed|2|0|reporting.renderer.license_unconfigured" \
  "select status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || coalesce(diagnostic_code, '<none>') from reporting.report_runs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${reporting_license_failure_run_id}';"

expect_equal \
  "project-periodic run completed from a pinned project profile" \
  "Succeeded|Complete|1|2|project-periodic-certified|pmcs.reporting.project-periodic.project-profile/v1|true" \
  "select status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || definition_code || '|' || (pinned_project_profile->>'schemaVersion') || '|' || ((pinned_project_profile->>'id') = project_id::text and (pinned_project_profile->>'tenantId') = tenant_id::text and (pinned_project_profile->>'timeZone') = project_time_zone)::text from reporting.report_runs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${reporting_periodic_run_id}';"

expect_equal \
  "project-periodic semantic snapshot records explicit NotConfigured evidence" \
  "pmcs.reporting.project-periodic.snapshot/v1|NotConfigured|project-periodic-certified|Weekly|3|0" \
  "select schema_version || '|' || data_status || '|' || (payload_json->>'definitionCode') || '|' || (payload_json#>>'{period,kind}') || '|' || jsonb_array_length(payload_json->'reasonCodes')::text || '|' || jsonb_array_length(payload_json->'officialReports')::text from reporting.report_snapshots where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_periodic_run_id}';"

expect_equal \
  "project-periodic PDF and XLSX outputs are governed and complete" \
  "2|Pdf,Xlsx|2|0" \
  "select count(*)::text || '|' || string_agg(format, ',' order by format) || '|' || count(*) filter (where retention_policy = 'LongTerm' and archive_state = 'Active')::text || '|' || count(*) filter (where size_bytes <= 0 or sha256 !~ '^[0-9a-f]{64}$' or manifest_sha256 !~ '^[0-9a-f]{64}$')::text from reporting.report_outputs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_periodic_run_id}';"

expect_equal \
  "project-periodic run has singular queue snapshot completion and idempotency evidence" \
  "1|1|1|1|1" \
  "select count(*) filter (where event_type = 'CertifiedReportRunQueued')::text || '|' || count(*) filter (where event_type = 'CertifiedReportSnapshotBuilt')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunCompleted')::text || '|' || (select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'reporting.report.completed.v1' and payload->>'runId' = '${reporting_periodic_run_id}')::text || '|' || (select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key = 'qa-rpt1-periodic-create')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'ReportRun' and resource_id = '${reporting_periodic_run_id}';"

expect_equal \
  "project-progress run completed from a pinned project profile" \
  "Succeeded|Complete|1|2|project-progress-certified|pmcs.reporting.project-progress.project-profile/v1|true" \
  "select status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || definition_code || '|' || (pinned_project_profile->>'schemaVersion') || '|' || ((pinned_project_profile->>'id') = project_id::text and (pinned_project_profile->>'tenantId') = tenant_id::text and (pinned_project_profile->>'timeZone') = project_time_zone)::text from reporting.report_runs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${reporting_project_progress_run_id}';"

expect_equal \
  "project-progress permission snapshots require reporting and all three Planning reads" \
  "4|4|true|4|4|true" \
  "select jsonb_array_length(request_permission_snapshot->'decisions')::text || '|' || (select count(distinct decision->>'operation') from jsonb_array_elements(request_permission_snapshot->'decisions') decision where decision->>'operation' in ('reporting.run.create','planning.progress.read','planning.baselines.read','planning.milestones.read'))::text || '|' || (select bool_and((decision->>'allowed')::boolean) from jsonb_array_elements(request_permission_snapshot->'decisions') decision)::text || '|' || jsonb_array_length(processing_permission_snapshot->'decisions')::text || '|' || (select count(distinct decision->>'operation') from jsonb_array_elements(processing_permission_snapshot->'decisions') decision where decision->>'operation' in ('reporting.run.create','planning.progress.read','planning.baselines.read','planning.milestones.read'))::text || '|' || (select bool_and((decision->>'allowed')::boolean) from jsonb_array_elements(processing_permission_snapshot->'decisions') decision)::text from reporting.report_runs where id = '${reporting_project_progress_run_id}';"

expect_equal \
  "project-progress semantic snapshot is bounded and explicit when Planning Mode is None" \
  "pmcs.reporting.project-progress.snapshot/v1|NotConfigured|project-progress-certified|None|1|0|0|0|true" \
  "select schema_version || '|' || data_status || '|' || (payload_json->>'definitionCode') || '|' || (payload_json#>>'{configuration,planningMode}') || '|' || jsonb_array_length(payload_json->'reasonCodes')::text || '|' || jsonb_array_length(payload_json->'entries')::text || '|' || jsonb_array_length(payload_json->'milestones')::text || '|' || jsonb_array_length(payload_json->'curve')::text || '|' || (payload_json::text !~* 'forecast|earned.?value|composite.?health|finance|commercial')::text from reporting.report_snapshots where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_project_progress_run_id}';"

expect_equal \
  "project-progress PDF and XLSX outputs are governed and complete" \
  "2|Pdf,Xlsx|2|0" \
  "select count(*)::text || '|' || string_agg(format, ',' order by format) || '|' || count(*) filter (where retention_policy = 'LongTerm' and archive_state = 'Active')::text || '|' || count(*) filter (where size_bytes <= 0 or sha256 !~ '^[0-9a-f]{64}$' or manifest_sha256 !~ '^[0-9a-f]{64}$')::text from reporting.report_outputs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_project_progress_run_id}';"

expect_equal \
  "project-progress run has singular queue snapshot completion and idempotency evidence" \
  "1|1|1|1|1" \
  "select count(*) filter (where event_type = 'CertifiedReportRunQueued')::text || '|' || count(*) filter (where event_type = 'CertifiedReportSnapshotBuilt')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunCompleted')::text || '|' || (select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'reporting.report.completed.v1' and payload->>'runId' = '${reporting_project_progress_run_id}')::text || '|' || (select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key = 'qa-rpt1-project-progress-create')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'ReportRun' and resource_id = '${reporting_project_progress_run_id}';"

expect_equal \
  "queued report cancellation is final and unclaimed" \
  "Cancelled|Cancelled|0|0|<none>" \
  "select status || '|' || pipeline_stage || '|' || attempt_count::text || '|' || output_count::text || '|' || coalesce(diagnostic_code, '<none>') from reporting.report_runs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id = '${reporting_cancelled_run_id}';"

expect_equal \
  "cancelled report has no snapshot or output" \
  "0|0" \
  "select (select count(*) from reporting.report_snapshots where run_id = '${reporting_cancelled_run_id}')::text || '|' || (select count(*) from reporting.report_outputs where run_id = '${reporting_cancelled_run_id}')::text;"

expect_equal \
  "cancelled report audit lifecycle is singular" \
  "1|1" \
  "select count(*) filter (where event_type = 'CertifiedReportRunQueued')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunCancelled')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type = 'ReportRun' and resource_id = '${reporting_cancelled_run_id}';"

expect_equal \
  "cancelled report transactional outbox is singular" \
  "1|1" \
  "select count(*) filter (where event_type = 'Reporting.ReportRunQueued')::text || '|' || count(*) filter (where event_type = 'Reporting.ReportRunCancelled')::text from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and payload->>'id' = '${reporting_cancelled_run_id}';"

expect_equal \
  "cancelled report idempotency receipts are singular" \
  "1|1" \
  "select count(*) filter (where key = 'qa-rpt1-cancel-create')::text || '|' || count(*) filter (where key = 'qa-rpt1-cancel')::text from foundation.idempotency_records where tenant_id = '${tenant_id}';"

expect_equal \
  "worker concurrency and recovery runs completed with bounded attempts" \
  "5|5|1,1,2,2,2" \
  "select count(*)::text || '|' || count(*) filter (where status = 'Succeeded' and pipeline_stage = 'Complete' and output_count = 1)::text || '|' || string_agg(attempt_count::text, ',' order by id) from reporting.report_runs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and id in ('${reporting_concurrency_locked_run_id}', '${reporting_concurrency_skipped_run_id}', '${reporting_crash_before_storage_run_id}', '${reporting_crash_after_storage_run_id}', '${reporting_stale_lease_run_id}');"

expect_equal \
  "worker recovery outputs retain one-to-one governed document ownership" \
  "5|5|5|0" \
  "select count(distinct run.id)::text || '|' || count(distinct output.id)::text || '|' || count(distinct asset.id)::text || '|' || count(*) filter (where asset.status <> 'Released' or asset.owner_type <> 'ReportOutput' or asset.owner_id <> output.id or asset.sha256 <> output.sha256)::text from reporting.report_runs run join reporting.report_outputs output on output.run_id = run.id join documents.assets asset on asset.id = output.generated_document_id where run.id in ('${reporting_concurrency_locked_run_id}', '${reporting_concurrency_skipped_run_id}', '${reporting_crash_before_storage_run_id}', '${reporting_crash_after_storage_run_id}', '${reporting_stale_lease_run_id}');"

expect_equal \
  "crashed rendering attempts have one start, one resume and one completion" \
  "1|1|1|1|1|1" \
  "select count(*) filter (where resource_id = '${reporting_crash_before_storage_run_id}' and event_type = 'CertifiedReportRenderingStarted')::text || '|' || count(*) filter (where resource_id = '${reporting_crash_before_storage_run_id}' and event_type = 'CertifiedReportRenderingResumed')::text || '|' || count(*) filter (where resource_id = '${reporting_crash_before_storage_run_id}' and event_type = 'CertifiedReportRunCompleted')::text || '|' || count(*) filter (where resource_id = '${reporting_crash_after_storage_run_id}' and event_type = 'CertifiedReportRenderingStarted')::text || '|' || count(*) filter (where resource_id = '${reporting_crash_after_storage_run_id}' and event_type = 'CertifiedReportRenderingResumed')::text || '|' || count(*) filter (where resource_id = '${reporting_crash_after_storage_run_id}' and event_type = 'CertifiedReportRunCompleted')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}';"

expect_equal \
  "after-storage recovery reuses one document publication" \
  "1|1" \
  "select (select count(*) from foundation.audit_events event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${reporting_crash_after_storage_run_id}' and event.event_type = 'GeneratedReportDocumentReleased')::text || '|' || (select count(*) from foundation.outbox_messages event join reporting.report_runs run on run.correlation_id = event.correlation_id where run.id = '${reporting_crash_after_storage_run_id}' and event.event_type = 'documents.asset.released.v1')::text;"

expect_equal \
  "worker recovery idempotency receipts are singular" \
  "5" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key in ('qa-rpt1-recovery-concurrency-locked', 'qa-rpt1-recovery-concurrency-skipped', 'qa-rpt1-recovery-crash-before-storage', 'qa-rpt1-recovery-crash-after-storage', 'qa-rpt1-recovery-stale-lease');"

expect_equal \
  "worker-time permission revocation fails before document publication" \
  "Failed|Failed|1|0|reporting.permission.revoked|1|0" \
  "select run.status || '|' || run.pipeline_stage || '|' || run.attempt_count::text || '|' || run.output_count::text || '|' || coalesce(run.diagnostic_code, '<none>') || '|' || (select count(*) from reporting.report_snapshots where run_id = run.id)::text || '|' || (select count(*) from foundation.audit_events event where event.correlation_id = run.correlation_id and event.event_type = 'GeneratedReportDocumentReleased')::text from reporting.report_runs run where run.tenant_id = '${tenant_id}' and run.project_id = '${project_id}' and run.id = '${reporting_worker_revocation_run_id}';"

expect_equal \
  "worker-time revocation stores denied permission evidence" \
  "2" \
  "select count(*) from reporting.report_runs run cross join lateral jsonb_array_elements(run.processing_permission_snapshot->'decisions') decision where run.id = '${reporting_worker_revocation_run_id}' and decision->>'operation' in ('reporting.run.create', 'field.daily-reports.read') and not (decision->>'allowed')::boolean;"

expect_equal \
  "worker-time revocation audit and idempotency are singular" \
  "1|1" \
  "select (select count(*) from foundation.audit_events where event_type = 'CertifiedReportRunFailed' and resource_type = 'ReportRun' and resource_id = '${reporting_worker_revocation_run_id}' and data->>'workerInstanceId' = 'qa-rpt1-worker-revocation' and data->>'diagnosticCode' = 'reporting.permission.revoked')::text || '|' || (select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key = 'qa-rpt1-worker-revocation')::text;"

expect_equal \
  "capacity runs isolate poison without delaying healthy work" \
  "20|20|20|Failed|Failed|3|0|reporting.qa.transient_injected|true" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value) select (select count(*) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id)::text || '|' || (select count(*) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id where run.status = 'Succeeded' and run.pipeline_stage = 'Complete' and run.attempt_count = 1 and run.output_count = 1)::text || '|' || (select count(*) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id where run.completed_at is not null)::text || '|' || poison.status || '|' || poison.pipeline_stage || '|' || poison.attempt_count::text || '|' || poison.output_count::text || '|' || coalesce(poison.diagnostic_code, '<none>') || '|' || ((select percentile_disc(0.95) within group (order by extract(epoch from run.completed_at - run.created_at)) from reporting.report_runs run join healthy_ids on healthy_ids.id = run.id) < 30)::text from reporting.report_runs poison where poison.id = '${reporting_capacity_poison_run_id}';"

expect_equal \
  "capacity poison retry and terminal audit lineage is bounded" \
  "2|1|1|2|0|0" \
  "select count(*) filter (where event_type = 'CertifiedReportRunRequeued')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunFailed')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRenderingStarted')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRenderingResumed')::text || '|' || (select count(*) from reporting.report_outputs where run_id = '${reporting_capacity_poison_run_id}')::text || '|' || (select count(*) from foundation.outbox_messages where event_type = 'reporting.report.completed.v1' and payload->>'runId' = '${reporting_capacity_poison_run_id}')::text from foundation.audit_events where resource_type = 'ReportRun' and resource_id = '${reporting_capacity_poison_run_id}' and data->>'workerInstanceId' = 'qa-rpt1-capacity-worker';"

expect_equal \
  "capacity outputs retain singular document and event ownership" \
  "20|20|20|0|20|20|21" \
  "with healthy_ids as (select ('72000000-0000-4000-8000-' || lpad(value::text, 12, '0'))::uuid id from generate_series(101, 120) value), ownership as (select count(distinct run.id) runs, count(distinct output.id) outputs, count(distinct asset.id) assets, count(*) filter (where asset.status <> 'Released' or asset.owner_type <> 'ReportOutput' or asset.owner_id <> output.id or asset.sha256 <> output.sha256) invalid from healthy_ids join reporting.report_runs run on run.id = healthy_ids.id join reporting.report_outputs output on output.run_id = run.id join documents.assets asset on asset.id = output.generated_document_id) select ownership.runs::text || '|' || ownership.outputs::text || '|' || ownership.assets::text || '|' || ownership.invalid::text || '|' || (select count(*) from foundation.audit_events event join healthy_ids on healthy_ids.id::text = event.resource_id where event.resource_type = 'ReportRun' and event.event_type = 'CertifiedReportRunCompleted' and event.data->>'workerInstanceId' = 'qa-rpt1-capacity-worker')::text || '|' || (select count(*) from foundation.outbox_messages event join healthy_ids on healthy_ids.id::text = event.payload->>'runId' where event.event_type = 'reporting.report.completed.v1')::text || '|' || (select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and key like 'qa-rpt1-capacity-%')::text from ownership;"

expect_equal \
  "generated report orphan inventory is empty after recovery" \
  "0" \
  "select count(*) from documents.assets asset left join reporting.report_outputs output on output.id = asset.owner_id and output.tenant_id = asset.tenant_id and output.project_id = asset.project_id where asset.tenant_id = '${tenant_id}' and asset.project_id = '${project_id}' and asset.owner_type = 'ReportOutput' and output.id is null;"

expect_equal \
  "expired generated report orphan remediation is singular audited and object-key free" \
  "1|ReportOutput|LongTerm|true" \
  "select count(*)::text || '|' || min(data->>'ownerType') || '|' || min(data->>'retentionPolicy') || '|' || bool_and(not (data ? 'objectKey'))::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and actor_user_id = '${system_actor_id}' and event_type = 'GeneratedReportOrphanRemediated' and resource_type = 'DocumentAsset' and resource_id = '${reporting_remediated_orphan_document_id}';"

expect_equal \
  "object and metadata tamper attempts are audited" \
  "5" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type = 'CertifiedReportOutputIntegrityFailed' and resource_type = 'ReportOutput' and resource_id in (select id::text from reporting.report_outputs where run_id = '${reporting_succeeded_run_id}');"

expect_equal \
  "reporting snapshots remain immutable across render retry" \
  "2|2|0" \
  "select count(*)::text || '|' || count(distinct run_id)::text || '|' || count(*) filter (where sha256 !~ '^[0-9a-f]{64}$' or source_manifest_sha256 !~ '^[0-9a-f]{64}$')::text from reporting.report_snapshots where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id in ('${reporting_succeeded_run_id}', '${reporting_license_failure_run_id}');"

expect_equal \
  "certified XLSX output metadata is complete" \
  "1|Xlsx|application/vnd.openxmlformats-officedocument.spreadsheetml.sheet|LongTerm|Active|0" \
  "select count(*)::text || '|' || min(format) || '|' || min(content_type) || '|' || min(retention_policy) || '|' || min(archive_state) || '|' || count(*) filter (where size_bytes <= 0 or sha256 !~ '^[0-9a-f]{64}$' or manifest_sha256 !~ '^[0-9a-f]{64}$' or verification_code !~ '^RPT-([0-9A-F]{4}-){4}[0-9A-F]{4}$')::text from reporting.report_outputs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_succeeded_run_id}';"

expect_equal \
  "failed PDF run published no partial output" \
  "0" \
  "select count(*) from reporting.report_outputs where tenant_id = '${tenant_id}' and project_id = '${project_id}' and run_id = '${reporting_license_failure_run_id}';"

expect_equal \
  "certified output is a released governed document" \
  "1" \
  "select count(*) from reporting.report_outputs output join documents.assets asset on asset.id = output.generated_document_id and asset.tenant_id = output.tenant_id and asset.project_id = output.project_id and asset.owner_type = 'ReportOutput' and asset.owner_id = output.id and asset.version_number = 1 and asset.original_file_name = output.file_name and asset.content_type = output.content_type and asset.size_bytes = output.size_bytes and asset.sha256 = output.sha256 and asset.classification = output.classification and asset.retention_policy = output.retention_policy and asset.retention_policy = 'LongTerm' and asset.retain_until >= asset.created_at + interval '10 years' and not asset.legal_hold and asset.status = 'Released' and asset.scan_verdict = 'Clean' and asset.scan_provider = 'pmcs-generated-content' and asset.created_by = '${system_actor_id}' and asset.released_by = '${system_actor_id}' and asset.released_at is not null where output.tenant_id = '${tenant_id}' and output.project_id = '${project_id}' and output.run_id = '${reporting_succeeded_run_id}';"

expect_equal \
  "certified reporting audit lifecycle coverage" \
  "2|2|1|1|1|3|2|1" \
  "select count(*) filter (where event_type = 'CertifiedReportRunQueued')::text || '|' || count(*) filter (where event_type = 'CertifiedReportSnapshotBuilt')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunCompleted')::text || '|' || count(*) filter (where event_type = 'GeneratedReportDocumentReleased')::text || '|' || count(*) filter (where event_type = 'CertifiedReportOutputDownloaded')::text || '|' || count(*) filter (where event_type = 'CertifiedReportOutputVerified')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunFailed')::text || '|' || count(*) filter (where event_type = 'CertifiedReportRunRetried')::text from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and ((resource_type = 'ReportRun' and resource_id in ('${reporting_succeeded_run_id}', '${reporting_license_failure_run_id}')) or (event_type = 'GeneratedReportDocumentReleased' and data->>'ownerId' in (select id::text from reporting.report_outputs where run_id = '${reporting_succeeded_run_id}')) or (resource_type = 'ReportOutput' and resource_id in (select id::text from reporting.report_outputs where run_id = '${reporting_succeeded_run_id}')));"

expect_equal \
  "certified reporting transactional outbox coverage" \
  "2|1|1|1" \
  "select count(*) filter (where event_type = 'Reporting.ReportRunQueued' and payload->>'id' in ('${reporting_succeeded_run_id}', '${reporting_license_failure_run_id}'))::text || '|' || count(*) filter (where event_type = 'reporting.report.completed.v1' and payload->>'runId' = '${reporting_succeeded_run_id}')::text || '|' || count(*) filter (where event_type = 'documents.asset.released.v1' and payload->>'ownerType' = 'ReportOutput' and payload->>'ownerId' in (select id::text from reporting.report_outputs where run_id = '${reporting_succeeded_run_id}'))::text || '|' || count(*) filter (where event_type = 'Reporting.ReportRunRetried' and payload->>'id' = '${reporting_license_failure_run_id}')::text from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}';"

expect_equal \
  "certified reporting idempotency receipts are singular" \
  "1|1|1" \
  "select count(*) filter (where key = 'qa-rpt1-xlsx-create')::text || '|' || count(*) filter (where key = 'qa-rpt1-pdf-license-create')::text || '|' || count(*) filter (where key = 'qa-rpt1-pdf-license-retry')::text from foundation.idempotency_records where tenant_id = '${tenant_id}';"

printf 'QA permission, workflow, database and audit verification passed for %s.\n' "${database_name}"
