using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class PurchaseOrder : AggregateRoot
{
    private PurchaseOrder()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid PurchaseRequestId { get; private set; }

    public Guid PartyId { get; private set; }

    public Guid? ContractId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public DateOnly? DeliveryDueDate { get; private set; }

    public Guid? SupplyItemId { get; private set; }

    public decimal? OrderedQuantity { get; private set; }

    public string? UnitCode { get; private set; }

    public string? DeliveryLocation { get; private set; }

    public PurchaseOrderStatus Status { get; private set; }

    public Guid IssuedBy { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public Guid? ClosedBy { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosureComment { get; private set; }

    public static PurchaseOrder Issue(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid purchaseRequestId,
        Guid partyId,
        Guid? contractId,
        string number,
        string title,
        decimal amount,
        string currencyCode,
        DateOnly? deliveryDueDate,
        Guid issuedBy,
        DateTimeOffset issuedAt,
        Guid? supplyItemId = null,
        decimal? orderedQuantity = null,
        string? unitCode = null,
        string? deliveryLocation = null)
    {
        CommercialRules.Identity(id, tenantId, projectId, purchaseRequestId, partyId, issuedBy);
        var validatedAmount = CommercialRules.OptionalPositiveAmount(amount, "commercial.order.amount.invalid");
        if (supplyItemId.HasValue != orderedQuantity.HasValue)
            throw new DomainRuleException("commercial.order.supply_basis.invalid", "Supply item and ordered quantity must be supplied together.");
        if (orderedQuantity.HasValue != !string.IsNullOrWhiteSpace(unitCode))
            throw new DomainRuleException("commercial.order.quantity_basis.invalid", "Order quantity and unit must be supplied together.");
        if (orderedQuantity.HasValue)
            SupplyRules.PositiveQuantity(orderedQuantity.Value, "commercial.order.quantity.invalid");
        if (orderedQuantity.HasValue && string.IsNullOrWhiteSpace(deliveryLocation))
            throw new DomainRuleException("commercial.order.delivery_location.required", "Quantified order requires a delivery location.");
        return new PurchaseOrder
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            PurchaseRequestId = purchaseRequestId,
            PartyId = partyId,
            ContractId = contractId,
            Number = CommercialRules.Required(number, 80, "commercial.order.number.invalid"),
            Title = CommercialRules.Required(title, 240, "commercial.order.title.invalid"),
            Amount = validatedAmount!.Value,
            CurrencyCode = CommercialRules.Currency(currencyCode),
            DeliveryDueDate = deliveryDueDate,
            SupplyItemId = supplyItemId,
            OrderedQuantity = orderedQuantity,
            UnitCode = string.IsNullOrWhiteSpace(unitCode) ? null : SupplyRules.Unit(unitCode),
            DeliveryLocation = CommercialRules.Optional(deliveryLocation, 240, "commercial.order.delivery_location.too_long"),
            Status = PurchaseOrderStatus.Issued,
            IssuedBy = issuedBy,
            IssuedAt = issuedAt,
            ChangedAt = issuedAt
        };
    }

    public void Close(long baseRevision, Guid closedBy, DateTimeOffset closedAt, string? comment)
    {
        CommercialRules.Revision(Revision, baseRevision, "commercial.order.revision.conflict");
        EnsureIssued();
        Status = PurchaseOrderStatus.Closed;
        ClosedBy = closedBy;
        ClosedAt = closedAt;
        ClosureComment = CommercialRules.Optional(comment, 1_000, "commercial.order.comment.too_long");
        ChangedAt = closedAt;
        AdvanceRevision();
    }

    public void Cancel(long baseRevision, Guid cancelledBy, DateTimeOffset cancelledAt, string? comment)
    {
        CommercialRules.Revision(Revision, baseRevision, "commercial.order.revision.conflict");
        EnsureIssued();
        Status = PurchaseOrderStatus.Cancelled;
        ClosedBy = cancelledBy;
        ClosedAt = cancelledAt;
        ClosureComment = CommercialRules.Required(comment, 1_000, "commercial.order.cancel_reason.required");
        ChangedAt = cancelledAt;
        AdvanceRevision();
    }

    private void EnsureIssued()
    {
        if (Status != PurchaseOrderStatus.Issued)
        {
            throw new DomainRuleException("commercial.order.close.invalid_state", "Only an issued order can be closed or cancelled.");
        }
    }
}

public enum PurchaseOrderStatus
{
    Issued = 1,
    Closed = 2,
    Cancelled = 3
}
