using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.FieldOperations.Migrations;

internal sealed class FieldOperationsLocationLinkMigration : IDatabaseMigration
{
    public string ModuleName => "field-operations";

    public long Order => 431;

    public string Version => "20260913-005";

    public string Description => "Link observed facts to the module-owned project Location registry";

    public string Sql => """
        alter table field_operations.daily_report_facts
            add column if not exists location_id uuid null;

        create index if not exists ix_daily_report_facts_location
            on field_operations.daily_report_facts(location_id)
            where location_id is not null;
        """;
}
