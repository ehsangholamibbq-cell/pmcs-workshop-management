using Microsoft.EntityFrameworkCore;

namespace Pmcs.Modules.Platform.Persistence;

internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("foundation");

        modelBuilder.Entity<AuditEventRecord>(builder =>
        {
            builder.ToTable("audit_events");
            builder.HasKey(x => x.EventId);
            builder.Property(x => x.EventId).HasColumnName("event_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(160);
            builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(120);
            builder.Property(x => x.ResourceId).HasColumnName("resource_id").HasMaxLength(120);
            builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            builder.Property(x => x.Data).HasColumnName("data").HasColumnType("jsonb");
            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(120);
            builder.HasIndex(x => new { x.TenantId, x.OccurredAt });
        });

        modelBuilder.Entity<IdempotencyRecord>(builder =>
        {
            builder.ToTable("idempotency_records");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(160);
            builder.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(160);
            builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(128);
            builder.Property(x => x.StatusCode).HasColumnName("status_code");
            builder.Property(x => x.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.HasIndex(x => new { x.TenantId, x.Key, x.Operation }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessageRecord>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(x => x.MessageId);
            builder.Property(x => x.MessageId).HasColumnName("message_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200);
            builder.Property(x => x.EventVersion).HasColumnName("event_version");
            builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(120);
            builder.Property(x => x.PublishedAt).HasColumnName("published_at");
            builder.Property(x => x.Attempts).HasColumnName("attempts");
            builder.Property(x => x.LastError).HasColumnName("last_error");
            builder.HasIndex(x => new { x.PublishedAt, x.OccurredAt });
        });
    }
}
