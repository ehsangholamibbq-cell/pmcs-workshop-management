using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.FieldOperations.Migrations;

internal sealed class FieldOperationsStructuredFactsMigration : IDatabaseMigration
{
    public string ModuleName => "field-operations";

    public long Order => 410;

    public string Version => "20260909-002";

    public string Description => "Add structured fields to daily report facts";

    public string Sql => """
        alter table field_operations.daily_report_facts
            add column if not exists category varchar(120) null,
            add column if not exists location_name varchar(200) null,
            add column if not exists resource_count integer null,
            add column if not exists hours numeric(18, 2) null,
            add column if not exists impact_level varchar(40) null,
            add column if not exists reference_code varchar(120) null;

        alter table field_operations.daily_report_facts
            drop constraint if exists ck_daily_fact_resource_count,
            add constraint ck_daily_fact_resource_count
                check (resource_count is null or resource_count > 0),
            drop constraint if exists ck_daily_fact_hours,
            add constraint ck_daily_fact_hours
                check (hours is null or (hours >= 0 and hours <= 100000));
        """;
}
