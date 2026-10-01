using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectBootstrapMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 306;

    public string Version => "20260918-007";

    public string Description => "Create controlled project bootstrap plans with versioned preview and result evidence";

    public string Sql => """
        create table if not exists projects.project_bootstrap_plans (
            id uuid primary key,
            tenant_id uuid not null,
            source_project_id uuid not null,
            target_project_id uuid not null,
            conflict_policy varchar(40) not null,
            selected_categories jsonb not null,
            member_selections jsonb not null,
            status varchar(40) not null,
            contributor_catalog_version varchar(80) not null,
            preview_digest varchar(64) not null,
            membership_snapshot_token varchar(64) not null,
            preview_json jsonb not null,
            result_json jsonb null,
            source_revision bigint not null,
            target_revision bigint not null,
            created_by uuid not null,
            created_at timestamptz not null,
            previewed_at timestamptz null,
            preview_expires_at timestamptz null,
            executed_at timestamptz null,
            activated_at timestamptz null,
            revision bigint not null,
            constraint fk_project_bootstrap_source
                foreign key (tenant_id, source_project_id)
                references projects.projects(tenant_id, id),
            constraint fk_project_bootstrap_target
                foreign key (tenant_id, target_project_id)
                references projects.projects(tenant_id, id),
            constraint ck_project_bootstrap_source_target
                check (source_project_id <> target_project_id),
            constraint ck_project_bootstrap_preview_window
                check (preview_expires_at is null or previewed_at is not null and preview_expires_at > previewed_at),
            unique (tenant_id, target_project_id)
        );

        create index if not exists ix_project_bootstrap_source_status
            on projects.project_bootstrap_plans(tenant_id, source_project_id, status);
        create index if not exists ix_project_bootstrap_preview_expiry
            on projects.project_bootstrap_plans(tenant_id, preview_expires_at)
            where status = 'PreviewReady';
        """;
}
