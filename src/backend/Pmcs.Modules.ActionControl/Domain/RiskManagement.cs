using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

public sealed class GovernanceRiskMatrixVersion : AggregateRoot
{
    private GovernanceRiskMatrixVersion() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FormulaVersion { get; private set; } = string.Empty;
    public int LowMaximum { get; private set; }
    public int ModerateMaximum { get; private set; }
    public int HighMaximum { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static GovernanceRiskMatrixVersion Create(Guid id, Guid tenantId, Guid projectId, int version,
        string title, int lowMaximum, int moderateMaximum, int highMaximum,
        DateTimeOffset effectiveFrom, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Identities(id, tenantId, projectId, actor);
        if (version <= 0 || lowMaximum is < 1 or > 22 || moderateMaximum <= lowMaximum ||
            highMaximum <= moderateMaximum || highMaximum >= 25)
            throw new DomainRuleException("governance.risk_matrix.thresholds.invalid", "Risk matrix thresholds are invalid.");
        return new GovernanceRiskMatrixVersion
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Version = version,
            Title = GovernanceRules.Required(title, 200, "governance.risk_matrix.title.invalid"),
            FormulaVersion = "probability-x-maximum-impact-v1", LowMaximum = lowMaximum,
            ModerateMaximum = moderateMaximum, HighMaximum = highMaximum,
            EffectiveFrom = effectiveFrom, CreatedBy = actor, CreatedAt = at
        };
    }

    public (int Score, RiskRatingBand Band) Rate(ProbabilityBand probability, ImpactBand maximumImpact)
    {
        if (!Enum.IsDefined(probability) || !Enum.IsDefined(maximumImpact))
            throw new DomainRuleException("governance.risk.assessment.invalid", "Probability and impact are required.");
        var score = (int)probability * (int)maximumImpact;
        var band = score <= LowMaximum ? RiskRatingBand.Low : score <= ModerateMaximum
            ? RiskRatingBand.Moderate : score <= HighMaximum ? RiskRatingBand.High : RiskRatingBand.Critical;
        return (score, band);
    }
}

