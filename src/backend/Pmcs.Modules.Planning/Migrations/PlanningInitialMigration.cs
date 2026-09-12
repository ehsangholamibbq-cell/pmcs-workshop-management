using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Planning.Migrations;

internal sealed class PlanningInitialMigration : IDatabaseMigration
{
    public string ModuleName => "planning";

    public long Order => 550;

    public string Version => "20260911-001";

    public string Description => "Create the independent measurement catalog used by progress facts";

    public string Sql => """
        create schema if not exists planning;

        create table if not exists planning.measurement_items (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            code varchar(80) not null,
            title varchar(240) not null,
            unit varchar(40) not null,
            target_quantity numeric(24, 6) null check (target_quantity > 0),
            notes varchar(2000) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            last_modified_by uuid not null,
            last_modified_at timestamptz not null,
            revision bigint not null
        );

        create unique index if not exists ux_measurement_items_project_code
            on planning.measurement_items(tenant_id, project_id, lower(code));
        create index if not exists ix_measurement_items_project_status
            on planning.measurement_items(tenant_id, project_id, status);
        """;
}
