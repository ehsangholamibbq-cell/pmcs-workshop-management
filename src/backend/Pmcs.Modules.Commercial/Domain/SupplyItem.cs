using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class SupplyItem : AggregateRoot
{
    private SupplyItem() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public SupplyItemKind Kind { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string BaseUnit { get; private set; } = string.Empty;
    public string UnitConversionsJson { get; private set; } = "[]";
    public string? TechnicalSpecificationReference { get; private set; }
    public SupplyTrackingPolicy TrackingPolicy { get; private set; }
    public bool InspectionRequired { get; private set; }
    public string? StorageCondition { get; private set; }
    public string? AcceptanceCriteria { get; private set; }
    public SupplyItemStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    public IReadOnlyCollection<UnitConversionDefinition> UnitConversions =>
        JsonSerializer.Deserialize<UnitConversionDefinition[]>(UnitConversionsJson, SupplyRules.JsonOptions) ?? [];

    public static SupplyItem Create(
        Guid id, Guid tenantId, Guid projectId, string code, string name, SupplyItemKind kind,
        string category, string baseUnit, string? technicalSpecificationReference,
        SupplyTrackingPolicy trackingPolicy, bool inspectionRequired, string? storageCondition,
        string? acceptanceCriteria, Guid createdBy, DateTimeOffset createdAt)
    {
        SupplyRules.Identity(id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(trackingPolicy))
            throw new DomainRuleException("supply.item.type.invalid", "Supply item type or tracking policy is invalid.");
        if (kind != SupplyItemKind.Material && trackingPolicy != SupplyTrackingPolicy.None)
            throw new DomainRuleException("supply.item.service_tracking.invalid", "Services cannot use stock tracking.");
        if (kind == SupplyItemKind.Material && trackingPolicy is SupplyTrackingPolicy.Batch or SupplyTrackingPolicy.Serial)
            throw new DomainRuleException("supply.item.tracking.unsupported", "Batch and serial tracking require the dedicated traceability slice.");

        var unit = SupplyRules.Unit(baseUnit);
        return new SupplyItem
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Code = SupplyRules.Code(code, 48, "supply.item.code.invalid"),
            Name = CommercialRules.Required(name, 240, "supply.item.name.invalid"),
            Kind = kind,
            Category = CommercialRules.Required(category, 120, "supply.item.category.invalid"),
            BaseUnit = unit,
            UnitConversionsJson = JsonSerializer.Serialize(
                new[] { new UnitConversionDefinition(unit, 1m, 1) }, SupplyRules.JsonOptions),
            TechnicalSpecificationReference = CommercialRules.Optional(
                technicalSpecificationReference, 500, "supply.item.specification.too_long"),
            TrackingPolicy = trackingPolicy,
            InspectionRequired = inspectionRequired,
            StorageCondition = CommercialRules.Optional(storageCondition, 500, "supply.item.storage.too_long"),
            AcceptanceCriteria = CommercialRules.Optional(acceptanceCriteria, 1_000, "supply.item.acceptance.too_long"),
            Status = SupplyItemStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ChangedAt = createdAt
        };
    }

    public void AddUnitConversion(long baseRevision, string unitCode, decimal factorToBase, int version, DateTimeOffset changedAt)
    {
        CommercialRules.Revision(Revision, baseRevision, "supply.item.revision.conflict");
        var unit = SupplyRules.Unit(unitCode);
        if (unit == BaseUnit || factorToBase <= 0 || factorToBase > 1_000_000_000m || version <= 0)
            throw new DomainRuleException("supply.item.conversion.invalid", "Unit conversion is invalid.");
        var definitions = UnitConversions.ToList();
        if (definitions.Any(item => item.UnitCode == unit && item.Version == version))
            throw new DomainRuleException("supply.item.conversion.duplicate", "The unit conversion version already exists.");
        definitions.Add(new UnitConversionDefinition(unit, factorToBase, version));
        UnitConversionsJson = JsonSerializer.Serialize(definitions, SupplyRules.JsonOptions);
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    public ConvertedQuantity ConvertToBase(decimal quantity, string? unitCode, int? conversionVersion = null)
    {
        SupplyRules.PositiveQuantity(quantity, "supply.quantity.invalid");
        var unit = SupplyRules.Unit(unitCode ?? BaseUnit);
        var definition = UnitConversions.Where(item => item.UnitCode == unit &&
                (!conversionVersion.HasValue || item.Version == conversionVersion.Value))
            .OrderByDescending(item => item.Version).FirstOrDefault()
            ?? throw new DomainRuleException("supply.item.conversion.not_found", "No matching unit conversion exists.");
        var baseQuantity = decimal.Round(quantity * definition.FactorToBase, 6, MidpointRounding.AwayFromZero);
        SupplyRules.PositiveQuantity(baseQuantity, "supply.quantity.base.invalid");
        return new ConvertedQuantity(quantity, unit, baseQuantity, BaseUnit, definition.Version);
    }

    public void SetStatus(long baseRevision, SupplyItemStatus status, DateTimeOffset changedAt)
    {
        CommercialRules.Revision(Revision, baseRevision, "supply.item.revision.conflict");
        if (!Enum.IsDefined(status))
            throw new DomainRuleException("supply.item.status.invalid", "Supply item status is invalid.");
        Status = status;
        ChangedAt = changedAt;
        AdvanceRevision();
    }
}

public sealed record UnitConversionDefinition(string UnitCode, decimal FactorToBase, int Version);
public sealed record ConvertedQuantity(decimal Quantity, string UnitCode, decimal BaseQuantity, string BaseUnit, int ConversionVersion);

public enum SupplyItemKind { Material = 1, Service = 2, EquipmentRental = 3 }
public enum SupplyTrackingPolicy { None = 1, Quantity = 2, Batch = 3, Serial = 4 }
public enum SupplyItemStatus { Active = 1, Inactive = 2 }
