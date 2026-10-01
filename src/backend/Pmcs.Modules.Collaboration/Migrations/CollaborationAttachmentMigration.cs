using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Collaboration.Migrations;

internal sealed class CollaborationAttachmentMigration : IDatabaseMigration
{
    public string ModuleName => "collaboration";
    public long Order => 1302;
    public string Version => "20260928-003";
    public string Description => "Bind released shared documents to scoped project messages";

    public string Sql => """
        create table if not exists collaboration.message_attachments (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            message_id uuid not null,
            document_id uuid not null,
            document_sha256 varchar(64) not null,
            document_version integer not null,
            attached_by uuid not null,
            attached_at timestamptz not null,
            constraint fk_collaboration_attachment_message
                foreign key (tenant_id, project_id, message_id)
                references collaboration.messages(tenant_id, project_id, id),
            constraint ck_collaboration_attachment_hash
                check (document_sha256 ~ '^[0-9a-f]{64}$'),
            constraint ck_collaboration_attachment_version check (document_version > 0),
            unique (tenant_id, project_id, document_id)
        );
        create index if not exists ix_collaboration_attachments_message
            on collaboration.message_attachments(tenant_id, project_id, message_id, attached_at);
        """;
}
