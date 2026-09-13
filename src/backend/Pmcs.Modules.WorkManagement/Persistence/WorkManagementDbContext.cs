using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.WorkManagement.Domain;

namespace Pmcs.Modules.WorkManagement.Persistence;

internal sealed class WorkManagementDbContext(DbContextOptions<WorkManagementDbContext> options) : DbContext(options)
{
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("work_management");
        modelBuilder.Entity<InAppNotification>(builder =>
        {
            builder.ToTable("notifications");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            builder.Property(x => x.DeduplicationKey).HasColumnName("deduplication_key").HasMaxLength(240);
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(80);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.Body).HasColumnName("body").HasMaxLength(2_000);
            builder.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(80);
            builder.Property(x => x.TargetId).HasColumnName("target_id");
            builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            builder.Property(x => x.ReadAt).HasColumnName("read_at");
            builder.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.RecipientUserId, x.OccurredAt });
            builder.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.DeduplicationKey }).IsUnique();
        });
    }
}
