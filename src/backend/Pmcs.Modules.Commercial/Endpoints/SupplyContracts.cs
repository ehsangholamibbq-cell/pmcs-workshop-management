using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Modules.Commercial.Endpoints;

public sealed record CreateSupplyItemRequest(
    Guid? ClientGeneratedId, string Code, string Name, SupplyItemKind Kind, string Category,
    string BaseUnit, string? TechnicalSpecificationReference, SupplyTrackingPolicy TrackingPolicy,
    bool InspectionRequired, string? StorageCondition, string? AcceptanceCriteria);

public sealed record AddUnitConversionRequest(long BaseRevision, string UnitCode, decimal FactorToBase, int Version);
public sealed record SupplyStatusRequest(long BaseRevision, bool Active);

public sealed record SupplyItemResponse(
    Guid Id, string Code, string Name, SupplyItemKind Kind, string Category, string BaseUnit,
    IReadOnlyCollection<UnitConversionDefinition> UnitConversions, string? TechnicalSpecificationReference,
    SupplyTrackingPolicy TrackingPolicy, bool InspectionRequired, string? StorageCondition,
    string? AcceptanceCriteria, SupplyItemStatus Status, long Revision)
{
    internal static SupplyItemResponse From(SupplyItem item) => new(
        item.Id, item.Code, item.Name, item.Kind, item.Category, item.BaseUnit,
        item.UnitConversions, item.TechnicalSpecificationReference, item.TrackingPolicy,
        item.InspectionRequired, item.StorageCondition, item.AcceptanceCriteria, item.Status, item.Revision);
}

public sealed record CreateInventoryLocationRequest(
    Guid? ClientGeneratedId, string Code, string Name, InventoryLocationType Type, bool AllowsAvailableStock);

public sealed record InventoryLocationResponse(
    Guid Id, string Code, string Name, InventoryLocationType Type, bool AllowsAvailableStock,
    InventoryLocationStatus Status, long Revision)
{
    internal static InventoryLocationResponse From(InventoryLocation item) => new(
        item.Id, item.Code, item.Name, item.Type, item.AllowsAvailableStock, item.Status, item.Revision);
}

public sealed record CreateGoodsReceiptRequest(
    Guid? ClientGeneratedId, Guid PurchaseOrderId, Guid ItemId, Guid? StockLocationId,
    string DispatchNote, DateTimeOffset ArrivedAt, string DeliveryLocation,
    decimal ShippedQuantity, decimal ReceivedQuantity, decimal DamagedQuantity,
    string? UnitCode, int? ConversionVersion, string? BatchOrLotReference,
    string? PackageCondition, string? ExcessApprovalReason,
    IReadOnlyCollection<string>? EvidenceReferences);

public sealed record InspectGoodsReceiptRequest(
    long BaseRevision, decimal AcceptedBaseQuantity, decimal RejectedBaseQuantity,
    decimal QuarantinedBaseQuantity, string InspectionType, string? InspectionReference,
    string? Comment);

public sealed record SupplyTransitionRequest(long BaseRevision, string? Comment = null);

public sealed record GoodsReceiptResponse(
    Guid Id, string Number, Guid PurchaseOrderId, Guid PartyId, Guid ItemId, Guid? StockLocationId,
    string DispatchNote, DateTimeOffset ArrivedAt, string DeliveryLocation,
    decimal ShippedQuantity, decimal ReceivedQuantity, decimal BaseReceivedQuantity,
    decimal DamagedQuantity, string UnitCode, string BaseUnit, int ConversionVersion,
    string? BatchOrLotReference, string? PackageCondition, string? ExcessApprovalReason,
    IReadOnlyCollection<string> EvidenceReferences, GoodsReceiptStatus Status,
    decimal AcceptedBaseQuantity, decimal RejectedBaseQuantity, decimal QuarantinedBaseQuantity,
    DateTimeOffset? InspectedAt, string? InspectionType, string? InspectionReference,
    string? InspectionComment, DateTimeOffset? StockPostedAt, long Revision)
{
    internal static GoodsReceiptResponse From(GoodsReceipt item) => new(
        item.Id, item.Number, item.PurchaseOrderId, item.PartyId, item.ItemId, item.StockLocationId,
        item.DispatchNote, item.ArrivedAt, item.DeliveryLocation, item.ShippedQuantity,
        item.ReceivedQuantity, item.BaseReceivedQuantity, item.DamagedQuantity,
        item.UnitCode, item.BaseUnit, item.ConversionVersion, item.BatchOrLotReference,
        item.PackageCondition, item.ExcessApprovalReason, item.EvidenceReferences, item.Status,
        item.AcceptedBaseQuantity, item.RejectedBaseQuantity, item.QuarantinedBaseQuantity,
        item.InspectedAt, item.InspectionType, item.InspectionReference,
        item.InspectionComment, item.StockPostedAt, item.Revision);
}

