using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Platform.Migrations;

internal sealed class PlatformInitialMigration : IDatabaseMigration
{
    public string ModuleName => "platform";

    public long Order => 100;

    public string Version => "20260909-001";

    public string Description => "Create audit, idempotency and outbox foundations";

    public string Sql => """
        create schema if not exists foundation;

        create table if not exists foundation.audit_events (
            event_id uuid primary key,
            tenant_id uuid not null,
            project_id uuid null,
            actor_user_id uuid not null,
            event_type varchar(160) not null,
            resource_type varchar(120) not null,
            resource_id varchar(120) not null,
            occurred_at timestamptz not null,
            data jsonb not null,
            correlation_id varchar(120) null
        );
        create index if not exists ix_audit_tenant_time
            on foundation.audit_events(tenant_id, occurred_at desc);

        create table if not exists foundation.idempotency_records (
            id uuid primary key,
            tenant_id uuid not null,
            key varchar(160) not null,
            operation varchar(160) not null,
            request_hash varchar(128) not null,
            status_code integer not null,
            response_body jsonb not null,
            created_at timestamptz not null,
            expires_at timestamptz not null,
            unique (tenant_id, key, operation)
        );

        create table if not exists foundation.outbox_messages (
            message_id uuid primary key,
            tenant_id uuid not null,
            project_id uuid null,
            event_type varchar(200) not null,
            event_version integer not null,
            occurred_at timestamptz not null,
            payload jsonb not null,
            correlation_id varchar(120) null,
            published_at timestamptz null,
            attempts integer not null default 0,
            last_error text null
        );
        create index if not exists ix_outbox_pending
            on foundation.outbox_messages(published_at, occurred_at);
        """;
}
