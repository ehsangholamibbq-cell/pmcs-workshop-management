using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class PortfolioSummaryReportCatalogMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";
    public long Order => 1211;
    public string Version => "20260927-012";
    public string Description => "Publish the certified tenant Portfolio Summary catalog and template";

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
            'f1000000-0000-4000-8000-000000000001',
            'portfolio-summary-certified',
            'گزارش رسمی خلاصهٔ مجموعه پروژه‌ها',
            'نمای مجاز و قابل ممیزی وضعیت عملیاتی و exposure مستقل هر ارز در Portfolio',
            'Portfolio', 'Confidential', '["Pdf","Xlsx"]'::jsonb,
            '["portfolio.read","project-state.read"]'::jsonb,
            'pmcs.reporting.portfolio-summary.parameters/v1', 'Active', null,
            '2026-09-27T00:00:00Z', '2026-09-27T00:00:00Z')
        on conflict (code) do nothing;

        insert into reporting.report_template_versions(
            id, definition_id, version, renderer_contract_version,
            layout_contract_version, content_digest, published_at, retired_at,
            page_size, orientation, locale, calendar)
        values (
            'f1000000-0000-4000-8000-000000000002',
            'f1000000-0000-4000-8000-000000000001', '1.0.0',
            'pmcs.reporting.portfolio-summary.renderer/v1',
            'pmcs.reporting.portfolio-summary.layout/v1',
            '9a2df8c64c6f4d461130f00cd106822d529e713de0a97b9ee72c90468bd8115f',
            '2026-09-27T00:00:00Z', null, 'A4', 'Landscape', 'fa-IR', 'Persian')
        on conflict (definition_id, version) do nothing;

        do $$ begin
            if not exists (
                select 1 from reporting.report_definitions
                where id = 'f1000000-0000-4000-8000-000000000001'
                  and code = 'portfolio-summary-certified'
                  and scope = 'Portfolio' and classification = 'Confidential'
                  and supported_formats = '["Pdf","Xlsx"]'::jsonb
                  and required_permissions = '["portfolio.read","project-state.read"]'::jsonb
                  and parameter_schema_version = 'pmcs.reporting.portfolio-summary.parameters/v1'
                  and status = 'Active'
            ) or not exists (
                select 1 from reporting.report_template_versions
                where id = 'f1000000-0000-4000-8000-000000000002'
                  and definition_id = 'f1000000-0000-4000-8000-000000000001'
                  and version = '1.0.0'
                  and renderer_contract_version = 'pmcs.reporting.portfolio-summary.renderer/v1'
                  and layout_contract_version = 'pmcs.reporting.portfolio-summary.layout/v1'
                  and content_digest = '9a2df8c64c6f4d461130f00cd106822d529e713de0a97b9ee72c90468bd8115f'
                  and page_size = 'A4' and orientation = 'Landscape'
                  and locale = 'fa-IR' and calendar = 'Persian' and retired_at is null
            ) then
                raise exception 'The certified Portfolio Summary catalog conflicts with an existing record.';
            end if;
        end $$;

        update reporting.report_definitions
        set current_template_version_id = 'f1000000-0000-4000-8000-000000000002',
            updated_at = '2026-09-27T00:00:00Z'
        where id = 'f1000000-0000-4000-8000-000000000001';

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;
        alter table reporting.report_definitions
            add constraint fk_reporting_definition_current_template
            foreign key (current_template_version_id)
            references reporting.report_template_versions(id);
        """;
}