public sealed record CreateMaterialIssueRequest(
    Guid? ClientGeneratedId, Guid ItemId, Guid SourceLocationId, decimal Quantity,
    string? UnitCode, int? ConversionVersion, string IssuedTo, string DestinationLocation,
    string? WorkItemReference, string? WbsReference, Guid? PurchaseRequestId,
    IReadOnlyCollection<string>? EvidenceReferences);

public sealed record AcknowledgeMaterialIssueRequest(long BaseRevision, string Reference);

public sealed record ReconcileMaterialIssueRequest(
    long BaseRevision, decimal ConsumedBaseQuantity, decimal ReturnedBaseQuantity,
    decimal WasteBaseQuantity, string? WasteReason, IReadOnlyCollection<string>? EvidenceReferences);

public sealed record MaterialIssueResponse(
    Guid Id, string Number, Guid ItemId, Guid SourceLocationId, decimal IssuedBaseQuantity,
    string BaseUnit, string IssuedTo, string DestinationLocation, string? WorkItemReference,
    string? WbsReference, Guid? PurchaseRequestId, IReadOnlyCollection<string> EvidenceReferences,
    decimal ConsumedBaseQuantity, decimal ReturnedBaseQuantity, decimal WasteBaseQuantity,
    decimal UnreconciledBaseQuantity, MaterialIssueStatus Status, DateTimeOffset IssuedAt,
    DateTimeOffset? AcknowledgedAt, string? AcknowledgmentReference, DateTimeOffset? ReconciledAt,
    long Revision)
{
    internal static MaterialIssueResponse From(MaterialIssue item) => new(
        item.Id, item.Number, item.ItemId, item.SourceLocationId, item.IssuedBaseQuantity,
        item.BaseUnit, item.IssuedTo, item.DestinationLocation, item.WorkItemReference,
        item.WbsReference, item.PurchaseRequestId, item.EvidenceReferences,
        item.ConsumedBaseQuantity, item.ReturnedBaseQuantity, item.WasteBaseQuantity,
        item.UnreconciledBaseQuantity, item.Status, item.IssuedAt, item.AcknowledgedAt,
        item.AcknowledgmentReference, item.ReconciledAt, item.Revision);
}

public sealed record MaterialReconciliationResponse(
    Guid Id, Guid MaterialIssueId, decimal ConsumedBaseQuantity, decimal ReturnedBaseQuantity,
    decimal WasteBaseQuantity, string BaseUnit, string? WasteReason,
    IReadOnlyCollection<string> EvidenceReferences, DateTimeOffset RecordedAt)
{
    internal static MaterialReconciliationResponse From(MaterialReconciliationRecord item) => new(
        item.Id, item.MaterialIssueId, item.ConsumedBaseQuantity, item.ReturnedBaseQuantity,
        item.WasteBaseQuantity, item.BaseUnit, item.WasteReason, item.EvidenceReferences, item.RecordedAt);
}

public sealed record TransferInventoryRequest(
    Guid? ClientGeneratedId, Guid ItemId, Guid SourceLocationId, Guid DestinationLocationId,
    decimal Quantity, string? UnitCode, int? ConversionVersion, string Reason);

public sealed record InventoryTransferResponse(
    Guid TransactionId, Guid ItemId, Guid SourceLocationId, Guid DestinationLocationId,
    decimal BaseQuantity, string BaseUnit, DateTimeOffset OccurredAt);

public sealed record ProposeInventoryAdjustmentRequest(
    Guid? ClientGeneratedId, Guid ItemId, Guid LocationId, DateTimeOffset CutoffAt,
    decimal CountedBaseQuantity, string Reason, IReadOnlyCollection<string>? EvidenceReferences);

public sealed record ReviewInventoryAdjustmentRequest(long BaseRevision, bool Approved, string? Comment);

