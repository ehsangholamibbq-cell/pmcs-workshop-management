using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Persistence;

internal sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectLocation> ProjectLocations => Set<ProjectLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("projects");

        modelBuilder.Entity<Project>(builder =>
        {
            builder.ToTable("projects");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32);
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            builder.Property(x => x.ContractModel).HasColumnName("contract_model").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.PlanningMode).HasColumnName("planning_mode").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.BudgetMode).HasColumnName("budget_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.QualityMode).HasColumnName("quality_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.HseMode).HasColumnName("hse_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.FinanceMode).HasColumnName("finance_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ProcurementMode).HasColumnName("procurement_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.BaseCurrencyCode).HasColumnName("base_currency_code").HasMaxLength(3);
            builder.Property(x => x.ProjectType).HasColumnName("project_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ExecutionPhase).HasColumnName("execution_phase").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2);
            builder.Property(x => x.Region).HasColumnName("region").HasMaxLength(200);
            builder.Property(x => x.StartDate).HasColumnName("start_date");
            builder.Property(x => x.PlannedFinishDate).HasColumnName("planned_finish_date");
            builder.Property(x => x.ShortDescription).HasColumnName("short_description").HasMaxLength(1000);
            builder.Property(x => x.UnitSystem).HasColumnName("unit_system").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.DailyCutoffLocalTime).HasColumnName("daily_cutoff_local_time");
            builder.Property(x => x.ReportingFrequency).HasColumnName("reporting_frequency").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.DailyReportWorkflow).HasColumnName("daily_report_workflow").HasConversion<string>().HasMaxLength(60);
            builder.Property(x => x.OfflinePolicyAccepted).HasColumnName("offline_policy_accepted");
            builder.Property(x => x.ConfigurationVersion).HasColumnName("configuration_version");
            builder.Property(x => x.ActivatedConfigurationVersion).HasColumnName("activated_configuration_version");
            builder.Property(x => x.CalendarMode).HasColumnName("calendar_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.WorkingDaysMask).HasColumnName("working_days_mask");
            builder.Property(x => x.ConfigurationChangedAt).HasColumnName("configuration_changed_at");
            builder.Property(x => x.TimeZone).HasColumnName("time_zone").HasMaxLength(100);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ActivatedBy).HasColumnName("activated_by");
            builder.Property(x => x.ActivatedAt).HasColumnName("activated_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ProjectLocation>(builder =>
        {
            builder.ToTable("project_locations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40);
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            builder.Property(x => x.ParentLocationId).HasColumnName("parent_location_id");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ParentLocationId });
            builder.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ProjectLocation>()
                .WithMany()
                .HasForeignKey(x => x.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
