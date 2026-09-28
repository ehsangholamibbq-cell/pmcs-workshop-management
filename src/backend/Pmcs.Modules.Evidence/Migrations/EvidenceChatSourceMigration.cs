using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Evidence.Migrations;

internal sealed class EvidenceChatSourceMigration : IDatabaseMigration
{
    public string ModuleName => "evidence";
    public long Order => 1306;
    public string Version => "20260928-002";
    public string Description => "Retain released project chat source on converted evidence";
    public string Sql => """
        alter table evidence.files
            add column if not exists source_message_id uuid null,
            add column if not exists source_document_id uuid null,
            add column if not exists source_document_version integer null;
        alter table evidence.files
            add constraint ck_evidence_chat_source_complete check (
                (source_message_id is null and source_document_id is null and source_document_version is null)
                or (source_message_id is not null and source_document_id is not null and
                    source_document_version is not null and source_document_version > 0 and
                    status = 'Uploaded'));
        create index if not exists ix_evidence_chat_source
            on evidence.files(tenant_id, project_id, source_message_id, source_document_id)
            where source_message_id is not null;
        """;
}
