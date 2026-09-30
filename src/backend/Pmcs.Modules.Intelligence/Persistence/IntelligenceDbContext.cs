using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Modules.Intelligence.Persistence;

internal sealed class IntelligenceDbContext(DbContextOptions<IntelligenceDbContext> options) : DbContext(options)
{
    public DbSet<InsightGenerationRequest> GenerationRequests => Set<InsightGenerationRequest>();
    public DbSet<AdvisoryInsight> AdvisoryInsights => Set<AdvisoryInsight>();
    public DbSet<IntelligenceAdministrationGrant> AdministrationGrants => Set<IntelligenceAdministrationGrant>();
    public DbSet<IntelligenceModelCatalog> ModelCatalog => Set<IntelligenceModelCatalog>();
    public DbSet<IntelligenceProfileVersion> ProfileVersions => Set<IntelligenceProfileVersion>();
    public DbSet<IntelligenceProfileSelection> ProfileSelections => Set<IntelligenceProfileSelection>();
    public DbSet<IntelligenceReferenceRun> ReferenceRuns => Set<IntelligenceReferenceRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("intelligence");

        modelBuilder.Entity<IntelligenceAdministrationGrant>(builder =>
        {
            builder.ToTable("administration_grants");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.ActorTenantId).HasColumnName("actor_tenant_id");
            builder.Property(x => x.ActorId).HasColumnName("actor_id");
            builder.Property(x => x.ScopeTenantId).HasColumnName("scope_tenant_id");
            builder.Property(x => x.Permission).HasColumnName("permission").HasMaxLength(100);
            builder.Property(x => x.IssuedBy).HasColumnName("issued_by");
            builder.Property(x => x.StartsAt).HasColumnName("starts_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.HasIndex(x => new { x.ActorTenantId, x.ActorId, x.Permission, x.ScopeTenantId });
        });

        modelBuilder.Entity<IntelligenceModelCatalog>(builder =>
        {
            builder.ToTable("model_catalog");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.Version).HasColumnName("version");
            builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(40);
            builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(160);
            builder.Property(x => x.Capabilities).HasColumnName("capabilities").HasConversion<int>();
            builder.Property(x => x.MaximumDataClass).HasColumnName("maximum_data_class").HasConversion<int>();
            builder.Property(x => x.Enabled).HasColumnName("enabled");
            builder.Property(x => x.VerifiedAt).HasColumnName("verified_at");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.HasIndex(x => new { x.Provider, x.Model, x.Version }).IsUnique();
        });

        modelBuilder.Entity<IntelligenceProfileVersion>(builder =>
        {
            builder.ToTable("profile_versions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.Version).HasColumnName("version");
            builder.Property(x => x.UseCase).HasColumnName("use_case").HasMaxLength(100);
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectIdsJson).HasColumnName("project_ids_json").HasColumnType("jsonb");
            builder.Property(x => x.DefaultModelId).HasColumnName("default_model_id");
            builder.Property(x => x.AllowedModelIdsJson).HasColumnName("allowed_model_ids_json").HasColumnType("jsonb");
            builder.Property(x => x.FallbackModelIdsJson).HasColumnName("fallback_model_ids_json").HasColumnType("jsonb");
            builder.Property(x => x.AllowFallback).HasColumnName("allow_fallback");
            builder.Property(x => x.RequiredCapabilities).HasColumnName("required_capabilities").HasConversion<int>();
            builder.Property(x => x.MaximumDataClass).HasColumnName("maximum_data_class").HasConversion<int>();
            builder.Property(x => x.MaximumInputTokens).HasColumnName("maximum_input_tokens");
            builder.Property(x => x.MaximumOutputTokens).HasColumnName("maximum_output_tokens");
            builder.Property(x => x.TimeoutSeconds).HasColumnName("timeout_seconds");
            builder.Property(x => x.MaximumCostMicrounits).HasColumnName("maximum_cost_microunits");
            builder.Property(x => x.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100);
            builder.Property(x => x.PolicyVersion).HasColumnName("policy_version").HasMaxLength(100);
            builder.Property(x => x.PublishedBy).HasColumnName("published_by");
            builder.Property(x => x.PublishedAt).HasColumnName("published_at");
            builder.HasIndex(x => new { x.TenantId, x.UseCase, x.Version }).IsUnique();
        });

        modelBuilder.Entity<IntelligenceProfileSelection>(builder =>
        {
            builder.ToTable("profile_selections");
            builder.HasKey(x => new { x.TenantId, x.UseCase });
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UseCase).HasColumnName("use_case").HasMaxLength(100);
            builder.Property(x => x.ProfileVersionId).HasColumnName("profile_version_id");
            builder.Property(x => x.ModelId).HasColumnName("model_id");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        });

        modelBuilder.Entity<IntelligenceReferenceRun>(builder =>
        {
            builder.ToTable("reference_runs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.RequestedBy).HasColumnName("requested_by");
            builder.Property(x => x.ProfileVersionId).HasColumnName("profile_version_id");
            builder.Property(x => x.ProfileVersion).HasColumnName("profile_version");
            builder.Property(x => x.ModelCatalogId).HasColumnName("model_catalog_id");
            builder.Property(x => x.ModelVersion).HasColumnName("model_version");
            builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(40);
            builder.Property(x => x.Model).HasColumnName("model").HasMaxLength(160);
            builder.Property(x => x.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100);
            builder.Property(x => x.PolicyVersion).HasColumnName("policy_version").HasMaxLength(100);
            builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.ToolId).HasColumnName("tool_id").HasMaxLength(100);
            builder.Property(x => x.ToolDecision).HasColumnName("tool_decision").HasMaxLength(20);
            builder.Property(x => x.Fallback).HasColumnName("fallback");
            builder.Property(x => x.FallbackReason).HasColumnName("fallback_reason").HasMaxLength(120);
            builder.Property(x => x.InputTokens).HasColumnName("input_tokens");
            builder.Property(x => x.OutputTokens).HasColumnName("output_tokens");
            builder.Property(x => x.CostMicrounits).HasColumnName("cost_microunits");
            builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(120);
            builder.Property(x => x.RequestedAt).HasColumnName("requested_at");
            builder.Property(x => x.ValidatedAt).HasColumnName("validated_at");
            builder.Property(x => x.StartedAt).HasColumnName("started_at");
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.RequestedAt });
        });

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
