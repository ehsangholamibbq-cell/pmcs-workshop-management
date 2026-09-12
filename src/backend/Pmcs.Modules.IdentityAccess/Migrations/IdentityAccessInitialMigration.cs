using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.IdentityAccess.Migrations;

internal sealed class IdentityAccessInitialMigration : IDatabaseMigration
{
    public string ModuleName => "identity-access";

    public long Order => 200;

    public string Version => "20260909-001";

    public string Description => "Create tenant, user and project membership tables";

    public string Sql => """
        create schema if not exists identity_access;

        create table if not exists identity_access.tenants (
            id uuid primary key,
            name varchar(200) not null,
            status varchar(40) not null,
            created_at timestamptz not null,
            revision bigint not null
        );

        create table if not exists identity_access.users (
            id uuid primary key,
            tenant_id uuid not null,
            display_name varchar(200) not null,
            email varchar(320) not null,
            tenant_role varchar(60) not null,
            status varchar(40) not null,
            created_at timestamptz not null,
            revision bigint not null,
            unique (tenant_id, email)
        );
        create index if not exists ix_users_tenant on identity_access.users(tenant_id);

        create table if not exists identity_access.project_memberships (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            user_id uuid not null,
            role_code varchar(100) not null,
            starts_at timestamptz not null,
            ends_at timestamptz null,
            status varchar(40) not null,
            revision bigint not null,
            unique (tenant_id, project_id, user_id)
        );
        create index if not exists ix_memberships_user
            on identity_access.project_memberships(tenant_id, user_id, status);
        """;
}