public sealed record InventoryAdjustmentResponse(
    Guid Id, string Number, Guid ItemId, Guid LocationId, DateTimeOffset CutoffAt,
    decimal SystemBaseQuantity, decimal CountedBaseQuantity, decimal DeltaBaseQuantity,
    string BaseUnit, string Reason, IReadOnlyCollection<string> EvidenceReferences,
    InventoryAdjustmentStatus Status, DateTimeOffset ProposedAt, DateTimeOffset? ReviewedAt,
    string? ReviewComment, DateTimeOffset? PostedAt, long Revision)
{
    internal static InventoryAdjustmentResponse From(InventoryAdjustment item) => new(
        item.Id, item.Number, item.ItemId, item.LocationId, item.CutoffAt,
        item.SystemBaseQuantity, item.CountedBaseQuantity, item.DeltaBaseQuantity,
        item.BaseUnit, item.Reason, item.EvidenceReferences, item.Status,
        item.ProposedAt, item.ReviewedAt, item.ReviewComment, item.PostedAt, item.Revision);
}

public sealed record CreateServiceAcceptanceRequest(
    Guid? ClientGeneratedId, Guid PurchaseOrderId, Guid ItemId,
    DateOnly PeriodStart, DateOnly PeriodEnd, decimal DeliveredQuantity,
    decimal AcceptedQuantity, decimal RejectedQuantity, string? UnitCode,
    int? ConversionVersion, string AcceptanceCriteria,
    IReadOnlyCollection<string>? EvidenceReferences, string? Comment);

public sealed record ServiceAcceptanceResponse(
    Guid Id, string Number, Guid PurchaseOrderId, Guid PartyId, Guid ItemId,
    DateOnly PeriodStart, DateOnly PeriodEnd, decimal DeliveredBaseQuantity,
    decimal AcceptedBaseQuantity, decimal RejectedBaseQuantity, string BaseUnit,
    string AcceptanceCriteria, IReadOnlyCollection<string> EvidenceReferences,
    string? Comment, DateTimeOffset VerifiedAt, long Revision)
{
    internal static ServiceAcceptanceResponse From(ServiceAcceptance item) => new(
        item.Id, item.Number, item.PurchaseOrderId, item.PartyId, item.ItemId,
        item.PeriodStart, item.PeriodEnd, item.DeliveredBaseQuantity,
        item.AcceptedBaseQuantity, item.RejectedBaseQuantity, item.BaseUnit,
        item.AcceptanceCriteria, item.EvidenceReferences, item.Comment, item.VerifiedAt, item.Revision);
}

public sealed record InventoryLedgerResponse(
    Guid Id, Guid TransactionId, Guid ItemId, Guid LocationId, InventoryEventType EventType,
    decimal BaseQuantityDelta, string BaseUnit, string SourceType, Guid SourceId,
    string? Reason, DateTimeOffset OccurredAt)
{
    internal static InventoryLedgerResponse From(InventoryLedgerEntry item) => new(
        item.Id, item.TransactionId, item.ItemId, item.LocationId, item.EventType,
        item.BaseQuantityDelta, item.BaseUnit, item.SourceType, item.SourceId, item.Reason, item.OccurredAt);
}

public sealed record StockPositionResponse(
    Guid ItemId, Guid LocationId, string BaseUnit, decimal OnHandBaseQuantity,
    decimal ReservedBaseQuantity, decimal AvailableBaseQuantity, DateTimeOffset? LastMovementAt);

public sealed record SupplierDeliveryPerformanceResponse(
    Guid PartyId, int ReceiptCount, int OnTimeReceiptCount, int FullyAcceptedReceiptCount,
    int ReceiptWithRejectionCount);

public sealed record SupplyStateResponse(
    string CalculationVersion, DateTimeOffset CalculatedAt,
    IReadOnlyCollection<SupplyItemResponse> Items,
    IReadOnlyCollection<InventoryLocationResponse> Locations,
    IReadOnlyCollection<GoodsReceiptResponse> Receipts,
    IReadOnlyCollection<MaterialIssueResponse> MaterialIssues,
    IReadOnlyCollection<MaterialReconciliationResponse> Reconciliations,
    IReadOnlyCollection<InventoryAdjustmentResponse> Adjustments,
    IReadOnlyCollection<ServiceAcceptanceResponse> ServiceAcceptances,
    IReadOnlyCollection<InventoryLedgerResponse> Ledger,
    IReadOnlyCollection<StockPositionResponse> StockPositions,
    IReadOnlyCollection<SupplierDeliveryPerformanceResponse> SupplierPerformance,
    int PendingInspectionCount, int QuarantinedReceiptCount, int RejectedReceiptCount,
    int UnreconciledMaterialIssueCount, int PendingAdjustmentApprovalCount);
