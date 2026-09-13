using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Projects.Migrations;

internal sealed class ProjectActivationMetadataMigration : IDatabaseMigration
{
    public string ModuleName => "projects";

    public long Order => 304;

    public string Version => "20260913-005";

    public string Description => "Persist the actor and instant of controlled project activation";

    public string Sql => """
        alter table projects.projects
            add column if not exists activated_by uuid null,
            add column if not exists activated_at timestamptz null;

        alter table projects.projects
            drop constraint if exists ck_projects_activation_metadata;
        alter table projects.projects
            add constraint ck_projects_activation_metadata check (
                (activated_by is null and activated_at is null) or
                (activated_by is not null and activated_at is not null));
        """;
}
