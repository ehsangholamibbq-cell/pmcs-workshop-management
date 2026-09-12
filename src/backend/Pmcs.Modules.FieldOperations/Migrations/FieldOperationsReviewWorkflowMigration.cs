using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.FieldOperations.Migrations;

internal sealed class FieldOperationsReviewWorkflowMigration : IDatabaseMigration
{
    public string ModuleName => "field-operations";

    public long Order => 420;

    public string Version => "20260909-003";

    public string Description => "Add daily report review workflow metadata";

    public string Sql => """
        alter table field_operations.daily_reports
            add column if not exists reviewed_by uuid null,
            add column if not exists reviewed_at timestamptz null,
            add column if not exists review_comment varchar(1000) null;

        create index if not exists ix_daily_reports_review_inbox
            on field_operations.daily_reports(tenant_id, project_id, status, submitted_at);
        """;
}
