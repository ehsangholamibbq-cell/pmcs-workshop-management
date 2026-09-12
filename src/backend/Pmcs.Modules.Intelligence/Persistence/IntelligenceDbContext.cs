using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Modules.Intelligence.Persistence;

internal sealed class IntelligenceDbContext(DbContextOptions<IntelligenceDbContext> options) : DbContext(options)
{
    public DbSet<InsightGenerationRequest> GenerationRequests => Set<InsightGenerationRequest>();
    public DbSet<AdvisoryInsight> AdvisoryInsights => Set<AdvisoryInsight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("intelligence");

        modelBuilder.Entity<InsightGenerationRequest>(builder =>
        {
            builder.ToTable("generation_requests");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
            builder.Property(x => x.InsightId).HasColumnName("insight_id");
            builder.Property(x => x.RequestedAt).HasColumnName("requested_at");
            builder.Property(x => x.StartedAt).HasColumnName("started_at");
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
            builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            builder.Property(x => x.Attempts).HasColumnName("attempts");
            builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(120);
            builder.Property(x => x.Revision).HasColumnName("revision");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.RequestedAt });
            builder.HasIndex(x => new { x.Status, x.RequestedAt });
        });

        modelBuilder.Entity<AdvisoryInsight>(builder =>
        {
            builder.ToTable("advisory_insights");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.GenerationRequestId).HasColumnName("generation_request_id");
            builder.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
            builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
            builder.Property(x => x.OutputJson).HasColumnName("output_json").HasColumnType("jsonb");
            builder.Property(x => x.InsightType).HasColumnName("insight_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Statement).HasColumnName("statement").HasMaxLength(2_000);
            builder.Property(x => x.ConfidenceBand).HasColumnName("confidence_band").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.PotentialImpact).HasColumnName("potential_impact").HasMaxLength(1_000);
            builder.Property(x => x.SuggestedOwnerRole).HasColumnName("suggested_owner_role").HasMaxLength(120);
            builder.Property(x => x.ContextHash).HasColumnName("context_hash").HasMaxLength(64);
            builder.Property(x => x.IncludesFinancialData).HasColumnName("includes_financial_data");
            builder.Property(x => x.IncludesCommercialData).HasColumnName("includes_commercial_data");
            builder.Property(x => x.IncludesActionData).HasColumnName("includes_action_data");
            builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(80);
            builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(120);
            builder.Property(x => x.ProviderResponseId).HasColumnName("provider_response_id").HasMaxLength(200);
            builder.Property(x => x.PromptVersion).HasColumnName("prompt_version").HasMaxLength(80);
            builder.Property(x => x.PolicyVersion).HasColumnName("policy_version").HasMaxLength(80);
            builder.Property(x => x.GeneratedAt).HasColumnName("generated_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1_000);
            builder.Property(x => x.Revision).HasColumnName("revision");
            builder.Ignore(x => x.Output);
            builder.HasIndex(x => x.GenerationRequestId).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.GeneratedAt });
        });
    }
}
