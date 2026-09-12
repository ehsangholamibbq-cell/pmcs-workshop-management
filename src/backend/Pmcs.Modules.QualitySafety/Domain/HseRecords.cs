using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

public sealed class SafetyIncident : AggregateRoot
{
    private SafetyIncident() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid? SourceIntakeId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string Facts { get; private set; } = string.Empty;
    public InitialSeverity PreliminarySeverity { get; private set; }
    public InitialSeverity? FinalSeverity { get; private set; }
    public Guid MatrixVersionId { get; private set; }
    public DataClassification Classification { get; private set; }
    public IncidentStatus Status { get; private set; }
    public RootCauseStatus RootCauseStatus { get; private set; }
    public string? RootCause { get; private set; }
    public string ClosureEvidenceJson { get; private set; } = "[]";
    public Guid ReportedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static SafetyIncident Report(Guid id, Guid tenantId, Guid projectId, Guid? sourceIntakeId,
        DateTimeOffset occurredAt, string location, string facts, InitialSeverity preliminarySeverity,
        Guid matrixVersionId, DataClassification classification, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, matrixVersionId, actor);
        if (preliminarySeverity == InitialSeverity.Unassessed ||
            classification is DataClassification.GeneralProject or DataClassification.RestrictedQuality)
            throw new DomainRuleException("quality_safety.incident.classification.invalid", "Incident severity and restricted HSE classification are required.");
        return new SafetyIncident
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("INC", at, id),
            SourceIntakeId = sourceIntakeId, OccurredAt = occurredAt,
            Location = QualitySafetyRules.Required(location, 240, "quality_safety.incident.location.invalid"),
            Facts = QualitySafetyRules.Required(facts, 6_000, "quality_safety.incident.facts.invalid"),
            PreliminarySeverity = preliminarySeverity, MatrixVersionId = matrixVersionId,
            Classification = classification, Status = IncidentStatus.Reported,
            RootCauseStatus = RootCauseStatus.NotStarted, ReportedBy = actor, CreatedAt = at
        };
    }

    public void Transition(long baseRevision, IncidentStatus target, InitialSeverity? finalSeverity,
        RootCauseStatus rootCauseStatus, string? rootCause, IReadOnlyCollection<string>? closureEvidence,
        DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.incident.revision.conflict");
        if (!Allowed(Status).Contains(target))
            throw new DomainRuleException("quality_safety.incident.transition.invalid", "Incident transition is not allowed.");
        if (target >= IncidentStatus.FinalReview && (!finalSeverity.HasValue || finalSeverity == InitialSeverity.Unassessed))
            throw new DomainRuleException("quality_safety.incident.final_severity.required", "Final review requires a separately confirmed final severity.");
        if (rootCauseStatus == RootCauseStatus.Confirmed && string.IsNullOrWhiteSpace(rootCause))
            throw new DomainRuleException("quality_safety.incident.root_cause.required", "Confirmed root cause requires a documented cause.");
        var evidenceJson = QualitySafetyRules.JsonList(closureEvidence, 700, "quality_safety.incident.closure_evidence.invalid",
            target == IncidentStatus.Closed);
        Status = target; FinalSeverity = finalSeverity ?? FinalSeverity; RootCauseStatus = rootCauseStatus;
        RootCause = QualitySafetyRules.Optional(rootCause, 4_000, "quality_safety.incident.root_cause.too_long");
        if (target == IncidentStatus.Closed) { ClosureEvidenceJson = evidenceJson; ClosedAt = at; }
        AdvanceRevision();
    }

    private static HashSet<IncidentStatus> Allowed(IncidentStatus current) => current switch
    {
        IncidentStatus.Reported => new HashSet<IncidentStatus> { IncidentStatus.Triage },
        IncidentStatus.Triage => new HashSet<IncidentStatus> { IncidentStatus.Contained },
        IncidentStatus.Contained => new HashSet<IncidentStatus> { IncidentStatus.UnderInvestigation },
        IncidentStatus.UnderInvestigation => new HashSet<IncidentStatus> { IncidentStatus.ActionsOpen },
        IncidentStatus.ActionsOpen => new HashSet<IncidentStatus> { IncidentStatus.FinalReview },
        IncidentStatus.FinalReview => new HashSet<IncidentStatus> { IncidentStatus.Closed },
        IncidentStatus.Closed => new HashSet<IncidentStatus> { IncidentStatus.Reopened },
        IncidentStatus.Reopened => new HashSet<IncidentStatus> { IncidentStatus.UnderInvestigation },
        _ => new HashSet<IncidentStatus>()
    };
}

