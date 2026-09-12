using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Evidence.Domain;

namespace Pmcs.Modules.Evidence.Persistence;

internal sealed class EvidenceDbContext(DbContextOptions<EvidenceDbContext> options) : DbContext(options)
{
    public DbSet<EvidenceFile> EvidenceFiles => Set<EvidenceFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("evidence");
        modelBuilder.Entity<EvidenceFile>(builder =>
        {
            builder.ToTable("files");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.DailyReportId).HasColumnName("daily_report_id");
            builder.Property(x => x.DailyFactId).HasColumnName("daily_fact_id");
            builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255);
            builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(120);
            builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
            builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(700);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CapturedAtDevice).HasColumnName("captured_at_device");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UploadExpiresAt).HasColumnName("upload_expires_at");
            builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            builder.Property(x => x.StorageETag).HasColumnName("storage_etag").HasMaxLength(200);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.CreatedAt });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.DailyReportId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.DailyFactId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ObjectKey }).IsUnique();
        });
    }
}
