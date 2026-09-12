using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.QualitySafety.Domain;

public sealed class InspectionRecord : AggregateRoot
{
    private InspectionRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid? SourceIntakeId { get; private set; }
    public Guid? InspectionTestPlanVersionId { get; private set; }
    public string InspectionType { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string AcceptanceCriteria { get; private set; } = string.Empty;
    public string? ChecklistTemplateReference { get; private set; }
    public int? ChecklistTemplateVersion { get; private set; }
    public DateTimeOffset RequestedFor { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public InspectionReadiness Readiness { get; private set; }
    public string? ReadinessNote { get; private set; }
    public InspectionStatus Status { get; private set; }
    public InspectionResult? Result { get; private set; }
    public string? ResultNote { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public Guid? InspectedBy { get; private set; }
    public DateTimeOffset? InspectedAt { get; private set; }
    public IReadOnlyCollection<string> EvidenceReferences => QualitySafetyRules.ReadList(EvidenceReferencesJson);

    public static InspectionRecord Request(Guid id, Guid tenantId, Guid projectId, Guid? sourceIntakeId,
        Guid? inspectionTestPlanVersionId,
        string inspectionType, string location, string acceptanceCriteria, string? checklistTemplateReference,
        int? checklistTemplateVersion, DateTimeOffset requestedFor, Guid requestedBy, DateTimeOffset createdAt)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, requestedBy);
        if (checklistTemplateVersion.HasValue != !string.IsNullOrWhiteSpace(checklistTemplateReference) || checklistTemplateVersion is <= 0)
            throw new DomainRuleException("quality_safety.inspection.checklist.invalid", "Checklist reference and positive version must be supplied together.");
        return new InspectionRecord
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("IR", createdAt, id),
            SourceIntakeId = sourceIntakeId, InspectionTestPlanVersionId = inspectionTestPlanVersionId,
            InspectionType = QualitySafetyRules.Required(inspectionType, 160, "quality_safety.inspection.type.invalid"),
            Location = QualitySafetyRules.Required(location, 240, "quality_safety.inspection.location.invalid"),
            AcceptanceCriteria = QualitySafetyRules.Required(acceptanceCriteria, 4_000, "quality_safety.inspection.criteria.invalid"),
            ChecklistTemplateReference = QualitySafetyRules.Optional(checklistTemplateReference, 240, "quality_safety.inspection.checklist.too_long"),
            ChecklistTemplateVersion = checklistTemplateVersion, RequestedFor = requestedFor,
            RequestedBy = requestedBy, CreatedAt = createdAt, Readiness = InspectionReadiness.NotAssessed,
            Status = InspectionStatus.Requested
        };
    }

    public void RecordReadiness(long baseRevision, InspectionReadiness readiness, string? note)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.inspection.revision.conflict");
        if (Status is InspectionStatus.ResultRecorded or InspectionStatus.Cancelled || readiness == InspectionReadiness.NotAssessed)
            throw new DomainRuleException("quality_safety.inspection.readiness.invalid_state", "Readiness cannot be recorded in this state.");
        if (readiness == InspectionReadiness.NotReady && string.IsNullOrWhiteSpace(note))
            throw new DomainRuleException("quality_safety.inspection.readiness.reason.required", "Not-ready state requires a reason.");
        Readiness = readiness; ReadinessNote = QualitySafetyRules.Optional(note, 1_000, "quality_safety.inspection.readiness_note.too_long");
        Status = InspectionStatus.ReadinessRecorded; AdvanceRevision();
    }

    public void RecordResult(long baseRevision, InspectionResult result, string? note,
        IReadOnlyCollection<string>? evidence, Guid inspector, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.inspection.revision.conflict");
        QualitySafetyRules.Identity(inspector);
        if (Status != InspectionStatus.ReadinessRecorded || !Enum.IsDefined(result))
            throw new DomainRuleException("quality_safety.inspection.result.invalid_state", "Readiness must be recorded before the separate result.");
        if (result == InspectionResult.Pass && Readiness != InspectionReadiness.Ready)
            throw new DomainRuleException("quality_safety.inspection.pass.not_ready", "A not-ready request cannot receive a passing result.");
        if (result != InspectionResult.Pass && string.IsNullOrWhiteSpace(note))
            throw new DomainRuleException("quality_safety.inspection.result.note.required", "A non-pass result requires a note.");
        Result = result; ResultNote = QualitySafetyRules.Optional(note, 2_000, "quality_safety.inspection.result_note.too_long");
        EvidenceReferencesJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.inspection.evidence.invalid", result == InspectionResult.Pass);
        InspectedBy = inspector; InspectedAt = at; Status = InspectionStatus.ResultRecorded; AdvanceRevision();
    }
}

