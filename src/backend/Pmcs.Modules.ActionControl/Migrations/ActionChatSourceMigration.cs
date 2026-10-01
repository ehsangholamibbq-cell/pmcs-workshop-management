using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.ActionControl.Migrations;

internal sealed class ActionChatSourceMigration : IDatabaseMigration
{
    public string ModuleName => "action-control";
    public long Order => 1305;
    public string Version => "20260928-004";
    public string Description => "Distinguish message-derived Actions from approved daily facts";

    public string Sql => """
        alter table action_control.actions
            alter column source_fact_id drop not null,
            add column if not exists source_message_id uuid null;
        alter table action_control.actions
            add constraint ck_action_exactly_one_source
                check ((source_fact_id is null) <> (source_message_id is null));
        create unique index if not exists ux_actions_message_source
            on action_control.actions(tenant_id, project_id, source_message_id)
            where source_message_id is not null;
        """;
}
