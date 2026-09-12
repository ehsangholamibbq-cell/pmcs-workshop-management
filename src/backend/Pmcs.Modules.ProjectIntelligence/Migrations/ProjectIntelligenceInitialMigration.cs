using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ProjectIntelligence.Migrations;

internal sealed class ProjectIntelligenceInitialMigration : IDatabaseMigration
{
    public string ModuleName => "project-intelligence";

    public long Order => 500;

    public string Version => "20260909-001";

    public string Description => "Create versioned project state snapshots and attention items";

    public string Sql => """
        create schema if not exists project_intelligence;

        create table if not exists project_intelligence.project_state_snapshots (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            project_code varchar(32) not null,
            project_name varchar(200) not null,
            calculation_version varchar(80) not null,
            as_of_date date not null,
            window_start date not null,
            window_end date not null,
            calculated_at timestamptz not null,
            assessment_scope varchar(80) not null,
            is_partial boolean not null,
            operational_status varchar(40) not null,
            coverage_status varchar(40) not null,
            freshness_status varchar(40) not null,
            confidence_status varchar(40) not null,
            coverage_basis varchar(80) not null,
            coverage_percent numeric(5, 1) not null,
            expected_report_days integer not null,
            approved_report_days integer not null,
            last_approved_report_date date null,
            approved_fact_count integer not null,
            progress_fact_count integer not null,
            labor_fact_count integer not null,
            equipment_fact_count integer not null,
            material_fact_count integer not null,
            issue_count integer not null,
            stoppage_count integer not null,
            high_impact_count integer not null,
            critical_impact_count integer not null,
            oldest_attention_age_days integer null,
            contract_state varchar(40) not null,
            planning_state varchar(40) not null,
            budget_state varchar(40) not null,
            quality_state varchar(40) not null,
            hse_state varchar(40) not null,
            source_max_changed_at timestamptz null,
            constraint ck_project_state_coverage check (coverage_percent >= 0 and coverage_percent <= 100),
            constraint ck_project_state_report_days check (
                expected_report_days > 0 and approved_report_days >= 0 and approved_report_days <= expected_report_days)
        );
        create index if not exists ix_project_state_latest
            on project_intelligence.project_state_snapshots(tenant_id, project_id, calculated_at desc);

        create table if not exists project_intelligence.project_state_attention_items (
            id uuid primary key,
            snapshot_id uuid not null references project_intelligence.project_state_snapshots(id) on delete cascade,
            source_report_id uuid not null,
            source_fact_id uuid not null,
            report_date date not null,
            kind varchar(40) not null,
            description varchar(1000) not null,
            category varchar(120) null,
            location_name varchar(200) null,
            observed_impact varchar(40) null,
            priority varchar(40) not null,
            age_days integer not null,
            age_band varchar(40) not null,
            status varchar(40) not null,
            reference_code varchar(120) null,
            constraint ck_project_attention_age check (age_days >= 0),
            unique (snapshot_id, source_fact_id)
        );
        create index if not exists ix_project_attention_snapshot_priority
            on project_intelligence.project_state_attention_items(snapshot_id, priority, age_days desc);
        """;
}
