using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Collaboration.Migrations;

internal sealed class CollaborationConversionMigration : IDatabaseMigration
{
    public string ModuleName => "collaboration";
    public long Order => 1304;
    public string Version => "20260928-005";
    public string Description => "Preserve explicit official conversion lineage";

    public string Sql => """
        create table if not exists collaboration.message_conversions (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            message_id uuid not null,
            message_revision bigint not null check (message_revision > 0),
            destination_type varchar(40) not null,
            destination_id uuid not null,
            destination_reference varchar(500) not null,
            document_references_json jsonb not null default '[]'::jsonb,
            request_hash varchar(64) not null,
            confirmed_by uuid not null,
            confirmed_at timestamptz not null,
            constraint fk_collaboration_conversion_message
                foreign key (tenant_id, project_id, message_id)
                references collaboration.messages(tenant_id, project_id, id),
            unique (tenant_id, project_id, destination_type, destination_id)
        );
        create index if not exists ix_collaboration_conversion_source
            on collaboration.message_conversions(tenant_id, project_id, message_id, confirmed_at);
        """;
}
