using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ProjectFinancialPositionReportCatalogMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";

    public long Order => 1205;

    public string Version => "20260921-006";

    public string Description =>
        "Publish the certified project financial position catalog and template";

    public string Sql => """
        alter table reporting.report_definitions
            drop constraint if exists fk_reporting_definition_current_template;
        alter table reporting.report_definitions
            alter column current_template_version_id drop not null;

        insert into reporting.report_definitions(
            id, code, title, description, scope, classification, supported_formats,
            required_permissions, parameter_schema_version, status,
            current_template_version_id, created_at, updated_at)
        values (
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d5',
            'project-financial-position-certified',
            'گزارش رسمی وضعیت مالی پروژه',
            'نمای قابل ممیزی وجوه، تعهدات، سررسید و بودجه رسمی پروژه در cutoff گزارش',
            'Project',
            'Confidential',
            '["Pdf","Xlsx"]'::jsonb,
            '["financial-state.read","finance.records.read","finance.obligations.read","budget.baselines.read"]'::jsonb,
            'pmcs.reporting.project-financial-position.parameters/v1',
            'Active',
            null,
            '2026-09-21T00:00:00Z',
            '2026-09-21T00:00:00Z')
        on conflict (code) do nothing;

        insert into reporting.report_template_versions(
            id, definition_id, version, renderer_contract_version,
            layout_contract_version, content_digest, published_at, retired_at,
            page_size, orientation, locale, calendar)
        values (
            'a9e11d34-73ca-4e08-802d-041b8261f929',
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d5',
            '1.0.0',
            'pmcs.reporting.project-financial-position.renderer/v1',
            'pmcs.reporting.project-financial-position.layout/v1',
            'e6ad4cbf2559d825d70b1579e687e7f9ce15020afaf697692e263f18480f18e4',
            '2026-09-21T00:00:00Z',
            null,
            'A4',
            'Landscape',
            'fa-IR',
            'Persian')
        on conflict (definition_id, version) do nothing;

        do $$
        begin
            if not exists (
                select 1
                from reporting.report_definitions
                where id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d5'
                  and code = 'project-financial-position-certified'
                  and scope = 'Project'
                  and classification = 'Confidential'
                  and supported_formats = '["Pdf","Xlsx"]'::jsonb
                  and required_permissions = '["financial-state.read","finance.records.read","finance.obligations.read","budget.baselines.read"]'::jsonb
                  and status = 'Active'
                  and parameter_schema_version = 'pmcs.reporting.project-financial-position.parameters/v1'
            ) or not exists (
                select 1
                from reporting.report_template_versions
                where id = 'a9e11d34-73ca-4e08-802d-041b8261f929'
                  and definition_id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d5'
                  and version = '1.0.0'
                  and renderer_contract_version = 'pmcs.reporting.project-financial-position.renderer/v1'
                  and layout_contract_version = 'pmcs.reporting.project-financial-position.layout/v1'
                  and content_digest = 'e6ad4cbf2559d825d70b1579e687e7f9ce15020afaf697692e263f18480f18e4'
                  and page_size = 'A4'
                  and orientation = 'Landscape'
                  and locale = 'fa-IR'
                  and calendar = 'Persian'
                  and retired_at is null
            ) then
                raise exception 'The project-financial-position certified catalog conflicts with an existing record.';
            end if;
        end $$;

        update reporting.report_definitions
        set current_template_version_id = 'a9e11d34-73ca-4e08-802d-041b8261f929',
            updated_at = '2026-09-21T00:00:00Z'
        where id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d5';

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;
        alter table reporting.report_definitions
            add constraint fk_reporting_definition_current_template
            foreign key (current_template_version_id)
            references reporting.report_template_versions(id);
        """;
}
