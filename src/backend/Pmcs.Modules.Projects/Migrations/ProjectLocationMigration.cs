using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectLocationMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 303;

    public string Version => "20260913-004";

    public string Description => "Create the minimal project Location and LBS registry";

    public string Sql => """
        create unique index if not exists ux_projects_tenant_id_id
            on projects.projects(tenant_id, id);

        create table if not exists projects.project_locations (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            code varchar(40) not null,
            name varchar(200) not null,
            parent_location_id uuid null,
            status varchar(30) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            changed_at timestamptz not null,
            revision bigint not null,
            constraint fk_project_locations_project
                foreign key (tenant_id, project_id)
                references projects.projects(tenant_id, id),
            constraint ck_project_locations_root_hierarchy check (
                (code = 'ROOT' and parent_location_id is null) or
                (code <> 'ROOT' and parent_location_id is not null)),
            unique (tenant_id, project_id, code)
        );

        create unique index if not exists ux_project_locations_scope_id
            on projects.project_locations(tenant_id, project_id, id);
        create index if not exists ix_project_locations_parent
            on projects.project_locations(tenant_id, project_id, parent_location_id);
        create index if not exists ix_project_locations_status
            on projects.project_locations(tenant_id, project_id, status);

        do $$
        begin
            if not exists (
                select 1 from pg_constraint
                where conname = 'fk_project_locations_parent_scope'
                    and conrelid = 'projects.project_locations'::regclass
            ) then
                alter table projects.project_locations
                    add constraint fk_project_locations_parent_scope
                    foreign key (tenant_id, project_id, parent_location_id)
                    references projects.project_locations(tenant_id, project_id, id);
            end if;
        end $$;

        insert into projects.project_locations(
            id, tenant_id, project_id, code, name, parent_location_id, status,
            created_by, created_at, changed_at, revision)
        select
            md5(project.id::text || ':ROOT')::uuid,
            project.tenant_id,
            project.id,
            'ROOT',
            'کل پروژه',
            null,
            'Active',
            project.created_by,
            project.created_at,
            project.created_at,
            1
        from projects.projects project
        where not exists (
            select 1 from projects.project_locations location
            where location.tenant_id = project.tenant_id
                and location.project_id = project.id
                and location.code = 'ROOT'
        );
        """;
}
