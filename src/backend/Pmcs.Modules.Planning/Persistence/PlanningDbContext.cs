using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Planning.Domain;

namespace Pmcs.Modules.Planning.Persistence;

internal sealed class PlanningDbContext(DbContextOptions<PlanningDbContext> options) : DbContext(options)
{
    public DbSet<MeasurementItem> MeasurementItems => Set<MeasurementItem>();

    public DbSet<PlanningBaseline> PlanningBaselines => Set<PlanningBaseline>();

    public DbSet<MilestoneProgressUpdate> MilestoneProgressUpdates => Set<MilestoneProgressUpdate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("planning");
        modelBuilder.Entity<MeasurementItem>(builder =>
        {
            builder.ToTable("measurement_items");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
            builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(item => item.Unit).HasColumnName("unit").HasMaxLength(40);
            builder.Property(item => item.TargetQuantity).HasColumnName("target_quantity").HasPrecision(24, 6);
            builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2_000);
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CreatedBy).HasColumnName("created_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.LastModifiedBy).HasColumnName("last_modified_by");
            builder.Property(item => item.LastModifiedAt).HasColumnName("last_modified_at");
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Code }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        });

        modelBuilder.Entity<PlanningBaseline>(builder =>
        {
            builder.ToTable("planning_baselines");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.VersionCode).HasColumnName("version_code").HasMaxLength(80);
            builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(item => item.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.SourceSystem).HasColumnName("source_system").HasMaxLength(120);
            builder.Property(item => item.SourceReference).HasColumnName("source_reference").HasMaxLength(500);
            builder.Property(item => item.DefinitionJson).HasColumnName("definition_json").HasColumnType("jsonb");
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CreatedBy).HasColumnName("created_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(item => item.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(item => item.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.Entries);
            builder.Ignore(item => item.HasSchedule);
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.VersionCode }).IsUnique();
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        });

        modelBuilder.Entity<MilestoneProgressUpdate>(builder =>
        {
            builder.ToTable("milestone_progress_updates");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(item => item.TenantId).HasColumnName("tenant_id");
            builder.Property(item => item.ProjectId).HasColumnName("project_id");
            builder.Property(item => item.BaselineId).HasColumnName("baseline_id");
            builder.Property(item => item.BaselineEntryId).HasColumnName("baseline_entry_id");
            builder.Property(item => item.StatusDate).HasColumnName("status_date");
            builder.Property(item => item.ProgressPercent).HasColumnName("progress_percent").HasPrecision(7, 2);
            builder.Property(item => item.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(500);
            builder.Property(item => item.Note).HasColumnName("note").HasMaxLength(2_000);
            builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.CreatedBy).HasColumnName("created_by");
            builder.Property(item => item.CreatedAt).HasColumnName("created_at");
            builder.Property(item => item.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(item => item.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(item => item.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(item => item.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(item => item.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(item => item.DomainEvents);
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.BaselineId, item.BaselineEntryId });
            builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Status });
        });
    }
}
