using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Planning.Migrations;

internal sealed class PlanningBaselineMigration : IDatabaseMigration
{
    public string ModuleName => "planning";

    public long Order => 551;

    public string Version => "20260911-002";

    public string Description => "Add versioned planning baselines and reviewed milestone progress";

    public string Sql => """
        create table if not exists planning.planning_baselines (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            version_code varchar(80) not null,
            title varchar(240) not null,
            kind varchar(40) not null,
            source_system varchar(120) null,
            source_reference varchar(500) null,
            definition_json jsonb not null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null
        );

        create unique index if not exists ux_planning_baselines_project_version
            on planning.planning_baselines(tenant_id, project_id, lower(version_code));
        create index if not exists ix_planning_baselines_project_status
            on planning.planning_baselines(tenant_id, project_id, status);

        create table if not exists planning.milestone_progress_updates (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            baseline_id uuid not null,
            baseline_entry_id uuid not null,
            status_date date not null,
            progress_percent numeric(7, 2) not null check (progress_percent >= 0 and progress_percent <= 100),
            evidence_reference varchar(500) not null,
            note varchar(2000) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            submitted_at timestamptz null,
            reviewed_by uuid null,
            reviewed_at timestamptz null,
            review_comment varchar(1000) null,
            revision bigint not null,
            constraint fk_milestone_updates_baseline
                foreign key (baseline_id) references planning.planning_baselines(id)
        );

        create index if not exists ix_milestone_updates_entry
            on planning.milestone_progress_updates(tenant_id, project_id, baseline_id, baseline_entry_id);
        create index if not exists ix_milestone_updates_project_status
            on planning.milestone_progress_updates(tenant_id, project_id, status);
        """;
}
