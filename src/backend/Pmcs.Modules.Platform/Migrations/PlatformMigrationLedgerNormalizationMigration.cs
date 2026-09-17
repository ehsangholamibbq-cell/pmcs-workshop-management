using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Platform.Migrations;

internal sealed class PlatformMigrationLedgerNormalizationMigration : IDatabaseMigration
{
    public string ModuleName => "platform";

    public long Order => 102;

    public string Version => "20260917-001";

    public string Description => "Normalize the historical field-operations migration ledger identity";

    public string Sql => """
        delete from foundation.schema_migrations as legacy
        where legacy.module = 'field_operations'
          and legacy.version = '20260911-004'
          and exists (
              select 1
              from foundation.schema_migrations as canonical
              where canonical.module = 'field-operations'
                and canonical.version = legacy.version
          );

        update foundation.schema_migrations
        set module = 'field-operations'
        where module = 'field_operations'
          and version = '20260911-004';
        """;
}
