using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Sync.Domain;

namespace Pmcs.Modules.Sync.Persistence;

internal sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredSyncDevice> Devices => Set<RegisteredSyncDevice>();
    public DbSet<OfflineAuthorizationLease> OfflineLeases => Set<OfflineAuthorizationLease>();
    public DbSet<SyncSession> Sessions => Set<SyncSession>();
    public DbSet<SyncConflictCase> Conflicts => Set<SyncConflictCase>();
    public DbSet<SyncConflictResolution> ConflictResolutions => Set<SyncConflictResolution>();
    public DbSet<SyncChangeFeedEntry> ChangeFeed => Set<SyncChangeFeedEntry>();
    public DbSet<DeviceCheckpoint> DeviceCheckpoints => Set<DeviceCheckpoint>();
    public DbSet<CheckpointOffer> CheckpointOffers => Set<CheckpointOffer>();
    public DbSet<SyncOperationReceipt> OperationReceipts => Set<SyncOperationReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sync_control");

        modelBuilder.Entity<RegisteredSyncDevice>(builder =>
        {
            builder.ToTable("devices");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160);
            builder.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(120);
            builder.Property(x => x.AppVersion).HasColumnName("app_version").HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.RegisteredAt).HasColumnName("registered_at");
            builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
            builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            builder.Property(x => x.RevokedBy).HasColumnName("revoked_by");
            builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(500);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.Status, x.LastSeenAt });
        });

        modelBuilder.Entity<OfflineAuthorizationLease>(builder =>
        {
            builder.ToTable("offline_leases");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.AuthorizationVersion).HasColumnName("authorization_version");
            builder.Property(x => x.IssuedAt).HasColumnName("issued_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId, x.ProjectId, x.AuthorizationVersion }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId, x.ProjectId, x.Status });
        });

        modelBuilder.Entity<SyncSession>(builder =>
        {
            builder.ToTable("sessions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.LeaseId).HasColumnName("lease_id");
            builder.Property(x => x.StartingSequence).HasColumnName("starting_sequence");
            builder.Property(x => x.ProtocolVersion).HasColumnName("protocol_version");
            builder.Property(x => x.LocalSchemaVersion).HasColumnName("local_schema_version");
            builder.Property(x => x.IssuedAt).HasColumnName("issued_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.BootstrapRequired).HasColumnName("bootstrap_required");
            builder.Property(x => x.ClockSkewSeconds).HasColumnName("clock_skew_seconds");
            builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId, x.ProjectId, x.ExpiresAt });
        });

        modelBuilder.Entity<SyncConflictCase>(builder =>
        {
            builder.ToTable("conflicts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.OperationId).HasColumnName("operation_id").HasMaxLength(26);
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            builder.Property(x => x.EntityId).HasColumnName("entity_id");
            builder.Property(x => x.CommandType).HasColumnName("command_type").HasMaxLength(160);
            builder.Property(x => x.LocalBaseRevision).HasColumnName("local_base_revision");
            builder.Property(x => x.ServerRevision).HasColumnName("server_revision");
            builder.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(160);
            builder.Property(x => x.LocalIntentJson).HasColumnName("local_intent").HasColumnType("jsonb");
            builder.Property(x => x.ServerProjectionJson).HasColumnName("server_projection").HasColumnType("jsonb");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.DetectedAt).HasColumnName("detected_at");
            builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
            builder.Property(x => x.ResolutionType).HasColumnName("resolution_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ResolutionComment).HasColumnName("resolution_comment").HasMaxLength(1000);
            builder.Property(x => x.ReplacementOperationId).HasColumnName("replacement_operation_id").HasMaxLength(26);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId, x.OperationId }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.DetectedAt });
        });

        modelBuilder.Entity<SyncConflictResolution>(builder =>
        {
            builder.ToTable("conflict_resolutions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.ConflictId).HasColumnName("conflict_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
            builder.Property(x => x.ResolutionType).HasColumnName("resolution_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ReplacementOperationId).HasColumnName("replacement_operation_id").HasMaxLength(26);
            builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
            builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            builder.HasIndex(x => x.ConflictId).IsUnique();
        });

        modelBuilder.Entity<SyncChangeFeedEntry>(builder =>
        {
            builder.ToTable("change_feed");
            builder.HasKey(x => x.Sequence);
            builder.Property(x => x.Sequence).HasColumnName("sequence").ValueGeneratedOnAdd();
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            builder.Property(x => x.SourceDeviceId).HasColumnName("source_device_id").HasMaxLength(120);
            builder.Property(x => x.OperationId).HasColumnName("operation_id").HasMaxLength(26);
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            builder.Property(x => x.EntityId).HasColumnName("entity_id");
            builder.Property(x => x.ChangeType).HasColumnName("change_type").HasMaxLength(120);
            builder.Property(x => x.Revision).HasColumnName("revision");
            builder.Property(x => x.ProjectionJson).HasColumnName("projection").HasColumnType("jsonb");
            builder.Property(x => x.Classification).HasColumnName("classification").HasMaxLength(40);
            builder.Property(x => x.EffectiveAt).HasColumnName("effective_at");
            builder.Property(x => x.ServerAt).HasColumnName("server_at");
            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(120);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Sequence });
            builder.HasIndex(x => new { x.TenantId, x.ActorUserId, x.SourceDeviceId, x.OperationId }).IsUnique();
        });

        modelBuilder.Entity<DeviceCheckpoint>(builder =>
        {
            builder.ToTable("device_checkpoints");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.Dataset).HasColumnName("dataset").HasMaxLength(80);
            builder.Property(x => x.LastSequence).HasColumnName("last_sequence");
            builder.Property(x => x.CurrentToken).HasColumnName("current_token").HasMaxLength(36);
            builder.Property(x => x.AdvancedAt).HasColumnName("advanced_at");
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.ProjectId, x.DeviceId, x.Dataset }).IsUnique();
        });

        modelBuilder.Entity<CheckpointOffer>(builder =>
        {
            builder.ToTable("checkpoint_offers");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.SessionId).HasColumnName("session_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.Dataset).HasColumnName("dataset").HasMaxLength(80);
            builder.Property(x => x.Sequence).HasColumnName("sequence");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
            builder.HasIndex(x => new { x.SessionId, x.ExpiresAt });
        });

        modelBuilder.Entity<SyncOperationReceipt>(builder =>
        {
            builder.ToTable("operation_receipts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id").HasMaxLength(120);
            builder.Property(x => x.OperationId).HasColumnName("operation_id").HasMaxLength(26);
            builder.Property(x => x.LocalSequence).HasColumnName("local_sequence");
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            builder.Property(x => x.EntityId).HasColumnName("entity_id");
            builder.Property(x => x.CommandType).HasColumnName("command_type").HasMaxLength(160);
            builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40);
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(160);
            builder.Property(x => x.ConflictId).HasColumnName("conflict_id");
            builder.Property(x => x.ServerRevision).HasColumnName("server_revision");
            builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
            builder.Property(x => x.ReplayCount).HasColumnName("replay_count");
            builder.Property(x => x.FirstAttemptAt).HasColumnName("first_attempt_at");
            builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
            builder.Property(x => x.LastCorrelationId).HasColumnName("last_correlation_id").HasMaxLength(120);
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.DeviceId, x.OperationId }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.UserId, x.DeviceId, x.LastAttemptAt });
        });
    }
}
