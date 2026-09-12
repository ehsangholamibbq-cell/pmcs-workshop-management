using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

public sealed class ManagementIssue : AggregateRoot
{
    private ManagementIssue() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string ObservedFact { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public GovernanceSeverity Severity { get; private set; }
    public GovernanceUrgency Urgency { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string OwnerDisplayName { get; private set; } = string.Empty;
    public DateOnly TargetResolutionDate { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceEntityType { get; private set; } = string.Empty;
    public Guid? SourceEntityId { get; private set; }
    public long? SourceRevision { get; private set; }
    public string SourceSnapshot { get; private set; } = string.Empty;
    public Guid? MaterializedFromRiskId { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public RecordConfidentiality Confidentiality { get; private set; }
    public IssueStatus Status { get; private set; }
    public DateTimeOffset? SlaDueAt { get; private set; }
    public Guid? SlaRuleVersionId { get; private set; }
    public string? ResolutionNote { get; private set; }
    public string ClosureEvidenceJson { get; private set; } = "[]";
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? LastChangedBy { get; private set; }
    public DateTimeOffset? LastChangedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public IReadOnlyCollection<string> EvidenceReferences => GovernanceRules.ReadList(EvidenceReferencesJson);
    public IReadOnlyCollection<string> ClosureEvidence => GovernanceRules.ReadList(ClosureEvidenceJson);

    public static ManagementIssue Create(Guid id, Guid tenantId, Guid projectId, string title,
        string observedFact, string category, GovernanceSeverity severity, GovernanceUrgency urgency,
        Guid ownerUserId, string ownerDisplayName, DateOnly targetResolutionDate,
        string sourceModule, string sourceEntityType, Guid? sourceEntityId, long? sourceRevision,
        string sourceSnapshot, Guid? materializedFromRiskId, IReadOnlyCollection<string>? evidence,
        RecordConfidentiality confidentiality, DateTimeOffset? slaDueAt, Guid? slaRuleVersionId,
        Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Identities(id, tenantId, projectId, ownerUserId, actor);
        if (!Enum.IsDefined(severity) || !Enum.IsDefined(urgency) || !Enum.IsDefined(confidentiality) || targetResolutionDate == default)
            throw new DomainRuleException("governance.issue.classification.invalid", "Issue classification and target date are required.");
        return new ManagementIssue
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = GovernanceRules.Number("ISS", at, id),
            Title = GovernanceRules.Required(title, 240, "governance.issue.title.invalid"),
            ObservedFact = GovernanceRules.Required(observedFact, 5_000, "governance.issue.fact.invalid"),
            Category = GovernanceRules.Required(category, 120, "governance.issue.category.invalid"),
            Severity = severity, Urgency = urgency, OwnerUserId = ownerUserId,
            OwnerDisplayName = GovernanceRules.Required(ownerDisplayName, 200, "governance.issue.owner.invalid"),
            TargetResolutionDate = targetResolutionDate,
            SourceModule = GovernanceRules.Required(sourceModule, 80, "governance.source.module.invalid"),
            SourceEntityType = GovernanceRules.Required(sourceEntityType, 120, "governance.source.type.invalid"),
            SourceEntityId = sourceEntityId, SourceRevision = sourceRevision,
            SourceSnapshot = GovernanceRules.Required(sourceSnapshot, 1_000, "governance.source.snapshot.invalid"),
            MaterializedFromRiskId = materializedFromRiskId,
            EvidenceReferencesJson = GovernanceRules.JsonList(evidence, 700, "governance.issue.evidence.invalid", true),
            Confidentiality = confidentiality, Status = IssueStatus.Open, SlaDueAt = slaDueAt,
            SlaRuleVersionId = slaRuleVersionId, CreatedBy = actor, CreatedAt = at
        };
    }

    public void Transition(long baseRevision, IssueStatus target, string? resolutionNote,
        IReadOnlyCollection<string>? closureEvidence, Guid actor, DateTimeOffset at)
    {
        GovernanceRules.Revision(Revision, baseRevision, "governance.issue.revision.conflict");
        GovernanceRules.Identities(actor);
        if (!Allowed(Status).Contains(target))
            throw new DomainRuleException("governance.issue.transition.invalid", "Issue transition is not allowed.");
        if (target == IssueStatus.Resolved && string.IsNullOrWhiteSpace(resolutionNote))
            throw new DomainRuleException("governance.issue.resolution.required", "Resolution note is required.");
        var closure = GovernanceRules.JsonList(closureEvidence, 700, "governance.issue.closure_evidence.invalid", target == IssueStatus.Closed);
        if (target == IssueStatus.Closed && Status != IssueStatus.Resolved)
            throw new DomainRuleException("governance.issue.verify_before_close", "A resolved issue must be independently verified before close.");
        if (target == IssueStatus.Closed && ResolvedBy == actor)
            throw new DomainRuleException("governance.issue.independent_verifier.required", "The resolver cannot verify and close the same issue.");
        Status = target; ResolutionNote = GovernanceRules.Optional(resolutionNote, 2_000, "governance.issue.resolution.too_long");
        if (target == IssueStatus.Resolved) { ResolvedAt = at; ResolvedBy = actor; }
        if (target == IssueStatus.Closed) { ClosureEvidenceJson = closure; ClosedAt = at; ClosedBy = actor; }
        if (target == IssueStatus.Reopened)
        {
            ResolvedAt = null; ResolvedBy = null; ClosedAt = null; ClosedBy = null;
            ClosureEvidenceJson = "[]";
        }
        LastChangedBy = actor; LastChangedAt = at; AdvanceRevision();
    }

    private static HashSet<IssueStatus> Allowed(IssueStatus current) => current switch
    {
        IssueStatus.Open => new HashSet<IssueStatus> { IssueStatus.UnderAssessment, IssueStatus.NotAnIssue, IssueStatus.Void },
        IssueStatus.UnderAssessment => new HashSet<IssueStatus> { IssueStatus.ResponseInProgress, IssueStatus.NotAnIssue, IssueStatus.Void },
        IssueStatus.ResponseInProgress => new HashSet<IssueStatus> { IssueStatus.PendingVerification, IssueStatus.Resolved },
        IssueStatus.PendingVerification => new HashSet<IssueStatus> { IssueStatus.Resolved, IssueStatus.ResponseInProgress },
        IssueStatus.Resolved => new HashSet<IssueStatus> { IssueStatus.Closed, IssueStatus.Reopened },
        IssueStatus.Closed => new HashSet<IssueStatus> { IssueStatus.Reopened },
        IssueStatus.Reopened => new HashSet<IssueStatus> { IssueStatus.UnderAssessment, IssueStatus.ResponseInProgress },
        _ => new HashSet<IssueStatus>()
    };
}
