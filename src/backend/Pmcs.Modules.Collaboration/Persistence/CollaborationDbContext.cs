using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Collaboration.Domain;

namespace Pmcs.Modules.Collaboration.Persistence;

internal sealed class CollaborationDbContext(DbContextOptions<CollaborationDbContext> options) : DbContext(options)
{
    public DbSet<ProjectRoom> Rooms => Set<ProjectRoom>();
    public DbSet<ProjectMessage> Messages => Set<ProjectMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("collaboration");
        modelBuilder.Entity<ProjectRoom>(builder =>
        {
            builder.ToTable("project_rooms");
            builder.HasKey(item => item.ProjectId);
            builder.Property(item => item.ProjectId).HasColumnName("project_id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.LastSequence).HasColumnName("last_sequence");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.Entity<ProjectMessage>(builder =>
        {
            builder.ToTable("messages");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Sequence).HasColumnName("sequence");
            builder.Property(item => item.AuthorUserId).HasColumnName("author_user_id");
            builder.Property(item => item.ClientMessageId).HasColumnName("client_message_id");
            builder.Property(item => item.Body).HasColumnName("body").HasMaxLength(4_000);
            builder.Property(item => item.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Sequence }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.AuthorUserId, item.ClientMessageId }).IsUnique();
        });
    }
}
