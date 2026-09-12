using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class ServiceAcceptance : AggregateRoot
{
    private ServiceAcceptance() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid PurchaseOrderId { get; private set; }
    public Guid PartyId { get; private set; }
    public Guid ItemId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal DeliveredBaseQuantity { get; private set; }
    public decimal AcceptedBaseQuantity { get; private set; }
    public decimal RejectedBaseQuantity { get; private set; }
    public string BaseUnit { get; private set; } = string.Empty;
    public string AcceptanceCriteria { get; private set; } = string.Empty;
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public string? Comment { get; private set; }
    public Guid VerifiedBy { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }

    public IReadOnlyCollection<string> EvidenceReferences => SupplyRules.ReadEvidence(EvidenceReferencesJson);

    public static ServiceAcceptance Create(
        Guid id, Guid tenantId, Guid projectId, Guid purchaseOrderId, Guid partyId, Guid itemId,
        DateOnly periodStart, DateOnly periodEnd, decimal deliveredBaseQuantity,
        decimal acceptedBaseQuantity, decimal rejectedBaseQuantity, string baseUnit,
        string acceptanceCriteria, IReadOnlyCollection<string>? evidenceReferences,
        string? comment, Guid verifiedBy, DateTimeOffset verifiedAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, purchaseOrderId, partyId, itemId, verifiedBy);
        if (periodEnd < periodStart)
            throw new DomainRuleException("supply.service.period.invalid", "Service period is invalid.");
        var delivered = SupplyRules.PositiveQuantity(deliveredBaseQuantity, "supply.service.quantity.invalid");
        var accepted = SupplyRules.NonNegativeQuantity(acceptedBaseQuantity, "supply.service.quantity.invalid");
        var rejected = SupplyRules.NonNegativeQuantity(rejectedBaseQuantity, "supply.service.quantity.invalid");
        if (accepted + rejected != delivered)
            throw new DomainRuleException("supply.service.balance.invalid", "Accepted and rejected service must reconcile to delivered service.");
        if (rejected > 0 && string.IsNullOrWhiteSpace(comment))
            throw new DomainRuleException("supply.service.reject.reason.required", "Rejected service requires a reason.");
        var evidence = SupplyRules.Evidence(evidenceReferences, "supply.service.evidence.invalid");
        if (SupplyRules.ReadEvidence(evidence).Count == 0)
            throw new DomainRuleException("supply.service.evidence.required", "Service acceptance evidence is required.");
        return new ServiceAcceptance
        {
            Id = id, TenantId = tenantId, ProjectId = projectId,
            Number = SupplyRules.OfficialNumber("SAC", verifiedAt, id),
            PurchaseOrderId = purchaseOrderId, PartyId = partyId, ItemId = itemId,
            PeriodStart = periodStart, PeriodEnd = periodEnd,
            DeliveredBaseQuantity = delivered, AcceptedBaseQuantity = accepted,
            RejectedBaseQuantity = rejected, BaseUnit = SupplyRules.Unit(baseUnit),
            AcceptanceCriteria = CommercialRules.Required(acceptanceCriteria, 1_000, "supply.service.criteria.invalid"),
            EvidenceReferencesJson = evidence,
            Comment = CommercialRules.Optional(comment, 2_000, "supply.service.comment.too_long"),
            VerifiedBy = verifiedBy, VerifiedAt = verifiedAt
        };
    }
}
