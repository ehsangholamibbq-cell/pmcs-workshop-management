using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class InventoryLocation : AggregateRoot
{
    private InventoryLocation() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public InventoryLocationType Type { get; private set; }
    public bool AllowsAvailableStock { get; private set; }
    public InventoryLocationStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static InventoryLocation Create(
        Guid id, Guid tenantId, Guid projectId, string code, string name, InventoryLocationType type,
        bool allowsAvailableStock, Guid createdBy, DateTimeOffset createdAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(type))
            throw new DomainRuleException("supply.location.type.invalid", "Inventory location type is invalid.");
        if ((type is InventoryLocationType.QuarantineArea or InventoryLocationType.RejectedArea) && allowsAvailableStock)
            throw new DomainRuleException("supply.location.availability.invalid", "Quarantine and rejected locations cannot hold available stock.");
        return new InventoryLocation
        {
            Id = id, TenantId = tenantId, ProjectId = projectId,
            Code = SupplyRules.Code(code, 40, "supply.location.code.invalid"),
            Name = CommercialRules.Required(name, 200, "supply.location.name.invalid"),
            Type = type, AllowsAvailableStock = allowsAvailableStock,
            Status = InventoryLocationStatus.Active,
            CreatedBy = createdBy, CreatedAt = createdAt
        };
    }

    public void Deactivate(long baseRevision)
    {
        CommercialRules.Revision(Revision, baseRevision, "supply.location.revision.conflict");
        Status = InventoryLocationStatus.Inactive;
        AdvanceRevision();
    }
}

public enum InventoryLocationType
{
    CentralStore = 1, SiteStore = 2, FloorOrZoneStore = 3, LaydownArea = 4,
    ContractorCustody = 5, QuarantineArea = 6, RejectedArea = 7
}
public enum InventoryLocationStatus { Active = 1, Inactive = 2 }
