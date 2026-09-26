using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ProjectPeriodicReportCatalogMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";

    public long Order => 1202;

    public string Version => "20260920-003";

    public string Description =>
        "Publish the certified project-periodic catalog and pin accepted project profiles";

    public string Sql => """
        alter table reporting.report_definitions
            alter column parameter_schema_version type varchar(80);

        alter table reporting.report_template_versions
            alter column renderer_contract_version type varchar(80),
            alter column layout_contract_version type varchar(80);

        alter table reporting.report_runs
            add column if not exists pinned_project_profile jsonb null;

        do $$
        begin
            if not exists (
                select 1 from pg_constraint
                where conname = 'ck_reporting_run_pinned_project_profile'
                  and conrelid = 'reporting.report_runs'::regclass
            ) then
                alter table reporting.report_runs
                    add constraint ck_reporting_run_pinned_project_profile
                    check (pinned_project_profile is null or jsonb_typeof(pinned_project_profile) = 'object');
            end if;
        end $$;

        alter table reporting.report_definitions
            drop constraint if exists fk_reporting_definition_current_template;
        alter table reporting.report_definitions
            alter column current_template_version_id drop not null;

        insert into reporting.report_definitions(
            id, code, title, description, scope, classification, supported_formats,
            required_permissions, parameter_schema_version, status,
            current_template_version_id, created_at, updated_at)
        values (
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d2',
            'project-periodic-certified',
            'گزارش هفتگی و ماهانه رسمی پروژه',
            'تجمیع قابل ممیزی نسخه‌های رسمی گزارش روزانه در بازه هفتگی یا ماه شمسی',
            'Project',
            'Internal',
            '["Pdf","Xlsx"]'::jsonb,
            '["field.daily-reports.read"]'::jsonb,
            'pmcs.reporting.project-periodic.parameters/v1',
            'Active',
            null,
            '2026-09-20T00:00:00Z',
            '2026-09-20T00:00:00Z')
        on conflict (code) do nothing;

        insert into reporting.report_template_versions(
            id, definition_id, version, renderer_contract_version,
            layout_contract_version, content_digest, published_at, retired_at,
            page_size, orientation, locale, calendar)
        values (
            'a9e11d34-73ca-4e08-802d-041b8261f926',
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d2',
            '1.0.0',
            'pmcs.reporting.project-periodic.renderer/v1',
            'pmcs.reporting.project-periodic.layout/v1',
            'eca353e00f07fbdb054613768399496e137ab0deda731c5d319d5cd4ebd3be6b',
            '2026-09-20T00:00:00Z',
            null,
            'A4',
            'Portrait',
            'fa-IR',
            'Persian')
        on conflict (definition_id, version) do nothing;

        do $$
        begin
            if not exists (
                select 1
                from reporting.report_definitions
                where id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d2'
                  and code = 'project-periodic-certified'
                  and scope = 'Project'
                  and classification = 'Internal'
                  and supported_formats = '["Pdf","Xlsx"]'::jsonb
                  and required_permissions = '["field.daily-reports.read"]'::jsonb
                  and status = 'Active'
                  and parameter_schema_version = 'pmcs.reporting.project-periodic.parameters/v1'
            ) or not exists (
                select 1
                from reporting.report_template_versions
                where id = 'a9e11d34-73ca-4e08-802d-041b8261f926'
                  and definition_id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d2'
                  and version = '1.0.0'
                  and renderer_contract_version = 'pmcs.reporting.project-periodic.renderer/v1'
                  and layout_contract_version = 'pmcs.reporting.project-periodic.layout/v1'
                  and content_digest = 'eca353e00f07fbdb054613768399496e137ab0deda731c5d319d5cd4ebd3be6b'
                  and page_size = 'A4'
                  and orientation = 'Portrait'
                  and locale = 'fa-IR'
                  and calendar = 'Persian'
                  and retired_at is null
            ) then
                raise exception 'The project-periodic certified catalog conflicts with an existing record.';
            end if;
        end $$;

        update reporting.report_definitions
        set current_template_version_id = 'a9e11d34-73ca-4e08-802d-041b8261f926',
            updated_at = '2026-09-20T00:00:00Z'
        where id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d2';

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;
        alter table reporting.report_definitions
            add constraint fk_reporting_definition_current_template
            foreign key (current_template_version_id)
            references reporting.report_template_versions(id);
        """;
}
