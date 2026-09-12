using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Evidence.Migrations;

internal sealed class EvidenceInitialMigration : IDatabaseMigration
{
    public string ModuleName => "evidence";

    public long Order => 600;

    public string Version => "20260909-001";

    public string Description => "Create immutable evidence metadata and resumable upload sessions";

    public string Sql => """
        create schema if not exists evidence;

        create table if not exists evidence.files (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            daily_report_id uuid not null,
            daily_fact_id uuid null,
            original_file_name varchar(255) not null,
            content_type varchar(120) not null,
            size_bytes bigint not null,
            sha256 varchar(64) not null,
            object_key varchar(700) not null,
            status varchar(40) not null,
            captured_at_device timestamptz null,
            created_by uuid not null,
            created_at timestamptz not null,
            upload_expires_at timestamptz not null,
            uploaded_at timestamptz null,
            storage_etag varchar(200) null,
            revision bigint not null,
            constraint ck_evidence_size check (size_bytes > 0 and size_bytes <= 26214400),
            constraint ck_evidence_sha256 check (char_length(sha256) = 64),
            unique (tenant_id, project_id, object_key)
        );
        create index if not exists ix_evidence_project_created
            on evidence.files(tenant_id, project_id, created_at desc);
        create index if not exists ix_evidence_daily_report
            on evidence.files(tenant_id, project_id, daily_report_id);
        create index if not exists ix_evidence_daily_fact
            on evidence.files(tenant_id, project_id, daily_fact_id)
            where daily_fact_id is not null;
        """;
}
