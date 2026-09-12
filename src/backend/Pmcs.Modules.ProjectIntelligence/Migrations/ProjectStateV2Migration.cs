using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ProjectIntelligence.Migrations;

internal sealed class ProjectStateV2Migration : IDatabaseMigration
{
    public string ModuleName => "project-intelligence";

    public long Order => 501;

    public string Version => "20260909-002";

    public string Description => "Track project configuration revision for project-state-v2";

    public string Sql => """
        alter table project_intelligence.project_state_snapshots
            add column if not exists project_configuration_revision bigint not null default 0;
        """;
}
