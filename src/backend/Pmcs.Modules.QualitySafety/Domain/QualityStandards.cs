using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

public sealed class InspectionTestPlanVersion : AggregateRoot
{
    private InspectionTestPlanVersion() { }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ProjectId { get; private set; }
    public string Code { get; private set; } = string.Empty; public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty; public string StagesJson { get; private set; } = "[]";
    public string AcceptanceCriteria { get; private set; } = string.Empty; public string InspectorRole { get; private set; } = string.Empty;
    public InspectionPointType PointType { get; private set; } public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid CreatedBy { get; private set; } public DateTimeOffset CreatedAt { get; private set; }
    public static InspectionTestPlanVersion Create(Guid id, Guid tenantId, Guid projectId, string code, int version,
        string title, IReadOnlyCollection<string> stages, string acceptanceCriteria, string inspectorRole,
        InspectionPointType pointType, DateTimeOffset effectiveFrom, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        if (version <= 0 || !Enum.IsDefined(pointType)) throw new DomainRuleException("quality_safety.itp.version.invalid", "A positive plan version and point type are required.");
        return new InspectionTestPlanVersion { Id = id, TenantId = tenantId, ProjectId = projectId,
            Code = QualitySafetyRules.Required(code, 80, "quality_safety.itp.code.invalid").ToUpperInvariant(), Version = version,
            Title = QualitySafetyRules.Required(title, 240, "quality_safety.itp.title.invalid"),
            StagesJson = QualitySafetyRules.JsonList(stages, 500, "quality_safety.itp.stage.invalid", true),
            AcceptanceCriteria = QualitySafetyRules.Required(acceptanceCriteria, 4_000, "quality_safety.itp.criteria.invalid"),
            InspectorRole = QualitySafetyRules.Required(inspectorRole, 160, "quality_safety.itp.inspector.invalid"),
            PointType = pointType, EffectiveFrom = effectiveFrom, CreatedBy = actor, CreatedAt = at };
    }
}

public sealed class ChecklistTemplateVersion : AggregateRoot
{
    private ChecklistTemplateVersion() { }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ProjectId { get; private set; }
    public string Code { get; private set; } = string.Empty; public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty; public string ItemsJson { get; private set; } = "[]";
    public DateTimeOffset EffectiveFrom { get; private set; } public Guid CreatedBy { get; private set; } public DateTimeOffset CreatedAt { get; private set; }
    public static ChecklistTemplateVersion Create(Guid id, Guid tenantId, Guid projectId, string code, int version,
        string title, IReadOnlyCollection<string> items, DateTimeOffset effectiveFrom, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        if (version <= 0) throw new DomainRuleException("quality_safety.checklist.version.invalid", "A positive checklist version is required.");
        return new ChecklistTemplateVersion { Id = id, TenantId = tenantId, ProjectId = projectId,
            Code = QualitySafetyRules.Required(code, 80, "quality_safety.checklist.code.invalid").ToUpperInvariant(), Version = version,
            Title = QualitySafetyRules.Required(title, 240, "quality_safety.checklist.title.invalid"),
            ItemsJson = QualitySafetyRules.JsonList(items, 500, "quality_safety.checklist.item.invalid", true),
            EffectiveFrom = effectiveFrom, CreatedBy = actor, CreatedAt = at };
    }
}

