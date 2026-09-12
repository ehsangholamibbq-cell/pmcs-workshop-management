using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.IdentityAccess.Migrations;

internal sealed class IdentityAdministrationCleanupMigration : IDatabaseMigration
{
    public string ModuleName => "identity-access";

    public long Order => 202;

    public string Version => "20260911-003";

    public string Description => "Track provisioned identities and make invitation cleanup durable";

    public string Sql => """
        alter table identity_access.user_invitations
            add column if not exists provider_user_id uuid null;
        create index if not exists ix_user_invitations_provider_user
            on identity_access.user_invitations(provider_user_id)
            where provider_user_id is not null;
        create unique index if not exists ux_identity_provider_delete_open
            on identity_access.identity_provider_operations(user_id)
            where operation_type = 'Delete'
                and status in ('Queued', 'Processing', 'RetryScheduled');
        """;
}
