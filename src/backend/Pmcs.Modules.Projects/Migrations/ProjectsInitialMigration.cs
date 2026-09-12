using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectsInitialMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 300;

    public string Version => "20260909-001";

    public string Description => "Create the project configuration aggregate";

    public string Sql => """
        create schema if not exists projects;

        create table if not exists projects.projects (
            id uuid primary key,
            tenant_id uuid not null,
            code varchar(32) not null,
            name varchar(200) not null,
            contract_model varchar(60) not null,
            planning_mode varchar(60) not null,
            budget_mode varchar(40) not null,
            quality_mode varchar(40) not null,
            hse_mode varchar(40) not null,
            time_zone varchar(100) not null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            revision bigint not null,
            unique (tenant_id, code)
        );
        create index if not exists ix_projects_tenant_status
            on projects.projects(tenant_id, status);
        """;
}
