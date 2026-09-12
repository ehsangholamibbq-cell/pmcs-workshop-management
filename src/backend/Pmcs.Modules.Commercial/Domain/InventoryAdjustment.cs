using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class InventoryAdjustment : AggregateRoot
{
    private InventoryAdjustment() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid ItemId { get; private set; }
    public Guid LocationId { get; private set; }
    public DateTimeOffset CutoffAt { get; private set; }
    public decimal SystemBaseQuantity { get; private set; }
    public decimal CountedBaseQuantity { get; private set; }
    public decimal DeltaBaseQuantity => CountedBaseQuantity - SystemBaseQuantity;
    public string BaseUnit { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string EvidenceReferencesJson { get; private set; } = "[]";
    public InventoryAdjustmentStatus Status { get; private set; }
    public Guid ProposedBy { get; private set; }
    public DateTimeOffset ProposedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyCollection<string> EvidenceReferences => SupplyRules.ReadEvidence(EvidenceReferencesJson);

    public static InventoryAdjustment Propose(
        Guid id, Guid tenantId, Guid projectId, Guid itemId, Guid locationId, DateTimeOffset cutoffAt,
        decimal systemBaseQuantity, decimal countedBaseQuantity, string baseUnit, string reason,
        IReadOnlyCollection<string>? evidenceReferences, Guid proposedBy, DateTimeOffset proposedAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, itemId, locationId, proposedBy);
        SupplyRules.NonNegativeQuantity(systemBaseQuantity, "supply.count.quantity.invalid");
        SupplyRules.NonNegativeQuantity(countedBaseQuantity, "supply.count.quantity.invalid");
        if (systemBaseQuantity == countedBaseQuantity)
            throw new DomainRuleException("supply.count.variance.required", "An adjustment requires a physical count variance.");
        var evidence = SupplyRules.Evidence(evidenceReferences, "supply.count.evidence.invalid");
        if (SupplyRules.ReadEvidence(evidence).Count == 0)
            throw new DomainRuleException("supply.count.evidence.required", "Count evidence is required.");
        return new InventoryAdjustment
        {
            Id = id, TenantId = tenantId, ProjectId = projectId,
            Number = SupplyRules.OfficialNumber("ADJ", proposedAt, id),
            ItemId = itemId, LocationId = locationId, CutoffAt = cutoffAt,
            SystemBaseQuantity = systemBaseQuantity, CountedBaseQuantity = countedBaseQuantity,
            BaseUnit = SupplyRules.Unit(baseUnit),
            Reason = CommercialRules.Required(reason, 1_000, "supply.count.reason.invalid"),
            EvidenceReferencesJson = evidence, Status = InventoryAdjustmentStatus.Proposed,
            ProposedBy = proposedBy, ProposedAt = proposedAt
        };
    }

    public void Review(long baseRevision, bool approved, Guid reviewedBy, DateTimeOffset at, string? comment)
    {
        EnsureRevision(baseRevision);
        SupplyRules.Identity(reviewedBy);
        if (Status != InventoryAdjustmentStatus.Proposed)
            throw new DomainRuleException("supply.adjustment.review.invalid_state", "Only a proposed adjustment can be reviewed.");
        if (!approved && string.IsNullOrWhiteSpace(comment))
            throw new DomainRuleException("supply.adjustment.reject.reason.required", "Rejected adjustment requires a reason.");
        Status = approved ? InventoryAdjustmentStatus.Approved : InventoryAdjustmentStatus.Rejected;
        ReviewedBy = reviewedBy; ReviewedAt = at;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "supply.adjustment.comment.too_long");
        AdvanceRevision();
    }

    public void MarkPosted(long baseRevision, DateTimeOffset at)
    {
        EnsureRevision(baseRevision);
        if (Status != InventoryAdjustmentStatus.Approved)
            throw new DomainRuleException("supply.adjustment.post.invalid_state", "Only an approved adjustment can be posted.");
        Status = InventoryAdjustmentStatus.Posted;
        PostedAt = at;
        AdvanceRevision();
    }

    private void EnsureRevision(long supplied) =>
        CommercialRules.Revision(Revision, supplied, "supply.adjustment.revision.conflict");
}

public enum InventoryAdjustmentStatus { Proposed = 1, Approved = 2, Rejected = 3, Posted = 4 }
