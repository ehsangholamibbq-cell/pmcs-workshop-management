using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pmcs.Modules.FieldOperations.Domain;

namespace Pmcs.Modules.FieldOperations.Persistence;

internal sealed class FieldOperationsDbContext(DbContextOptions<FieldOperationsDbContext> options) : DbContext(options)
{
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();

    public DbSet<DailyReportFact> DailyReportFacts => Set<DailyReportFact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("field_operations");

        modelBuilder.Entity<DailyReport>(builder =>
        {
            builder.ToTable("daily_reports");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.ReportDate).HasColumnName("report_date");
            builder.Property(x => x.LocationName).HasColumnName("location_name").HasMaxLength(200);
            builder.Property(x => x.Narrative).HasColumnName("narrative").HasMaxLength(4_000);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.LastModifiedAt).HasColumnName("last_modified_at");
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ReportDate }).IsUnique();

            builder.HasMany(x => x.Facts)
                .WithOne()
                .HasForeignKey(x => x.DailyReportId)
                .OnDelete(DeleteBehavior.Cascade);

            var factsNavigation = builder.Metadata.FindNavigation(nameof(DailyReport.Facts))
                ?? throw new InvalidOperationException("Daily report facts navigation was not configured.");
            factsNavigation.SetField("_facts");
            factsNavigation.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<DailyReportFact>(builder =>
        {
            builder.ToTable("daily_report_facts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.DailyReportId).HasColumnName("daily_report_id");
            builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1_000);
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(120);
            builder.Property(x => x.LocationName).HasColumnName("location_name").HasMaxLength(200);
            builder.Property(x => x.LocationId).HasColumnName("location_id");
            builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(24, 6);
            builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(40);
            builder.Property(x => x.ResourceCount).HasColumnName("resource_count");
            builder.Property(x => x.Hours).HasColumnName("hours").HasPrecision(18, 2);
            builder.Property(x => x.ImpactLevel).HasColumnName("impact_level").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasMaxLength(120);
            builder.Property(x => x.MeasurementItemId).HasColumnName("measurement_item_id");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.HasIndex(x => x.DailyReportId);
            builder.HasIndex(x => x.MeasurementItemId);
            builder.HasIndex(x => x.LocationId);
        });
    }
}