public sealed class ProjectRisk : AggregateRoot
{
    private ProjectRisk() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public RiskType Type { get; private set; }
    public string Cause { get; private set; } = string.Empty;
    public string UncertainEvent { get; private set; } = string.Empty;
    public string ImpactStatement { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public string OwnerDisplayName { get; private set; } = string.Empty;
    public RecordConfidentiality Confidentiality { get; private set; }
    public RiskStatus Status { get; private set; }
    public ProbabilityBand? Probability { get; private set; }
    public ImpactBand? TimeImpact { get; private set; }
    public ImpactBand? CostImpact { get; private set; }
    public ImpactBand? QualityImpact { get; private set; }
    public ImpactBand? SafetyImpact { get; private set; }
    public ImpactBand? ContractImpact { get; private set; }
    public ImpactBand? OperationsImpact { get; private set; }
    public int? InherentScore { get; private set; }
    public RiskRatingBand? InherentRating { get; private set; }
    public ProbabilityBand? ResidualProbability { get; private set; }
    public ImpactBand? ResidualImpact { get; private set; }
    public int? ResidualScore { get; private set; }
    public RiskRatingBand? ResidualRating { get; private set; }
    public Guid? MatrixVersionId { get; private set; }
    public int? MatrixVersion { get; private set; }
    public string? FormulaVersion { get; private set; }
    public RiskResponseStrategy? ResponseStrategy { get; private set; }
    public string? ResponsePlan { get; private set; }
    public string? EarlyWarningIndicator { get; private set; }
    public DateOnly? ReviewDate { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceEntityType { get; private set; } = string.Empty;
    public Guid? SourceEntityId { get; private set; }
    public long? SourceRevision { get; private set; }
    public string SourceSnapshot { get; private set; } = string.Empty;
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public Guid? MaterializedIssueId { get; private set; }
    public DateTimeOffset? SlaDueAt { get; private set; }
    public Guid? SlaRuleVersionId { get; private set; }
    public string? ClosureReason { get; private set; }
    public string ClosureEvidenceJson { get; private set; } = "[]";
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? LastReviewedBy { get; private set; }
    public DateTimeOffset? LastReviewedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public IReadOnlyCollection<string> EvidenceReferences => GovernanceRules.ReadList(EvidenceReferencesJson);
    public IReadOnlyCollection<string> ClosureEvidence => GovernanceRules.ReadList(ClosureEvidenceJson);

    public static ProjectRisk Propose(Guid id, Guid tenantId, Guid projectId, RiskType type,
        string cause, string uncertainEvent, string impact, string category, Guid ownerUserId,
        string ownerDisplayName, RecordConfidentiality confidentiality, string sourceModule,
        string sourceEntityType, Guid? sourceEntityId, long? sourceRevision, string sourceSnapshot,
        IReadOnlyCollection<string>? evidence, DateTimeOffset? slaDueAt, Guid? slaRuleVersionId,
        Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Identities(id, tenantId, projectId, ownerUserId, actor);
        if (!Enum.IsDefined(type) || !Enum.IsDefined(confidentiality))
            throw new DomainRuleException("governance.risk.classification.invalid", "Risk type and confidentiality are invalid.");
        return new ProjectRisk
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = GovernanceRules.Number("RSK", at, id),
            Type = type, Cause = GovernanceRules.Required(cause, 1_500, "governance.risk.cause.invalid"),
            UncertainEvent = GovernanceRules.Required(uncertainEvent, 1_500, "governance.risk.event.invalid"),
            ImpactStatement = GovernanceRules.Required(impact, 2_000, "governance.risk.impact.invalid"),
            Category = GovernanceRules.Required(category, 120, "governance.risk.category.invalid"),
            OwnerUserId = ownerUserId, OwnerDisplayName = GovernanceRules.Required(ownerDisplayName, 200, "governance.risk.owner.invalid"),
            Confidentiality = confidentiality, Status = RiskStatus.Proposed,
            SourceModule = GovernanceRules.Required(sourceModule, 80, "governance.source.module.invalid"),
            SourceEntityType = GovernanceRules.Required(sourceEntityType, 120, "governance.source.type.invalid"),
            SourceEntityId = sourceEntityId, SourceRevision = sourceRevision,
            SourceSnapshot = GovernanceRules.Required(sourceSnapshot, 1_000, "governance.source.snapshot.invalid"),
            EvidenceReferencesJson = GovernanceRules.JsonList(evidence, 700, "governance.risk.evidence.invalid", true),
            SlaDueAt = slaDueAt, SlaRuleVersionId = slaRuleVersionId, CreatedBy = actor, CreatedAt = at
        };
    }

    public void Assess(long baseRevision, GovernanceRiskMatrixVersion matrix, ProbabilityBand probability,
        ImpactBand timeImpact, ImpactBand costImpact, ImpactBand qualityImpact, ImpactBand safetyImpact,
        ImpactBand contractImpact, ImpactBand operationsImpact, RiskResponseStrategy strategy,
        string responsePlan, string earlyWarningIndicator, DateOnly reviewDate, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.risk.revision.conflict"); GovernanceRules.Identities(actor);
        if (Status is not (RiskStatus.Proposed or RiskStatus.Reopened) || matrix.ProjectId != ProjectId || matrix.TenantId != TenantId || !Enum.IsDefined(strategy))
            throw new DomainRuleException("governance.risk.assessment.invalid_state", "Risk cannot be assessed in this state or matrix scope.");
        var maximum = new[] { timeImpact, costImpact, qualityImpact, safetyImpact, contractImpact, operationsImpact }.Max();
        var rating = matrix.Rate(probability, maximum);
        Probability = probability; TimeImpact = timeImpact; CostImpact = costImpact; QualityImpact = qualityImpact;
        SafetyImpact = safetyImpact; ContractImpact = contractImpact; OperationsImpact = operationsImpact;
        InherentScore = rating.Score; InherentRating = rating.Band; MatrixVersionId = matrix.Id;
        MatrixVersion = matrix.Version; FormulaVersion = matrix.FormulaVersion; ResponseStrategy = strategy;
        ResponsePlan = GovernanceRules.Required(responsePlan, 4_000, "governance.risk.response.invalid");
        EarlyWarningIndicator = GovernanceRules.Required(earlyWarningIndicator, 1_000, "governance.risk.trigger.invalid");
        ReviewDate = reviewDate; Status = RiskStatus.Assessed; LastReviewedBy = actor; LastReviewedAt = at; AdvanceRevision();
    }

    public void Activate(long baseRevision, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.risk.revision.conflict"); GovernanceRules.Identities(actor);
        if (Status != RiskStatus.Assessed) throw new DomainRuleException("governance.risk.activate.invalid_state", "Only an assessed risk can become active.");
        Status = RiskStatus.Active; LastReviewedBy = actor; LastReviewedAt = at; AdvanceRevision();
    }

    public void Review(long baseRevision, GovernanceRiskMatrixVersion matrix, ProbabilityBand residualProbability,
        ImpactBand residualImpact, string responsePlan, DateOnly nextReviewDate, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.risk.revision.conflict"); GovernanceRules.Identities(actor);
        if (Status is not (RiskStatus.Active or RiskStatus.Monitoring) || matrix.Id != MatrixVersionId)
            throw new DomainRuleException("governance.risk.review.invalid_state", "Only an active risk can be reviewed with its pinned matrix.");
        var rating = matrix.Rate(residualProbability, residualImpact);
        ResidualProbability = residualProbability; ResidualImpact = residualImpact; ResidualScore = rating.Score;
        ResidualRating = rating.Band; ResponsePlan = GovernanceRules.Required(responsePlan, 4_000, "governance.risk.response.invalid");
        ReviewDate = nextReviewDate; Status = RiskStatus.Monitoring; LastReviewedBy = actor; LastReviewedAt = at; AdvanceRevision();
    }

    public void Materialize(long baseRevision, Guid issueId, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.risk.revision.conflict"); GovernanceRules.Identities(issueId, actor);
        if (Status is not (RiskStatus.Active or RiskStatus.Monitoring) || MaterializedIssueId.HasValue)
            throw new DomainRuleException("governance.risk.materialize.invalid_state", "Only an unmaterialized active risk can materialize.");
        MaterializedIssueId = issueId; Status = RiskStatus.Materialized; LastReviewedBy = actor; LastReviewedAt = at; AdvanceRevision();
    }

    public void Close(long baseRevision, bool expired, string reason, IReadOnlyCollection<string>? evidence,
        Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.risk.revision.conflict"); GovernanceRules.Identities(actor);
        if (Status is RiskStatus.Closed or RiskStatus.Expired or RiskStatus.Materialized)
            throw new DomainRuleException("governance.risk.close.invalid_state", "Risk cannot close in this state.");
        ClosureReason = GovernanceRules.Required(reason, 1_500, "governance.risk.closure_reason.invalid");
        ClosureEvidenceJson = GovernanceRules.JsonList(evidence, 700, "governance.risk.closure_evidence.invalid", true);
        Status = expired ? RiskStatus.Expired : RiskStatus.Closed; ClosedAt = at; LastReviewedBy = actor; LastReviewedAt = at; AdvanceRevision();
    }

    public void Reopen(long baseRevision, string reason, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.risk.revision.conflict"); GovernanceRules.Identities(actor);
        if (Status is not (RiskStatus.Closed or RiskStatus.Expired))
            throw new DomainRuleException("governance.risk.reopen.invalid_state", "Only a closed or expired risk can reopen.");
        _ = GovernanceRules.Required(reason, 1_000, "governance.risk.reopen_reason.invalid");
        Status = RiskStatus.Reopened; ClosedAt = null; LastReviewedBy = actor; LastReviewedAt = at; AdvanceRevision();
    }
}
