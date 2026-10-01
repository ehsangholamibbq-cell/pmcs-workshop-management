using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ProjectQualityHseReportCatalogMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";
    public long Order => 1208;
    public string Version => "20260927-009";
    public string Description => "Publish the certified Quality and HSE report catalog and template";

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
            'b37df4b8-2bc4-4d12-8d57-cf9f3517f208',
            'project-quality-hse-certified',
            'گزارش رسمی کیفیت و ایمنی پروژه',
            'نمای قابل ممیزی وضعیت مستقل کیفیت و HSE پروژه در cutoff گزارش',
            'Project', 'Confidential', '["Pdf","Xlsx"]'::jsonb,
            '["quality.read","hse.read","hse.confidential.read"]'::jsonb,
            'pmcs.reporting.project-quality-hse.parameters/v1', 'Active', null,
            '2026-09-27T00:00:00Z', '2026-09-27T00:00:00Z')
        on conflict (code) do nothing;

        insert into reporting.report_template_versions(
            id, definition_id, version, renderer_contract_version,
            layout_contract_version, content_digest, published_at, retired_at,
            page_size, orientation, locale, calendar)
        values (
            'c7e345c1-3fd0-4a21-9b3e-7ed67917f208',
            'b37df4b8-2bc4-4d12-8d57-cf9f3517f208', '1.0.0',
            'pmcs.reporting.project-quality-hse.renderer/v1',
            'pmcs.reporting.project-quality-hse.layout/v1',
            '5bd7a0ea06984076cfbfed7715329a59b1afeffbcade4378afefc1413c287a61',
            '2026-09-27T00:00:00Z', null, 'A4', 'Landscape', 'fa-IR', 'Persian')
        on conflict (definition_id, version) do nothing;

        do $$ begin
            if not exists (
                select 1 from reporting.report_definitions
                where id = 'b37df4b8-2bc4-4d12-8d57-cf9f3517f208'
                  and code = 'project-quality-hse-certified'
                  and scope = 'Project' and classification = 'Confidential'
                  and supported_formats = '["Pdf","Xlsx"]'::jsonb
                  and required_permissions = '["quality.read","hse.read","hse.confidential.read"]'::jsonb
                  and status = 'Active'
                  and parameter_schema_version = 'pmcs.reporting.project-quality-hse.parameters/v1'
            ) or not exists (
                select 1 from reporting.report_template_versions
                where id = 'c7e345c1-3fd0-4a21-9b3e-7ed67917f208'
                  and definition_id = 'b37df4b8-2bc4-4d12-8d57-cf9f3517f208'
                  and version = '1.0.0'
                  and renderer_contract_version = 'pmcs.reporting.project-quality-hse.renderer/v1'
                  and layout_contract_version = 'pmcs.reporting.project-quality-hse.layout/v1'
                  and content_digest = '5bd7a0ea06984076cfbfed7715329a59b1afeffbcade4378afefc1413c287a61'
                  and page_size = 'A4' and orientation = 'Landscape'
                  and locale = 'fa-IR' and calendar = 'Persian' and retired_at is null
            ) then
                raise exception 'The project-quality-hse certified catalog conflicts with an existing record.';
            end if;
        end $$;

        update reporting.report_definitions
        set current_template_version_id = 'c7e345c1-3fd0-4a21-9b3e-7ed67917f208',
            updated_at = '2026-09-27T00:00:00Z'
        where id = 'b37df4b8-2bc4-4d12-8d57-cf9f3517f208';

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;
        alter table reporting.report_definitions
            add constraint fk_reporting_definition_current_template
            foreign key (current_template_version_id)
            references reporting.report_template_versions(id);
        """;
}