public sealed class NonConformanceRecord : AggregateRoot
{
    private NonConformanceRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid? SourceIntakeId { get; private set; }
    public Guid? InspectionId { get; private set; }
    public Guid? GoodsReceiptId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public Guid? VendorPartyId { get; private set; }
    public Guid? SupplyItemId { get; private set; }
    public string? LotReference { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Requirement { get; private set; } = string.Empty;
    public string NonConformity { get; private set; } = string.Empty;
    public NcrStatus Status { get; private set; }
    public NcrDisposition Disposition { get; private set; }
    public string? DispositionNote { get; private set; }
    public Guid? ConcessionApprovedBy { get; private set; }
    public RootCauseStatus RootCauseStatus { get; private set; }
    public string? RootCause { get; private set; }
    public string ClosureEvidenceJson { get; private set; } = "[]";
    public string? ClosureWaiverReason { get; private set; }
    public Guid? ClosureWaivedBy { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public IReadOnlyCollection<string> ClosureEvidence => QualitySafetyRules.ReadList(ClosureEvidenceJson);

    public static NonConformanceRecord Create(Guid id, Guid tenantId, Guid projectId, Guid? sourceIntakeId,
        Guid? inspectionId, Guid? goodsReceiptId, Guid? purchaseOrderId, Guid? vendorPartyId, Guid? supplyItemId,
        string? lotReference, string title, string requirement, string nonConformity, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        return new NonConformanceRecord
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("NCR", at, id),
            SourceIntakeId = sourceIntakeId, InspectionId = inspectionId, GoodsReceiptId = goodsReceiptId,
            PurchaseOrderId = purchaseOrderId, VendorPartyId = vendorPartyId, SupplyItemId = supplyItemId,
            LotReference = QualitySafetyRules.Optional(lotReference, 160, "quality_safety.ncr.lot.too_long"),
            Title = QualitySafetyRules.Required(title, 240, "quality_safety.ncr.title.invalid"),
            Requirement = QualitySafetyRules.Required(requirement, 4_000, "quality_safety.ncr.requirement.invalid"),
            NonConformity = QualitySafetyRules.Required(nonConformity, 4_000, "quality_safety.ncr.nonconformity.invalid"),
            Status = NcrStatus.Draft, Disposition = NcrDisposition.NotDecided, RootCauseStatus = RootCauseStatus.NotStarted,
            CreatedBy = actor, CreatedAt = at
        };
    }

    public void Transition(long baseRevision, NcrStatus target, NcrDisposition disposition, string? dispositionNote,
        Guid? concessionApprovedBy, RootCauseStatus rootCauseStatus, string? rootCause,
        IReadOnlyCollection<string>? closureEvidence, string? waiverReason, Guid? waivedBy, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.ncr.revision.conflict");
        if (!Allowed(Status).Contains(target))
            throw new DomainRuleException("quality_safety.ncr.transition.invalid", "NCR transition is not allowed.");
        if (target >= NcrStatus.ActionImplementation && disposition == NcrDisposition.NotDecided)
            throw new DomainRuleException("quality_safety.ncr.disposition.required", "Disposition is required before action implementation.");
        if (disposition == NcrDisposition.UseAsIsWithConcession && !concessionApprovedBy.HasValue)
            throw new DomainRuleException("quality_safety.ncr.concession.approver.required", "Use-as-is disposition requires concession approval.");
        if (rootCauseStatus == RootCauseStatus.Confirmed && string.IsNullOrWhiteSpace(rootCause))
            throw new DomainRuleException("quality_safety.ncr.root_cause.required", "Confirmed root cause requires a documented cause.");
        var evidenceJson = QualitySafetyRules.JsonList(closureEvidence, 700, "quality_safety.ncr.closure_evidence.invalid");
        if (target == NcrStatus.Closed && QualitySafetyRules.ReadList(evidenceJson).Count == 0 &&
            (string.IsNullOrWhiteSpace(waiverReason) || !waivedBy.HasValue))
            throw new DomainRuleException("quality_safety.ncr.closure_evidence.required", "Closure requires verification evidence or an audited waiver.");
        Disposition = disposition; DispositionNote = QualitySafetyRules.Optional(dispositionNote, 2_000, "quality_safety.ncr.disposition_note.too_long");
        ConcessionApprovedBy = concessionApprovedBy; RootCauseStatus = rootCauseStatus;
        RootCause = QualitySafetyRules.Optional(rootCause, 4_000, "quality_safety.ncr.root_cause.too_long");
        Status = target; ClosureEvidenceJson = evidenceJson;
        ClosureWaiverReason = QualitySafetyRules.Optional(waiverReason, 1_000, "quality_safety.ncr.waiver.too_long");
        ClosureWaivedBy = waivedBy; ClosedAt = target == NcrStatus.Closed ? at : null; AdvanceRevision();
    }

    private static HashSet<NcrStatus> Allowed(NcrStatus current) => current switch
    {
        NcrStatus.Draft => new HashSet<NcrStatus> { NcrStatus.Issued },
        NcrStatus.Issued => new HashSet<NcrStatus> { NcrStatus.Containment },
        NcrStatus.Containment => new HashSet<NcrStatus> { NcrStatus.InvestigationDisposition },
        NcrStatus.InvestigationDisposition => new HashSet<NcrStatus> { NcrStatus.ActionImplementation },
        NcrStatus.ActionImplementation => new HashSet<NcrStatus> { NcrStatus.ReinspectionVerification },
        NcrStatus.ReinspectionVerification => new HashSet<NcrStatus> { NcrStatus.Closed },
        NcrStatus.Closed => new HashSet<NcrStatus> { NcrStatus.Reopened },
        NcrStatus.Reopened => new HashSet<NcrStatus> { NcrStatus.Containment },
        _ => new HashSet<NcrStatus>()
    };
}

public sealed class DefectRecord : AggregateRoot
{
    private DefectRecord() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid? SourceIntakeId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public Guid? AssigneeUserId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DefectStatus Status { get; private set; }
    public string RectificationEvidenceJson { get; private set; } = "[]";
    public string VerificationEvidenceJson { get; private set; } = "[]";
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public static DefectRecord Create(Guid id, Guid tenantId, Guid projectId, Guid? sourceIntakeId,
        string title, string location, Guid actor, DateTimeOffset at)
    {
        QualitySafetyRules.Identity(id, tenantId, projectId, actor);
        return new DefectRecord
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, Number = QualitySafetyRules.Number("DEF", at, id),
            SourceIntakeId = sourceIntakeId,
            Title = QualitySafetyRules.Required(title, 240, "quality_safety.defect.title.invalid"),
            Location = QualitySafetyRules.Required(location, 240, "quality_safety.defect.location.invalid"),
            Status = DefectStatus.Open, CreatedBy = actor, CreatedAt = at
        };
    }

