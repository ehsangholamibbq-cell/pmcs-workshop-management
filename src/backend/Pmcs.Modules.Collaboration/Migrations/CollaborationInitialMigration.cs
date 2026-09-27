using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Collaboration.Migrations;

internal sealed class CollaborationInitialMigration : IDatabaseMigration
{
    public string ModuleName => "collaboration";
    public long Order => 1300;
    public string Version => "20260928-001";
    public string Description => "Add bounded default project rooms and ordered idempotent messages";

    public string Sql => """
        create schema if not exists collaboration;

        create table if not exists collaboration.project_rooms (
            project_id uuid primary key,
            tenant_id uuid not null,
            last_sequence bigint not null default 0,
            created_at timestamptz not null,
            constraint ck_collaboration_room_sequence check (last_sequence >= 0),
            unique (tenant_id, project_id)
        );

        create table if not exists collaboration.messages (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            sequence bigint not null,
            author_user_id uuid not null,
            client_message_id uuid not null,
            body varchar(4000) not null,
            request_hash varchar(64) not null,
            created_at timestamptz not null,
            constraint fk_collaboration_room foreign key (tenant_id, project_id)
                references collaboration.project_rooms(tenant_id, project_id),
            constraint ck_collaboration_message_sequence check (sequence > 0),
            constraint ck_collaboration_message_body check (length(trim(body)) between 1 and 4000),
            constraint ck_collaboration_message_hash check (request_hash ~ '^[0-9a-f]{64}$'),
            unique (tenant_id, project_id, sequence),
            unique (tenant_id, project_id, author_user_id, client_message_id)
        );

        create index if not exists ix_collaboration_messages_page
            on collaboration.messages(tenant_id, project_id, sequence desc);
        """;
}
