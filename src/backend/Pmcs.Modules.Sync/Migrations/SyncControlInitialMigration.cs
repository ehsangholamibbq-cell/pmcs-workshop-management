using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Sync.Migrations;

internal sealed class SyncControlInitialMigration : IDatabaseMigration
{
    public string ModuleName => "sync-control";
    public long Order => 450;
    public string Version => "20260912-001";
    public string Description => "Create device, offline lease, sync session, change feed, checkpoint and conflict control";

    public string Sql => """
        create schema if not exists sync_control;

        create table if not exists sync_control.devices (
            id uuid primary key,
            tenant_id uuid not null,
            user_id uuid not null,
            device_id varchar(120) not null,
            display_name varchar(160) not null,
            platform varchar(120) not null,
            app_version varchar(40) not null,
            status varchar(40) not null,
            registered_at timestamptz not null,
            last_seen_at timestamptz not null,
            revoked_at timestamptz null,
            revoked_by uuid null,
            revocation_reason varchar(500) null,
            revision bigint not null,
            unique (tenant_id, user_id, device_id)
        );
        create index if not exists ix_sync_devices_tenant_status_seen
            on sync_control.devices(tenant_id, status, last_seen_at desc);

        create table if not exists sync_control.offline_leases (
            id uuid primary key,
            tenant_id uuid not null,
            user_id uuid not null,
            project_id uuid not null,
            device_id varchar(120) not null,
            authorization_version bigint not null,
            issued_at timestamptz not null,
            expires_at timestamptz not null,
            status varchar(40) not null,
            revoked_at timestamptz null,
            unique (tenant_id, user_id, device_id, project_id, authorization_version)
        );
        create index if not exists ix_sync_leases_scope_status
            on sync_control.offline_leases(tenant_id, user_id, device_id, project_id, status);

        create table if not exists sync_control.sessions (
            id uuid primary key,
            tenant_id uuid not null,
            user_id uuid not null,
            project_id uuid not null,
            device_id varchar(120) not null,
            lease_id uuid not null,
            starting_sequence bigint not null,
            protocol_version integer not null,
            local_schema_version integer not null,
            issued_at timestamptz not null,
            expires_at timestamptz not null,
            bootstrap_required boolean not null,
            clock_skew_seconds bigint not null,
            closed_at timestamptz null
        );
        create index if not exists ix_sync_sessions_scope_expiry
            on sync_control.sessions(tenant_id, user_id, device_id, project_id, expires_at desc);

        create table if not exists sync_control.conflicts (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            user_id uuid not null,
            device_id varchar(120) not null,
            operation_id varchar(26) not null,
            entity_type varchar(120) not null,
            entity_id uuid not null,
            command_type varchar(160) not null,
            local_base_revision bigint null,
            server_revision bigint null,
            reason_code varchar(160) not null,
            local_intent jsonb not null,
            server_projection jsonb null,
            status varchar(40) not null,
            detected_at timestamptz not null,
            resolved_at timestamptz null,
            resolved_by uuid null,
            resolution_type varchar(40) null,
            resolution_comment varchar(1000) null,
            replacement_operation_id varchar(26) null,
            revision bigint not null,
            unique (tenant_id, user_id, device_id, operation_id)
        );
        create index if not exists ix_sync_conflicts_project_status_age
            on sync_control.conflicts(tenant_id, project_id, status, detected_at);

        create table if not exists sync_control.conflict_resolutions (
            id uuid primary key,
            conflict_id uuid not null references sync_control.conflicts(id),
            tenant_id uuid not null,
            project_id uuid not null,
            resolved_by uuid not null,
            resolution_type varchar(40) not null,
            replacement_operation_id varchar(26) null,
            comment varchar(1000) null,
            resolved_at timestamptz not null,
            unique (conflict_id)
        );

        create table if not exists sync_control.change_feed (
            sequence bigint generated always as identity primary key,
            id uuid not null unique,
            tenant_id uuid not null,
            project_id uuid not null,
            actor_user_id uuid not null,
            source_device_id varchar(120) not null,
            operation_id varchar(26) null,
            entity_type varchar(120) not null,
            entity_id uuid not null,
            change_type varchar(120) not null,
            revision bigint null,
            projection jsonb not null,
            classification varchar(40) not null,
            effective_at timestamptz not null,
            server_at timestamptz not null,
            correlation_id varchar(120) not null,
            unique (tenant_id, actor_user_id, source_device_id, operation_id)
        );
        create index if not exists ix_sync_change_feed_scope_sequence
            on sync_control.change_feed(tenant_id, project_id, sequence);

        create table if not exists sync_control.device_checkpoints (
            id uuid primary key,
            tenant_id uuid not null,
            user_id uuid not null,
            project_id uuid not null,
            device_id varchar(120) not null,
            dataset varchar(80) not null,
            last_sequence bigint not null,
            current_token varchar(36) not null,
            advanced_at timestamptz not null,
            unique (tenant_id, user_id, project_id, device_id, dataset)
        );

        create table if not exists sync_control.checkpoint_offers (
            id uuid primary key,
            session_id uuid not null references sync_control.sessions(id),
            tenant_id uuid not null,
            user_id uuid not null,
            project_id uuid not null,
            device_id varchar(120) not null,
            dataset varchar(80) not null,
            sequence bigint not null,
            created_at timestamptz not null,
            expires_at timestamptz not null,
            consumed_at timestamptz null
        );
        create index if not exists ix_sync_checkpoint_offers_session_expiry
            on sync_control.checkpoint_offers(session_id, expires_at);
        """;
}
