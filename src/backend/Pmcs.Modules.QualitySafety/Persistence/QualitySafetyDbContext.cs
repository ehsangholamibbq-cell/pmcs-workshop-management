using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.QualitySafety.Domain;

namespace Pmcs.Modules.QualitySafety.Persistence;

internal sealed class QualitySafetyDbContext(DbContextOptions<QualitySafetyDbContext> options) : DbContext(options)
{
    public DbSet<QualitySafetyConfiguration> Configurations => Set<QualitySafetyConfiguration>();
    public DbSet<RiskMatrixVersion> RiskMatrices => Set<RiskMatrixVersion>();
    public DbSet<QualitySafetyIntake> Intakes => Set<QualitySafetyIntake>();
    public DbSet<InspectionRecord> Inspections => Set<InspectionRecord>();
    public DbSet<NonConformanceRecord> NonConformances => Set<NonConformanceRecord>();
    public DbSet<DefectRecord> Defects => Set<DefectRecord>();
    public DbSet<SafetyIncident> Incidents => Set<SafetyIncident>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<PermitToWork> Permits => Set<PermitToWork>();
    public DbSet<ToolboxTalk> ToolboxTalks => Set<ToolboxTalk>();
    public DbSet<InspectionTestPlanVersion> InspectionTestPlans => Set<InspectionTestPlanVersion>();
    public DbSet<ChecklistTemplateVersion> ChecklistTemplates => Set<ChecklistTemplateVersion>();
    public DbSet<QualityTestRecord> TestRecords => Set<QualityTestRecord>();
    public DbSet<HseCompetencyRecord> CompetencyRecords => Set<HseCompetencyRecord>();
    public DbSet<ExposureHoursRecord> ExposureHours => Set<ExposureHoursRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("quality_safety");
        ConfigureConfiguration(modelBuilder.Entity<QualitySafetyConfiguration>());
        ConfigureMatrix(modelBuilder.Entity<RiskMatrixVersion>());
        ConfigureIntake(modelBuilder.Entity<QualitySafetyIntake>());
        ConfigureInspection(modelBuilder.Entity<InspectionRecord>());
        ConfigureNcr(modelBuilder.Entity<NonConformanceRecord>());
        ConfigureDefect(modelBuilder.Entity<DefectRecord>());
        ConfigureIncident(modelBuilder.Entity<SafetyIncident>());
        ConfigureAction(modelBuilder.Entity<CorrectiveAction>());
        ConfigurePermit(modelBuilder.Entity<PermitToWork>());
        ConfigureToolbox(modelBuilder.Entity<ToolboxTalk>());
        ConfigureInspectionTestPlan(modelBuilder.Entity<InspectionTestPlanVersion>());
        ConfigureChecklist(modelBuilder.Entity<ChecklistTemplateVersion>());
        ConfigureTestRecord(modelBuilder.Entity<QualityTestRecord>());
        ConfigureCompetency(modelBuilder.Entity<HseCompetencyRecord>());
        ConfigureExposure(modelBuilder.Entity<ExposureHoursRecord>());
    }

    private static void ConfigureConfiguration(EntityTypeBuilder<QualitySafetyConfiguration> b)
    {
        b.ToTable("configurations"); Aggregate(b);
        b.Property(x => x.QualityMode).HasColumnName("quality_mode").HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.HseMode).HasColumnName("hse_mode").HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.QualityOwnerUserId).HasColumnName("quality_owner_user_id"); b.Property(x => x.HseOwnerUserId).HasColumnName("hse_owner_user_id");
        b.Property(x => x.QualityMatrixVersionId).HasColumnName("quality_matrix_version_id"); b.Property(x => x.HseMatrixVersionId).HasColumnName("hse_matrix_version_id");
        b.Property(x => x.WorkflowAndSlaDefined).HasColumnName("workflow_sla_defined"); b.Property(x => x.TemplatesDefined).HasColumnName("templates_defined");
        b.Property(x => x.EvidenceAndClosureRulesDefined).HasColumnName("evidence_closure_rules_defined"); b.Property(x => x.ChangedAt).HasColumnName("changed_at"); b.Property(x => x.ChangedBy).HasColumnName("changed_by");
        b.Ignore(x => x.QualityReady); b.Ignore(x => x.HseReady); b.HasIndex(x => new { x.TenantId, x.ProjectId }).IsUnique();
    }

    private static void ConfigureMatrix(EntityTypeBuilder<RiskMatrixVersion> b)
    {
        b.ToTable("risk_matrix_versions"); Aggregate(b); b.Property(x => x.Area).HasColumnName("area").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Version).HasColumnName("version"); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        b.Property(x => x.DefinitionJson).HasColumnName("definition_json").HasColumnType("jsonb"); b.Property(x => x.EffectiveFrom).HasColumnName("effective_from");
        b.Property(x => x.CreatedBy).HasColumnName("created_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Area, x.Version }).IsUnique();
    }

    private static void ConfigureIntake(EntityTypeBuilder<QualitySafetyIntake> b)
    {
        b.ToTable("intakes"); Aggregate(b); Number(b); b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ObservedAt).HasColumnName("observed_at"); b.Property(x => x.Location).HasColumnName("location").HasMaxLength(240);
        b.Property(x => x.Facts).HasColumnName("facts").HasMaxLength(4_000); b.Property(x => x.InitialSeverity).HasColumnName("initial_severity").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ImmediateAction).HasColumnName("immediate_action").HasMaxLength(2_000); b.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb");
        b.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(50); b.Property(x => x.ReportedBy).HasColumnName("reported_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40); b.Property(x => x.ConversionType).HasColumnName("conversion_type").HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ConvertedRecordId).HasColumnName("converted_record_id"); b.Property(x => x.TriageNote).HasColumnName("triage_note").HasMaxLength(2_000); b.Property(x => x.TriagedBy).HasColumnName("triaged_by"); b.Property(x => x.TriagedAt).HasColumnName("triaged_at");
        b.Ignore(x => x.EvidenceReferences); b.Ignore(x => x.IsQuality); b.Ignore(x => x.IsHse); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.ObservedAt });
    }

    private static void ConfigureInspection(EntityTypeBuilder<InspectionRecord> b)
    {
        b.ToTable("inspections"); Aggregate(b); Number(b); b.Property(x => x.SourceIntakeId).HasColumnName("source_intake_id");
        b.Property(x => x.InspectionTestPlanVersionId).HasColumnName("inspection_test_plan_version_id");
        b.Property(x => x.InspectionType).HasColumnName("inspection_type").HasMaxLength(160); b.Property(x => x.Location).HasColumnName("location").HasMaxLength(240);
        b.Property(x => x.AcceptanceCriteria).HasColumnName("acceptance_criteria").HasMaxLength(4_000); b.Property(x => x.ChecklistTemplateReference).HasColumnName("checklist_template_reference").HasMaxLength(240);
        b.Property(x => x.ChecklistTemplateVersion).HasColumnName("checklist_template_version"); b.Property(x => x.RequestedFor).HasColumnName("requested_for"); b.Property(x => x.RequestedBy).HasColumnName("requested_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.Readiness).HasColumnName("readiness").HasConversion<string>().HasMaxLength(40); b.Property(x => x.ReadinessNote).HasColumnName("readiness_note").HasMaxLength(1_000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40); b.Property(x => x.Result).HasColumnName("result").HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ResultNote).HasColumnName("result_note").HasMaxLength(2_000); b.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb"); b.Ignore(x => x.EvidenceReferences);
        b.Property(x => x.InspectedBy).HasColumnName("inspected_by"); b.Property(x => x.InspectedAt).HasColumnName("inspected_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.RequestedFor });
    }

    private static void ConfigureNcr(EntityTypeBuilder<NonConformanceRecord> b)
    {
        b.ToTable("nonconformances"); Aggregate(b); Number(b); b.Property(x => x.SourceIntakeId).HasColumnName("source_intake_id"); b.Property(x => x.InspectionId).HasColumnName("inspection_id");
        b.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id"); b.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id"); b.Property(x => x.VendorPartyId).HasColumnName("vendor_party_id"); b.Property(x => x.SupplyItemId).HasColumnName("supply_item_id");
        b.Property(x => x.LotReference).HasColumnName("lot_reference").HasMaxLength(160); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(240); b.Property(x => x.Requirement).HasColumnName("requirement").HasMaxLength(4_000); b.Property(x => x.NonConformity).HasColumnName("nonconformity").HasMaxLength(4_000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50); b.Property(x => x.Disposition).HasColumnName("disposition").HasConversion<string>().HasMaxLength(50); b.Property(x => x.DispositionNote).HasColumnName("disposition_note").HasMaxLength(2_000); b.Property(x => x.ConcessionApprovedBy).HasColumnName("concession_approved_by");
        b.Property(x => x.RootCauseStatus).HasColumnName("root_cause_status").HasConversion<string>().HasMaxLength(40); b.Property(x => x.RootCause).HasColumnName("root_cause").HasMaxLength(4_000); b.Property(x => x.ClosureEvidenceJson).HasColumnName("closure_evidence_json").HasColumnType("jsonb"); b.Ignore(x => x.ClosureEvidence);
        b.Property(x => x.ClosureWaiverReason).HasColumnName("closure_waiver_reason").HasMaxLength(1_000); b.Property(x => x.ClosureWaivedBy).HasColumnName("closure_waived_by"); b.Property(x => x.CreatedBy).HasColumnName("created_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ClosedAt).HasColumnName("closed_at");
        b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status }); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.VendorPartyId });
    }

    private static void ConfigureDefect(EntityTypeBuilder<DefectRecord> b)
    {
        b.ToTable("defects"); Aggregate(b); Number(b); b.Property(x => x.SourceIntakeId).HasColumnName("source_intake_id"); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(240); b.Property(x => x.Location).HasColumnName("location").HasMaxLength(240);
        b.Property(x => x.AssigneeUserId).HasColumnName("assignee_user_id"); b.Property(x => x.DueDate).HasColumnName("due_date"); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.RectificationEvidenceJson).HasColumnName("rectification_evidence_json").HasColumnType("jsonb"); b.Property(x => x.VerificationEvidenceJson).HasColumnName("verification_evidence_json").HasColumnType("jsonb");
        b.Property(x => x.CreatedBy).HasColumnName("created_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ClosedAt).HasColumnName("closed_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.DueDate });
    }

    private static void ConfigureIncident(EntityTypeBuilder<SafetyIncident> b)
    {
        b.ToTable("incidents"); Aggregate(b); Number(b); b.Property(x => x.SourceIntakeId).HasColumnName("source_intake_id"); b.Property(x => x.OccurredAt).HasColumnName("occurred_at"); b.Property(x => x.Location).HasColumnName("location").HasMaxLength(240); b.Property(x => x.Facts).HasColumnName("facts").HasMaxLength(6_000);
        b.Property(x => x.PreliminarySeverity).HasColumnName("preliminary_severity").HasConversion<string>().HasMaxLength(30); b.Property(x => x.FinalSeverity).HasColumnName("final_severity").HasConversion<string>().HasMaxLength(30); b.Property(x => x.MatrixVersionId).HasColumnName("matrix_version_id");
        b.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(50); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50); b.Property(x => x.RootCauseStatus).HasColumnName("root_cause_status").HasConversion<string>().HasMaxLength(40); b.Property(x => x.RootCause).HasColumnName("root_cause").HasMaxLength(4_000);
        b.Property(x => x.ClosureEvidenceJson).HasColumnName("closure_evidence_json").HasColumnType("jsonb"); b.Property(x => x.ReportedBy).HasColumnName("reported_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ClosedAt).HasColumnName("closed_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.OccurredAt });
    }

    private static void ConfigureAction(EntityTypeBuilder<CorrectiveAction> b)
    {
        b.ToTable("corrective_actions"); Aggregate(b); Number(b); b.Property(x => x.SourceArea).HasColumnName("source_area").HasConversion<string>().HasMaxLength(30); b.Property(x => x.SourceRecordType).HasColumnName("source_record_type").HasMaxLength(120); b.Property(x => x.SourceRecordId).HasColumnName("source_record_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(30); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(240); b.Property(x => x.OwnerUserId).HasColumnName("owner_user_id"); b.Property(x => x.ResponsibleParty).HasColumnName("responsible_party").HasMaxLength(240); b.Property(x => x.DueDate).HasColumnName("due_date"); b.Property(x => x.SuccessCriteria).HasColumnName("success_criteria").HasMaxLength(2_000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50); b.Property(x => x.CompletionEvidenceJson).HasColumnName("completion_evidence_json").HasColumnType("jsonb"); b.Property(x => x.VerificationEvidenceJson).HasColumnName("verification_evidence_json").HasColumnType("jsonb"); b.Property(x => x.VerifiedBy).HasColumnName("verified_by"); b.Property(x => x.VerifiedAt).HasColumnName("verified_at");
        b.Property(x => x.ExtendedDueDate).HasColumnName("extended_due_date"); b.Property(x => x.ExtensionReason).HasColumnName("extension_reason").HasMaxLength(1_000); b.Property(x => x.ExtensionApprovedBy).HasColumnName("extension_approved_by"); b.Property(x => x.CreatedBy).HasColumnName("created_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ClosedAt).HasColumnName("closed_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.DueDate });
    }

    private static void ConfigurePermit(EntityTypeBuilder<PermitToWork> b)
    {
        b.ToTable("permits"); Aggregate(b); Number(b); b.Property(x => x.WorkDescription).HasColumnName("work_description").HasMaxLength(2_000); b.Property(x => x.Location).HasColumnName("location").HasMaxLength(240); b.Property(x => x.ValidFrom).HasColumnName("valid_from"); b.Property(x => x.ValidTo).HasColumnName("valid_to"); b.Property(x => x.HazardsJson).HasColumnName("hazards_json").HasColumnType("jsonb"); b.Property(x => x.ControlsJson).HasColumnName("controls_json").HasColumnType("jsonb"); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40); b.Property(x => x.RequestedBy).HasColumnName("requested_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ApprovedBy).HasColumnName("approved_by"); b.Property(x => x.ApprovedAt).HasColumnName("approved_at"); b.Property(x => x.ClosedAt).HasColumnName("closed_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status, x.ValidTo });
    }

    private static void ConfigureToolbox(EntityTypeBuilder<ToolboxTalk> b)
    {
        b.ToTable("toolbox_talks"); Aggregate(b); Number(b); b.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(500); b.Property(x => x.HeldAt).HasColumnName("held_at"); b.Property(x => x.Location).HasColumnName("location").HasMaxLength(240); b.Property(x => x.AttendeesJson).HasColumnName("attendees_json").HasColumnType("jsonb"); b.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb"); b.Property(x => x.RecordedBy).HasColumnName("recorded_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.HeldAt });
    }

    private static void ConfigureInspectionTestPlan(EntityTypeBuilder<InspectionTestPlanVersion> b)
    {
        b.ToTable("inspection_test_plan_versions"); Aggregate(b); b.Property(x => x.Code).HasColumnName("code").HasMaxLength(80); b.Property(x => x.Version).HasColumnName("version"); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(240); b.Property(x => x.StagesJson).HasColumnName("stages_json").HasColumnType("jsonb"); b.Property(x => x.AcceptanceCriteria).HasColumnName("acceptance_criteria").HasMaxLength(4_000); b.Property(x => x.InspectorRole).HasColumnName("inspector_role").HasMaxLength(160); b.Property(x => x.PointType).HasColumnName("point_type").HasConversion<string>().HasMaxLength(30); b.Property(x => x.EffectiveFrom).HasColumnName("effective_from"); b.Property(x => x.CreatedBy).HasColumnName("created_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code, x.Version }).IsUnique();
    }

    private static void ConfigureChecklist(EntityTypeBuilder<ChecklistTemplateVersion> b)
    {
        b.ToTable("checklist_template_versions"); Aggregate(b); b.Property(x => x.Code).HasColumnName("code").HasMaxLength(80); b.Property(x => x.Version).HasColumnName("version"); b.Property(x => x.Title).HasColumnName("title").HasMaxLength(240); b.Property(x => x.ItemsJson).HasColumnName("items_json").HasColumnType("jsonb"); b.Property(x => x.EffectiveFrom).HasColumnName("effective_from"); b.Property(x => x.CreatedBy).HasColumnName("created_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code, x.Version }).IsUnique();
    }

    private static void ConfigureTestRecord(EntityTypeBuilder<QualityTestRecord> b)
    {
        b.ToTable("test_records"); Aggregate(b); Number(b); b.Property(x => x.InspectionId).HasColumnName("inspection_id"); b.Property(x => x.TestType).HasColumnName("test_type").HasMaxLength(160); b.Property(x => x.TestedAt).HasColumnName("tested_at"); b.Property(x => x.SampleReference).HasColumnName("sample_reference").HasMaxLength(240); b.Property(x => x.AcceptanceCriteria).HasColumnName("acceptance_criteria").HasMaxLength(2_000); b.Property(x => x.Result).HasColumnName("result").HasConversion<string>().HasMaxLength(30); b.Property(x => x.ResultDetails).HasColumnName("result_details").HasMaxLength(2_000); b.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb"); b.Property(x => x.RecordedBy).HasColumnName("recorded_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.TestedAt });
    }

    private static void ConfigureCompetency(EntityTypeBuilder<HseCompetencyRecord> b)
    {
        b.ToTable("competency_records"); Aggregate(b); b.Property(x => x.PersonReference).HasColumnName("person_reference").HasMaxLength(240); b.Property(x => x.InductionDate).HasColumnName("induction_date"); b.Property(x => x.ValidUntil).HasColumnName("valid_until"); b.Property(x => x.CompetenciesJson).HasColumnName("competencies_json").HasColumnType("jsonb"); b.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb"); b.Property(x => x.Classification).HasColumnName("classification").HasConversion<string>().HasMaxLength(50); b.Property(x => x.RecordedBy).HasColumnName("recorded_by"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.PersonReference, x.ValidUntil });
    }

    private static void ConfigureExposure(EntityTypeBuilder<ExposureHoursRecord> b)
    {
        b.ToTable("exposure_hours"); Aggregate(b); b.Property(x => x.PeriodStart).HasColumnName("period_start"); b.Property(x => x.PeriodEnd).HasColumnName("period_end"); b.Property(x => x.Hours).HasColumnName("hours").HasPrecision(18, 2); b.Property(x => x.SourceReference).HasColumnName("source_reference").HasMaxLength(500); b.Property(x => x.EvidenceReferencesJson).HasColumnName("evidence_references_json").HasColumnType("jsonb"); b.Property(x => x.ApprovedBy).HasColumnName("approved_by"); b.Property(x => x.ApprovedAt).HasColumnName("approved_at"); b.HasIndex(x => new { x.TenantId, x.ProjectId, x.PeriodStart, x.PeriodEnd });
    }

    private static void Aggregate<T>(EntityTypeBuilder<T> b) where T : AggregateRoot
    {
        b.HasKey("Id"); b.Property<Guid>("Id").HasColumnName("id").ValueGeneratedNever(); b.Property<Guid>("TenantId").HasColumnName("tenant_id"); b.Property<Guid>("ProjectId").HasColumnName("project_id"); b.Property<long>("Revision").HasColumnName("revision").IsConcurrencyToken(); b.Ignore("DomainEvents");
    }
    private static void Number<T>(EntityTypeBuilder<T> b) where T : AggregateRoot { b.Property<string>("Number").HasColumnName("number").HasMaxLength(80); b.HasIndex("TenantId", "ProjectId", "Number").IsUnique(); }
}
