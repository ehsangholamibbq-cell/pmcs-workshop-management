using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Collaboration.Migrations;

internal sealed class CollaborationInteractionMigration : IDatabaseMigration
{
    public string ModuleName => "collaboration";
    public long Order => 1301;
    public string Version => "20260928-002";
    public string Description => "Add scoped replies, mentions, reactions, pins and read cursors";

    public string Sql => """
        alter table collaboration.messages
            add column if not exists reply_to_message_id uuid null,
            add column if not exists mentioned_user_ids uuid[] not null default '{}',
            add column if not exists pinned_at timestamptz null,
            add column if not exists pinned_by uuid null;

        alter table collaboration.messages
            add constraint uq_collaboration_message_scope_id unique (tenant_id, project_id, id),
            add constraint fk_collaboration_reply_scope
                foreign key (tenant_id, project_id, reply_to_message_id)
                references collaboration.messages(tenant_id, project_id, id),
            add constraint ck_collaboration_mentions_count
                check (cardinality(mentioned_user_ids) <= 20),
            add constraint ck_collaboration_pin_pair
                check ((pinned_at is null) = (pinned_by is null));

        create table if not exists collaboration.reactions (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            message_id uuid not null,
            actor_user_id uuid not null,
            emoji varchar(16) not null,
            created_at timestamptz not null,
            constraint fk_collaboration_reaction_scope
                foreign key (tenant_id, project_id, message_id)
                references collaboration.messages(tenant_id, project_id, id),
            constraint ck_collaboration_reaction_emoji
                check (emoji in ('👍', '✅', '⚠️', '❤️')),
            unique (tenant_id, project_id, message_id, actor_user_id, emoji)
        );

        create table if not exists collaboration.read_cursors (
            tenant_id uuid not null,
            project_id uuid not null,
            user_id uuid not null,
            last_read_sequence bigint not null,
            updated_at timestamptz not null,
            primary key (tenant_id, project_id, user_id),
            constraint fk_collaboration_cursor_room
                foreign key (tenant_id, project_id)
                references collaboration.project_rooms(tenant_id, project_id),
            constraint ck_collaboration_cursor_positive check (last_read_sequence >= 0)
        );

        create index if not exists ix_collaboration_message_pins
            on collaboration.messages(tenant_id, project_id, pinned_at desc)
            where pinned_at is not null;
        """;
}
