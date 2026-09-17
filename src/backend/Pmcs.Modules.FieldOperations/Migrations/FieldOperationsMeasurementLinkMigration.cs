using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.FieldOperations.Migrations;

internal sealed class FieldOperationsMeasurementLinkMigration : IDatabaseMigration
{
    public string ModuleName => "field-operations";

    public long Order => 430;

    public string Version => "20260911-004";

    public string Description => "Link progress facts to optional independent measurement items";

    public string Sql => """
        alter table field_operations.daily_report_facts
            add column if not exists measurement_item_id uuid null;

        create index if not exists ix_daily_report_facts_measurement_item
            on field_operations.daily_report_facts(measurement_item_id)
            where measurement_item_id is not null;
        """;
}
