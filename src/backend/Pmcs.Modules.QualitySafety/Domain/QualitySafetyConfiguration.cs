using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

public sealed class QualitySafetyConfiguration : AggregateRoot
{
    private QualitySafetyConfiguration() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public QualityOperatingMode QualityMode { get; private set; }
    public HseOperatingMode HseMode { get; private set; }
    public Guid? QualityOwnerUserId { get; private set; }
    public Guid? HseOwnerUserId { get; private set; }
    public Guid? QualityMatrixVersionId { get; private set; }
    public Guid? HseMatrixVersionId { get; private set; }
    public bool WorkflowAndSlaDefined { get; private set; }
    public bool TemplatesDefined { get; private set; }
    public bool EvidenceAndClosureRulesDefined { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public Guid ChangedBy { get; private set; }

    public bool QualityReady => QualityMode == QualityOperatingMode.Disabled ||
        QualityOwnerUserId.HasValue && QualityMatrixVersionId.HasValue && WorkflowAndSlaDefined && TemplatesDefined && EvidenceAndClosureRulesDefined;
    public bool HseReady => HseMode == HseOperatingMode.Disabled ||
        HseOwnerUserId.HasValue && HseMatrixVersionId.HasValue && WorkflowAndSlaDefined && TemplatesDefined && EvidenceAndClosureRulesDefined;

    public static QualitySafetyConfiguration Create(
        Guid id, Guid tenantId, Guid projectId, QualityOperatingMode qualityMode, HseOperatingMode hseMode,
        Guid? qualityOwnerUserId, Guid? hseOwnerUserId, Guid? qualityMatrixVersionId, Guid? hseMatrixVersionId,
        bool workflowAndSlaDefined, bool templatesDefined, bool evidenceAndClosureRulesDefined,
        Guid changedBy, DateTimeOffset changedAt)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, changedBy);
        EnsureValid(qualityMode, hseMode, qualityOwnerUserId, hseOwnerUserId, qualityMatrixVersionId, hseMatrixVersionId);
        return new QualitySafetyConfiguration
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, QualityMode = qualityMode, HseMode = hseMode,
            QualityOwnerUserId = qualityOwnerUserId, HseOwnerUserId = hseOwnerUserId,
            QualityMatrixVersionId = qualityMatrixVersionId, HseMatrixVersionId = hseMatrixVersionId,
            WorkflowAndSlaDefined = workflowAndSlaDefined, TemplatesDefined = templatesDefined,
            EvidenceAndClosureRulesDefined = evidenceAndClosureRulesDefined, ChangedBy = changedBy, ChangedAt = changedAt
        };
    }

    public void Reconfigure(long baseRevision, QualityOperatingMode qualityMode, HseOperatingMode hseMode,
        Guid? qualityOwnerUserId, Guid? hseOwnerUserId, Guid? qualityMatrixVersionId, Guid? hseMatrixVersionId,
        bool workflowAndSlaDefined, bool templatesDefined, bool evidenceAndClosureRulesDefined,
        Guid changedBy, DateTimeOffset changedAt)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.configuration.revision.conflict");
        QualitySafetyRules.Identity(changedBy);
        EnsureValid(qualityMode, hseMode, qualityOwnerUserId, hseOwnerUserId, qualityMatrixVersionId, hseMatrixVersionId);
        QualityMode = qualityMode; HseMode = hseMode; QualityOwnerUserId = qualityOwnerUserId; HseOwnerUserId = hseOwnerUserId;
        QualityMatrixVersionId = qualityMatrixVersionId; HseMatrixVersionId = hseMatrixVersionId;
        WorkflowAndSlaDefined = workflowAndSlaDefined; TemplatesDefined = templatesDefined;
        EvidenceAndClosureRulesDefined = evidenceAndClosureRulesDefined; ChangedBy = changedBy; ChangedAt = changedAt;
        AdvanceRevision();
    }

    private static void EnsureValid(QualityOperatingMode qualityMode, HseOperatingMode hseMode,
        Guid? qualityOwner, Guid? hseOwner, Guid? qualityMatrix, Guid? hseMatrix)
    {
        if (!Enum.IsDefined(qualityMode) || !Enum.IsDefined(hseMode))
            throw new DomainRuleException("quality_safety.configuration.mode.invalid", "Configuration mode is invalid.");
        if (qualityMode == QualityOperatingMode.Disabled && (qualityOwner.HasValue || qualityMatrix.HasValue))
            throw new DomainRuleException("quality_safety.configuration.quality.disabled_details", "Disabled quality cannot have an owner or matrix.");
        if (hseMode == HseOperatingMode.Disabled && (hseOwner.HasValue || hseMatrix.HasValue))
            throw new DomainRuleException("quality_safety.configuration.hse.disabled_details", "Disabled HSE cannot have an owner or matrix.");
    }
}

public sealed class RiskMatrixVersion : AggregateRoot
{
    private RiskMatrixVersion() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public ControlArea Area { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string DefinitionJson { get; private set; } = "{}";
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static RiskMatrixVersion Create(Guid id, Guid tenantId, Guid projectId, ControlArea area, int version,
        string title, string definitionJson, DateTimeOffset effectiveFrom, Guid createdBy, DateTimeOffset createdAt)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(area) || version <= 0)
            throw new DomainRuleException("quality_safety.matrix.version.invalid", "Matrix area and positive version are required.");
        var definition = QualitySafetyRules.Required(definitionJson, 20_000, "quality_safety.matrix.definition.invalid");
        try { System.Text.Json.JsonDocument.Parse(definition); }
        catch (System.Text.Json.JsonException exception)
        { throw new DomainRuleException("quality_safety.matrix.definition.invalid", exception.Message); }
        return new RiskMatrixVersion
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Area = area, Version = version,
            Title = QualitySafetyRules.Required(title, 200, "quality_safety.matrix.title.invalid"),
            DefinitionJson = definition, EffectiveFrom = effectiveFrom, CreatedBy = createdBy, CreatedAt = createdAt
        };
    }
}
