using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Platform.Migrations;

internal sealed class PlatformIdempotencyRetentionMigration : IDatabaseMigration
{
    public string ModuleName => "platform";

    public long Order => 101;

    public string Version => "20260913-002";

    public string Description => "Index idempotency receipt expiry for bounded retention";

    public string Sql => """
        create index if not exists ix_idempotency_records_expiry
            on foundation.idempotency_records(expires_at);
        """;
}
