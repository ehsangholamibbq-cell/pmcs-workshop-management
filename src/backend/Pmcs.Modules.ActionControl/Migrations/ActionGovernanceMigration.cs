using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ActionControl.Migrations;

internal sealed class ActionGovernanceMigration : IDatabaseMigration
{
    public string ModuleName => "action-control";
    public long Order => 701;
    public string Version => "20260911-002";
    public string Description => "Create evidence-backed issue, risk, decision, service-level and escalation records";

    public string Sql => """
        create table if not exists action_control.risk_matrix_versions (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            version integer not null check (version > 0), title varchar(200) not null,
            formula_version varchar(100) not null, low_maximum integer not null,
            moderate_maximum integer not null, high_maximum integer not null,
            effective_from timestamptz not null, created_by uuid not null,
            created_at timestamptz not null, revision bigint not null,
            unique (tenant_id, project_id, version),
            constraint ck_risk_matrix_thresholds check (
                low_maximum >= 1 and low_maximum < moderate_maximum and
                moderate_maximum < high_maximum and high_maximum < 25)
        );

        create table if not exists action_control.sla_rule_versions (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            version integer not null check (version > 0), title varchar(200) not null,
            entity_type varchar(40) not null, severity varchar(40) null,
            duration integer not null check (duration > 0 and duration <= 365),
            duration_unit varchar(40) not null, warning_lead_minutes integer not null,
            escalation_delay_minutes integer not null,
            escalation_recipient_user_id uuid not null,
            escalation_recipient_display_name varchar(200) not null,
            effective_from timestamptz not null, created_by uuid not null,
            created_at timestamptz not null, revision bigint not null,
            constraint ck_sla_minutes check (
                warning_lead_minutes between 0 and 43200 and
                escalation_delay_minutes between 0 and 43200)
        );
        create unique index if not exists ux_sla_rule_scope_version
            on action_control.sla_rule_versions(tenant_id, project_id, entity_type, coalesce(severity, ''), version);

        create table if not exists action_control.risks (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, type varchar(40) not null,
            cause varchar(1500) not null, uncertain_event varchar(1500) not null,
            impact_statement varchar(2000) not null, category varchar(120) not null,
            owner_user_id uuid not null, owner_display_name varchar(200) not null,
            confidentiality varchar(40) not null, status varchar(40) not null,
            probability varchar(40) null, time_impact varchar(40) null,
            cost_impact varchar(40) null, quality_impact varchar(40) null,
            safety_impact varchar(40) null, contract_impact varchar(40) null,
            operations_impact varchar(40) null, inherent_score integer null,
            inherent_rating varchar(40) null, residual_probability varchar(40) null,
            residual_impact varchar(40) null, residual_score integer null,
            residual_rating varchar(40) null, matrix_version_id uuid null,
            matrix_version integer null, formula_version varchar(100) null,
            response_strategy varchar(40) null, response_plan varchar(4000) null,
            early_warning_indicator varchar(1000) null, review_date date null,
            source_module varchar(80) not null, source_entity_type varchar(120) not null,
            source_entity_id uuid null, source_revision bigint null,
            source_snapshot varchar(1000) not null,
            evidence_references_json jsonb not null default '[]'::jsonb,
            materialized_issue_id uuid null, sla_due_at timestamptz null,
            sla_rule_version_id uuid null, closure_reason varchar(1500) null,
            closure_evidence_json jsonb not null default '[]'::jsonb,
            created_by uuid not null, created_at timestamptz not null,
            last_reviewed_by uuid null, last_reviewed_at timestamptz null,
            closed_at timestamptz null, revision bigint not null,
            unique (tenant_id, project_id, number)
        );
        create index if not exists ix_risks_project_status_review
            on action_control.risks(tenant_id, project_id, status, review_date);

        create table if not exists action_control.issues (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, title varchar(240) not null,
            observed_fact varchar(5000) not null, category varchar(120) not null,
            severity varchar(40) not null, urgency varchar(40) not null,
            owner_user_id uuid not null, owner_display_name varchar(200) not null,
            target_resolution_date date not null, source_module varchar(80) not null,
            source_entity_type varchar(120) not null, source_entity_id uuid null,
            source_revision bigint null, source_snapshot varchar(1000) not null,
            materialized_from_risk_id uuid null,
            evidence_references_json jsonb not null default '[]'::jsonb,
            confidentiality varchar(40) not null, status varchar(40) not null,
            sla_due_at timestamptz null, sla_rule_version_id uuid null,
            resolution_note varchar(2000) null,
            closure_evidence_json jsonb not null default '[]'::jsonb,
            created_by uuid not null, created_at timestamptz not null,
            last_changed_by uuid null, last_changed_at timestamptz null,
            resolved_at timestamptz null, resolved_by uuid null,
            closed_at timestamptz null, closed_by uuid null,
            revision bigint not null, unique (tenant_id, project_id, number)
        );
        create index if not exists ix_issues_project_status_target
            on action_control.issues(tenant_id, project_id, status, target_resolution_date);

        create table if not exists action_control.decision_requests (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            number varchar(80) not null, question varchar(1000) not null,
            why_now varchar(2000) not null, required_by date not null,
            authority_user_id uuid not null, authority_display_name varchar(200) not null,
            known_facts_json jsonb not null default '[]'::jsonb,
            assumptions_json jsonb not null default '[]'::jsonb,
            predictions_json jsonb not null default '[]'::jsonb,
            options_json jsonb not null default '[]'::jsonb,
            recommendation varchar(2000) null,
            constraints_json jsonb not null default '[]'::jsonb,
            evidence_references_json jsonb not null default '[]'::jsonb,
            confidentiality varchar(40) not null, source_module varchar(80) not null,
            source_entity_type varchar(120) not null, source_entity_id uuid null,
            source_revision bigint null, source_snapshot varchar(1000) not null,
            status varchar(40) not null, information_request varchar(2000) null,
            decision_record_id uuid null, sla_due_at timestamptz null,
            sla_rule_version_id uuid null, created_by uuid not null,
            created_at timestamptz not null, last_changed_by uuid null,
            last_changed_at timestamptz null, revision bigint not null,
            unique (tenant_id, project_id, number)
        );
        create index if not exists ix_decision_requests_project_status_due
            on action_control.decision_requests(tenant_id, project_id, status, required_by);

        create table if not exists action_control.decisions (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            decision_request_id uuid not null, number varchar(80) not null,
            selected_option varchar(2000) not null, rationale varchar(4000) not null,
            conditions_json jsonb not null default '[]'::jsonb,
            channel varchar(40) not null, decided_at timestamptz not null,
            effective_date date null, decided_by uuid not null,
            decided_by_display_name varchar(200) not null,
            supersedes_decision_id uuid null, superseded_by_decision_id uuid null,
            effect_review varchar(4000) null,
            effect_evidence_json jsonb not null default '[]'::jsonb,
            status varchar(40) not null, recorded_at timestamptz not null,
            revision bigint not null, unique (tenant_id, project_id, number)
        );
        create index if not exists ix_decisions_request
            on action_control.decisions(tenant_id, project_id, decision_request_id);

        create table if not exists action_control.escalation_threads (
            id uuid primary key, tenant_id uuid not null, project_id uuid not null,
            thread_key varchar(180) not null, entity_type varchar(40) not null,
            entity_id uuid not null, entity_number varchar(80) not null,
            entity_title varchar(500) not null, reason varchar(40) not null,
            level integer not null check (level between 1 and 5),
            recipient_user_id uuid not null, recipient_display_name varchar(200) not null,
            confidentiality varchar(40) not null, status varchar(40) not null,
            occurrence_count integer not null, first_raised_at timestamptz not null,
            last_raised_at timestamptz not null, acknowledged_by uuid null,
            acknowledged_at timestamptz null, acknowledgement_note varchar(1000) null,
            revision bigint not null, unique (tenant_id, project_id, thread_key)
        );
        create index if not exists ix_escalations_project_status_recipient
            on action_control.escalation_threads(tenant_id, project_id, status, recipient_user_id);
        """;
}
