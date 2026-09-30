using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Intelligence.Migrations;

internal sealed class IntelligenceAdministrationGrantMigration : IDatabaseMigration
{
    public string ModuleName => "intelligence";
    public long Order => 601;
    public string Version => "20261001-001";
    public string Description => "Create explicit scoped INT1 administration grants, default empty";

    public string Sql => """
        create table if not exists intelligence.administration_grants (
            id uuid primary key,
            actor_tenant_id uuid not null,
            actor_id uuid not null,
            scope_tenant_id uuid null,
            permission varchar(100) not null,
            issued_by uuid not null,
            starts_at timestamptz not null,
            expires_at timestamptz null,
            revoked_at timestamptz null,
            revision bigint not null,
            constraint ck_intelligence_grant_scope check (
                (scope_tenant_id is null and permission in
                    ('intelligence.providers.manage', 'intelligence.profiles.publish', 'intelligence.catalog.read')) or
                (scope_tenant_id is not null and permission in
                    ('intelligence.profiles.select', 'intelligence.catalog.read'))),
            constraint ck_intelligence_grant_expiry check (expires_at is null or expires_at > starts_at),
            constraint ck_intelligence_grant_revision check (revision > 0)
        );
        create index if not exists ix_intelligence_grants_actor
            on intelligence.administration_grants(actor_tenant_id, actor_id, permission, scope_tenant_id)
            where revoked_at is null;
        """;
}