    public void Assign(long baseRevision, Guid assignee, DateOnly dueDate)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.defect.revision.conflict"); QualitySafetyRules.Identity(assignee);
        if (Status is not DefectStatus.Open and not DefectStatus.Reopened)
            throw new DomainRuleException("quality_safety.defect.assign.invalid_state", "Only an open defect can be assigned.");
        AssigneeUserId = assignee; DueDate = dueDate; Status = DefectStatus.Assigned; AdvanceRevision();
    }

    public void Transition(long baseRevision, DefectStatus target, IReadOnlyCollection<string>? evidence, DateTimeOffset at)
    {
        QualitySafetyRules.Revision(Revision, baseRevision, "quality_safety.defect.revision.conflict");
        if (!Allowed(Status).Contains(target))
            throw new DomainRuleException("quality_safety.defect.transition.invalid", "Defect transition is not allowed.");
        var evidenceJson = QualitySafetyRules.JsonList(evidence, 700, "quality_safety.defect.evidence.invalid",
            target is DefectStatus.Rectified or DefectStatus.Accepted);
        if (target == DefectStatus.Rectified) RectificationEvidenceJson = evidenceJson;
        if (target == DefectStatus.Accepted) VerificationEvidenceJson = evidenceJson;
        Status = target; ClosedAt = target == DefectStatus.Closed ? at : null; AdvanceRevision();
    }

    private static HashSet<DefectStatus> Allowed(DefectStatus current) => current switch
    {
        DefectStatus.Assigned => new HashSet<DefectStatus> { DefectStatus.Rectified },
        DefectStatus.Rectified => new HashSet<DefectStatus> { DefectStatus.ReadyForVerification },
        DefectStatus.ReadyForVerification => new HashSet<DefectStatus> { DefectStatus.Accepted, DefectStatus.Rejected },
        DefectStatus.Accepted => new HashSet<DefectStatus> { DefectStatus.Closed },
        DefectStatus.Rejected => new HashSet<DefectStatus> { DefectStatus.Assigned },
        DefectStatus.Closed => new HashSet<DefectStatus> { DefectStatus.Reopened },
        DefectStatus.Reopened => new HashSet<DefectStatus> { DefectStatus.Assigned },
        _ => new HashSet<DefectStatus>()
    };
}
