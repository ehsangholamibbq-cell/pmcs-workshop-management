using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

public sealed class CorrectiveAction : AggregateRoot
{
    private CorrectiveAction() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public ControlArea SourceArea { get; private set; }
    public string SourceRecordType { get; private set; } = string.Empty;
    public Guid SourceRecordId { get; private set; }
    public CorrectiveActionKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public string ResponsibleParty { get; private set; } = string.Empty;
    public DateOnly DueDate { get; private set; }
    public string SuccessCriteria { get; private set; } = string.Empty;
    public CorrectiveActionStatus Status { get; private set; }
    public string CompletionEvidenceJson { get; private set; } = "[]";
    public string VerificationEvidenceJson { get; private set; } = "[]";
    public Guid? VerifiedBy { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateOnly? ExtendedDueDate { get; private set; }
    public string? ExtensionReason { get; private set; }
    public Guid? ExtensionApprovedBy { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static CorrectiveAction Create(Guid id, Guid tenantId, Guid projectId, ControlArea sourceArea,
        string sourceRecordType, Guid sourceRecordId, CorrectiveActionKind kind, string title,
        Guid ownerUserId, string responsibleParty, DateOnly dueDate, string successCriteria,
        Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, sourceRecordId, ownerUserId, actor);
        if (!Enum.IsDefined(sourceArea) || !Enum.IsDefined(kind))
            throw new DomainRuleException("quality_safety.action.classification.invalid", "Corrective action classification is invalid.");
        if (dueDate == default)
            throw new DomainRuleException("quality_safety.action.due_date.required", "Corrective action due date is required.");
        return new CorrectiveAction
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("CAPA", at, id),
            SourceArea = sourceArea,
            SourceRecordType = QualitySafetyRules.Required(sourceRecordType, 120, "quality_safety.action.source_type.invalid"),
            SourceRecordId = sourceRecordId, Kind = kind,
            Title = QualitySafetyRules.Required(title, 240, "quality_safety.action.title.invalid"), OwnerUserId = ownerUserId,
            ResponsibleParty = QualitySafetyRules.Required(responsibleParty, 240, "quality_safety.action.party.invalid"),
            DueDate = dueDate, SuccessCriteria = QualitySafetyRules.Required(successCriteria, 2_000, "quality_safety.action.criteria.invalid"),
            Status = CorrectiveActionStatus.Open, CreatedBy = actor, CreatedAt = at
        };
    }

    public void Extend(long baseRevision, DateOnly extendedDueDate, string reason, Guid approvedBy)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.action.revision.conflict"); QualitySafetyRules.Identity(approvedBy);
        if (Status is CorrectiveActionStatus.Closed or CorrectiveActionStatus.Cancelled || extendedDueDate <= (ExtendedDueDate ?? DueDate))
            throw new DomainRuleException("quality_safety.action.extension.invalid", "An open action requires a later approved due date.");
        ExtendedDueDate = extendedDueDate;
        ExtensionReason = QualitySafetyRules.Required(reason, 1_000, "quality_safety.action.extension_reason.invalid");
        ExtensionApprovedBy = approvedBy; AdvanceRevision();
    }

    public void Transition(long baseRevision, CorrectiveActionStatus target, IReadOnlyCollection<string>? evidence,
        Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.action.revision.conflict"); QualitySafetyRules.Identity(actor);
        if (!Allowed(Status).Contains(target))
            throw new DomainRuleException("quality_safety.action.transition.invalid", "Corrective action transition is not allowed.");
        var evidenceJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.action.evidence.invalid",
            target is CorrectiveActionStatus.Completed or CorrectiveActionStatus.Verified);
        if (target == CorrectiveActionStatus.Completed) CompletionEvidenceJson = evidenceJson;
        if (target == CorrectiveActionStatus.Verified)
        { VerificationEvidenceJson = evidenceJson; VerifiedBy = actor; VerifiedAt = at; }
        Status = target; ClosedAt = target == CorrectiveActionStatus.Closed ? at : null; AdvanceRevision();
    }

    private static HashSet<CorrectiveActionStatus> Allowed(CorrectiveActionStatus current) => current switch
    {
        CorrectiveActionStatus.Open => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.InProgress, CorrectiveActionStatus.Cancelled },
        CorrectiveActionStatus.InProgress => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.Completed, CorrectiveActionStatus.Cancelled },
        CorrectiveActionStatus.Completed => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.ReadyForVerification, CorrectiveActionStatus.InProgress },
        CorrectiveActionStatus.ReadyForVerification => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.Verified, CorrectiveActionStatus.InProgress },
        CorrectiveActionStatus.Verified => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.Closed, CorrectiveActionStatus.InProgress },
        CorrectiveActionStatus.Closed => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.Reopened },
        CorrectiveActionStatus.Reopened => new HashSet<CorrectiveActionStatus> { CorrectiveActionStatus.InProgress },
        _ => new HashSet<CorrectiveActionStatus>()
    };
}
