using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.IdentityAccess.Migrations;

internal sealed class IdentityAdministrationMigration : IDatabaseMigration
{
    public string ModuleName => "identity-access";

    public long Order => 201;

    public string Version => "20260911-002";

    public string Description => "Create durable user invitation and project assignment tables";

    public string Sql => """
        create table if not exists identity_access.user_invitations (
            id uuid primary key,
            tenant_id uuid not null,
            display_name varchar(200) not null,
            email varchar(320) not null,
            tenant_role varchar(60) not null,
            status varchar(40) not null,
            created_by uuid not null,
            created_at timestamptz not null,
            expires_at timestamptz not null,
            sent_at timestamptz null,
            next_attempt_at timestamptz null,
            user_id uuid null,
            attempts integer not null,
            last_error_code varchar(120) null,
            revision bigint not null
        );
        create index if not exists ix_user_invitations_tenant_email
            on identity_access.user_invitations(tenant_id, email);
        create index if not exists ix_user_invitations_dispatch
            on identity_access.user_invitations(status, next_attempt_at);
        create unique index if not exists ux_user_invitations_open_email
            on identity_access.user_invitations(tenant_id, email)
            where status in ('Queued', 'Processing', 'RetryScheduled');

        create table if not exists identity_access.invitation_project_assignments (
            id uuid primary key,
            invitation_id uuid not null references identity_access.user_invitations(id) on delete cascade,
            tenant_id uuid not null,
            project_id uuid not null,
            role_code varchar(100) not null,
            unique(invitation_id, project_id)
        );
        create index if not exists ix_invitation_project_assignments_tenant
            on identity_access.invitation_project_assignments(tenant_id, project_id);

        create table if not exists identity_access.identity_provider_operations (
            id uuid primary key,
            tenant_id uuid not null,
            user_id uuid not null,
            operation_type varchar(60) not null,
            status varchar(40) not null,
            created_at timestamptz not null,
            completed_at timestamptz null,
            next_attempt_at timestamptz null,
            attempts integer not null,
            last_error_code varchar(120) null,
            revision bigint not null
        );
        create index if not exists ix_identity_provider_operations_dispatch
            on identity_access.identity_provider_operations(status, next_attempt_at);
        create index if not exists ix_identity_provider_operations_user
            on identity_access.identity_provider_operations(tenant_id, user_id);
        """;
}