public sealed class PermitToWork : AggregateRoot
{
    private PermitToWork() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string WorkDescription { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidTo { get; private set; }
    public string HazardsJson { get; private set; } = "[]";
    public string ControlsJson { get; private set; } = "[]";
    public PermitStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static PermitToWork Create(Guid id, Guid tenantId, Guid projectId, string workDescription,
        string location, DateTimeOffset validFrom, DateTimeOffset validTo, IReadOnlyCollection<string> hazards,
        IReadOnlyCollection<string> controls, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        if (validTo <= validFrom)
            throw new DomainRuleException("quality_safety.permit.window.invalid", "Permit validity end must be after its start.");
        return new PermitToWork
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("PTW", at, id),
            WorkDescription = QualitySafetyRules.Required(workDescription, 2_000, "quality_safety.permit.work.invalid"),
            Location = QualitySafetyRules.Required(location, 240, "quality_safety.permit.location.invalid"),
            ValidFrom = validFrom, ValidTo = validTo,
            HazardsJson = QualitySafetyRules.JsonList(hazards, 500, "quality_safety.permit.hazard.invalid", true),
            ControlsJson = QualitySafetyRules.JsonList(controls, 500, "quality_safety.permit.control.invalid", true),
            Status = PermitStatus.Draft, RequestedBy = actor, CreatedAt = at
        };
    }

    public void Transition(long baseRevision, PermitStatus target, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.permit.revision.conflict"); QualitySafetyRules.Identity(actor);
        var allowed = Status switch
        {
            PermitStatus.Draft => target is PermitStatus.Submitted or PermitStatus.Cancelled,
            PermitStatus.Submitted => target is PermitStatus.Approved or PermitStatus.Cancelled,
            PermitStatus.Approved => target is PermitStatus.Active or PermitStatus.Cancelled,
            PermitStatus.Active => target is PermitStatus.Suspended or PermitStatus.Closed,
            PermitStatus.Suspended => target is PermitStatus.Active or PermitStatus.Closed,
            _ => false
        };
        if (!allowed) throw new DomainRuleException("quality_safety.permit.transition.invalid", "Permit transition is not allowed.");
        if (target == PermitStatus.Active && (at < ValidFrom || at > ValidTo))
            throw new DomainRuleException("quality_safety.permit.activation.outside_window", "Permit can only activate inside its validity window.");
        Status = target;
        if (target == PermitStatus.Approved) { ApprovedBy = actor; ApprovedAt = at; }
        if (target == PermitStatus.Closed) ClosedAt = at;
        AdvanceRevision();
    }
}

public sealed class ToolboxTalk : AggregateRoot
{
    private ToolboxTalk() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Topic { get; private set; } = string.Empty;
    public DateTimeOffset HeldAt { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string AttendeesJson { get; private set; } = "[]";
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ToolboxTalk Record(Guid id, Guid tenantId, Guid projectId, string topic, DateTimeOffset heldAt,
        string location, IReadOnlyCollection<string> attendees, IReadOnlyCollection<string> evidence, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        return new ToolboxTalk
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("TBT", at, id),
            Topic = QualitySafetyRules.Required(topic, 500, "quality_safety.toolbox.topic.invalid"), HeldAt = heldAt,
            Location = QualitySafetyRules.Required(location, 240, "quality_safety.toolbox.location.invalid"),
            AttendeesJson = QualitySafetyRules.JsonList(attendees, 240, "quality_safety.toolbox.attendee.invalid", true),
            EvidenceReferencesJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.toolbox.evidence.invalid", true),
            RecordedBy = actor, CreatedAt = at
        };
    }
}
