using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ProjectIntelligence.Migrations;

internal sealed class ProjectStateLocationLinkMigration : IDatabaseMigration
{
    public string ModuleName => "project-intelligence";

    public long Order => 502;

    public string Version => "20260913-003";

    public string Description => "Preserve the stable project Location identity in attention snapshots";

    public string Sql => """
        alter table project_intelligence.project_state_attention_items
            add column if not exists location_id uuid null;

        create index if not exists ix_project_attention_snapshot_location
            on project_intelligence.project_state_attention_items(snapshot_id, location_id)
            where location_id is not null;
        """;
}
