using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class GoodsReceipt : AggregateRoot
{
    private GoodsReceipt() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid PurchaseOrderId { get; private set; }
    public Guid PartyId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? StockLocationId { get; private set; }
    public string DispatchNote { get; private set; } = string.Empty;
    public DateTimeOffset ArrivedAt { get; private set; }
    public string DeliveryLocation { get; private set; } = string.Empty;
    public decimal ShippedQuantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal BaseReceivedQuantity { get; private set; }
    public decimal DamagedQuantity { get; private set; }
    public string UnitCode { get; private set; } = string.Empty;
    public string BaseUnit { get; private set; } = string.Empty;
    public int ConversionVersion { get; private set; }
    public string? BatchOrLotReference { get; private set; }
    public string? PackageCondition { get; private set; }
    public string? ExcessApprovalReason { get; private set; }
    public Guid? ExcessApprovedBy { get; private set; }
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public GoodsReceiptStatus Status { get; private set; }
    public decimal AcceptedBaseQuantity { get; private set; }
    public decimal RejectedBaseQuantity { get; private set; }
    public decimal QuarantinedBaseQuantity { get; private set; }
    public Guid ReceivedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? InspectedBy { get; private set; }
    public DateTimeOffset? InspectedAt { get; private set; }
    public string? InspectionType { get; private set; }
    public string? InspectionReference { get; private set; }
    public string? InspectionComment { get; private set; }
    public DateTimeOffset? StockPostedAt { get; private set; }

    public IReadOnlyCollection<string> EvidenceReferences => SupplyRules.ReadEvidence(EvidenceReferencesJson);

    public static GoodsReceipt Create(
        Guid id, Guid tenantId, Guid projectId, Guid purchaseOrderId, Guid partyId, Guid itemId,
        Guid? stockLocationId, string dispatchNote, DateTimeOffset arrivedAt, string deliveryLocation,
        ConvertedQuantity shipped, ConvertedQuantity received, decimal damagedQuantity,
        string? batchOrLotReference, string? packageCondition, string? excessApprovalReason,
        Guid? excessApprovedBy, IReadOnlyCollection<string>? evidenceReferences,
        Guid receivedBy, DateTimeOffset createdAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, purchaseOrderId, partyId, itemId, receivedBy);
        if (shipped.UnitCode != received.UnitCode || shipped.BaseUnit != received.BaseUnit ||
            shipped.ConversionVersion != received.ConversionVersion || received.Quantity > shipped.Quantity)
            throw new DomainRuleException("supply.receipt.quantity.invalid", "Received quantity must use the shipment basis and cannot exceed shipped quantity.");
        SupplyRules.NonNegativeQuantity(damagedQuantity, "supply.receipt.damaged.invalid");
        if (damagedQuantity > received.Quantity)
            throw new DomainRuleException("supply.receipt.damaged.invalid", "Damaged quantity cannot exceed physically received quantity.");
        if (excessApprovedBy.HasValue != !string.IsNullOrWhiteSpace(excessApprovalReason))
            throw new DomainRuleException("supply.receipt.excess_approval.invalid", "Excess approval requires both approver and reason.");
        var evidence = SupplyRules.Evidence(evidenceReferences, "supply.receipt.evidence.invalid");
        if (SupplyRules.ReadEvidence(evidence).Count == 0)
            throw new DomainRuleException("supply.receipt.evidence.required", "Delivery evidence is required.");

        return new GoodsReceipt
        {
            Id = id, TenantId = tenantId, ProjectId = projectId,
            Number = SupplyRules.OfficialNumber("REC", createdAt, id),
            PurchaseOrderId = purchaseOrderId, PartyId = partyId, ItemId = itemId,
            StockLocationId = stockLocationId,
            DispatchNote = CommercialRules.Required(dispatchNote, 120, "supply.receipt.dispatch_note.invalid"),
            ArrivedAt = arrivedAt,
            DeliveryLocation = CommercialRules.Required(deliveryLocation, 240, "supply.receipt.location.invalid"),
            ShippedQuantity = shipped.Quantity, ReceivedQuantity = received.Quantity,
            BaseReceivedQuantity = received.BaseQuantity, DamagedQuantity = damagedQuantity,
            UnitCode = received.UnitCode, BaseUnit = received.BaseUnit,
            ConversionVersion = received.ConversionVersion,
            BatchOrLotReference = CommercialRules.Optional(batchOrLotReference, 160, "supply.receipt.lot.too_long"),
            PackageCondition = CommercialRules.Optional(packageCondition, 500, "supply.receipt.condition.too_long"),
            ExcessApprovalReason = CommercialRules.Optional(excessApprovalReason, 1_000, "supply.receipt.excess_reason.too_long"),
            ExcessApprovedBy = excessApprovedBy,
            EvidenceReferencesJson = evidence, Status = GoodsReceiptStatus.Received,
            ReceivedBy = receivedBy, CreatedAt = createdAt
        };
    }

    public void SubmitForInspection(long baseRevision)
    {
        EnsureRevision(baseRevision);
        if (Status != GoodsReceiptStatus.Received)
            throw new DomainRuleException("supply.receipt.inspection.invalid_state", "Only a new receipt can enter inspection.");
        Status = GoodsReceiptStatus.PendingInspection;
        AdvanceRevision();
    }

    public void RecordInspection(
        long baseRevision, decimal acceptedBaseQuantity, decimal rejectedBaseQuantity,
        decimal quarantinedBaseQuantity, string inspectionType, string? inspectionReference,
        string? comment, Guid inspectedBy, DateTimeOffset inspectedAt)
    {
        EnsureRevision(baseRevision);
        SupplyRules.Identity(inspectedBy);
        if (Status != GoodsReceiptStatus.PendingInspection)
            throw new DomainRuleException("supply.receipt.inspection.invalid_state", "Only a pending receipt can be inspected.");
        var accepted = SupplyRules.NonNegativeQuantity(acceptedBaseQuantity, "supply.inspection.quantity.invalid");
        var rejected = SupplyRules.NonNegativeQuantity(rejectedBaseQuantity, "supply.inspection.quantity.invalid");
        var quarantined = SupplyRules.NonNegativeQuantity(quarantinedBaseQuantity, "supply.inspection.quantity.invalid");
        if (accepted + rejected + quarantined != BaseReceivedQuantity)
            throw new DomainRuleException("supply.inspection.balance.invalid", "Inspection quantities must reconcile to physically received quantity.");
        if ((rejected > 0 || quarantined > 0) && string.IsNullOrWhiteSpace(comment))
            throw new DomainRuleException("supply.inspection.reason.required", "Rejected or quarantined quantity requires a reason.");

        AcceptedBaseQuantity = accepted;
        RejectedBaseQuantity = rejected;
        QuarantinedBaseQuantity = quarantined;
        InspectionType = CommercialRules.Required(inspectionType, 120, "supply.inspection.type.invalid");
        InspectionReference = CommercialRules.Optional(inspectionReference, 500, "supply.inspection.reference.too_long");
        InspectionComment = CommercialRules.Optional(comment, 2_000, "supply.inspection.comment.too_long");
        InspectedBy = inspectedBy;
        InspectedAt = inspectedAt;
        Status = accepted == BaseReceivedQuantity
            ? GoodsReceiptStatus.Accepted
            : accepted > 0 ? GoodsReceiptStatus.PartiallyAccepted
            : quarantined > 0 ? GoodsReceiptStatus.Quarantined : GoodsReceiptStatus.Rejected;
        AdvanceRevision();
    }

    public void MarkStockPosted(long baseRevision, DateTimeOffset postedAt)
    {
        EnsureRevision(baseRevision);
        if (Status is not GoodsReceiptStatus.Accepted and not GoodsReceiptStatus.PartiallyAccepted ||
            AcceptedBaseQuantity <= 0 || !StockLocationId.HasValue || StockPostedAt.HasValue)
            throw new DomainRuleException("supply.receipt.stock.invalid_state", "Only accepted and unposted material can enter stock.");
        StockPostedAt = postedAt;
        AdvanceRevision();
    }

    private void EnsureRevision(long supplied) =>
        CommercialRules.Revision(Revision, supplied, "supply.receipt.revision.conflict");
}

public enum GoodsReceiptStatus
{
    Received = 1, PendingInspection = 2, Accepted = 3, PartiallyAccepted = 4,
    Rejected = 5, Quarantined = 6
}
