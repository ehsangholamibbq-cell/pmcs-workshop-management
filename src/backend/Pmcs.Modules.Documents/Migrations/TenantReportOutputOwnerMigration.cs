using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Documents.Migrations;

internal sealed class TenantReportOutputOwnerMigration : IDatabaseMigration
{
    public string ModuleName => "documents";
    public long Order => 1101;
    public string Version => "20260927-001";
    public string Description => "Separate tenant-owned generated report outputs from project documents";

    public string Sql => """
        alter table documents.assets
            drop constraint ck_documents_owner_scope;
        alter table documents.assets
            add constraint ck_documents_owner_scope check (
                (owner_type in ('ProjectGeneral', 'ProjectChat', 'ReportOutput', 'TechnicalDocument')
                    and project_id is not null)
                or
                (owner_type in ('MemberProfile', 'LoginExperience', 'TenantReportOutput')
                    and project_id is null));
        """;
}
