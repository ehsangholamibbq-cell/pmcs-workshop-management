using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Persistence;

internal sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

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
            builder.Property(x => x.CalendarMode).HasColumnName("calendar_mode").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.WorkingDaysMask).HasColumnName("working_days_mask");
            builder.Property(x => x.ConfigurationChangedAt).HasColumnName("configuration_changed_at");
            builder.Property(x => x.TimeZone).HasColumnName("time_zone").HasMaxLength(100);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });
    }
}
