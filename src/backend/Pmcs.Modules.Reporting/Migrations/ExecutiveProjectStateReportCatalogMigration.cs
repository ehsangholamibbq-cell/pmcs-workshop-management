using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ExecutiveProjectStateReportCatalogMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";

    public long Order => 1203;

    public string Version => "20260920-004";

    public string Description =>
        "Publish the certified executive project state catalog and template";

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
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d3',
            'executive-project-state-certified',
            'گزارش مدیریتی رسمی وضعیت پروژه',
            'نمای ثابت و قابل ممیزی Snapshot رسمی Project State در cutoff گزارش',
            'Project',
            'Internal',
            '["Pdf","Xlsx"]'::jsonb,
            '["project-state.read"]'::jsonb,
            'pmcs.reporting.executive-project-state.parameters/v1',
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
            'a9e11d34-73ca-4e08-802d-041b8261f927',
            '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d3',
            '1.0.0',
            'pmcs.reporting.executive-project-state.renderer/v1',
            'pmcs.reporting.executive-project-state.layout/v1',
            '4bf4f1f5de92eda854ab16702fc87aaebae951eea17ef023569cc338a5ce7d7a',
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
                where id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d3'
                  and code = 'executive-project-state-certified'
                  and scope = 'Project'
                  and classification = 'Internal'
                  and supported_formats = '["Pdf","Xlsx"]'::jsonb
                  and required_permissions = '["project-state.read"]'::jsonb
                  and status = 'Active'
                  and parameter_schema_version = 'pmcs.reporting.executive-project-state.parameters/v1'
            ) or not exists (
                select 1
                from reporting.report_template_versions
                where id = 'a9e11d34-73ca-4e08-802d-041b8261f927'
                  and definition_id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d3'
                  and version = '1.0.0'
                  and renderer_contract_version = 'pmcs.reporting.executive-project-state.renderer/v1'
                  and layout_contract_version = 'pmcs.reporting.executive-project-state.layout/v1'
                  and content_digest = '4bf4f1f5de92eda854ab16702fc87aaebae951eea17ef023569cc338a5ce7d7a'
                  and page_size = 'A4'
                  and orientation = 'Portrait'
                  and locale = 'fa-IR'
                  and calendar = 'Persian'
                  and retired_at is null
            ) then
                raise exception 'The executive-project-state certified catalog conflicts with an existing record.';
            end if;
        end $$;

        update reporting.report_definitions
        set current_template_version_id = 'a9e11d34-73ca-4e08-802d-041b8261f927',
            updated_at = '2026-09-20T00:00:00Z'
        where id = '8d30b9d5-4a9b-4be5-9cb8-69e9b5c663d3';

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;
        alter table reporting.report_definitions
            add constraint fk_reporting_definition_current_template
            foreign key (current_template_version_id)
            references reporting.report_template_versions(id);
        """;
}
