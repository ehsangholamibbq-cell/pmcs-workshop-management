using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class MaterialIssue : AggregateRoot
{
    private MaterialIssue() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid ItemId { get; private set; }
    public Guid SourceLocationId { get; private set; }
    public decimal IssuedBaseQuantity { get; private set; }
    public string BaseUnit { get; private set; } = string.Empty;
    public string IssuedTo { get; private set; } = string.Empty;
    public string DestinationLocation { get; private set; } = string.Empty;
    public string? WorkItemReference { get; private set; }
    public string? WbsReference { get; private set; }
    public Guid? PurchaseRequestId { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public decimal ConsumedBaseQuantity { get; private set; }
    public decimal ReturnedBaseQuantity { get; private set; }
    public decimal WasteBaseQuantity { get; private set; }
    public MaterialIssueStatus Status { get; private set; }
    public Guid IssuedBy { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public Guid? AcknowledgedBy { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? AcknowledgmentReference { get; private set; }
    public DateTimeOffset? ReconciledAt { get; private set; }

    public IReadOnlyCollection<string> EvidenceReferences => SupplyRules.ReadEvidence(EvidenceReferencesJson);
    public decimal UnreconciledBaseQuantity =>
        IssuedBaseQuantity - ConsumedBaseQuantity - ReturnedBaseQuantity - WasteBaseQuantity;

    public static MaterialIssue Create(
        Guid id, Guid tenantId, Guid projectId, Guid itemId, Guid sourceLocationId,
        decimal issuedBaseQuantity, string baseUnit, string issuedTo, string destinationLocation,
        string? workItemReference, string? wbsReference, Guid? purchaseRequestId,
        IReadOnlyCollection<string>? evidenceReferences, Guid issuedBy, DateTimeOffset issuedAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, itemId, sourceLocationId, issuedBy);
        var evidence = SupplyRules.Evidence(evidenceReferences, "supply.issue.evidence.invalid");
        if (SupplyRules.ReadEvidence(evidence).Count == 0)
            throw new DomainRuleException("supply.issue.evidence.required", "Material issue evidence is required.");
        return new MaterialIssue
        {
            Id = id, TenantId = tenantId, ProjectId = projectId,
            Number = SupplyRules.OfficialNumber("ISS", issuedAt, id),
            ItemId = itemId, SourceLocationId = sourceLocationId,
            IssuedBaseQuantity = SupplyRules.PositiveQuantity(issuedBaseQuantity, "supply.issue.quantity.invalid"),
            BaseUnit = SupplyRules.Unit(baseUnit),
            IssuedTo = CommercialRules.Required(issuedTo, 240, "supply.issue.recipient.invalid"),
            DestinationLocation = CommercialRules.Required(destinationLocation, 240, "supply.issue.destination.invalid"),
            WorkItemReference = CommercialRules.Optional(workItemReference, 240, "supply.issue.work_item.too_long"),
            WbsReference = CommercialRules.Optional(wbsReference, 240, "supply.issue.wbs.too_long"),
            PurchaseRequestId = purchaseRequestId,
            EvidenceReferencesJson = evidence,
            Status = MaterialIssueStatus.Issued,
            IssuedBy = issuedBy, IssuedAt = issuedAt
        };
    }

    public void Acknowledge(long baseRevision, Guid acknowledgedBy, DateTimeOffset at, string reference)
    {
        EnsureRevision(baseRevision);
        SupplyRules.Identity(acknowledgedBy);
        if (Status != MaterialIssueStatus.Issued)
            throw new DomainRuleException("supply.issue.acknowledge.invalid_state", "Only an issued material handover can be acknowledged.");
        AcknowledgedBy = acknowledgedBy;
        AcknowledgedAt = at;
        AcknowledgmentReference = CommercialRules.Required(reference, 500, "supply.issue.acknowledgment.invalid");
        Status = MaterialIssueStatus.PendingReconciliation;
        AdvanceRevision();
    }

    public void Reconcile(
        long baseRevision, decimal consumedBaseQuantity, decimal returnedBaseQuantity,
        decimal wasteBaseQuantity, string? wasteReason, IReadOnlyCollection<string>? evidenceReferences,
        DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        if (Status is not MaterialIssueStatus.Issued and not MaterialIssueStatus.PendingReconciliation and not MaterialIssueStatus.PartiallyReconciled)
            throw new DomainRuleException("supply.issue.reconcile.invalid_state", "This material issue cannot be reconciled.");
        var consumed = SupplyRules.NonNegativeQuantity(consumedBaseQuantity, "supply.issue.reconcile.quantity.invalid");
        var returned = SupplyRules.NonNegativeQuantity(returnedBaseQuantity, "supply.issue.reconcile.quantity.invalid");
        var waste = SupplyRules.NonNegativeQuantity(wasteBaseQuantity, "supply.issue.reconcile.quantity.invalid");
        if (consumed + returned + waste <= 0 || consumed + returned + waste > UnreconciledBaseQuantity)
            throw new DomainRuleException("supply.issue.reconcile.balance.invalid", "Reconciled quantity exceeds the outstanding custody balance.");
        if (waste > 0 && (string.IsNullOrWhiteSpace(wasteReason) || evidenceReferences is null || evidenceReferences.Count == 0))
            throw new DomainRuleException("supply.issue.waste.evidence.required", "Waste requires a reason and evidence.");

        ConsumedBaseQuantity += consumed;
        ReturnedBaseQuantity += returned;
        WasteBaseQuantity += waste;
        Status = UnreconciledBaseQuantity == 0
            ? MaterialIssueStatus.Reconciled
            : MaterialIssueStatus.PartiallyReconciled;
        if (Status == MaterialIssueStatus.Reconciled) ReconciledAt = at;
        AdvanceRevision();
    }

    private void EnsureRevision(long supplied) =>
        CommercialRules.Revision(Revision, supplied, "supply.issue.revision.conflict");
}

public sealed class MaterialReconciliationRecord : Entity
{
    private MaterialReconciliationRecord() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid MaterialIssueId { get; private set; }
    public decimal ConsumedBaseQuantity { get; private set; }
    public decimal ReturnedBaseQuantity { get; private set; }
    public decimal WasteBaseQuantity { get; private set; }
    public string BaseUnit { get; private set; } = string.Empty;
    public string? WasteReason { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public IReadOnlyCollection<string> EvidenceReferences => SupplyRules.ReadEvidence(EvidenceReferencesJson);

    public static MaterialReconciliationRecord Create(
        Guid id, Guid tenantId, Guid projectId, Guid materialIssueId,
        decimal consumedBaseQuantity, decimal returnedBaseQuantity, decimal wasteBaseQuantity,
        string baseUnit, string? wasteReason, IReadOnlyCollection<string>? evidenceReferences,
        Guid recordedBy, DateTimeOffset recordedAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, materialIssueId, recordedBy);
        var consumed = SupplyRules.NonNegativeQuantity(consumedBaseQuantity, "supply.issue.reconcile.quantity.invalid");
        var returned = SupplyRules.NonNegativeQuantity(returnedBaseQuantity, "supply.issue.reconcile.quantity.invalid");
        var waste = SupplyRules.NonNegativeQuantity(wasteBaseQuantity, "supply.issue.reconcile.quantity.invalid");
        if (consumed + returned + waste <= 0)
            throw new DomainRuleException("supply.issue.reconcile.quantity.required", "A reconciliation quantity is required.");
        if (waste > 0 && (string.IsNullOrWhiteSpace(wasteReason) || evidenceReferences is null || evidenceReferences.Count == 0))
            throw new DomainRuleException("supply.issue.waste.evidence.required", "Waste requires a reason and evidence.");
        return new MaterialReconciliationRecord
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, MaterialIssueId = materialIssueId,
            ConsumedBaseQuantity = consumed, ReturnedBaseQuantity = returned, WasteBaseQuantity = waste,
            BaseUnit = SupplyRules.Unit(baseUnit),
            WasteReason = CommercialRules.Optional(wasteReason, 1_000, "supply.issue.waste_reason.too_long"),
            EvidenceReferencesJson = SupplyRules.Evidence(evidenceReferences, "supply.issue.evidence.invalid"),
            RecordedBy = recordedBy, RecordedAt = recordedAt
        };
    }
}

public enum MaterialIssueStatus { Issued = 1, PendingReconciliation = 2, PartiallyReconciled = 3, Reconciled = 4 }
