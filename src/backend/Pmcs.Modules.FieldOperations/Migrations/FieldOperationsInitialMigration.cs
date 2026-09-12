using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.FieldOperations.Migrations;

internal sealed class FieldOperationsInitialMigration : IDatabaseMigration
{
    public string ModuleName => "field-operations";

    public long Order => 400;

    public string Version => "20260909-001";

    public string Description => "Create daily report and observed fact tables";

    public string Sql => """
        create schema if not exists field_operations;

        create table if not exists field_operations.daily_reports (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            report_date date not null,
            location_name varchar(200) null,
            narrative varchar(4000) null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            last_modified_at timestamptz not null,
            submitted_at timestamptz null,
            revision bigint not null,
            unique (tenant_id, project_id, report_date)
        );
        create index if not exists ix_daily_reports_tenant_project_status
            on field_operations.daily_reports(tenant_id, project_id, status);

        create table if not exists field_operations.daily_report_facts (
            id uuid primary key,
            daily_report_id uuid not null references field_operations.daily_reports(id) on delete cascade,
            kind varchar(40) not null,
            description varchar(1000) not null,
            quantity numeric(24, 6) null,
            unit varchar(40) null,
            created_by uuid not null,
            created_at timestamptz not null
        );
        create index if not exists ix_daily_report_facts_report
            on field_operations.daily_report_facts(daily_report_id);
        """;
}