public sealed class QualityTestRecord : AggregateRoot
{
    private QualityTestRecord() { }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty; public Guid? InspectionId { get; private set; }
    public string TestType { get; private set; } = string.Empty; public DateTimeOffset TestedAt { get; private set; }
    public string SampleReference { get; private set; } = string.Empty; public string AcceptanceCriteria { get; private set; } = string.Empty;
    public QualityTestResult Result { get; private set; } public string ResultDetails { get; private set; } = string.Empty;
    public string EvidenceReferencesJson { get; private set; } = "[]"; public Guid RecordedBy { get; private set; } public DateTimeOffset CreatedAt { get; private set; }
    public static QualityTestRecord Record(Guid id, Guid tenantId, Guid projectId, Guid? inspectionId, string testType,
        DateTimeOffset testedAt, string sampleReference, string acceptanceCriteria, QualityTestResult result,
        string resultDetails, IReadOnlyCollection<string> evidence, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        if (!Enum.IsDefined(result)) throw new DomainRuleException("quality_safety.test.result.invalid", "Test result is invalid.");
        return new QualityTestRecord { Id = id, TenantId = tenantId, ProjectId = projectId,
            Number = QualitySafetyRules.Number("TST", at, id), InspectionId = inspectionId,
            TestType = QualitySafetyRules.Required(testType, 160, "quality_safety.test.type.invalid"), TestedAt = testedAt,
            SampleReference = QualitySafetyRules.Required(sampleReference, 240, "quality_safety.test.sample.invalid"),
            AcceptanceCriteria = QualitySafetyRules.Required(acceptanceCriteria, 2_000, "quality_safety.test.criteria.invalid"), Result = result,
            ResultDetails = QualitySafetyRules.Required(resultDetails, 2_000, "quality_safety.test.details.invalid"),
            EvidenceReferencesJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.test.evidence.invalid", true),
            RecordedBy = actor, CreatedAt = at };
    }
}

public sealed class HseCompetencyRecord : AggregateRoot
{
    private HseCompetencyRecord() { }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ProjectId { get; private set; }
    public string PersonReference { get; private set; } = string.Empty; public DateOnly InductionDate { get; private set; }
    public DateOnly? ValidUntil { get; private set; } public string CompetenciesJson { get; private set; } = "[]";
    public string EvidenceReferencesJson { get; private set; } = "[]"; public DataClassification Classification { get; private set; }
    public Guid RecordedBy { get; private set; } public DateTimeOffset CreatedAt { get; private set; }
    public static HseCompetencyRecord Record(Guid id, Guid tenantId, Guid projectId, string personReference,
        DateOnly inductionDate, DateOnly? validUntil, IReadOnlyCollection<string> competencies,
        IReadOnlyCollection<string> evidence, DataClassification classification, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        if (validUntil.HasValue && validUntil < inductionDate || classification is DataClassification.GeneralProject or DataClassification.RestrictedQuality)
            throw new DomainRuleException("quality_safety.competency.classification.invalid", "Competency validity and classification are invalid.");
        return new HseCompetencyRecord { Id = id, TenantId = tenantId, ProjectId = projectId,
            PersonReference = QualitySafetyRules.Required(personReference, 240, "quality_safety.competency.person.invalid"),
            InductionDate = inductionDate, ValidUntil = validUntil,
            CompetenciesJson = QualitySafetyRules.JsonList(competencies, 300, "quality_safety.competency.item.invalid", true),
            EvidenceReferencesJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.competency.evidence.invalid", true),
            Classification = classification, RecordedBy = actor, CreatedAt = at };
    }
}

public sealed class ExposureHoursRecord : AggregateRoot
{
    private ExposureHoursRecord() { }
    public Guid Id { get; private set; } public Guid TenantId { get; private set; } public Guid ProjectId { get; private set; }
    public DateOnly PeriodStart { get; private set; } public DateOnly PeriodEnd { get; private set; }
    public decimal Hours { get; private set; } public string SourceReference { get; private set; } = string.Empty;
    public string EvidenceReferencesJson { get; private set; } = "[]"; public Guid ApprovedBy { get; private set; } public DateTimeOffset ApprovedAt { get; private set; }
    public static ExposureHoursRecord Record(Guid id, Guid tenantId, Guid projectId, DateOnly periodStart,
        DateOnly periodEnd, decimal hours, string sourceReference, IReadOnlyCollection<string> evidence,
        Guid approver, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, approver);
        if (periodEnd < periodStart || hours <= 0 || decimal.Round(hours, 2) != hours)
            throw new DomainRuleException("quality_safety.exposure.basis.invalid", "Exposure period and positive hours with two decimals are required.");
        return new ExposureHoursRecord { Id = id, TenantId = tenantId, ProjectId = projectId,
            PeriodStart = periodStart, PeriodEnd = periodEnd, Hours = hours,
            SourceReference = QualitySafetyRules.Required(sourceReference, 500, "quality_safety.exposure.source.invalid"),
            EvidenceReferencesJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.exposure.evidence.invalid", true),
            ApprovedBy = approver, ApprovedAt = at };
    }
}
