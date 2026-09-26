using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Documents.Domain;

namespace Pmcs.Modules.Documents.Persistence;

internal sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<DocumentAsset> Assets => Set<DocumentAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("documents");
        modelBuilder.Entity<DocumentAsset>(builder =>
        {
            builder.ToTable("assets");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.OwnerType).HasColumnName("owner_type").HasConversion<string>().HasMaxLength(80);
            builder.Property(x => x.OwnerId).HasColumnName("owner_id");
            builder.Property(x => x.VersionNumber).HasColumnName("version_number");
            builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255);
            builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(160);
            builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            builder.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
            builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(700);
            builder.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.RetentionPolicy).HasColumnName("retention_policy").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.RetainUntil).HasColumnName("retain_until");
            builder.Property(x => x.LegalHold).HasColumnName("legal_hold");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ScanVerdict).HasColumnName("scan_verdict").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ScanProvider).HasColumnName("scan_provider").HasMaxLength(120);
            builder.Property(x => x.ScanDetails).HasColumnName("scan_details").HasMaxLength(500);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UploadExpiresAt).HasColumnName("upload_expires_at");
            builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            builder.Property(x => x.StorageETag).HasColumnName("storage_etag").HasMaxLength(200);
            builder.Property(x => x.ReleasedBy).HasColumnName("released_by");
            builder.Property(x => x.ReleasedAt).HasColumnName("released_at");
            builder.Property(x => x.ClassifiedBy).HasColumnName("classified_by");
            builder.Property(x => x.ClassifiedAt).HasColumnName("classified_at");
            builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.CreatedAt });
            builder.HasIndex(x => new { x.TenantId, x.OwnerType, x.OwnerId, x.VersionNumber }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ObjectKey }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.Status, x.UploadExpiresAt });
        });
    }
}
