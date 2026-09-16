using Pmcs.BuildingBlocks.Persistence;

namespace Pmcs.Modules.Sync.Migrations;

internal sealed class SyncRecoveryDiagnosticsMigration : IDatabaseMigration
{
    public string ModuleName => "sync-control";
    public long Order => 451;
    public string Version => "20260916-001";
    public string Description => "Add payload-free operation receipts for sync recovery diagnostics";

    public string Sql => """
        create table if not exists sync_control.operation_receipts (
            id uuid primary key,
            tenant_id uuid not null,
            project_id uuid not null,
            user_id uuid not null,
            device_id varchar(120) not null,
            operation_id varchar(26) not null,
            local_sequence bigint not null,
            entity_type varchar(120) not null,
            entity_id uuid not null,
            command_type varchar(160) not null,
            status varchar(40) not null,
            code varchar(160) null,
            conflict_id uuid null,
            server_revision bigint null,
            attempt_count integer not null,
            replay_count integer not null,
            first_attempt_at timestamptz not null,
            last_attempt_at timestamptz not null,
            last_correlation_id varchar(120) not null,
            unique (tenant_id, user_id, device_id, operation_id)
        );
        create index if not exists ix_sync_operation_receipts_diagnostics
            on sync_control.operation_receipts(tenant_id, project_id, user_id, device_id, last_attempt_at desc);
        """;
}
