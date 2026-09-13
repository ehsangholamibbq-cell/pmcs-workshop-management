using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.WorkManagement.Migrations;

internal sealed class WorkManagementInitialMigration : IDatabaseMigration
{
    public string ModuleName => "work-management";
    public long Order => 1_050;
    public string Version => "20260913-001";
    public string Description => "Create permission-scoped in-app notification inbox";

    public string Sql => """
        create schema if not exists work_management;

        create table if not exists work_management.notifications (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            recipient_user_id uuid not null,
            deduplication_key varchar(240) not null,
            category varchar(80) not null,
            title varchar(240) not null,
            body varchar(2000) not null,
            target_type varchar(80) not null,
            target_id uuid not null,
            occurred_at timestamptz not null,
            read_at timestamptz null,
            acknowledged_at timestamptz null,
            revision bigint not null,
            unique (tenant_id, recipient_user_id, deduplication_key)
        );
        create index if not exists ix_notifications_recipient_time
            on work_management.notifications(tenant_id, project_id, recipient_user_id, occurred_at desc);
        create index if not exists ix_notifications_unread
            on work_management.notifications(tenant_id, project_id, recipient_user_id, occurred_at desc)
            where read_at is null;
        """;
}
