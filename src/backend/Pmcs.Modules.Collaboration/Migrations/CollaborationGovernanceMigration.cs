using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Collaboration.Migrations;

internal sealed class CollaborationGovernanceMigration : IDatabaseMigration
{
    public string ModuleName => "collaboration";
    public long Order => 1303;
    public string Version => "20260928-004";
    public string Description => "Preserve revisions, tombstones, moderation and legal hold";

    public string Sql => """
        alter table collaboration.messages
            add column if not exists revision bigint not null default 1,
            add column if not exists edited_at timestamptz null,
            add column if not exists deleted_at timestamptz null,
            add column if not exists redacted_at timestamptz null,
            add column if not exists legal_hold boolean not null default false;
        alter table collaboration.messages
            add constraint ck_collaboration_revision_positive check (revision > 0),
            add constraint ck_collaboration_single_tombstone
                check (deleted_at is null or redacted_at is null);

        create table if not exists collaboration.message_revisions (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            message_id uuid not null,
            from_revision bigint not null,
            body varchar(4000) not null,
            action varchar(32) not null,
            actor_user_id uuid not null,
            occurred_at timestamptz not null,
            constraint fk_collaboration_revision_message
                foreign key (tenant_id, project_id, message_id)
                references collaboration.messages(tenant_id, project_id, id),
            constraint ck_collaboration_history_action
                check (action in ('Edited', 'Deleted', 'Redacted')),
            unique (tenant_id, project_id, message_id, from_revision)
        );

        create table if not exists collaboration.moderation_records (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            message_id uuid not null,
            message_revision bigint not null,
            action varchar(32) not null,
            reason varchar(500) not null,
            actor_user_id uuid not null,
            occurred_at timestamptz not null,
            constraint fk_collaboration_moderation_message
                foreign key (tenant_id, project_id, message_id)
                references collaboration.messages(tenant_id, project_id, id),
            constraint ck_collaboration_moderation_action
                check (action in ('Redacted', 'HoldApplied', 'HoldRemoved')),
            constraint ck_collaboration_moderation_reason
                check (length(trim(reason)) between 1 and 500)
        );
        create index if not exists ix_collaboration_moderation_message
            on collaboration.moderation_records(tenant_id, project_id, message_id, occurred_at);
        """;
}
