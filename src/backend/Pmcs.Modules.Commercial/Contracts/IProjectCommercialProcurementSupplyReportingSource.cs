using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Commercial.Contracts;

public interface IProjectCommercialProcurementSupplyReportingSource
{
    Task<ProjectCommercialProcurementSupplyReportingResult> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default);
}

public static class ProjectCommercialProcurementSupplyReportingContract
{
    public const string Version =
        "pmcs.commercial.project-commercial-procurement-supply-reporting/v1";
    public const string SourceManifestVersion =
        "pmcs.commercial.project-commercial-procurement-supply-manifest/v1";
    public const string PolicyVersion =
        "pmcs.commercial.project-commercial-procurement-supply-policy/v1";
    public const int MaximumContracts = 20_000;
    public const int MaximumAmendments = 100_000;
    public const int MaximumPurchaseRequests = 100_000;
    public const int MaximumPurchaseOrders = 100_000;
    public const int MaximumSupplyEvidence = 250_000;
    public const int MaximumPartyAndItemSnapshots = 50_000;
}

public sealed record ProjectCommercialProcurementSupplyReportingProjection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    IReadOnlyCollection<ProjectCommercialConfigurationVersion> Configurations,
    IReadOnlyCollection<ProjectCommercialPartySnapshotVersion> PartySnapshots,
    IReadOnlyCollection<ProjectCommercialItemSnapshotVersion> ItemSnapshots,
    IReadOnlyCollection<ProjectCommercialContractVersion> Contracts,
    IReadOnlyCollection<ProjectCommercialAmendmentVersion> Amendments,
    IReadOnlyCollection<ProjectCommercialPurchaseRequestVersion> PurchaseRequests,
    IReadOnlyCollection<ProjectCommercialPurchaseOrderVersion> PurchaseOrders,
    IReadOnlyCollection<ProjectCommercialGoodsReceiptVersion> GoodsReceipts,
    IReadOnlyCollection<ProjectCommercialServiceAcceptanceVersion> ServiceAcceptances,
    ProjectCommercialReportingSourceCompleteness ContractLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness AmendmentLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness ProcurementLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness PartySnapshotCompleteness,
    ProjectCommercialReportingSourceCompleteness ItemSnapshotCompleteness,
    ProjectCommercialReportingSourceCompleteness ReceiptInspectionCompleteness,
    ProjectCommercialReportingSourceCompleteness ServiceAcceptanceCompleteness,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialConfigurationVersion(
    long ConfigurationVersion,
    long ProjectRevision,
    ContractModel ContractModel,
    ProjectFeatureState ContractState,
    ProjectFeatureState ProcurementState,
    string TimeZone,
    string BaseCurrencyCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialPartySnapshotVersion(
    Guid PartyId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    string Code,
    string Name,
    PartyType Type,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialItemSnapshotVersion(
    Guid ItemId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    string Code,
    string Name,
    SupplyItemKind Kind,
    string BaseUnit,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialContractVersion(
    Guid ContractId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    Guid PartyId,
    string Number,
    string Title,
    ProjectContractType Type,
    decimal? OriginalApprovedAmount,
    string CurrencyCode,
    DateOnly? StartDate,
    DateOnly? OriginalEndDate,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<ProjectCommercialContractLifecycleEvent> Lifecycle,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialContractLifecycleEvent(
    long Sequence,
    ProjectCommercialContractEventType Type,
    DateTimeOffset OccurredAt);

public sealed record ProjectCommercialAmendmentVersion(
    Guid AmendmentId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    Guid ContractId,
    string Number,
    string Title,
    ContractAmendmentType Type,
    decimal? AmountDelta,
    string CurrencyCode,
    int? ExtensionDays,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<ProjectCommercialAmendmentLifecycleEvent> Lifecycle,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialAmendmentLifecycleEvent(
    long Sequence,
    ProjectCommercialAmendmentEventType Type,
    DateTimeOffset OccurredAt);

public sealed record ProjectCommercialPurchaseRequestVersion(
    Guid PurchaseRequestId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    string Number,
    string Title,
    decimal? EstimatedAmount,
    string CurrencyCode,
    Guid? ItemId,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<ProjectCommercialPurchaseRequestLifecycleEvent> Lifecycle,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialPurchaseRequestLifecycleEvent(
    long Sequence,
    ProjectCommercialPurchaseRequestEventType Type,
    DateTimeOffset OccurredAt);

public sealed record ProjectCommercialPurchaseOrderVersion(
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    Guid PurchaseRequestId,
    Guid PartyId,
    Guid? ContractId,
    string Number,
    string Title,
    decimal Amount,
    string CurrencyCode,
    DateOnly? DeliveryDueDate,
    Guid? ItemId,
    decimal? OrderedQuantity,
    string? UnitCode,
    decimal? OrderedBaseQuantity,
    string? BaseUnit,
    int? ConversionVersion,
    IReadOnlyCollection<ProjectCommercialPurchaseOrderLifecycleEvent> Lifecycle,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialPurchaseOrderLifecycleEvent(
    long Sequence,
    ProjectCommercialPurchaseOrderEventType Type,
    DateTimeOffset OccurredAt);

public sealed record ProjectCommercialGoodsReceiptVersion(
    Guid ReceiptId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    string Number,
    Guid PurchaseOrderId,
    Guid PartyId,
    Guid ItemId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ArrivedAt,
    decimal ReceivedBaseQuantity,
    string BaseUnit,
    int ConversionVersion,
    GoodsReceiptStatus Status,
    DateTimeOffset? InspectedAt,
    decimal AcceptedBaseQuantity,
    decimal RejectedBaseQuantity,
    decimal QuarantinedBaseQuantity,
    Guid? ExcessApprovalId,
    DateTimeOffset? ExcessApprovedAt,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialServiceAcceptanceVersion(
    Guid AcceptanceId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    string Number,
    Guid PurchaseOrderId,
    Guid PartyId,
    Guid ItemId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal DeliveredBaseQuantity,
    decimal AcceptedBaseQuantity,
    decimal RejectedBaseQuantity,
    string BaseUnit,
    DateTimeOffset VerifiedAt,
    Guid? ExcessApprovalId,
    DateTimeOffset? ExcessApprovedAt,
    ProjectCommercialReportingClassification Classification);

public sealed record ProjectCommercialProcurementSupplyReportingResult(
    string ContractVersion,
    string PolicyVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectCommercialReportingClassification Classification,
    ProjectCommercialReportingDataStatus DataStatus,
    IReadOnlyCollection<ProjectCommercialReportingReasonCode> ReasonCodes,
    ProjectCommercialConfigurationVersion? Configuration,
    ProjectCommercialReportingSectionStatus ContractStatus,
    ProjectCommercialContractSummary? ContractSummary,
    IReadOnlyCollection<ProjectCommercialContractReportRow> ContractRegister,
    IReadOnlyCollection<ProjectCommercialAmendmentReportRow> ApprovedAmendments,
    ProjectCommercialReportingSectionStatus ProcurementStatus,
    ProjectCommercialProcurementSummary? ProcurementSummary,
    IReadOnlyCollection<ProjectCommercialPurchaseOrderReportRow> PurchaseOrders,
    ProjectCommercialReportingSectionStatus SupplyStatus,
    IReadOnlyCollection<ProjectCommercialSupplySummary> SupplySummaries,
    IReadOnlyCollection<ProjectCommercialSupplierPerformance> SupplierPerformance,
    ProjectCommercialSourceCounts SourceCounts,
    DateTimeOffset? SourceMaxChangedAt,
    ProjectCommercialProcurementSupplySourceManifest SourceManifest,
    string SourceManifestSha256);

public sealed record ProjectCommercialContractSummary(
    int OfficialContractCount,
    int ActiveContractCount,
    int SuspendedContractCount,
    int ClosedContractCount,
    int TerminatedContractCount,
    int ExpiredActiveContractCount,
    int PendingContractWorkflowCount,
    int ApprovedAmendmentCount,
    int PendingAmendmentWorkflowCount,
    decimal ApprovedAmountDelta,
    int ApprovedExtensionDays,
    decimal KnownEffectiveContractCeilingSubtotal,
    decimal? EffectiveContractCeilingTotal);

public sealed record ProjectCommercialContractReportRow(
    Guid ContractId,
    string Number,
    string Title,
    string PartyCode,
    string PartyName,
    PartyType PartyType,
    ProjectContractType Type,
    ProjectCommercialContractState State,
    DateOnly? StartDate,
    DateOnly? OriginalEndDate,
    DateOnly? EffectiveEndDate,
    decimal? OriginalApprovedAmount,
    decimal ApprovedAmountDelta,
    decimal? EffectiveApprovedAmount,
    int ApprovedAmendmentCount,
    int ApprovedExtensionDays);

public sealed record ProjectCommercialAmendmentReportRow(
    Guid AmendmentId,
    Guid ContractId,
    string ContractNumber,
    string Number,
    string Title,
    ContractAmendmentType Type,
    decimal? AmountDelta,
    int? ExtensionDays,
    DateTimeOffset ApprovedAt);

public sealed record ProjectCommercialProcurementSummary(
    int DraftRequestCount,
    int SubmittedRequestCount,
    int ReturnedRequestCount,
    int ApprovedRequestCount,
    int OrderedRequestCount,
    int CancelledRequestCount,
    int ApprovedRequestsAwaitingOrderCount,
    int IssuedOrderCount,
    int ClosedOrderCount,
    int CancelledOrderCount,
    decimal TotalIssuedOrderAmount,
    decimal OpenOrderAmount);

public sealed record ProjectCommercialPurchaseOrderReportRow(
    Guid PurchaseOrderId,
    string Number,
    string Title,
    string PurchaseRequestNumber,
    string? ContractNumber,
    string PartyCode,
    string PartyName,
    PartyType PartyType,
    decimal Amount,
    DateOnly? DeliveryDueDate,
    ProjectCommercialPurchaseOrderState State,
    string? ItemCode,
    string? ItemName,
    SupplyItemKind? ItemKind,
    decimal? OrderedQuantity,
    string? UnitCode,
    decimal? OrderedBaseQuantity,
    string? BaseUnit,
    int? ConversionVersion,
    decimal? DeliveredBaseQuantity,
    decimal? AcceptedBaseQuantity,
    decimal? RejectedBaseQuantity,
    decimal? QuarantinedBaseQuantity,
    decimal? RemainingOrderedQuantity,
    decimal? AcceptedExcessQuantity,
    decimal? FulfillmentPercent,
    DateOnly? CompletionDate,
    ProjectCommercialDeliveryStatus DeliveryStatus,
    int GoodsReceiptCount,
    int PendingInspectionCount,
    int ServiceAcceptanceCount,
    int RejectedOrQuarantinedEvidenceCount);

public sealed record ProjectCommercialSupplySummary(
    string ItemCode,
    string ItemName,
    SupplyItemKind ItemKind,
    string BaseUnit,
    int OrderCount,
    decimal OrderedBaseQuantity,
    decimal DeliveredBaseQuantity,
    decimal AcceptedBaseQuantity,
    decimal RejectedBaseQuantity,
    decimal QuarantinedBaseQuantity);

public sealed record ProjectCommercialSupplierPerformance(
    Guid PartyId,
    string PartyCode,
    string PartyName,
    PartyType PartyType,
    int IssuedOrderCount,
    int OpenOrderCount,
    int ClosedOrderCount,
    int CancelledOrderCount,
    int AssessableCompletedOrderCount,
    int OnTimeFulfilledCount,
    int LateFulfilledCount,
    int OverdueOpenCount,
    int ClosedShortCount,
    int NotAssessableCount,
    int GoodsReceiptCount,
    int PendingInspectionCount,
    int ReceiptWithRejectedOrQuarantinedCount,
    int ServiceAcceptanceCount,
    int ServiceAcceptanceWithRejectedCount,
    decimal? OnTimeFulfillmentRate);

public sealed record ProjectCommercialSourceCounts(
    int PartySnapshotSourceCount,
    int EffectivePartySnapshotCount,
    int ItemSnapshotSourceCount,
    int EffectiveItemSnapshotCount,
    int ContractSourceCount,
    int OfficialContractCount,
    int PendingContractCount,
    int AmendmentSourceCount,
    int ApprovedAmendmentCount,
    int PendingAmendmentCount,
    int PurchaseRequestSourceCount,
    int PurchaseRequestAtCutoffCount,
    int PurchaseOrderSourceCount,
    int OfficialPurchaseOrderCount,
    int GoodsReceiptSourceCount,
    int EligibleGoodsReceiptCount,
    int ServiceAcceptanceSourceCount,
    int EligibleServiceAcceptanceCount,
    int IncompleteCollectionCount);

public sealed record ProjectCommercialProcurementSupplySourceManifest(
    string ManifestVersion,
    string SourceContractVersion,
    string PolicyVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectCommercialConfigurationManifest? Configuration,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> PartySnapshots,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> ItemSnapshots,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> Contracts,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> Amendments,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> PurchaseRequests,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> PurchaseOrders,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> GoodsReceipts,
    IReadOnlyCollection<ProjectCommercialSourceEntryManifest> ServiceAcceptances,
    ProjectCommercialReportingSourceCompleteness ContractLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness AmendmentLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness ProcurementLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness PartySnapshotCompleteness,
    ProjectCommercialReportingSourceCompleteness ItemSnapshotCompleteness,
    ProjectCommercialReportingSourceCompleteness ReceiptInspectionCompleteness,
    ProjectCommercialReportingSourceCompleteness ServiceAcceptanceCompleteness);

public sealed record ProjectCommercialConfigurationManifest(
    long ConfigurationVersion,
    long ProjectRevision,
    ContractModel ContractModel,
    ProjectFeatureState ContractState,
    ProjectFeatureState ProcurementState,
    string TimeZone,
    string BaseCurrencyCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

public sealed record ProjectCommercialSourceEntryManifest(
    Guid SourceId,
    long Revision,
    string DefinitionSha256);

public enum ProjectCommercialReportingClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public enum ProjectCommercialReportingSourceCompleteness
{
    Complete = 1,
    Incomplete = 2
}

public enum ProjectCommercialReportingDataStatus
{
    NotConfigured = 1,
    NoData = 2,
    InsufficientData = 3,
    Available = 4
}

public enum ProjectCommercialReportingSectionStatus
{
    NotConfigured = 1,
    SetupRequired = 2,
    Suspended = 3,
    NoData = 4,
    InsufficientData = 5,
    Available = 6
}

public enum ProjectCommercialContractEventType
{
    Submitted = 1,
    Returned = 2,
    Activated = 3,
    Suspended = 4,
    Closed = 5,
    Terminated = 6
}

public enum ProjectCommercialAmendmentEventType
{
    Submitted = 1,
    Returned = 2,
    Approved = 3
}

public enum ProjectCommercialPurchaseRequestEventType
{
    Submitted = 1,
    Returned = 2,
    Approved = 3,
    Ordered = 4,
    Cancelled = 5
}

public enum ProjectCommercialPurchaseOrderEventType
{
    Issued = 1,
    Closed = 2,
    Cancelled = 3
}

public enum ProjectCommercialContractState
{
    Active = 1,
    Suspended = 2,
    Closed = 3,
    Terminated = 4,
    Expired = 5
}

public enum ProjectCommercialPurchaseRequestState
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Ordered = 5,
    Cancelled = 6
}

public enum ProjectCommercialPurchaseOrderState
{
    Issued = 1,
    Closed = 2,
    Cancelled = 3
}

public enum ProjectCommercialDeliveryStatus
{
    NotAssessable = 1,
    PendingDue = 2,
    OnTimeFulfilled = 3,
    LateFulfilled = 4,
    OverdueOpen = 5,
    ClosedShort = 6,
    Cancelled = 7
}

public enum ProjectCommercialReportingReasonCode
{
    CommercialReportingNotConfigured = 1,
    ContractSetupRequired = 2,
    ContractSuspended = 3,
    ProcurementSetupRequired = 4,
    ProcurementSuspended = 5,
    OfficialContractsMissing = 6,
    OfficialProcurementMissing = 7,
    OfficialSupplyEvidenceMissing = 8,
    ContractLifecycleIncomplete = 9,
    AmendmentLifecycleIncomplete = 10,
    ProcurementLifecycleIncomplete = 11,
    SupplyLineageIncomplete = 12,
    PartySnapshotUnavailable = 13,
    UnitConversionHistoryUnavailable = 14,
    ContractCeilingUnavailable = 15,
    ContractEndDateUnavailable = 16,
    OrderQuantityBasisUnavailable = 17,
    DeliveryDueDateUnavailable = 18,
    ClosedOrderSupplyGap = 19,
    PendingInspection = 20,
    RejectedOrQuarantinedSupply = 21
}
