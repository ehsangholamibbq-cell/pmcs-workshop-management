using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ReportingInitialMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";

    public long Order => 1200;

    public string Version => "20260918-001";

    public string Description => "Create certified reporting catalog, run, snapshot and output stores";

    public string Sql => """
        create schema if not exists reporting;

        create table if not exists reporting.report_definitions (
            id uuid primary key,
            code varchar(120) not null unique,
            title varchar(200) not null,
            description varchar(1000) not null,
            scope varchar(40) not null,
            classification varchar(40) not null,
            supported_formats jsonb not null,
            required_permissions jsonb not null,
            parameter_schema_version varchar(40) not null,
            status varchar(40) not null,
            current_template_version_id uuid null,
            created_at timestamptz not null,
            updated_at timestamptz not null,
            constraint ck_reporting_definition_scope check (scope in ('Project', 'Tenant')),
            constraint ck_reporting_definition_classification check (
                classification in ('Internal', 'Confidential', 'Restricted')),
            constraint ck_reporting_definition_status check (status in ('Active', 'Retired')),
            constraint ck_reporting_definition_formats check (jsonb_typeof(supported_formats) = 'array'),
            constraint ck_reporting_definition_permissions check (jsonb_typeof(required_permissions) = 'array')
        );

        create table if not exists reporting.report_template_versions (
            id uuid primary key,
            definition_id uuid not null references reporting.report_definitions(id),
            version varchar(40) not null,
            renderer_contract_version varchar(40) not null,
            layout_contract_version varchar(40) not null,
            content_digest varchar(64) not null,
            published_at timestamptz not null,
            retired_at timestamptz null,
            page_size varchar(20) not null,
            orientation varchar(20) not null,
            locale varchar(20) not null,
            calendar varchar(20) not null,
            constraint ck_reporting_template_digest check (content_digest ~ '^[0-9a-f]{64}$'),
            unique (definition_id, version)
        );

        insert into reporting.report_definitions(
            id, code, title, description, scope, classification, supported_formats,
            required_permissions, parameter_schema_version, status,
            current_template_version_id, created_at, updated_at)
        values (
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d1',
            'daily-report-certified',
            'گزارش روزانه رسمی',
            'نسخه رسمی گزارش روزانه و زنجیره اصلاحات آن',
            'Project',
            'Internal',
            '["Pdf","Xlsx"]'::jsonb,
            '["field.daily-reports.read"]'::jsonb,
            '1.0',
            'Active',
            null,
            '2026-09-18T00:00:00Z',
            '2026-09-18T00:00:00Z')
        on conflict (code) do nothing;

        insert into reporting.report_template_versions(
            id, definition_id, version, renderer_contract_version,
            layout_contract_version, content_digest, published_at, retired_at,
            page_size, orientation, locale, calendar)
        values (
            'a9e11d34-73ca-4e08-802d-041b8261f925',
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d1',
            '1.0.0',
            '1.0',
            '1.0',
            '4d417a9e4b9a95a6db517105b7c248046d6eb15f7262725b9ad476f064ebb4cf',
            '2026-09-18T00:00:00Z',
            null,
            'A4',
            'Portrait',
            'fa-IR',
            'Persian')
        on conflict (definition_id, version) do nothing;

        update reporting.report_definitions
        set current_template_version_id = 'a9e11d34-73ca-4e08-802d-041b8261f925'
        where code = 'daily-report-certified' and current_template_version_id is null;

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;

        do $$
        begin
            if not exists (
                select 1 from pg_constraint
                where conname = 'fk_reporting_definition_current_template'
                  and conrelid = 'reporting.report_definitions'::regclass
            ) then
                alter table reporting.report_definitions
                    add constraint fk_reporting_definition_current_template
                    foreign key (current_template_version_id)
                    references reporting.report_template_versions(id);
            end if;
        end $$;

        create table if not exists reporting.report_runs (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            definition_id uuid not null references reporting.report_definitions(id),
            definition_code varchar(120) not null,
            template_version_id uuid not null references reporting.report_template_versions(id),
            template_version varchar(40) not null,
            parameters_json jsonb not null,
            parameters_hash varchar(64) not null,
            requested_formats jsonb not null,
            as_of_utc timestamptz not null,
            project_time_zone varchar(120) not null,
            requested_by uuid not null,
            request_permission_snapshot jsonb not null,
            processing_permission_snapshot jsonb null,
            correlation_id varchar(160) not null,
            idempotency_key_hash varchar(64) not null,
            status varchar(40) not null,
            pipeline_stage varchar(40) not null,
            attempt_count integer not null,
            created_at timestamptz not null,
            claimed_at timestamptz null,
            started_at timestamptz null,
            completed_at timestamptz null,
            next_attempt_at timestamptz null,
            diagnostic_code varchar(120) null,
            diagnostic_detail varchar(500) null,
            snapshot_id uuid null,
            output_count integer not null,
            revision bigint not null,
            constraint ck_reporting_run_status check (
                status in ('Queued', 'Processing', 'Succeeded', 'Failed', 'Cancelled')),
            constraint ck_reporting_run_stage check (
                pipeline_stage in ('Queued', 'BuildingSnapshot', 'SnapshotReady', 'Rendering', 'Complete', 'Failed', 'Cancelled')),
            constraint ck_reporting_run_attempts check (attempt_count >= 0),
            constraint ck_reporting_run_outputs check (output_count >= 0),
            constraint ck_reporting_run_parameters_hash check (parameters_hash ~ '^[0-9a-f]{64}$'),
            constraint ck_reporting_run_idempotency_hash check (idempotency_key_hash ~ '^[0-9a-f]{64}$'),
            constraint ck_reporting_run_formats check (jsonb_typeof(requested_formats) = 'array'),
            unique (tenant_id, idempotency_key_hash)
        );
        create index if not exists ix_reporting_runs_project_created
            on reporting.report_runs(tenant_id, project_id, created_at desc, id desc);
        create index if not exists ix_reporting_runs_ready
            on reporting.report_runs(status, next_attempt_at, created_at)
            where status in ('Queued', 'Processing');

        create table if not exists reporting.report_snapshots (
            id uuid primary key,
            run_id uuid not null unique references reporting.report_runs(id),
            tenant_id uuid not null,
            project_id uuid not null,
            schema_version varchar(80) not null,
            data_status varchar(40) not null,
            payload_json jsonb not null,
            source_manifest_json jsonb not null,
            sha256 varchar(64) not null,
            source_manifest_sha256 varchar(64) not null,
            classification varchar(40) not null,
            built_at timestamptz not null,
            source_cutoff_utc timestamptz not null,
            constraint ck_reporting_snapshot_data_status check (
                data_status in ('Available', 'NoData', 'InsufficientData', 'NotConfigured')),
            constraint ck_reporting_snapshot_classification check (
                classification in ('Internal', 'Confidential', 'Restricted')),
            constraint ck_reporting_snapshot_sha256 check (sha256 ~ '^[0-9a-f]{64}$'),
            constraint ck_reporting_source_manifest_sha256 check (source_manifest_sha256 ~ '^[0-9a-f]{64}$')
        );
        create index if not exists ix_reporting_snapshots_project_built
            on reporting.report_snapshots(tenant_id, project_id, built_at desc);

        do $$
        begin
            if not exists (
                select 1 from pg_constraint
                where conname = 'fk_reporting_run_snapshot'
                  and conrelid = 'reporting.report_runs'::regclass
            ) then
                alter table reporting.report_runs
                    add constraint fk_reporting_run_snapshot
                    foreign key (snapshot_id) references reporting.report_snapshots(id);
            end if;
        end $$;

        create table if not exists reporting.report_outputs (
            id uuid primary key,
            run_id uuid not null references reporting.report_runs(id),
            snapshot_id uuid not null references reporting.report_snapshots(id),
            template_version_id uuid not null references reporting.report_template_versions(id),
            tenant_id uuid not null,
            project_id uuid not null,
            format varchar(20) not null,
            content_type varchar(160) not null,
            file_name varchar(255) not null,
            generated_document_id uuid not null unique,
            size_bytes bigint not null,
            sha256 varchar(64) not null,
            verification_code varchar(80) not null unique,
            manifest_sha256 varchar(64) not null,
            classification varchar(40) not null,
            retention_policy varchar(40) not null,
            archive_state varchar(40) not null,
            created_at timestamptz not null,
            archived_at timestamptz null,
            constraint ck_reporting_output_format check (format in ('Pdf', 'Xlsx', 'Csv')),
            constraint ck_reporting_output_size check (size_bytes > 0),
            constraint ck_reporting_output_sha256 check (sha256 ~ '^[0-9a-f]{64}$'),
            constraint ck_reporting_output_manifest_sha256 check (manifest_sha256 ~ '^[0-9a-f]{64}$'),
            constraint ck_reporting_output_classification check (
                classification in ('Internal', 'Confidential', 'Restricted')),
            constraint ck_reporting_output_archive check (archive_state in ('Active', 'Archived')),
            unique (run_id, format)
        );
        create index if not exists ix_reporting_outputs_project_created
            on reporting.report_outputs(tenant_id, project_id, created_at desc);
        """;
}
