using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class InventoryLedgerEntry : Entity
{
    private InventoryLedgerEntry() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid LocationId { get; private set; }
    public InventoryEventType EventType { get; private set; }
    public decimal BaseQuantityDelta { get; private set; }
    public string BaseUnit { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public static InventoryLedgerEntry Create(
        Guid id, Guid tenantId, Guid projectId, Guid transactionId, Guid itemId, Guid locationId,
        InventoryEventType eventType, decimal baseQuantityDelta, string baseUnit,
        string sourceType, Guid sourceId, Guid? reversesEntryId, string? reason,
        DateTimeOffset occurredAt, Guid recordedBy, DateTimeOffset recordedAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, transactionId, itemId, locationId, sourceId, recordedBy);
        if (!Enum.IsDefined(eventType) || baseQuantityDelta == 0 ||
            decimal.Round(baseQuantityDelta, 6, MidpointRounding.AwayFromZero) != baseQuantityDelta)
            throw new DomainRuleException("supply.ledger.quantity.invalid", "Ledger event quantity is invalid.");
        if (((eventType is InventoryEventType.AcceptedReceipt or InventoryEventType.ReturnFromSite or InventoryEventType.TransferIn) && baseQuantityDelta < 0) ||
            ((eventType is InventoryEventType.MaterialIssue or InventoryEventType.TransferOut) && baseQuantityDelta > 0))
            throw new DomainRuleException("supply.ledger.direction.invalid", "Ledger event direction is invalid.");
        if (eventType == InventoryEventType.Adjustment && string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("supply.ledger.adjustment.reason.required", "Adjustment reason is required.");
        if (eventType == InventoryEventType.Reversal && (!reversesEntryId.HasValue || string.IsNullOrWhiteSpace(reason)))
            throw new DomainRuleException("supply.ledger.reversal.basis.required", "A reversal requires the original entry and a reason.");
        if (eventType != InventoryEventType.Reversal && reversesEntryId.HasValue)
            throw new DomainRuleException("supply.ledger.reversal.basis.invalid", "Only a reversal can reference another ledger entry.");
        return new InventoryLedgerEntry
        {
            Id = id, TenantId = tenantId, ProjectId = projectId, TransactionId = transactionId,
            ItemId = itemId, LocationId = locationId, EventType = eventType,
            BaseQuantityDelta = baseQuantityDelta, BaseUnit = SupplyRules.Unit(baseUnit),
            SourceType = CommercialRules.Required(sourceType, 80, "supply.ledger.source_type.invalid"),
            SourceId = sourceId, ReversesEntryId = reversesEntryId,
            Reason = CommercialRules.Optional(reason, 1_000, "supply.ledger.reason.too_long"),
            OccurredAt = occurredAt, RecordedBy = recordedBy, RecordedAt = recordedAt
        };
    }
}

public enum InventoryEventType
{
    AcceptedReceipt = 1, MaterialIssue = 2, TransferOut = 3, TransferIn = 4,
    ReturnFromSite = 5, Adjustment = 6, Reversal = 7
}
