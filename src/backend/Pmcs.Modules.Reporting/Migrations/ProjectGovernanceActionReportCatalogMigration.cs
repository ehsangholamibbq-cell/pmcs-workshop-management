using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Reporting.Migrations;

internal sealed class ProjectGovernanceActionReportCatalogMigration : IDatabaseMigration
{
    public string ModuleName => "reporting";
    public long Order => 1209;
    public string Version => "20260927-010";
    public string Description => "Publish the certified Governance and Action report catalog and template";

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
            'f0900000-0000-4000-8000-000000000001',
            'project-governance-action-certified',
            'گزارش رسمی راهبری و اقدام پروژه',
            'نمای قابل ممیزی Issue، Risk، Decision، Escalation و Action در cutoff گزارش',
            'Project', 'Confidential', '["Pdf","Xlsx"]'::jsonb,
            '["governance.read","governance.sensitive.read","actions.read"]'::jsonb,
            'pmcs.reporting.project-governance-action.parameters/v1', 'Active', null,
            '2026-09-27T00:00:00Z', '2026-09-27T00:00:00Z')
        on conflict (code) do nothing;

        insert into reporting.report_template_versions(
            id, definition_id, version, renderer_contract_version,
            layout_contract_version, content_digest, published_at, retired_at,
            page_size, orientation, locale, calendar)
        values (
            'f0900000-0000-4000-8000-000000000002',
            'f0900000-0000-4000-8000-000000000001', '1.0.0',
            'pmcs.reporting.project-governance-action.renderer/v1',
            'pmcs.reporting.project-governance-action.layout/v1',
            '7fcb7af589562b09850491fe88d7067b0e20297d8fdb03148b0408db97b52a01',
            '2026-09-27T00:00:00Z', null, 'A4', 'Landscape', 'fa-IR', 'Persian')
        on conflict (definition_id, version) do nothing;

        do $$ begin
            if not exists (
                select 1 from reporting.report_definitions
                where id = 'f0900000-0000-4000-8000-000000000001'
                  and code = 'project-governance-action-certified'
                  and scope = 'Project' and classification = 'Confidential'
                  and supported_formats = '["Pdf","Xlsx"]'::jsonb
                  and required_permissions = '["governance.read","governance.sensitive.read","actions.read"]'::jsonb
                  and status = 'Active'
                  and parameter_schema_version = 'pmcs.reporting.project-governance-action.parameters/v1'
            ) or not exists (
                select 1 from reporting.report_template_versions
                where id = 'f0900000-0000-4000-8000-000000000002'
                  and definition_id = 'f0900000-0000-4000-8000-000000000001'
                  and version = '1.0.0'
                  and renderer_contract_version = 'pmcs.reporting.project-governance-action.renderer/v1'
                  and layout_contract_version = 'pmcs.reporting.project-governance-action.layout/v1'
                  and content_digest = '7fcb7af589562b09850491fe88d7067b0e20297d8fdb03148b0408db97b52a01'
                  and page_size = 'A4' and orientation = 'Landscape'
                  and locale = 'fa-IR' and calendar = 'Persian' and retired_at is null
            ) then
                raise exception 'The project-governance-action certified catalog conflicts with an existing record.';
            end if;
        end $$;

        update reporting.report_definitions
        set current_template_version_id = 'f0900000-0000-4000-8000-000000000002',
            updated_at = '2026-09-27T00:00:00Z'
        where id = 'f0900000-0000-4000-8000-000000000001';

        alter table reporting.report_definitions
            alter column current_template_version_id set not null;
        alter table reporting.report_definitions
            add constraint fk_reporting_definition_current_template
            foreign key (current_template_version_id)
            references reporting.report_template_versions(id);
        """;
}
