using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Pmcs.Modules.ProjectIntelligence.Persistence;

internal sealed class ProjectIntelligenceDbContext(DbContextOptions<ProjectIntelligenceDbContext> options) : DbContext(options)
{
    public DbSet<ProjectStateSnapshot> ProjectStateSnapshots => Set<ProjectStateSnapshot>();

    public DbSet<ProjectStateAttentionItem> ProjectStateAttentionItems => Set<ProjectStateAttentionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("project_intelligence");

        modelBuilder.Entity<ProjectStateSnapshot>(builder =>
        {
            builder.ToTable("project_state_snapshots");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.ProjectCode).HasColumnName("project_code").HasMaxLength(32);
            builder.Property(x => x.ProjectName).HasColumnName("project_name").HasMaxLength(200);
            builder.Property(x => x.CalculationVersion).HasColumnName("calculation_version").HasMaxLength(80);
            builder.Property(x => x.ProjectConfigurationRevision).HasColumnName("project_configuration_revision");
            builder.Property(x => x.AsOfDate).HasColumnName("as_of_date");
            builder.Property(x => x.WindowStart).HasColumnName("window_start");
            builder.Property(x => x.WindowEnd).HasColumnName("window_end");
            builder.Property(x => x.CalculatedAt).HasColumnName("calculated_at");
            builder.Property(x => x.AssessmentScope).HasColumnName("assessment_scope").HasConversion<string>().HasMaxLength(80);
            builder.Property(x => x.IsPartial).HasColumnName("is_partial");
            builder.Property(x => x.OperationalStatus).HasColumnName("operational_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CoverageStatus).HasColumnName("coverage_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.FreshnessStatus).HasColumnName("freshness_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ConfidenceStatus).HasColumnName("confidence_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CoverageBasis).HasColumnName("coverage_basis").HasConversion<string>().HasMaxLength(80);
            builder.Property(x => x.CoveragePercent).HasColumnName("coverage_percent").HasPrecision(5, 1);
            builder.Property(x => x.ExpectedReportDays).HasColumnName("expected_report_days");
            builder.Property(x => x.ApprovedReportDays).HasColumnName("approved_report_days");
            builder.Property(x => x.LastApprovedReportDate).HasColumnName("last_approved_report_date");
            builder.Property(x => x.ApprovedFactCount).HasColumnName("approved_fact_count");
            builder.Property(x => x.ProgressFactCount).HasColumnName("progress_fact_count");
            builder.Property(x => x.LaborFactCount).HasColumnName("labor_fact_count");
            builder.Property(x => x.EquipmentFactCount).HasColumnName("equipment_fact_count");
            builder.Property(x => x.MaterialFactCount).HasColumnName("material_fact_count");
            builder.Property(x => x.IssueCount).HasColumnName("issue_count");
            builder.Property(x => x.StoppageCount).HasColumnName("stoppage_count");
            builder.Property(x => x.HighImpactCount).HasColumnName("high_impact_count");
            builder.Property(x => x.CriticalImpactCount).HasColumnName("critical_impact_count");
            builder.Property(x => x.OldestAttentionAgeDays).HasColumnName("oldest_attention_age_days");
            builder.Property(x => x.ContractState).HasColumnName("contract_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.PlanningState).HasColumnName("planning_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.BudgetState).HasColumnName("budget_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.QualityState).HasColumnName("quality_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.HseState).HasColumnName("hse_state").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.SourceMaxChangedAt).HasColumnName("source_max_changed_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.CalculatedAt });

            builder.HasMany(x => x.AttentionItems)
                .WithOne()
                .HasForeignKey(x => x.SnapshotId)
                .OnDelete(DeleteBehavior.Cascade);

            var attentionNavigation = builder.Metadata.FindNavigation(nameof(ProjectStateSnapshot.AttentionItems))
                ?? throw new InvalidOperationException("Project state attention navigation was not configured.");
            attentionNavigation.SetField("_attentionItems");
            attentionNavigation.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProjectStateAttentionItem>(builder =>
        {
            builder.ToTable("project_state_attention_items");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
            builder.Property(x => x.SourceReportId).HasColumnName("source_report_id");
            builder.Property(x => x.SourceFactId).HasColumnName("source_fact_id");
            builder.Property(x => x.ReportDate).HasColumnName("report_date");
            builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(1_000);
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(120);
            builder.Property(x => x.LocationName).HasColumnName("location_name").HasMaxLength(200);
            builder.Property(x => x.ObservedImpact).HasColumnName("observed_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.AgeDays).HasColumnName("age_days");
            builder.Property(x => x.AgeBand).HasColumnName("age_band").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasMaxLength(120);
            builder.HasIndex(x => new { x.SnapshotId, x.SourceFactId }).IsUnique();
        });
    }
}
