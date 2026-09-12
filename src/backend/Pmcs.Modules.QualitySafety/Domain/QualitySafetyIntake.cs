using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

public sealed class QualitySafetyIntake : AggregateRoot
{
    private QualitySafetyIntake() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public IntakeKind Kind { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string Facts { get; private set; } = string.Empty;
    public InitialSeverity InitialSeverity { get; private set; }
    public string? ImmediateAction { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public DataClassification Classification { get; private set; }
    public Guid ReportedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IntakeStatus Status { get; private set; }
    public IntakeConversionType ConversionType { get; private set; }
    public Guid? ConvertedRecordId { get; private set; }
    public string? TriageNote { get; private set; }
    public Guid? TriagedBy { get; private set; }
    public DateTimeOffset? TriagedAt { get; private set; }
    public IReadOnlyCollection<string> EvidenceReferences => QualitySafetyRules.ReadList(EvidenceReferencesJson);

    public bool IsQuality => Kind is IntakeKind.QualityObservation or IntakeKind.Defect;
    public bool IsHse => !IsQuality;

    public static QualitySafetyIntake Capture(Guid id, Guid tenantId, Guid projectId, IntakeKind kind,
        DateTimeOffset observedAt, string location, string facts, InitialSeverity initialSeverity,
        string? immediateAction, IReadOnlyCollection<string>? evidenceReferences,
        DataClassification classification, Guid reportedBy, DateTimeOffset createdAt)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, reportedBy);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(initialSeverity) || !Enum.IsDefined(classification))
            throw new DomainRuleException("quality_safety.intake.classification.invalid", "Intake classification is invalid.");
        if (observedAt > createdAt.AddMinutes(5))
            throw new DomainRuleException("quality_safety.intake.observed_at.future", "Observation time cannot be in the future.");
        if (kind == IntakeKind.IncidentIntake && classification == DataClassification.GeneralProject)
            throw new DomainRuleException("quality_safety.intake.incident.restricted", "Incident intake cannot use general classification.");
        return new QualitySafetyIntake
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("INT", createdAt, id),
            Kind = kind, ObservedAt = observedAt,
            Location = QualitySafetyRules.Required(location, 240, "quality_safety.intake.location.invalid"),
            Facts = QualitySafetyRules.Required(facts, 4_000, "quality_safety.intake.facts.invalid"),
            InitialSeverity = initialSeverity,
            ImmediateAction = QualitySafetyRules.Optional(immediateAction, 2_000, "quality_safety.intake.immediate_action.too_long"),
            EvidenceReferencesJson = QualitySafetyRules.JsonList(evidenceReferences, 700, "quality_safety.intake.evidence.invalid"),
            Classification = classification, ReportedBy = reportedBy, CreatedAt = createdAt,
            Status = IntakeStatus.Captured, ConversionType = IntakeConversionType.None
        };
    }

    public void BeginTriage(long baseRevision, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.intake.revision.conflict");
        QualitySafetyRules.Identity(actor);
        if (Status != IntakeStatus.Captured)
            throw new DomainRuleException("quality_safety.intake.triage.invalid_state", "Only a captured intake can enter triage.");
        Status = IntakeStatus.UnderTriage; TriagedBy = actor; TriagedAt = at; AdvanceRevision();
    }

    public void Convert(long baseRevision, IntakeConversionType type, Guid targetId, string note, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.intake.revision.conflict");
        QualitySafetyRules.Identity(targetId, actor);
        if (Status != IntakeStatus.UnderTriage || type is IntakeConversionType.None or IntakeConversionType.GeneralIssue)
            throw new DomainRuleException("quality_safety.intake.convert.invalid_state", "Only an intake under triage can be converted to a formal record.");
        if (IsQuality && type is IntakeConversionType.HseObservation or IntakeConversionType.Incident ||
            IsHse && type is IntakeConversionType.QualityObservation or IntakeConversionType.Defect or IntakeConversionType.NonConformance)
            throw new DomainRuleException("quality_safety.intake.convert.area_mismatch", "Conversion target does not match intake area.");
        Status = IntakeStatus.Converted; ConversionType = type; ConvertedRecordId = targetId;
        TriageNote = QualitySafetyRules.Required(note, 2_000, "quality_safety.intake.triage_note.invalid");
        TriagedBy = actor; TriagedAt = at; AdvanceRevision();
    }

    public void ResolveWithoutFormalRecord(long baseRevision, bool retainAsGeneralIssue, string note, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.intake.revision.conflict");
        QualitySafetyRules.Identity(actor);
        if (Status != IntakeStatus.UnderTriage)
            throw new DomainRuleException("quality_safety.intake.resolve.invalid_state", "Only an intake under triage can be resolved.");
        Status = retainAsGeneralIssue ? IntakeStatus.RetainedAsGeneralIssue : IntakeStatus.Dismissed;
        ConversionType = retainAsGeneralIssue ? IntakeConversionType.GeneralIssue : IntakeConversionType.None;
        TriageNote = QualitySafetyRules.Required(note, 2_000, "quality_safety.intake.triage_note.invalid");
        TriagedBy = actor; TriagedAt = at; AdvanceRevision();
    }
}
