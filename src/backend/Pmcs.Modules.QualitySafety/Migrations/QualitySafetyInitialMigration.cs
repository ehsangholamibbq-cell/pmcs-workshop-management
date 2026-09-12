using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.QualitySafety.Migrations;

internal sealed class QualitySafetyInitialMigration : IDatabaseMigration
{
    public string ModuleName => "quality-safety";
    public long Order => 725;
    public string Version => "20260911-001";
    public string Description => "Add independent quality, HSE and corrective-action records";
    public string Sql => """
        create schema if not exists quality_safety;
        create table if not exists quality_safety.configurations (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null,
          quality_mode varchar(50) not null, hse_mode varchar(50) not null,
          quality_owner_user_id uuid null, hse_owner_user_id uuid null,
          quality_matrix_version_id uuid null, hse_matrix_version_id uuid null,
          workflow_sla_defined boolean not null, templates_defined boolean not null,
          evidence_closure_rules_defined boolean not null, changed_at timestamptz not null,
          changed_by uuid not null, revision bigint not null);
        create unique index if not exists ux_quality_safety_configuration_project
          on quality_safety.configurations(tenant_id, project_id);

        create table if not exists quality_safety.risk_matrix_versions (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null,
          area varchar(30) not null, version integer not null, title varchar(200) not null,
          definition_json jsonb not null, effective_from timestamptz not null,
          created_by uuid not null, created_at timestamptz not null, revision bigint not null);
        create unique index if not exists ux_quality_safety_matrix_version
          on quality_safety.risk_matrix_versions(tenant_id, project_id, area, version);

        create table if not exists quality_safety.intakes (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          kind varchar(50) not null, observed_at timestamptz not null, location varchar(240) not null,
          facts varchar(4000) not null, initial_severity varchar(30) not null,
          immediate_action varchar(2000) null, evidence_references_json jsonb not null,
          classification varchar(50) not null, reported_by uuid not null, created_at timestamptz not null,
          status varchar(40) not null, conversion_type varchar(50) not null, converted_record_id uuid null,
          triage_note varchar(2000) null, triaged_by uuid null, triaged_at timestamptz null, revision bigint not null);
        create unique index if not exists ux_quality_safety_intake_number on quality_safety.intakes(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_intake_state on quality_safety.intakes(tenant_id, project_id, status, observed_at);

        create table if not exists quality_safety.inspections (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          source_intake_id uuid null, inspection_type varchar(160) not null, location varchar(240) not null,
          acceptance_criteria varchar(4000) not null, checklist_template_reference varchar(240) null,
          checklist_template_version integer null, requested_for timestamptz not null, requested_by uuid not null,
          created_at timestamptz not null, readiness varchar(40) not null, readiness_note varchar(1000) null,
          status varchar(40) not null, result varchar(50) null, result_note varchar(2000) null,
          evidence_references_json jsonb not null, inspected_by uuid null, inspected_at timestamptz null,
          revision bigint not null);
        create unique index if not exists ux_quality_safety_inspection_number on quality_safety.inspections(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_inspection_state on quality_safety.inspections(tenant_id, project_id, status, requested_for);

        create table if not exists quality_safety.nonconformances (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          source_intake_id uuid null, inspection_id uuid null, goods_receipt_id uuid null,
          purchase_order_id uuid null, vendor_party_id uuid null, supply_item_id uuid null,
          lot_reference varchar(160) null, title varchar(240) not null, requirement varchar(4000) not null,
          nonconformity varchar(4000) not null, status varchar(50) not null, disposition varchar(50) not null,
          disposition_note varchar(2000) null, concession_approved_by uuid null,
          root_cause_status varchar(40) not null, root_cause varchar(4000) null,
          closure_evidence_json jsonb not null, closure_waiver_reason varchar(1000) null,
          closure_waived_by uuid null, created_by uuid not null, created_at timestamptz not null,
          closed_at timestamptz null, revision bigint not null);
        create unique index if not exists ux_quality_safety_ncr_number on quality_safety.nonconformances(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_ncr_state on quality_safety.nonconformances(tenant_id, project_id, status);
        create index if not exists ix_quality_safety_ncr_vendor on quality_safety.nonconformances(tenant_id, project_id, vendor_party_id);

        create table if not exists quality_safety.defects (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          source_intake_id uuid null, title varchar(240) not null, location varchar(240) not null,
          assignee_user_id uuid null, due_date date null, status varchar(40) not null,
          rectification_evidence_json jsonb not null, verification_evidence_json jsonb not null,
          created_by uuid not null, created_at timestamptz not null, closed_at timestamptz null, revision bigint not null);
        create unique index if not exists ux_quality_safety_defect_number on quality_safety.defects(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_defect_state on quality_safety.defects(tenant_id, project_id, status, due_date);

        create table if not exists quality_safety.incidents (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          source_intake_id uuid null, occurred_at timestamptz not null, location varchar(240) not null,
          facts varchar(6000) not null, preliminary_severity varchar(30) not null, final_severity varchar(30) null,
          matrix_version_id uuid not null, classification varchar(50) not null, status varchar(50) not null,
          root_cause_status varchar(40) not null, root_cause varchar(4000) null,
          closure_evidence_json jsonb not null, reported_by uuid not null, created_at timestamptz not null,
          closed_at timestamptz null, revision bigint not null);
        create unique index if not exists ux_quality_safety_incident_number on quality_safety.incidents(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_incident_state on quality_safety.incidents(tenant_id, project_id, status, occurred_at);

        create table if not exists quality_safety.corrective_actions (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          source_area varchar(30) not null, source_record_type varchar(120) not null, source_record_id uuid not null,
          kind varchar(30) not null, title varchar(240) not null, owner_user_id uuid not null,
          responsible_party varchar(240) not null, due_date date not null, success_criteria varchar(2000) not null,
          status varchar(50) not null, completion_evidence_json jsonb not null,
          verification_evidence_json jsonb not null, verified_by uuid null, verified_at timestamptz null,
          extended_due_date date null, extension_reason varchar(1000) null, extension_approved_by uuid null,
          created_by uuid not null, created_at timestamptz not null, closed_at timestamptz null, revision bigint not null);
        create unique index if not exists ux_quality_safety_action_number on quality_safety.corrective_actions(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_action_state on quality_safety.corrective_actions(tenant_id, project_id, status, due_date);

        create table if not exists quality_safety.permits (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          work_description varchar(2000) not null, location varchar(240) not null,
          valid_from timestamptz not null, valid_to timestamptz not null,
          hazards_json jsonb not null, controls_json jsonb not null, status varchar(40) not null,
          requested_by uuid not null, created_at timestamptz not null, approved_by uuid null,
          approved_at timestamptz null, closed_at timestamptz null, revision bigint not null);
        create unique index if not exists ux_quality_safety_permit_number on quality_safety.permits(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_permit_state on quality_safety.permits(tenant_id, project_id, status, valid_to);

        create table if not exists quality_safety.toolbox_talks (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          topic varchar(500) not null, held_at timestamptz not null, location varchar(240) not null,
          attendees_json jsonb not null, evidence_references_json jsonb not null,
          recorded_by uuid not null, created_at timestamptz not null, revision bigint not null);
        create unique index if not exists ux_quality_safety_toolbox_number on quality_safety.toolbox_talks(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_toolbox_held on quality_safety.toolbox_talks(tenant_id, project_id, held_at);

        alter table quality_safety.inspections add column if not exists inspection_test_plan_version_id uuid null;
        create table if not exists quality_safety.inspection_test_plan_versions (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, code varchar(80) not null,
          version integer not null, title varchar(240) not null, stages_json jsonb not null,
          acceptance_criteria varchar(4000) not null, inspector_role varchar(160) not null,
          point_type varchar(30) not null, effective_from timestamptz not null,
          created_by uuid not null, created_at timestamptz not null, revision bigint not null);
        create unique index if not exists ux_quality_safety_itp_version
          on quality_safety.inspection_test_plan_versions(tenant_id, project_id, code, version);
        create table if not exists quality_safety.checklist_template_versions (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, code varchar(80) not null,
          version integer not null, title varchar(240) not null, items_json jsonb not null,
          effective_from timestamptz not null, created_by uuid not null, created_at timestamptz not null, revision bigint not null);
        create unique index if not exists ux_quality_safety_checklist_version
          on quality_safety.checklist_template_versions(tenant_id, project_id, code, version);
        create table if not exists quality_safety.test_records (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null, number varchar(80) not null,
          inspection_id uuid null, test_type varchar(160) not null, tested_at timestamptz not null,
          sample_reference varchar(240) not null, acceptance_criteria varchar(2000) not null,
          result varchar(30) not null, result_details varchar(2000) not null,
          evidence_references_json jsonb not null, recorded_by uuid not null, created_at timestamptz not null, revision bigint not null);
        create unique index if not exists ux_quality_safety_test_number on quality_safety.test_records(tenant_id, project_id, number);
        create index if not exists ix_quality_safety_test_date on quality_safety.test_records(tenant_id, project_id, tested_at);
        create table if not exists quality_safety.competency_records (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null,
          person_reference varchar(240) not null, induction_date date not null, valid_until date null,
          competencies_json jsonb not null, evidence_references_json jsonb not null,
          classification varchar(50) not null, recorded_by uuid not null, created_at timestamptz not null, revision bigint not null);
        create index if not exists ix_quality_safety_competency_validity
          on quality_safety.competency_records(tenant_id, project_id, person_reference, valid_until);
        create table if not exists quality_safety.exposure_hours (
          id uuid primary key, tenant_id uuid not null, project_id uuid not null,
          period_start date not null, period_end date not null, hours numeric(18,2) not null,
          source_reference varchar(500) not null, evidence_references_json jsonb not null,
          approved_by uuid not null, approved_at timestamptz not null, revision bigint not null);
        create index if not exists ix_quality_safety_exposure_period
          on quality_safety.exposure_hours(tenant_id, project_id, period_start, period_end);
        """;
}
