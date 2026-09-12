using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectProcurementMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 302;

    public string Version => "20260909-003";

    public string Description => "Add optional procurement capability configuration";

    public string Sql => """
        alter table projects.projects
            add column if not exists procurement_mode varchar(40) not null default 'Active';
        """;
}
