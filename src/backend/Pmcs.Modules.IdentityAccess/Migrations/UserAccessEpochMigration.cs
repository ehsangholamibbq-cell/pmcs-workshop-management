using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.IdentityAccess.Migrations;

internal sealed class UserAccessEpochMigration : IDatabaseMigration
{
    public string ModuleName => "identity-access";

    public long Order => 203;

    public string Version => "20260911-004";

    public string Description => "Invalidate access tokens issued before an account status transition";

    public string Sql => """
        alter table identity_access.users
            add column if not exists access_valid_after timestamptz null;
        update identity_access.users
            set access_valid_after = created_at - interval '1 second'
            where access_valid_after is null;
        alter table identity_access.users
            alter column access_valid_after set not null;
        """;
}
