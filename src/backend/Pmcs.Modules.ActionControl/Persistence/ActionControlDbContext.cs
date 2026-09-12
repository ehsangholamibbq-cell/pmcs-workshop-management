using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.ActionControl.Domain;

namespace Pmcs.Modules.ActionControl.Persistence;

internal sealed class ActionControlDbContext(DbContextOptions<ActionControlDbContext> options) : DbContext(options)
{
    public DbSet<ManagementAction> Actions => Set<ManagementAction>();

    public DbSet<AttentionDisposition> AttentionDispositions => Set<AttentionDisposition>();
    public DbSet<ManagementIssue> Issues => Set<ManagementIssue>();
    public DbSet<GovernanceRiskMatrixVersion> RiskMatrices => Set<GovernanceRiskMatrixVersion>();
    public DbSet<ProjectRisk> Risks => Set<ProjectRisk>();
    public DbSet<DecisionRequest> DecisionRequests => Set<DecisionRequest>();
    public DbSet<DecisionRecord> Decisions => Set<DecisionRecord>();
    public DbSet<SlaRuleVersion> SlaRules => Set<SlaRuleVersion>();
    public DbSet<EscalationThread> Escalations => Set<EscalationThread>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("action_control");
        modelBuilder.Entity<ManagementAction>(builder =>
        {
            builder.ToTable("actions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.SourceFactId).HasColumnName("source_fact_id");
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2_000);
            builder.Property(x => x.AssigneeUserId).HasColumnName("assignee_user_id");
            builder.Property(x => x.AssigneeDisplayName).HasColumnName("assignee_display_name").HasMaxLength(200);
            builder.Property(x => x.DueDate).HasColumnName("due_date");
            builder.Property(x => x.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.LastChangedBy).HasColumnName("last_changed_by");
            builder.Property(x => x.LastChangedAt).HasColumnName("last_changed_at");
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.DueDate });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.AssigneeUserId, x.Status });
        });

        modelBuilder.Entity<AttentionDisposition>(builder =>
        {
            builder.ToTable("attention_dispositions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.ProjectId).HasColumnName("project_id");
            builder.Property(x => x.SourceFactId).HasColumnName("source_fact_id");
            builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ActionId).HasColumnName("action_id");
            builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1_000);
            builder.Property(x => x.DecidedBy).HasColumnName("decided_by");
            builder.Property(x => x.DecidedAt).HasColumnName("decided_at");
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.SourceFactId }).IsUnique();
        });

        ConfigureIssues(modelBuilder);
        ConfigureRisks(modelBuilder);
        ConfigureDecisions(modelBuilder);
        ConfigureSla(modelBuilder);
    }

    private static void ConfigureIssues(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ManagementIssue>(builder =>
        {
            builder.ToTable("issues");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(240);
            builder.Property(x => x.ObservedFact).HasColumnName("observed_fact").HasMaxLength(5_000);
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(120);
            builder.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Urgency).HasColumnName("urgency").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            builder.Property(x => x.OwnerDisplayName).HasColumnName("owner_display_name").HasMaxLength(200);
            builder.Property(x => x.TargetResolutionDate).HasColumnName("target_resolution_date");
            MapSource(builder);
            builder.Property(x => x.MaterializedFromRiskId).HasColumnName("materialized_from_risk_id");
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.Confidentiality).HasColumnName("confidentiality").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.SlaDueAt).HasColumnName("sla_due_at");
            builder.Property(x => x.SlaRuleVersionId).HasColumnName("sla_rule_version_id");
            builder.Property(x => x.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(2_000);
            builder.Property(x => x.ClosureEvidenceJson).HasColumnName("closure_evidence_json").HasColumnType("jsonb");
            MapLifecycle(builder);
            builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            builder.Property(x => x.ResolvedBy).HasColumnName("resolved_by");
            builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
            builder.Property(x => x.ClosedBy).HasColumnName("closed_by");
            builder.Ignore(x => x.EvidenceReferences);
            builder.Ignore(x => x.ClosureEvidence);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.TargetResolutionDate });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
        });
    }

    private static void ConfigureRisks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GovernanceRiskMatrixVersion>(builder =>
        {
            builder.ToTable("risk_matrix_versions");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.Version).HasColumnName("version");
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
            builder.Property(x => x.FormulaVersion).HasColumnName("formula_version").HasMaxLength(100);
            builder.Property(x => x.LowMaximum).HasColumnName("low_maximum");
            builder.Property(x => x.ModerateMaximum).HasColumnName("moderate_maximum");
            builder.Property(x => x.HighMaximum).HasColumnName("high_maximum");
            builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Version }).IsUnique();
        });

        modelBuilder.Entity<ProjectRisk>(builder =>
        {
            builder.ToTable("risks");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Cause).HasColumnName("cause").HasMaxLength(1_500);
            builder.Property(x => x.UncertainEvent).HasColumnName("uncertain_event").HasMaxLength(1_500);
            builder.Property(x => x.ImpactStatement).HasColumnName("impact_statement").HasMaxLength(2_000);
            builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(120);
            builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            builder.Property(x => x.OwnerDisplayName).HasColumnName("owner_display_name").HasMaxLength(200);
            builder.Property(x => x.Confidentiality).HasColumnName("confidentiality").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Probability).HasColumnName("probability").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.TimeImpact).HasColumnName("time_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CostImpact).HasColumnName("cost_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.QualityImpact).HasColumnName("quality_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.SafetyImpact).HasColumnName("safety_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ContractImpact).HasColumnName("contract_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.OperationsImpact).HasColumnName("operations_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.InherentScore).HasColumnName("inherent_score");
            builder.Property(x => x.InherentRating).HasColumnName("inherent_rating").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ResidualProbability).HasColumnName("residual_probability").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ResidualImpact).HasColumnName("residual_impact").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ResidualScore).HasColumnName("residual_score");
            builder.Property(x => x.ResidualRating).HasColumnName("residual_rating").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.MatrixVersionId).HasColumnName("matrix_version_id");
            builder.Property(x => x.MatrixVersion).HasColumnName("matrix_version");
            builder.Property(x => x.FormulaVersion).HasColumnName("formula_version").HasMaxLength(100);
            builder.Property(x => x.ResponseStrategy).HasColumnName("response_strategy").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.ResponsePlan).HasColumnName("response_plan").HasMaxLength(4_000);
            builder.Property(x => x.EarlyWarningIndicator).HasColumnName("early_warning_indicator").HasMaxLength(1_000);
            builder.Property(x => x.ReviewDate).HasColumnName("review_date");
            MapSource(builder);
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.MaterializedIssueId).HasColumnName("materialized_issue_id");
            builder.Property(x => x.SlaDueAt).HasColumnName("sla_due_at");
            builder.Property(x => x.SlaRuleVersionId).HasColumnName("sla_rule_version_id");
            builder.Property(x => x.ClosureReason).HasColumnName("closure_reason").HasMaxLength(1_500);
            builder.Property(x => x.ClosureEvidenceJson).HasColumnName("closure_evidence_json").HasColumnType("jsonb");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.LastReviewedBy).HasColumnName("last_reviewed_by");
            builder.Property(x => x.LastReviewedAt).HasColumnName("last_reviewed_at");
            builder.Property(x => x.ClosedAt).HasColumnName("closed_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.EvidenceReferences);
            builder.Ignore(x => x.ClosureEvidence);
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.ReviewDate });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
        });
    }

    private static void ConfigureDecisions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DecisionRequest>(builder =>
        {
            builder.ToTable("decision_requests");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.Question).HasColumnName("question").HasMaxLength(1_000);
            builder.Property(x => x.WhyNow).HasColumnName("why_now").HasMaxLength(2_000);
            builder.Property(x => x.RequiredBy).HasColumnName("required_by");
            builder.Property(x => x.AuthorityUserId).HasColumnName("authority_user_id");
            builder.Property(x => x.AuthorityDisplayName).HasColumnName("authority_display_name").HasMaxLength(200);
            builder.Property(x => x.KnownFactsJson).HasColumnName("known_facts_json").HasColumnType("jsonb");
            builder.Property(x => x.AssumptionsJson).HasColumnName("assumptions_json").HasColumnType("jsonb");
            builder.Property(x => x.PredictionsJson).HasColumnName("predictions_json").HasColumnType("jsonb");
            builder.Property(x => x.OptionsJson).HasColumnName("options_json").HasColumnType("jsonb");
            builder.Property(x => x.Recommendation).HasColumnName("recommendation").HasMaxLength(2_000);
            builder.Property(x => x.ConstraintsJson).HasColumnName("constraints_json").HasColumnType("jsonb");
            builder.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
            builder.Property(x => x.Confidentiality).HasColumnName("confidentiality").HasConversion<string>().HasMaxLength(40);
            MapSource(builder);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.InformationRequest).HasColumnName("information_request").HasMaxLength(2_000);
            builder.Property(x => x.DecisionRecordId).HasColumnName("decision_record_id");
            builder.Property(x => x.SlaDueAt).HasColumnName("sla_due_at");
            builder.Property(x => x.SlaRuleVersionId).HasColumnName("sla_rule_version_id");
            MapLifecycle(builder);
            builder.Ignore(x => x.KnownFacts);
            builder.Ignore(x => x.Assumptions);
            builder.Ignore(x => x.Predictions);
            builder.Ignore(x => x.Options);
            builder.Ignore(x => x.Constraints);
            builder.Ignore(x => x.EvidenceReferences);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.RequiredBy });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<DecisionRecord>(builder =>
        {
            builder.ToTable("decisions");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.DecisionRequestId).HasColumnName("decision_request_id");
            builder.Property(x => x.Number).HasColumnName("number").HasMaxLength(80);
            builder.Property(x => x.SelectedOption).HasColumnName("selected_option").HasMaxLength(2_000);
            builder.Property(x => x.Rationale).HasColumnName("rationale").HasMaxLength(4_000);
            builder.Property(x => x.ConditionsJson).HasColumnName("conditions_json").HasColumnType("jsonb");
            builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.DecidedAt).HasColumnName("decided_at");
            builder.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            builder.Property(x => x.DecidedBy).HasColumnName("decided_by");
            builder.Property(x => x.DecidedByDisplayName).HasColumnName("decided_by_display_name").HasMaxLength(200);
            builder.Property(x => x.SupersedesDecisionId).HasColumnName("supersedes_decision_id");
            builder.Property(x => x.SupersededByDecisionId).HasColumnName("superseded_by_decision_id");
            builder.Property(x => x.EffectReview).HasColumnName("effect_review").HasMaxLength(4_000);
            builder.Property(x => x.EffectEvidenceJson).HasColumnName("effect_evidence_json").HasColumnType("jsonb");
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.Conditions);
            builder.Ignore(x => x.EffectEvidence);
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.DecisionRequestId });
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Number }).IsUnique();
        });
    }

    private static void ConfigureSla(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SlaRuleVersion>(builder =>
        {
            builder.ToTable("sla_rule_versions");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.Version).HasColumnName("version");
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Duration).HasColumnName("duration");
            builder.Property(x => x.DurationUnit).HasColumnName("duration_unit").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.WarningLeadMinutes).HasColumnName("warning_lead_minutes");
            builder.Property(x => x.EscalationDelayMinutes).HasColumnName("escalation_delay_minutes");
            builder.Property(x => x.EscalationRecipientUserId).HasColumnName("escalation_recipient_user_id");
            builder.Property(x => x.EscalationRecipientDisplayName).HasColumnName("escalation_recipient_display_name").HasMaxLength(200);
            builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.EntityType, x.Severity, x.Version });
        });

        modelBuilder.Entity<EscalationThread>(builder =>
        {
            builder.ToTable("escalation_threads");
            builder.HasKey(x => x.Id);
            MapIdentity(builder);
            builder.Property(x => x.ThreadKey).HasColumnName("thread_key").HasMaxLength(180);
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.EntityId).HasColumnName("entity_id");
            builder.Property(x => x.EntityNumber).HasColumnName("entity_number").HasMaxLength(80);
            builder.Property(x => x.EntityTitle).HasColumnName("entity_title").HasMaxLength(500);
            builder.Property(x => x.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Level).HasColumnName("level");
            builder.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            builder.Property(x => x.RecipientDisplayName).HasColumnName("recipient_display_name").HasMaxLength(200);
            builder.Property(x => x.Confidentiality).HasColumnName("confidentiality").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.OccurrenceCount).HasColumnName("occurrence_count");
            builder.Property(x => x.FirstRaisedAt).HasColumnName("first_raised_at");
            builder.Property(x => x.LastRaisedAt).HasColumnName("last_raised_at");
            builder.Property(x => x.AcknowledgedBy).HasColumnName("acknowledged_by");
            builder.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
            builder.Property(x => x.AcknowledgementNote).HasColumnName("acknowledgement_note").HasMaxLength(1_000);
            builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
            builder.Ignore(x => x.DomainEvents);
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ThreadKey }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.RecipientUserId });
        });
    }

    private static void MapIdentity<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<Guid>("Id").HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>("TenantId").HasColumnName("tenant_id");
        builder.Property<Guid>("ProjectId").HasColumnName("project_id");
    }

    private static void MapSource<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<string>("SourceModule").HasColumnName("source_module").HasMaxLength(80);
        builder.Property<string>("SourceEntityType").HasColumnName("source_entity_type").HasMaxLength(120);
        builder.Property<Guid?>("SourceEntityId").HasColumnName("source_entity_id");
        builder.Property<long?>("SourceRevision").HasColumnName("source_revision");
        builder.Property<string>("SourceSnapshot").HasColumnName("source_snapshot").HasMaxLength(1_000);
    }

    private static void MapLifecycle<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<Guid>("CreatedBy").HasColumnName("created_by");
        builder.Property<DateTimeOffset>("CreatedAt").HasColumnName("created_at");
        builder.Property<Guid?>("LastChangedBy").HasColumnName("last_changed_by");
        builder.Property<DateTimeOffset?>("LastChangedAt").HasColumnName("last_changed_at");
        builder.Property<long>("Revision").HasColumnName("revision").IsConcurrencyToken();
        builder.Ignore("DomainEvents");
    }
}
