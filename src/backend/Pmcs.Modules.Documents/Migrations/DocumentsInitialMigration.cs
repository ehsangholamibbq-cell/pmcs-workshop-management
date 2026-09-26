using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Documents.Migrations;

internal sealed class DocumentsInitialMigration : IDatabaseMigration
{
    public string ModuleName => "documents";

    public long Order => 1100;

    public string Version => "20260918-001";

    public string Description => "Create shared document assets, upload sessions and quarantine metadata";

    public string Sql => """
        create schema if not exists documents;

        create table if not exists documents.assets (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid null,
            owner_type varchar(80) not null,
            owner_id uuid not null,
            version_number integer not null,
            original_file_name varchar(255) not null,
            content_type varchar(160) not null,
            size_bytes bigint not null,
            sha256 varchar(64) not null,
            object_key varchar(700) not null,
            classification varchar(40) not null,
            retention_policy varchar(40) not null,
            retain_until timestamptz null,
            legal_hold boolean not null,
            status varchar(40) not null,
            scan_verdict varchar(40) not null,
            scan_provider varchar(120) null,
            scan_details varchar(500) null,
            created_by uuid not null,
            created_at timestamptz not null,
            upload_expires_at timestamptz not null,
            uploaded_at timestamptz null,
            storage_etag varchar(200) null,
            released_by uuid null,
            released_at timestamptz null,
            classified_by uuid null,
            classified_at timestamptz null,
            deleted_at timestamptz null,
            revision bigint not null,
            constraint ck_documents_version check (version_number > 0),
            constraint ck_documents_size check (size_bytes > 0 and size_bytes <= 26214400),
            constraint ck_documents_sha256 check (sha256 ~ '^[0-9a-f]{64}$'),
            constraint ck_documents_owner_scope check (
                (owner_type in ('ProjectGeneral', 'ProjectChat', 'ReportOutput', 'TechnicalDocument') and project_id is not null)
                or
                (owner_type in ('MemberProfile', 'LoginExperience') and project_id is null)
            ),
            constraint ck_documents_retention check (
                (retention_policy = 'Permanent' and retain_until is null)
                or
                (retention_policy in ('Standard', 'LongTerm') and retain_until is not null)
            ),
            unique (tenant_id, owner_type, owner_id, version_number),
            unique (tenant_id, object_key)
        );
        create index if not exists ix_documents_project_created
            on documents.assets(tenant_id, project_id, created_at desc);
        create index if not exists ix_documents_owner
            on documents.assets(tenant_id, owner_type, owner_id, version_number desc);
        create index if not exists ix_documents_quarantine
            on documents.assets(tenant_id, status, upload_expires_at)
            where status in ('PendingUpload', 'Quarantined');
        """;
}
